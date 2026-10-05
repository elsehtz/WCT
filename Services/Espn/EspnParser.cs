using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WorldCupTerminal.Services.Espn;

/// <summary>
/// Turns raw ESPN site-API JSON into the compact records in <see cref="EspnData"/>. The API
/// is unofficial, so every read is defensive: a missing field yields a default, a malformed
/// event/entry is skipped, and nothing here throws for shape drift.
/// </summary>
public static partial class EspnParser
{
    [GeneratedRegex(@"Group ([A-Z])", RegexOptions.IgnoreCase)]
    private static partial Regex GroupNoteRegex();

    public static List<EsTeam> ParseTeams(JsonElement root)
    {
        var teams = new List<EsTeam>();
        if (!TryPath(root, out var arr, "sports", "0", "leagues", "0", "teams")) return teams;

        foreach (var entry in arr.EnumerateArray())
        {
            if (!entry.TryGetProperty("team", out var t)) continue;
            var id = Str(t, "id");
            if (id.Length == 0) continue;
            teams.Add(new EsTeam(id, Str(t, "abbreviation"), Str(t, "displayName")));
        }
        return teams;
    }

    public static List<EsMatch> ParseScoreboard(JsonElement root)
    {
        var matches = new List<EsMatch>();
        if (!root.TryGetProperty("events", out var events) || events.ValueKind != JsonValueKind.Array)
            return matches;

        foreach (var ev in events.EnumerateArray())
        {
            var m = ParseEvent(ev);
            if (m is not null) matches.Add(m);
        }
        return matches;
    }

    private static EsMatch? ParseEvent(JsonElement ev)
    {
        var id = Str(ev, "id");
        if (id.Length == 0) return null;
        if (!DateTime.TryParse(Str(ev, "date"), CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var kickOff))
            return null;

        string stageSlug = TryPath(ev, out var slug, "season", "slug") ? slug.GetString() ?? "" : "";

        if (!TryPath(ev, out var comp, "competitions", "0")) return null;

        string? group = null;
        var note = Str(comp, "altGameNote");
        var gm = GroupNoteRegex().Match(note);
        if (gm.Success) group = gm.Groups[1].Value.ToUpperInvariant();

        string venue = "TBD";
        if (comp.TryGetProperty("venue", out var v))
        {
            venue = Str(v, "fullName");
            if (TryPath(v, out var city, "address", "city") && city.ValueKind == JsonValueKind.String)
                venue = venue.Length == 0 ? city.GetString()! : $"{venue}, {city.GetString()}";
        }

        string statusName = "", statusState = "";
        bool completed = false;
        if (TryPath(comp, out var st, "status", "type"))
        {
            statusName = Str(st, "name");
            statusState = Str(st, "state");
            completed = st.TryGetProperty("completed", out var c) && c.ValueKind == JsonValueKind.True;
        }

        var competitors = new List<EsCompetitor>();
        if (comp.TryGetProperty("competitors", out var comps) && comps.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in comps.EnumerateArray())
            {
                if (!c.TryGetProperty("team", out var t)) continue;
                competitors.Add(new EsCompetitor(
                    Str(t, "id"),
                    Str(t, "abbreviation"),
                    Str(t, "displayName"),
                    Str(c, "homeAway") == "home",
                    Int(c, "score") ?? 0,
                    Int(c, "shootoutScore"),
                    c.TryGetProperty("winner", out var w) && w.ValueKind == JsonValueKind.True));
            }
        }
        if (competitors.Count != 2) return null;

        var details = new List<EsDetail>();
        if (comp.TryGetProperty("details", out var det) && det.ValueKind == JsonValueKind.Array)
        {
            foreach (var d in det.EnumerateArray())
            {
                string type = TryPath(d, out var dt, "type", "text") ? dt.GetString() ?? "" : "";
                double clock = 0; string minute = "";
                if (d.TryGetProperty("clock", out var ck))
                {
                    clock = ck.TryGetProperty("value", out var cv) && cv.ValueKind == JsonValueKind.Number ? cv.GetDouble() : 0;
                    minute = Str(ck, "displayValue");
                }
                string teamId = TryPath(d, out var dTeam, "team", "id") ? Str(dTeam) : "";
                string player = TryPath(d, out var ath, "athletesInvolved", "0", "displayName") ? Str(ath) : "";

                details.Add(new EsDetail(type, clock, minute, teamId,
                    Flag(d, "scoringPlay"), Flag(d, "ownGoal"), Flag(d, "penaltyKick"),
                    Flag(d, "redCard"), Flag(d, "yellowCard"), Flag(d, "shootout"), player));
            }
        }

        return new EsMatch(id, kickOff, stageSlug, group, venue, statusName, statusState,
            completed, competitors, details);
    }

    public static EsSummary ParseSummary(string eventId, JsonElement root)
    {
        var lineups = new List<EsLineup>();
        if (root.TryGetProperty("rosters", out var rosters) && rosters.ValueKind == JsonValueKind.Array)
        {
            foreach (var r in rosters.EnumerateArray())
            {
                string teamId = TryPath(r, out var t, "team", "id") ? Str(t) : "";
                if (teamId.Length == 0) continue;
                var entries = new List<EsRosterEntry>();
                if (r.TryGetProperty("roster", out var list) && list.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in list.EnumerateArray())
                    {
                        string name = TryPath(p, out var an, "athlete", "displayName") ? Str(an) : "";
                        if (name.Length == 0) continue;
                        string pos = TryPath(p, out var pa, "position", "abbreviation") ? Str(pa) : "";
                        entries.Add(new EsRosterEntry(name, pos,
                            Int(p, "jersey") ?? 0,
                            Flag(p, "starter"), Flag(p, "subbedIn"), Flag(p, "subbedOut"),
                            Int(p, "formationPlace") ?? 0));
                    }
                }
                lineups.Add(new EsLineup(teamId, Str(r, "formation"), entries));
            }
        }

        var stats = new List<EsStatLine>();
        if (TryPath(root, out var bxTeams, "boxscore", "teams"))
        {
            foreach (var bt in bxTeams.EnumerateArray())
            {
                string teamId = TryPath(bt, out var t, "team", "id") ? Str(t) : "";
                if (teamId.Length == 0) continue;
                var values = new Dictionary<string, string>();
                if (bt.TryGetProperty("statistics", out var statArr) && statArr.ValueKind == JsonValueKind.Array)
                    foreach (var sEl in statArr.EnumerateArray())
                        values[Str(sEl, "name")] = Str(sEl, "displayValue");
                stats.Add(new EsStatLine(teamId, values));
            }
        }

        var subs = new List<EsSubstitution>();
        if (root.TryGetProperty("keyEvents", out var keyEvents) && keyEvents.ValueKind == JsonValueKind.Array)
        {
            foreach (var k in keyEvents.EnumerateArray())
            {
                if (!TryPath(k, out var kt, "type", "text") || Str(kt) != "Substitution") continue;
                double clock = 0; string minute = "";
                if (k.TryGetProperty("clock", out var ck))
                {
                    clock = ck.TryGetProperty("value", out var cv) && cv.ValueKind == JsonValueKind.Number ? cv.GetDouble() : 0;
                    minute = Str(ck, "displayValue");
                }
                string teamName = TryPath(k, out var tm, "team", "displayName") ? Str(tm) : "";
                subs.Add(new EsSubstitution(clock, minute, teamName, Str(k, "text")));
            }
        }

        return new EsSummary(eventId, lineups, stats, subs);
    }

    // ------------------------------------------------------------------ JSON helpers

    /// <summary>Walks properties; a numeric segment indexes into an array.</summary>
    private static bool TryPath(JsonElement el, out JsonElement result, params string[] path)
    {
        result = el;
        foreach (var seg in path)
        {
            if (result.ValueKind == JsonValueKind.Array && int.TryParse(seg, out var idx))
            {
                if (idx >= result.GetArrayLength()) return false;
                result = result[idx];
            }
            else if (result.ValueKind == JsonValueKind.Object && result.TryGetProperty(seg, out var next))
            {
                result = next;
            }
            else return false;
        }
        return true;
    }

    private static string Str(JsonElement el) =>
        el.ValueKind == JsonValueKind.String ? el.GetString() ?? "" : el.ToString();

    private static string Str(JsonElement el, string prop) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var p) ? Str(p) : "";

    /// <summary>Reads an int that ESPN may serialise as either a number or a string.</summary>
    private static int? Int(JsonElement el, string prop)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(prop, out var p)) return null;
        return p.ValueKind switch
        {
            JsonValueKind.Number => p.TryGetInt32(out var n) ? n : (int)p.GetDouble(),
            JsonValueKind.String => int.TryParse(p.GetString(), out var s) ? s : null,
            _ => null,
        };
    }

    private static bool Flag(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.True;
}

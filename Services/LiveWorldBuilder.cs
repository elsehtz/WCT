using WorldCupTerminal.Data;
using WorldCupTerminal.Models;
using WorldCupTerminal.Services.Espn;

namespace WorldCupTerminal.Services;

/// <summary>
/// Maps a scraped <see cref="LiveDataSet"/> to the domain: real teams (with squads drawn
/// from matchday rosters), real matches (scores, half splits derived from goal minutes,
/// lineups, event timelines) and the real knockout bracket. FIFA ranking, coach, confederation
/// and the registered squads (ages, clubs) come from <see cref="ReferenceData"/>. Personality
/// that no source provides (archetype, scouting prose) comes from the curated profiles / prose
/// banks, deterministically seeded so refreshes are stable.
/// </summary>
public class LiveWorldBuilder
{
    private readonly ReferenceData _reference;
    private readonly ILogger<LiveWorldBuilder> _log;

    public LiveWorldBuilder(ReferenceData reference, ILogger<LiveWorldBuilder> log)
    {
        _reference = reference;
        _log = log;
    }

    public (List<Team> Teams, List<Match> Matches, IReadOnlyList<BracketSlot> Bracket) Build(LiveDataSet data)
    {
        // Group letters come from the group-stage fixtures ("FIFA World Cup, Group A").
        var groupByTeamId = new Dictionary<string, string>();
        foreach (var m in data.Matches.Where(m => m.GroupLetter is not null))
            foreach (var c in m.Competitors)
                groupByTeamId[c.TeamId] = m.GroupLetter!;

        var teams = BuildTeams(data, groupByTeamId);
        var matches = new List<Match>();
        var matchByEventId = new Dictionary<string, Match>();

        foreach (var em in data.Matches.OrderBy(m => m.KickOffUtc))
        {
            var match = BuildMatch(em, teams, data.Summaries.GetValueOrDefault(em.EventId));
            if (match is null) continue;   // placeholder tie (e.g. "QF3 Winner") — bracket-only
            matches.Add(match);
            matchByEventId[em.EventId] = match;
        }

        CountPlayerGoals(teams, matches);

        var bracket = BuildBracket(data, teams, matchByEventId);
        return (teams.Values.OrderBy(t => t.Group).ThenBy(t => t.Name).ToList(), matches, bracket);
    }

    // ------------------------------------------------------------------ teams

    private Dictionary<string, Team> BuildTeams(LiveDataSet data, Dictionary<string, string> groupByTeamId)
    {
        // Performance-based pseudo-rank, used only for teams missing from the reference data
        // (the feed has no FIFA ranks): points → GD → GF over played matches.
        var perf = new Dictionary<string, (int Pts, int Gd, int Gf)>();
        foreach (var em in data.Matches.Where(m => m.Completed))
        {
            var home = em.Competitors.FirstOrDefault(c => c.Home);
            var away = em.Competitors.FirstOrDefault(c => !c.Home);
            if (home is null || away is null) continue;
            Tally(perf, home.TeamId, home.Score, away.Score);
            Tally(perf, away.TeamId, away.Score, home.Score);
        }

        var rankOrder = data.Teams
            .OrderByDescending(t => perf.GetValueOrDefault(t.Id).Pts)
            .ThenByDescending(t => perf.GetValueOrDefault(t.Id).Gd)
            .ThenByDescending(t => perf.GetValueOrDefault(t.Id).Gf)
            .ThenBy(t => t.Name)
            .Select((t, i) => (t.Id, Rank: i + 1))
            .ToDictionary(x => x.Id, x => x.Rank);

        var profiles = TournamentData.KnownProfiles.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        var teams = new Dictionary<string, Team>();
        foreach (var et in data.Teams)
        {
            var profile = profiles.GetValueOrDefault(et.Name);
            var code = et.Abbrev.Length > 0 ? et.Abbrev : et.Name[..Math.Min(3, et.Name.Length)].ToUpperInvariant();
            var reference = _reference.Get(code);
            var archetype = profile?.Arch
                ?? (Archetype)(Math.Abs(ProseBank.Seed(code)) % Enum.GetValues<Archetype>().Length);

            // A coach listed without a nationality is a native of the country they manage.
            var coach = reference is not null
                ? new Coach(reference.Coach, reference.CoachNationality ?? et.Name, reference.CoachNote)
                : profile is not null ? new Coach(profile.CoachName, profile.CoachNat) : new Coach("TBC", et.Name);

            var team = new Team
            {
                Name = et.Name,
                Code = code,
                Country = et.Name,
                Confederation = reference?.Confederation ?? profile?.Conf ?? "FIFA",
                Group = groupByTeamId.GetValueOrDefault(et.Id, "?"),
                ExternalId = et.Id,
                FifaRank = reference?.FifaRank ?? rankOrder.GetValueOrDefault(et.Id, data.Teams.Count),
                Archetype = archetype,
                Coach = coach,
                StyleNote = ProseBank.StyleNote(et.Name, archetype),
                ColourClass = TournamentData.AccentFor(code),
            };
            if (reference is null) _log.LogWarning("No reference data for {Code}; rank/coach/squad are modelled", code);
            BuildSquad(team, et.Id, data, reference);
            teams[et.Id] = team;
        }
        return teams;
    }

    private static void Tally(Dictionary<string, (int Pts, int Gd, int Gf)> perf, string id, int gf, int ga)
    {
        var cur = perf.GetValueOrDefault(id);
        perf[id] = (cur.Pts + (gf > ga ? 3 : gf == ga ? 1 : 0), cur.Gd + gf - ga, cur.Gf + gf);
    }

    /// <summary>
    /// Squad = the registered 26 from the reference data when available (real positions, ages
    /// and clubs), otherwise the union of players seen in this team's matchday rosters. Registered
    /// players take the feed's spelling of their name (matched by shirt number) so timeline
    /// events and lineups — which use the feed's spelling — resolve to the same player.
    /// </summary>
    private static void BuildSquad(Team team, string teamId, LiveDataSet data, RefTeam? reference)
    {
        var seen = new Dictionary<string, EsRosterEntry>();
        foreach (var summary in data.Summaries.Values)
            foreach (var lineup in summary.Lineups.Where(l => l.TeamId == teamId))
                foreach (var entry in lineup.Entries)
                {
                    // Bench entries carry "SUB" instead of a real position — keep any entry
                    // from a match where the player actually started.
                    if (!seen.TryGetValue(entry.Name, out var existing)
                        || existing.Position.Equals("SUB", StringComparison.OrdinalIgnoreCase))
                        seen[entry.Name] = entry;
                }

        if (reference is not null)
        {
            var unmatched = seen.Values.ToList();
            foreach (var rp in reference.Players)
            {
                // Tournament shirt numbers are registered and fixed, so they are the join key; the
                // spellings differ too often ("Manaf Younis" / "Munaf Younus", "Kaku") to rely on.
                // A shared name token only rescues a feed entry with a missing/odd number.
                var feed = unmatched.FirstOrDefault(e => e.Jersey == rp.Number)
                    ?? unmatched.FirstOrDefault(e => e.Jersey <= 0 && SameName(e.Name, rp.Name));
                if (feed is not null) unmatched.Remove(feed);

                var name = feed?.Name ?? rp.Name;
                var player = new Player
                {
                    Name = name,
                    Position = ParsePosition(rp.Pos),
                    Number = rp.Number,
                    Age = rp.Dob is DateOnly dob ? ReferenceData.AgeAt(dob, ReferenceData.TournamentStart) : 0,
                    Club = rp.Club.Length > 0 ? rp.Club : "—",
                };
                AddScoutNotes(player, team);
                team.Players.Add(player);
            }
            // Anyone the feed fielded who isn't registered (shouldn't happen) still gets a row.
            foreach (var entry in unmatched)
            {
                var player = new Player { Name = entry.Name, Position = ParsePosition(entry.Position), Number = entry.Jersey };
                AddScoutNotes(player, team);
                team.Players.Add(player);
            }
        }
        else
        {
            foreach (var entry in seen.Values)
            {
                var rng = new Random(ProseBank.Seed(entry.Name + team.Code));
                var player = new Player
                {
                    Name = entry.Name,
                    Position = ParsePosition(entry.Position),
                    Number = entry.Jersey,
                    // No reference squad: a plausible, stable value keeps the squad table alive.
                    Age = 22 + rng.Next(0, 14),
                };
                AddScoutNotes(player, team);
                team.Players.Add(player);
            }
        }

        team.Players.Sort((x, y) => x.Position != y.Position
            ? x.Position.CompareTo(y.Position)
            : x.Number.CompareTo(y.Number));
    }

    /// <summary>Modelled scouting prose (no source publishes this) — the sentiment pipeline's input.</summary>
    private static void AddScoutNotes(Player player, Team team)
    {
        var rng = new Random(ProseBank.Seed(player.Name + team.Code));
        var bank = ProseBank.NoteBank[team.Archetype];
        var a = bank[rng.Next(bank.Length)];
        string b;
        do { b = bank[rng.Next(bank.Length)]; } while (b == a);
        player.ScoutNotes.Add($"{player.Name} is {a}.");
        if (rng.Next(6) == 0)
            player.ScoutNotes.Add($"{player.Name} {ProseBank.Weaknesses[rng.Next(ProseBank.Weaknesses.Length)]}.");
        else
            player.ScoutNotes.Add($"{player.Name} is {b}.");
    }

    /// <summary>Loose name match across sources ("Vini Jr." vs "Vinícius Júnior" won't match, but
    /// "Unai Simón" vs "Unai Simon" will): any shared accent-folded token of 3+ letters.</summary>
    private static bool SameName(string a, string b)
    {
        var tokens = Tokens(a);
        return Tokens(b).Any(tokens.Contains);

        static HashSet<string> Tokens(string s) =>
            Fold(s).Split(new[] { ' ', '-', '.', '\'' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length >= 3).ToHashSet();
    }

    private static string Fold(string s)
    {
        var decomposed = s.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString();
    }

    /// <summary>Feed abbreviations are granular ("CD-L", "RB", "AM-R", "CF-L"); bench players
    /// are just "SUB", so callers should prefer an entry where the player actually started.</summary>
    private static Position ParsePosition(string abbrev)
    {
        var a = abbrev.ToUpperInvariant();
        int dash = a.IndexOf('-');
        if (dash > 0) a = a[..dash];
        return a switch
        {
            "G" or "GK" => Position.GK,
            "DF" or "CD" or "CB" or "LB" or "RB" or "RWB" or "LWB" or "WB" or "D" or "SW" => Position.DF,
            "LM" or "RM" or "CM" or "AM" or "DM" or "M" or "MF" => Position.MF,
            "F" or "CF" or "FW" or "ST" or "SS" or "LW" or "RW" or "A" => Position.FW,
            _ => Position.MF,
        };
    }

    private static void CountPlayerGoals(Dictionary<string, Team> teams, List<Match> matches)
    {
        var byTeam = teams.Values.GroupBy(t => t.Code).ToDictionary(g => g.Key,
            g => g.First().Players.GroupBy(p => p.Name).ToDictionary(n => n.Key, n => n.First()));

        foreach (var m in matches)
            foreach (var e in m.Events.Where(e => e.Type is MatchEventType.Goal or MatchEventType.Penalty))
                if (byTeam.TryGetValue(e.TeamCode, out var squad) && squad.TryGetValue(e.PlayerName, out var p))
                    p.Goals++;
    }

    // ------------------------------------------------------------------ matches

    private Match? BuildMatch(EsMatch em, Dictionary<string, Team> teams, EsSummary? summary)
    {
        var homeC = em.Competitors.FirstOrDefault(c => c.Home);
        var awayC = em.Competitors.FirstOrDefault(c => !c.Home);
        if (homeC is null || awayC is null) return null;

        // Ties with placeholder sides ("Quarterfinal 3 Winner") only exist on the bracket.
        if (!teams.TryGetValue(homeC.TeamId, out var home) || !teams.TryGetValue(awayC.TeamId, out var away))
            return null;

        var status = MapStatus(em);
        var match = new Match
        {
            Id = $"ES-{em.EventId}",
            ExternalId = em.EventId,
            Home = home,
            Away = away,
            Stage = MapStage(em),
            Group = em.GroupLetter ?? "",
            KickOff = em.KickOffUtc,
            Venue = em.Venue,
            Status = status,
            Played = status == MatchStatus.Finished,
        };

        FillScore(match, em, homeC, awayC);
        FillEvents(match, em, summary, home, away);
        FillLineups(match, summary, home, away);
        FillStats(match, summary, home, away);
        return match;
    }

    private MatchStatus MapStatus(EsMatch em)
    {
        if (em.Completed) return MatchStatus.Finished;
        switch (em.StatusName)
        {
            case "STATUS_HALFTIME": return MatchStatus.Paused;
            case "STATUS_POSTPONED": return MatchStatus.Postponed;
            case "STATUS_CANCELED":
            case "STATUS_CANCELLED": return MatchStatus.Cancelled;
        }
        return em.StatusState switch
        {
            "in" => MatchStatus.InPlay,
            "post" => MatchStatus.Finished,
            _ => MatchStatus.Scheduled,
        };
    }

    private Stage MapStage(EsMatch em)
    {
        switch (em.StageSlug)
        {
            case "group-stage": return Stage.Group;
            case "round-of-32": return Stage.RoundOf32;
            case "round-of-16": return Stage.RoundOf16;
            case "quarterfinals": return Stage.QuarterFinal;
            case "semifinals": return Stage.SemiFinal;
            case "3rd-place-match": return Stage.ThirdPlace;
            case "final": return Stage.Final;
        }
        _log.LogWarning("Unknown stage slug '{Slug}' for event {Id}", em.StageSlug, em.EventId);
        return em.GroupLetter is not null ? Stage.Group : Stage.RoundOf32;
    }

    /// <summary>Half splits derived from goal-event clocks (≤45' first half, ≤90' second, rest ET),
    /// reconciled against the authoritative final score.</summary>
    private static void FillScore(Match match, EsMatch em, EsCompetitor homeC, EsCompetitor awayC)
    {
        int hf = 0, hs = 0, he = 0, af = 0, asd = 0, ae = 0;
        foreach (var d in em.Details.Where(d => d.ScoringPlay && !d.Shootout))
        {
            bool isHome = d.TeamId == homeC.TeamId;
            // Own goals are attributed to the conceding player's team; the goal itself
            // counts for the opposition. (If a payload variant credits the benefiting team
            // instead, the score reconciliation below absorbs the drift.)
            if (d.OwnGoal) isHome = !isHome;

            if (d.ClockSeconds <= 2700) { if (isHome) hf++; else af++; }
            else if (d.ClockSeconds <= 5400) { if (isHome) hs++; else asd++; }
            else { if (isHome) he++; else ae++; }
        }

        bool extraTime = em.StatusName is "STATUS_FINAL_PEN" or "STATUS_FINAL_AET"
            || he > 0 || ae > 0
            || em.Details.Any(d => !d.Shootout && d.ClockSeconds > 5400);

        // Reconcile: the competitor score is the truth; push any drift into the last phase.
        int homeDrift = homeC.Score - (hf + hs + he);
        int awayDrift = awayC.Score - (af + asd + ae);
        if (extraTime) { he = Math.Max(0, he + homeDrift); ae = Math.Max(0, ae + awayDrift); }
        else { hs = Math.Max(0, hs + homeDrift); asd = Math.Max(0, asd + awayDrift); }

        match.HomeFirstHalf = hf; match.HomeSecondHalf = hs;
        match.AwayFirstHalf = af; match.AwaySecondHalf = asd;
        if (extraTime) { match.HomeExtraTime = he; match.AwayExtraTime = ae; }
        if (homeC.Shootout is not null || awayC.Shootout is not null)
        {
            match.HomePenalties = homeC.Shootout ?? 0;
            match.AwayPenalties = awayC.Shootout ?? 0;
        }
    }

    private static void FillEvents(Match match, EsMatch em, EsSummary? summary, Team home, Team away)
    {
        string CodeFor(string teamId) => teamId == home.ExternalId ? home.Code
            : teamId == away.ExternalId ? away.Code : "";

        foreach (var d in em.Details.Where(d => !d.Shootout))
        {
            MatchEventType? type =
                d.OwnGoal ? MatchEventType.OwnGoal
                : d.PenaltyKick || d.Type.StartsWith("Penalty", StringComparison.OrdinalIgnoreCase)
                    ? (d.ScoringPlay ? MatchEventType.Penalty : null)
                : d.ScoringPlay ? MatchEventType.Goal
                : d.RedCard ? MatchEventType.RedCard
                : d.YellowCard ? MatchEventType.YellowCard
                : null;
            if (type is null) continue;

            string detail = d.Type switch
            {
                "Goal - Header" => "header",
                "Goal - Free-kick" => "free-kick",
                "Goal - Volley" => "volley",
                "Penalty - Scored" => "penalty",
                "Own Goal" => "own goal",
                _ => "",
            };
            // Own goals credit the opposition on the scoreboard.
            string teamCode = type == MatchEventType.OwnGoal
                ? (CodeFor(d.TeamId) == home.Code ? away.Code : home.Code)
                : CodeFor(d.TeamId);

            match.Events.Add(new MatchEvent(MinuteOf(d.ClockSeconds), d.MinuteDisplay, type.Value,
                teamCode, d.PlayerName, detail));
        }

        if (summary is not null)
        {
            foreach (var sub in summary.Substitutions)
            {
                string teamCode = sub.TeamName == home.Name ? home.Code
                    : sub.TeamName == away.Name ? away.Code : "";
                var (playerIn, playerOut) = ParseSubText(sub.Text);
                match.Events.Add(new MatchEvent(MinuteOf(sub.ClockSeconds), sub.MinuteDisplay,
                    MatchEventType.Substitution, teamCode, playerIn,
                    playerOut.Length > 0 ? $"for {playerOut}" : ""));
            }
        }

        match.Events.Sort((a, b) => a.Minute.CompareTo(b.Minute));
    }

    private static int MinuteOf(double clockSeconds) =>
        clockSeconds <= 0 ? 0 : (int)Math.Ceiling(clockSeconds / 60.0);

    /// <summary>"Substitution, Spain. Ferran Torres replaces Álex Baena." → (in, out).</summary>
    private static (string In, string Out) ParseSubText(string text)
    {
        var idx = text.IndexOf(" replaces ", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return (text, "");
        var before = text[..idx];
        var playerIn = before[(before.LastIndexOf('.') + 1)..].Trim();
        var playerOut = text[(idx + " replaces ".Length)..].TrimEnd('.').Trim();
        return (playerIn, playerOut);
    }

    private static void FillLineups(Match match, EsSummary? summary, Team home, Team away)
    {
        if (summary is null) return;
        foreach (var lineup in summary.Lineups)
        {
            var team = lineup.TeamId == home.ExternalId ? home
                : lineup.TeamId == away.ExternalId ? away : null;
            if (team is null || lineup.Entries.Count == 0) continue;

            var squad = team.Players.GroupBy(p => p.Name).ToDictionary(g => g.Key, g => g.First());
            var byNumber = team.Players.Where(p => p.Number > 0).GroupBy(p => p.Number).ToDictionary(g => g.Key, g => g.First());
            var result = new MatchLineup { Formation = lineup.Formation };
            foreach (var entry in lineup.Entries.Where(e => e.Starter).OrderBy(e => e.FormationPlace))
                result.Starters.Add(Resolve(squad, byNumber, entry));
            foreach (var entry in lineup.Entries.Where(e => !e.Starter).OrderBy(e => e.Jersey))
                result.Bench.Add(Resolve(squad, byNumber, entry));

            if (match.Home == team) match.HomeLineup = result;
            else match.AwayLineup = result;
        }

        static Player Resolve(Dictionary<string, Player> squad, Dictionary<int, Player> byNumber, EsRosterEntry entry) =>
            squad.GetValueOrDefault(entry.Name) ?? byNumber.GetValueOrDefault(entry.Jersey) ?? new Player
            {
                Name = entry.Name,
                Position = ParsePosition(entry.Position),
                Number = entry.Jersey,
            };
    }

    private static void FillStats(Match match, EsSummary? summary, Team home, Team away)
    {
        // Cards prefer the boxscore counts; fall back to timeline events.
        int? homeCards = null, awayCards = null;
        if (summary is not null)
        {
            foreach (var line in summary.Stats)
            {
                bool isHome = line.TeamId == home.ExternalId;
                if (!isHome && line.TeamId != away.ExternalId) continue;

                int? cards = null;
                if (line.Values.TryGetValue("yellowCards", out var y) && int.TryParse(y, out var yi)) cards = yi;
                if (line.Values.TryGetValue("redCards", out var r) && int.TryParse(r, out var ri)) cards = (cards ?? 0) + ri;
                if (isHome) homeCards = cards; else awayCards = cards;

                if (isHome && line.Values.TryGetValue("possessionPct", out var poss)
                    && double.TryParse(poss, System.Globalization.CultureInfo.InvariantCulture, out var p))
                {
                    match.HomePossession = Math.Clamp((int)Math.Round(p), 1, 99);
                    match.PossessionFromFeed = true;
                }
            }
        }

        match.HomeCards = homeCards ?? match.Events.Count(e => IsCard(e) && e.TeamCode == home.Code);
        match.AwayCards = awayCards ?? match.Events.Count(e => IsCard(e) && e.TeamCode == away.Code);

        var homeLine = summary?.Stats.FirstOrDefault(l => l.TeamId == home.ExternalId)?.Values;
        var awayLine = summary?.Stats.FirstOrDefault(l => l.TeamId == away.ExternalId)?.Values;
        if (homeLine is not null && awayLine is not null)
        {
            foreach (var (label, key) in FeedStatLines)
                if (homeLine.TryGetValue(key, out var h) && awayLine.TryGetValue(key, out var a))
                    match.FeedStats.Add(new MatchStatLine(label, h, a));
            if (PassAccuracy(homeLine) is string hp && PassAccuracy(awayLine) is string ap)
                match.FeedStats.Add(new MatchStatLine("Pass accuracy %", hp, ap));
        }

        static bool IsCard(MatchEvent e) =>
            e.Type is MatchEventType.YellowCard or MatchEventType.SecondYellow or MatchEventType.RedCard;
    }

    /// <summary>Boxscore lines shown in the match centre, in display order (label, feed stat name).</summary>
    private static readonly (string Label, string Key)[] FeedStatLines =
    {
        ("Shots", "totalShots"),
        ("Shots on target", "shotsOnTarget"),
        ("Corners", "wonCorners"),
        ("Fouls", "foulsCommitted"),
        ("Offsides", "offsides"),
        ("Saves", "saves"),
    };

    /// <summary>The feed rounds passPct to one decimal of a fraction ("0.9"), so derive it from the counts.</summary>
    private static string? PassAccuracy(Dictionary<string, string> line) =>
        line.TryGetValue("accuratePasses", out var acc) && line.TryGetValue("totalPasses", out var tot)
        && int.TryParse(acc, out var a) && int.TryParse(tot, out var t) && t > 0
            ? Math.Round(100.0 * a / t).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : null;

    // ------------------------------------------------------------------ bracket

    private IReadOnlyList<BracketSlot> BuildBracket(LiveDataSet data, Dictionary<string, Team> teams,
        Dictionary<string, Match> matchByEventId)
    {
        var slots = new List<BracketSlot>();
        var stages = new (Stage Stage, string Slug, string Prefix)[]
        {
            (Stage.RoundOf32, "round-of-32", "R32-"),
            (Stage.RoundOf16, "round-of-16", "R16-"),
            (Stage.QuarterFinal, "quarterfinals", "QF"),
            (Stage.SemiFinal, "semifinals", "SF"),
            (Stage.ThirdPlace, "3rd-place-match", "3RD"),
            (Stage.Final, "final", "FINAL"),
        };

        foreach (var (stage, slug, prefix) in stages)
        {
            int i = 0;
            foreach (var em in data.Matches.Where(m => m.StageSlug == slug).OrderBy(m => m.KickOffUtc))
            {
                i++;
                var homeC = em.Competitors.FirstOrDefault(c => c.Home);
                var awayC = em.Competitors.FirstOrDefault(c => !c.Home);
                if (homeC is null || awayC is null) continue;

                var home = teams.GetValueOrDefault(homeC.TeamId);
                var away = teams.GetValueOrDefault(awayC.TeamId);
                slots.Add(new BracketSlot
                {
                    Stage = stage,
                    Label = stage is Stage.ThirdPlace or Stage.Final ? prefix : $"{prefix}{i}",
                    Home = home,
                    Away = away,
                    HomePlaceholder = home?.Code ?? homeC.Abbrev,
                    AwayPlaceholder = away?.Code ?? awayC.Abbrev,
                    Match = matchByEventId.GetValueOrDefault(em.EventId),
                });
            }
        }
        return slots;
    }
}

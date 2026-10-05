using WorldCupTerminal.Models;

namespace WorldCupTerminal.Services;

/// <summary>
/// Derives a complete <see cref="TournamentSnapshot"/> from a raw set of teams and matches:
/// per-team aggregates, the sentiment → stats → tags pipeline, group tables and the knockout
/// bracket. In simulated mode the bracket is provisionally seeded from the tables (mirroring
/// the original startup behaviour); in live mode the caller supplies the real bracket.
/// </summary>
public class TournamentSnapshotBuilder
{
    private readonly TeamAnalysisService _analysis;

    public TournamentSnapshotBuilder(TeamAnalysisService analysis) => _analysis = analysis;

    public TournamentSnapshot Build(List<Team> teams, List<Match> matches, DataMode mode,
        IReadOnlyList<BracketSlot>? liveBracket = null)
    {
        AggregateMatches(teams, matches);

        foreach (var t in teams) _analysis.RunSentiment(t);
        _analysis.ComputeStats(teams);
        _analysis.AssignTags(teams);

        var tables = new Dictionary<string, List<GroupStanding>>();
        foreach (var group in teams.Select(t => t.Group).Distinct())
            tables[group] = BuildTable(group, teams, matches);

        IReadOnlyList<BracketSlot> bracket;
        if (liveBracket is not null)
        {
            bracket = liveBracket;
        }
        else
        {
            bracket = BuildProvisionalBracket(tables);
            // Fold the (provisionally seeded) knockout ties into the schedule so they appear in
            // the fixtures feed and have working match-centre pages.
            matches.AddRange(bracket.Where(s => s.Match is not null).Select(s => s.Match!));
        }

        return new TournamentSnapshot
        {
            Teams = teams,
            Matches = matches,
            Bracket = bracket,
            Tables = tables,
            // GroupBy guards against duplicate codes in feed data — never worth crashing a refresh.
            ByCode = teams.GroupBy(t => t.Code).ToDictionary(g => g.Key, g => g.First()),
            Mode = mode,
            BuiltAtUtc = DateTime.UtcNow,
        };
    }

    // ------------------------------------------------------------------ aggregation

    private static void AggregateMatches(List<Team> teams, List<Match> matches)
    {
        var possSamples = teams.ToDictionary(t => t, _ => new List<int>());

        foreach (var m in matches.Where(m => m.Played))
        {
            Accumulate(m.Home, m.HomeFirstHalf, m.HomeSecondHalf, m.AwayFirstHalf, m.AwaySecondHalf,
                m.HomeSetPieceGoals, m.HomeCards, m.HomePossession, possSamples[m.Home]);
            Accumulate(m.Away, m.AwayFirstHalf, m.AwaySecondHalf, m.HomeFirstHalf, m.HomeSecondHalf,
                m.AwaySetPieceGoals, m.AwayCards, m.AwayPossession, possSamples[m.Away]);

            if (m.Commentary.Count >= 1) m.Home.MatchNotes.Add(m.Commentary[0]);
            if (m.Commentary.Count >= 2) m.Away.MatchNotes.Add(m.Commentary[1]);
        }

        foreach (var t in teams)
            t.AvgPossession = possSamples[t].Count == 0 ? 50 : possSamples[t].Average();
    }

    private static void Accumulate(Team t, int forFh, int forSh, int agFh, int agSh,
        int setPieces, int cards, int possession, List<int> possSamples)
    {
        t.Played++;
        t.GoalsFor += forFh + forSh;
        t.GoalsAgainst += agFh + agSh;
        t.FirstHalfGoals += forFh;
        t.SecondHalfGoals += forSh;
        t.FirstHalfConceded += agFh;
        t.SecondHalfConceded += agSh;
        t.SetPieceGoals += setPieces;
        t.Cards += cards;
        possSamples.Add(possession);
        if (agFh + agSh == 0) t.CleanSheets++;
        // Trailed at half-time but avoided defeat.
        if (forFh < agFh && (forFh + forSh) >= (agFh + agSh)) t.Comebacks++;
    }

    // ------------------------------------------------------------------ tables

    private static List<GroupStanding> BuildTable(string group, List<Team> teams, List<Match> matches)
    {
        var rows = teams.Where(t => t.Group == group)
            .ToDictionary(t => t, t => new GroupStanding { Team = t });

        foreach (var m in matches.Where(m => m.Played && m.Stage == Stage.Group && m.Group == group))
        {
            var h = rows[m.Home];
            var a = rows[m.Away];
            h.Played++; a.Played++;
            h.GoalsFor += m.HomeGoals; h.GoalsAgainst += m.AwayGoals;
            a.GoalsFor += m.AwayGoals; a.GoalsAgainst += m.HomeGoals;

            if (m.HomeGoals > m.AwayGoals) { h.Won++; a.Lost++; h.Form.Add('W'); a.Form.Add('L'); }
            else if (m.HomeGoals < m.AwayGoals) { a.Won++; h.Lost++; a.Form.Add('W'); h.Form.Add('L'); }
            else { h.Drawn++; a.Drawn++; h.Form.Add('D'); a.Form.Add('D'); }
        }

        var table = rows.Values
            .OrderByDescending(r => r.Points)
            .ThenByDescending(r => r.GoalDifference)
            .ThenByDescending(r => r.GoalsFor)
            .ThenBy(r => r.Team.FifaRank)
            .ToList();

        for (int i = 0; i < table.Count; i++) table[i].Position = i + 1;
        return table;
    }

    // ------------------------------------------------------------------ provisional bracket (simulated mode)

    private static IReadOnlyList<BracketSlot> BuildProvisionalBracket(
        Dictionary<string, List<GroupStanding>> tables)
    {
        Team? Qualifier(string group, int place) =>
            tables.GetValueOrDefault(group)?.ElementAtOrDefault(place - 1)?.Team;

        var slots = new List<BracketSlot>();

        // Round of 16: standard cross-group pairings. Provisionally filled from current tables.
        (string label, string home, string away)[] r16 =
        {
            ("R16-1", "1A", "2B"), ("R16-2", "1C", "2D"), ("R16-3", "1E", "2F"), ("R16-4", "1G", "2H"),
            ("R16-5", "1B", "2A"), ("R16-6", "1D", "2C"), ("R16-7", "1F", "2E"), ("R16-8", "1H", "2G"),
        };

        var r16Start = new DateTime(2026, 6, 23, 16, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < r16.Length; i++)
        {
            var (label, hp, ap) = r16[i];
            var home = Qualifier(hp[1].ToString(), hp[0] - '0');
            var away = Qualifier(ap[1].ToString(), ap[0] - '0');
            Match? match = null;
            if (home is not null && away is not null)
            {
                match = new Match
                {
                    Id = $"KO-{label}",
                    Home = home,
                    Away = away,
                    Stage = Stage.RoundOf16,
                    KickOff = r16Start.AddDays(i / 2).AddHours((i % 2) * 4),
                    Venue = "TBD",
                };
            }
            slots.Add(new BracketSlot
            {
                Stage = Stage.RoundOf16,
                Label = label,
                HomePlaceholder = hp,
                AwayPlaceholder = ap,
                Home = home,
                Away = away,
                Match = match,
            });
        }

        // Later rounds: structure only (winners are TBD while the tournament is in progress).
        AddTbd(slots, Stage.QuarterFinal, new[]
        {
            ("QF1", "W R16-1", "W R16-2"), ("QF2", "W R16-3", "W R16-4"),
            ("QF3", "W R16-5", "W R16-6"), ("QF4", "W R16-7", "W R16-8"),
        });
        AddTbd(slots, Stage.SemiFinal, new[]
        {
            ("SF1", "W QF1", "W QF2"), ("SF2", "W QF3", "W QF4"),
        });
        AddTbd(slots, Stage.Final, new[] { ("FINAL", "W SF1", "W SF2") });

        return slots;
    }

    private static void AddTbd(List<BracketSlot> slots, Stage stage, (string label, string home, string away)[] defs)
    {
        foreach (var (label, home, away) in defs)
            slots.Add(new BracketSlot
            {
                Stage = stage,
                Label = label,
                HomePlaceholder = home,
                AwayPlaceholder = away,
            });
    }
}

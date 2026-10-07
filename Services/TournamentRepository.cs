using WorldCupTerminal.Data;
using WorldCupTerminal.Models;

namespace WorldCupTerminal.Services;

/// <summary>
/// Thread-safe facade over the current <see cref="TournamentSnapshot"/>. The constructor
/// builds the simulated world synchronously so the site always renders at boot; the live
/// feed publishes refreshed snapshots via <see cref="Swap"/>. Snapshots are immutable after
/// publish, so every getter simply reads the current one — no locks needed.
/// </summary>
public class TournamentRepository
{
    private TournamentSnapshot _current;

    /// <summary>The fictional "today" the simulated world is anchored to (mid-tournament).</summary>
    private static readonly DateTime SimulatedNow = new(2026, 6, 18, 12, 0, 0, DateTimeKind.Utc);

    public TournamentRepository(TournamentSnapshotBuilder builder)
    {
        var (teams, matches) = TournamentData.Build();
        _current = builder.Build(teams, matches, DataMode.Simulated);
    }

    /// <summary>Atomically publish a new snapshot. In-flight requests keep the one they read.</summary>
    public void Swap(TournamentSnapshot snapshot) => Volatile.Write(ref _current, snapshot);

    private TournamentSnapshot Current => Volatile.Read(ref _current);

    public DataMode Mode => Current.Mode;
    public DateTime BuiltAtUtc => Current.BuiltAtUtc;
    public IReadOnlyList<Team> Teams => Current.Teams;
    public IReadOnlyList<Match> Matches => Current.Matches;
    public IReadOnlyList<BracketSlot> Bracket => Current.Bracket;

    /// <summary>"Now" for countdowns and scheduling: real time in live mode, the fictional anchor otherwise.</summary>
    public DateTime Now => Current.Mode == DataMode.Simulated ? SimulatedNow : DateTime.UtcNow;

    public IEnumerable<string> Groups => Current.Tables.Keys.OrderBy(g => g);

    public Team? GetTeam(string code) => Current.ByCode.GetValueOrDefault(code.ToUpperInvariant());

    public IReadOnlyList<GroupStanding> Table(string group)
    {
        var snapshot = Current;
        return snapshot.Tables.GetValueOrDefault(group) ?? new List<GroupStanding>();
    }

    public IReadOnlyList<Match> Upcoming(int? take = null)
    {
        var q = Current.Matches
            .Where(m => !m.Played && !m.IsLive)
            .OrderBy(m => m.KickOff).AsEnumerable();
        if (take is int n) q = q.Take(n);
        return q.ToList();
    }

    public IReadOnlyList<Match> Recent(int? take = null)
    {
        var q = Current.Matches.Where(m => m.Played).OrderByDescending(m => m.KickOff).AsEnumerable();
        if (take is int n) q = q.Take(n);
        return q.ToList();
    }

    /// <summary>Matches currently in play (or paused at half-time), if the live feed has any.</summary>
    public IReadOnlyList<Match> Live() =>
        Current.Matches.Where(m => m.IsLive).OrderBy(m => m.KickOff).ToList();

    public IReadOnlyList<Match> MatchesFor(Team t)
    {
        var snapshot = Current;
        return snapshot.Matches.Where(m => m.Home == t || m.Away == t).OrderBy(m => m.KickOff).ToList();
    }

    /// <summary>Cumulative goal difference over played matches (with a 0 baseline) — a momentum trace.</summary>
    public IReadOnlyList<double> Momentum(Team t)
    {
        var series = new List<double> { 0 };
        double cum = 0;
        foreach (var m in MatchesFor(t).Where(m => m.Played))
        {
            bool home = m.Home == t;
            int gf = home ? m.HomeGoals : m.AwayGoals;
            int ga = home ? m.AwayGoals : m.HomeGoals;
            cum += gf - ga;
            series.Add(cum);
        }
        return series;
    }

    public IEnumerable<BracketSlot> SlotsFor(Stage stage) => Current.Bracket.Where(s => s.Stage == stage);

    /// <summary>The played final, once there is one.</summary>
    public Match? FinalMatch => Current.Matches.FirstOrDefault(m => m.Stage == Stage.Final && m.Played);

    public Match? ThirdPlaceMatch => Current.Matches.FirstOrDefault(m => m.Stage == Stage.ThirdPlace && m.Played);

    public bool IsComplete => FinalMatch is not null;

    /// <summary>Goal-scorers ranked by goals (own goals excluded), ties by fewer penalties then name.</summary>
    public IReadOnlyList<(string Player, Team Team, int Goals, int Penalties)> TopScorers(int take)
    {
        var snapshot = Current;
        return snapshot.Matches.Where(m => m.Played)
            .SelectMany(m => m.Events)
            .Where(e => e.Type is MatchEventType.Goal or MatchEventType.Penalty && e.PlayerName.Length > 0)
            .GroupBy(e => (e.PlayerName, e.TeamCode))
            .Select(g => (Player: g.Key.PlayerName, Team: snapshot.ByCode.GetValueOrDefault(g.Key.TeamCode),
                Goals: g.Count(), Penalties: g.Count(e => e.Type == MatchEventType.Penalty)))
            .Where(s => s.Team is not null)
            .OrderByDescending(s => s.Goals).ThenBy(s => s.Penalties).ThenBy(s => s.Player)
            .Take(take)
            .Select(s => (s.Player, s.Team!, s.Goals, s.Penalties))
            .ToList();
    }

    /// <summary>
    /// How far a team got: "champions", "runners-up", "third place", "fourth place", or the round
    /// it went out in. Null while the team is still alive (or, before any knockout, still in its group).
    /// </summary>
    public string? FinishFor(Team t)
    {
        var knockouts = MatchesFor(t).Where(m => m.Stage != Stage.Group && m.Played).ToList();
        var last = knockouts.OrderByDescending(m => m.Stage).ThenByDescending(m => m.KickOff).FirstOrDefault();
        if (last is null) return IsComplete || Current.Bracket.Any(s => s.Match?.Played == true) ? "group stage" : null;

        bool won = last.Winner == t;
        return last.Stage switch
        {
            Stage.Final => won ? "champions" : "runners-up",
            Stage.ThirdPlace => won ? "third place" : "fourth place",
            Stage.SemiFinal when won => null,   // awaiting the final
            Stage.SemiFinal => IsComplete ? "semi-finals" : null,   // the 3rd-place match decides
            _ when won => null,
            Stage.RoundOf32 => "round of 32",
            Stage.RoundOf16 => "round of 16",
            _ => "quarter-finals",
        };
    }
}

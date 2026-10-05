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
}

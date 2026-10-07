using WorldCupTerminal.Models;

namespace WorldCupTerminal.Services;

/// <summary>Where the current world came from. Drives system alerts and the footer indicator.</summary>
public enum DataMode
{
    Simulated,
    CachedLive,
    Live,
    /// <summary>The tournament is over: real final results served from the archive/cache, no polling.</summary>
    Archived,
}

/// <summary>
/// One immutable, fully-derived view of the tournament. Snapshots are built off to the side
/// and published atomically into <see cref="TournamentRepository"/>; a published snapshot is
/// never mutated, so requests can read it without locks.
/// </summary>
public sealed class TournamentSnapshot
{
    public required IReadOnlyList<Team> Teams { get; init; }
    public required IReadOnlyList<Match> Matches { get; init; }
    public required IReadOnlyList<BracketSlot> Bracket { get; init; }
    public required IReadOnlyDictionary<string, List<GroupStanding>> Tables { get; init; }
    public required IReadOnlyDictionary<string, Team> ByCode { get; init; }
    public DataMode Mode { get; init; }
    public DateTime BuiltAtUtc { get; init; }
}

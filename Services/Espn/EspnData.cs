namespace WorldCupTerminal.Services.Espn;

// Compact, cache-friendly records holding only the fields the site consumes. The raw ESPN
// payloads are megabytes of news/odds/video noise; parsing down to these keeps the cache
// file small and the domain mapping simple.

public record EsTeam(string Id, string Abbrev, string Name);

public record EsCompetitor(string TeamId, string Abbrev, string Name, bool Home,
    int Score, int? Shootout, bool Winner);

/// <summary>One scoreboard timeline entry (goal or card) with its clock position.</summary>
public record EsDetail(string Type, double ClockSeconds, string MinuteDisplay, string TeamId,
    bool ScoringPlay, bool OwnGoal, bool PenaltyKick, bool RedCard, bool YellowCard, bool Shootout,
    string PlayerName);

public record EsMatch(string EventId, DateTime KickOffUtc, string StageSlug, string? GroupLetter,
    string Venue, string StatusName, string StatusState, bool Completed,
    List<EsCompetitor> Competitors, List<EsDetail> Details);

public record EsRosterEntry(string Name, string Position, int Jersey, bool Starter,
    bool SubbedIn, bool SubbedOut, int FormationPlace);

public record EsLineup(string TeamId, string Formation, List<EsRosterEntry> Entries);

/// <summary>Boxscore stat lines keyed by ESPN stat name (e.g. "possessionPct" → "67.8").</summary>
public record EsStatLine(string TeamId, Dictionary<string, string> Values);

public record EsSubstitution(double ClockSeconds, string MinuteDisplay, string TeamName, string Text);

public record EsSummary(string EventId, List<EsLineup> Lineups, List<EsStatLine> Stats,
    List<EsSubstitution> Substitutions);

/// <summary>Everything one scrape produced. This is what the cache file persists.</summary>
public class LiveDataSet
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public DateTime FetchedAtUtc { get; set; }
    public List<EsTeam> Teams { get; set; } = new();
    public List<EsMatch> Matches { get; set; } = new();

    /// <summary>Per-event summaries keyed by event id. Finished-match entries are immutable,
    /// so they survive across refreshes and are never re-fetched.</summary>
    public Dictionary<string, EsSummary> Summaries { get; set; } = new();

    /// <summary>True once the final has been played — from then on the data set never changes.</summary>
    public bool IsTournamentComplete() => Matches.Any(m => m.StageSlug == "final" && m.Completed);
}

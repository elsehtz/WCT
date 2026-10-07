namespace WorldCupTerminal.Services;

/// <summary>A system-level notice surfaced in the title bar and the dashboard alert popup.</summary>
/// <param name="Message">Short headline shown on the chip and in the popup.</param>
/// <param name="Detail">Longer explanation shown in the popup and as the chip tooltip.</param>
/// <param name="Severity">"warn" (amber) or "error" (red).</param>
public record SystemAlert(string Message, string Detail, string Severity);

/// <summary>
/// Single source of truth for active system notices, derived from the live-feed status.
/// The title-bar chips and the dashboard popup both render from this list, so a mode change
/// updates everywhere at once.
/// </summary>
public class SystemAlertProvider
{
    private readonly LiveFeedStatus _status;

    public SystemAlertProvider(LiveFeedStatus status) => _status = status;

    public IReadOnlyList<SystemAlert> Active => _status.Mode switch
    {
        DataMode.Archived => new List<SystemAlert>
        {
            new("tournament complete · final results",
                $"Scores, scorers, cards, lineups and match statistics are the feed's final data (captured {_status.LastSuccessUtc:dd MMM yyyy}); squads, coaches and pre-tournament FIFA rankings come from the published tournament squads. Heat-maps, scouting prose, commentary and the sentiment-derived team ratings are modelled.",
                "warn"),
        },
        DataMode.Live => new List<SystemAlert>
        {
            new("live feed active · modelled detail",
                $"Scores, lineups, cards and possession are scraped from a public feed on a daily cycle (last sync {_status.LastSuccessUtc:HH:mm} UTC); heat-maps and scouting prose are modelled around the real results.",
                "warn"),
        },
        DataMode.CachedLive => new List<SystemAlert>
        {
            new("live feed unreachable — showing cached data",
                $"The last scrape attempt failed{(string.IsNullOrEmpty(_status.LastError) ? "" : $" ({_status.LastError})")}; match centres show real data cached at {_status.LastSuccessUtc:u}.",
                "error"),
            new("modelled detail",
                "Heat-maps and scouting prose are modelled around the real results.",
                "warn"),
        },
        _ => new List<SystemAlert>
        {
            new("data is virtually generated!",
                "All teams, players, coaches and match results in this build are procedurally generated — this is not a live feed.",
                "warn"),
            new("match information capture is down",
                "Live match-information capture is currently offline; match centres display modelled data only.",
                "error"),
        },
    };
}

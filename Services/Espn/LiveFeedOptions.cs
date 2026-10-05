namespace WorldCupTerminal.Services.Espn;

/// <summary>Configuration for the daily live-feed scrape ("LiveFeed" section of appsettings).</summary>
public class LiveFeedOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>ESPN's unofficial site API root for the competition. Unofficial: shapes may change.</summary>
    public string BaseUrl { get; set; } = "https://site.api.espn.com/apis/site/v2/sports/soccer/fifa.world/";

    /// <summary>Scoreboard date range covering the whole tournament (YYYYMMDD-YYYYMMDD).</summary>
    public string TournamentDates { get; set; } = "20260611-20260719";

    /// <summary>JSON snapshot of the last successful scrape, relative to the content root.</summary>
    public string CacheFile { get; set; } = "App_Data/live-cache.json";

    /// <summary>UTC hour of the daily refresh.</summary>
    public int DailyRefreshUtcHour { get; set; } = 6;

    /// <summary>A cache older than this triggers an immediate scrape at boot.</summary>
    public int StaleAfterHours { get; set; } = 12;

    /// <summary>Minimum spacing between requests — polite scraping.</summary>
    public int RequestDelayMs { get; set; } = 1000;

    /// <summary>Test mode: serve canned JSON from <see cref="FixtureDirectory"/> instead of HTTP.</summary>
    public bool UseFixtureFiles { get; set; }

    public string FixtureDirectory { get; set; } = "Data/Fixtures";
}

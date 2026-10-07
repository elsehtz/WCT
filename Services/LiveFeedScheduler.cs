using Microsoft.Extensions.Options;
using WorldCupTerminal.Services.Espn;

namespace WorldCupTerminal.Services;

/// <summary>
/// Drives the daily scrape: at boot it publishes the cache (if any) and scrapes immediately
/// when that cache is stale or absent, then refreshes once per day at the configured UTC
/// hour, with short retry backoff on failure. Trigger computation lives in
/// <see cref="ComputeNextRefreshUtc"/> so a finer match-window cadence can be added later.
/// </summary>
public class LiveFeedScheduler : BackgroundService
{
    private static readonly TimeSpan[] RetryBackoff =
    {
        TimeSpan.FromMinutes(15), TimeSpan.FromHours(1), TimeSpan.FromHours(6),
    };

    private readonly LiveFeedService _feed;
    private readonly LiveFeedStatus _status;
    private readonly LiveFeedOptions _options;
    private readonly ILogger<LiveFeedScheduler> _log;

    public LiveFeedScheduler(LiveFeedService feed, LiveFeedStatus status,
        IOptions<LiveFeedOptions> options, ILogger<LiveFeedScheduler> log)
    {
        _feed = feed;
        _status = status;
        _options = options.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            _log.LogInformation("Live feed disabled — running in simulated mode");
            return;
        }

        // Yield so host startup isn't blocked by the first scrape.
        await Task.Yield();

        try
        {
            _feed.TryLoadCache();

            if (StopPolling()) return;

            var age = DateTime.UtcNow - (_feed.LastFetchUtc ?? DateTime.MinValue);
            if (age > TimeSpan.FromHours(_options.StaleAfterHours))
            {
                _log.LogInformation("Live data is {Age:0.#} h old — scraping now", age.TotalHours);
                await RefreshWithRetriesAsync(ct);
            }

            while (!ct.IsCancellationRequested)
            {
                if (StopPolling()) return;

                var next = ComputeNextRefreshUtc(DateTime.UtcNow, _options.DailyRefreshUtcHour);
                _status.NextRefreshUtc = next;
                _log.LogInformation("Next live refresh scheduled for {Next:u}", next);

                var delay = next - DateTime.UtcNow;
                if (delay > TimeSpan.Zero) await Task.Delay(delay, ct);

                await RefreshWithRetriesAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // normal shutdown
        }
    }

    /// <summary>Finished results never change, so once the final is in hand the feed is left alone.</summary>
    private bool StopPolling()
    {
        if (!_feed.TournamentComplete || _options.RefreshAfterCompletion) return false;
        _status.NextRefreshUtc = null;
        _log.LogInformation("Tournament complete — serving final results without further scrapes");
        return true;
    }

    /// <summary>The next daily refresh instant: today at the configured UTC hour, or tomorrow.</summary>
    public static DateTime ComputeNextRefreshUtc(DateTime nowUtc, int dailyHourUtc)
    {
        var today = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, dailyHourUtc, 0, 0, DateTimeKind.Utc);
        return today > nowUtc ? today : today.AddDays(1);
    }

    private async Task RefreshWithRetriesAsync(CancellationToken ct)
    {
        if (await _feed.RefreshAsync(ct)) return;

        foreach (var backoff in RetryBackoff)
        {
            _log.LogWarning("Live refresh failed — retrying in {Backoff}", backoff);
            _status.NextRefreshUtc = DateTime.UtcNow + backoff;
            await Task.Delay(backoff, ct);
            if (await _feed.RefreshAsync(ct)) return;
        }
        _log.LogWarning("Live refresh still failing — waiting for the next daily slot");
    }
}

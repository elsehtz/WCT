using Microsoft.Extensions.Options;
using WorldCupTerminal.Services.Espn;

namespace WorldCupTerminal.Services;

/// <summary>
/// Orchestrates one refresh: scrape → cache → build world → embellish → derive snapshot →
/// swap into the repository. A failed scrape never degrades the site — the current snapshot
/// stays, and at boot a cache file (if any) is published while the first scrape runs.
/// </summary>
public class LiveFeedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LiveDataCache _cache;
    private readonly LiveWorldBuilder _worldBuilder;
    private readonly MatchEmbellisher _embellisher;
    private readonly TournamentSnapshotBuilder _snapshotBuilder;
    private readonly TournamentRepository _repo;
    private readonly LiveFeedStatus _status;
    private readonly LiveFeedOptions _options;
    private readonly ILogger<LiveFeedService> _log;

    private LiveDataSet? _lastData;

    public LiveFeedService(IServiceScopeFactory scopeFactory, LiveDataCache cache,
        LiveWorldBuilder worldBuilder, MatchEmbellisher embellisher,
        TournamentSnapshotBuilder snapshotBuilder, TournamentRepository repo,
        LiveFeedStatus status, IOptions<LiveFeedOptions> options, ILogger<LiveFeedService> log)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _worldBuilder = worldBuilder;
        _embellisher = embellisher;
        _snapshotBuilder = snapshotBuilder;
        _repo = repo;
        _status = status;
        _options = options.Value;
        _log = log;
    }

    /// <summary>Age of the newest data we hold (cache or last scrape); null when none.</summary>
    public DateTime? LastFetchUtc => _lastData?.FetchedAtUtc;

    /// <summary>True once the data we hold includes a played final.</summary>
    public bool TournamentComplete => _lastData?.IsTournamentComplete() == true;

    /// <summary>Publish the cache file, if present, so a restart renders live data instantly.</summary>
    public bool TryLoadCache()
    {
        var data = _cache.Load();
        if (data is null) return false;

        _lastData = data;
        Publish(data, DataMode.CachedLive);
        _status.LastSuccessUtc = data.FetchedAtUtc;
        _log.LogInformation("Published live cache from {FetchedAt:u} ({Teams} teams, {Matches} matches)",
            data.FetchedAtUtc, data.Teams.Count, data.Matches.Count);
        return true;
    }

    public async Task<bool> RefreshAsync(CancellationToken ct)
    {
        if (!_options.Enabled) return false;

        try
        {
            // A fresh scope gives each scrape a fresh typed HttpClient from the factory pool.
            using var scope = _scopeFactory.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<IEspnClient>();

            var teams = await client.GetTeamsAsync(ct);
            var matches = await client.GetScoreboardAsync(ct);

            // Guard against "valid but empty" responses replacing a good world.
            if (teams is null || teams.Count < 8 || matches is null || matches.Count < 4)
            {
                Fail($"scoreboard/teams unavailable (teams: {teams?.Count ?? 0}, matches: {matches?.Count ?? 0})");
                return false;
            }

            var summaries = await FetchSummariesAsync(client, matches, ct);

            var data = new LiveDataSet
            {
                FetchedAtUtc = DateTime.UtcNow,
                Teams = teams,
                Matches = matches,
                Summaries = summaries,
            };
            _cache.Save(data);
            _lastData = data;

            Publish(data, DataMode.Live);
            _status.LastSuccessUtc = data.FetchedAtUtc;
            _status.LastError = null;
            _log.LogInformation("Live refresh complete: {Teams} teams, {Matches} matches, {Summaries} summaries",
                teams.Count, matches.Count, summaries.Count);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Live refresh failed");
            Fail(ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Summaries carry lineups/stats/subs. Finished matches never change, so previously
    /// fetched ones are reused; only new (finished or in-play) events cost a request.
    /// </summary>
    private async Task<Dictionary<string, EsSummary>> FetchSummariesAsync(IEspnClient client,
        List<EsMatch> matches, CancellationToken ct)
    {
        var previous = _lastData ?? _cache.Load();
        var previousCompleted = previous?.Matches.Where(m => m.Completed).Select(m => m.EventId).ToHashSet()
            ?? new HashSet<string>();

        var summaries = new Dictionary<string, EsSummary>();
        foreach (var m in matches)
        {
            bool wantSummary = m.Completed || m.StatusState == "in";
            if (!wantSummary) continue;

            // Reuse when the match was already finished at the previous scrape.
            if (m.Completed && previousCompleted.Contains(m.EventId)
                && previous!.Summaries.TryGetValue(m.EventId, out var cached))
            {
                summaries[m.EventId] = cached;
                continue;
            }

            var fetched = await client.GetSummaryAsync(m.EventId, ct);
            if (fetched is not null)
                summaries[m.EventId] = fetched;
            else if (previous?.Summaries.TryGetValue(m.EventId, out var stale) == true)
                summaries[m.EventId] = stale!;   // better a stale summary than none
        }
        return summaries;
    }

    private void Publish(LiveDataSet data, DataMode mode)
    {
        // A finished tournament is final whichever way it arrived (scrape, cache or archive).
        if (data.IsTournamentComplete()) mode = DataMode.Archived;

        var (teams, matches, bracket) = _worldBuilder.Build(data);
        _embellisher.EmbellishAll(matches);
        var snapshot = _snapshotBuilder.Build(teams, matches, mode, bracket);
        _repo.Swap(snapshot);
        _status.Mode = mode;
    }

    private void Fail(string error)
    {
        _status.LastError = error;
        // First scrape failed with nothing published yet — surface the cache if one exists.
        if (_status.Mode == DataMode.Simulated && TryLoadCache())
            return;
    }
}

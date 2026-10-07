using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace WorldCupTerminal.Services.Espn;

public interface IEspnClient
{
    Task<List<EsTeam>?> GetTeamsAsync(CancellationToken ct);
    Task<List<EsMatch>?> GetScoreboardAsync(CancellationToken ct);
    Task<EsSummary?> GetSummaryAsync(string eventId, CancellationToken ct);
}

/// <summary>
/// Fetches ESPN's unofficial site API. Requests are spaced by <see cref="LiveFeedOptions.RequestDelayMs"/>
/// (polite scraping); a failed or unparsable response returns null so the caller can skip or
/// fall back rather than fault the whole refresh. Standard resilience (retry/timeout) comes
/// from the Aspire service defaults on the underlying HttpClient.
/// </summary>
public class EspnClient : IEspnClient
{
    private readonly HttpClient _http;
    private readonly LiveFeedOptions _options;
    private readonly ILogger<EspnClient> _log;
    private DateTime _lastRequestUtc = DateTime.MinValue;

    public EspnClient(HttpClient http, IOptions<LiveFeedOptions> options, ILogger<EspnClient> log)
    {
        _http = http;
        _options = options.Value;
        _log = log;
    }

    public async Task<List<EsTeam>?> GetTeamsAsync(CancellationToken ct)
    {
        using var doc = await GetJsonAsync("teams", ct);
        return doc is null ? null : EspnParser.ParseTeams(doc.RootElement);
    }

    /// <summary>
    /// ESPN now answers date-range scoreboard queries for this competition with HTTP 400, so the
    /// tournament window is walked one day at a time and merged by event id (a late kick-off can
    /// appear under two calendar days). Any failed day voids the whole result — a partial
    /// schedule must never replace a complete one.
    /// </summary>
    public async Task<List<EsMatch>?> GetScoreboardAsync(CancellationToken ct)
    {
        var (from, to) = ParseDateRange(_options.TournamentDates);
        var byId = new Dictionary<string, EsMatch>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            using var doc = await GetJsonAsync($"scoreboard?dates={day:yyyyMMdd}", ct);
            if (doc is null)
            {
                _log.LogWarning("Scoreboard for {Day:yyyy-MM-dd} unavailable — abandoning this scrape", day);
                return null;
            }
            foreach (var m in EspnParser.ParseScoreboard(doc.RootElement))
                byId[m.EventId] = m;
        }
        return byId.Values.OrderBy(m => m.KickOffUtc).ToList();
    }

    /// <summary>"YYYYMMDD-YYYYMMDD" (or a single "YYYYMMDD") → inclusive date range.</summary>
    private static (DateOnly From, DateOnly To) ParseDateRange(string range)
    {
        var parts = range.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var from = DateOnly.ParseExact(parts[0], "yyyyMMdd", CultureInfo.InvariantCulture);
        var to = parts.Length > 1 ? DateOnly.ParseExact(parts[1], "yyyyMMdd", CultureInfo.InvariantCulture) : from;
        return (from, to);
    }

    public async Task<EsSummary?> GetSummaryAsync(string eventId, CancellationToken ct)
    {
        using var doc = await GetJsonAsync($"summary?event={Uri.EscapeDataString(eventId)}", ct);
        return doc is null ? null : EspnParser.ParseSummary(eventId, doc.RootElement);
    }

    private async Task<JsonDocument?> GetJsonAsync(string relativeUrl, CancellationToken ct)
    {
        // Space requests out; the feed is scraped, not subscribed to.
        var wait = _lastRequestUtc.AddMilliseconds(_options.RequestDelayMs) - DateTime.UtcNow;
        if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
        _lastRequestUtc = DateTime.UtcNow;

        try
        {
            using var response = await _http.GetAsync(relativeUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("Live feed request {Url} returned {Status}", relativeUrl, response.StatusCode);
                return null;
            }
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Live feed request {Url} failed", relativeUrl);
            return null;
        }
    }
}

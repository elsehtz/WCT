using System.Text.Json;
using Microsoft.Extensions.Options;

namespace WorldCupTerminal.Services.Espn;

/// <summary>
/// Test double activated by LiveFeed:UseFixtureFiles — serves canned ESPN-format JSON from
/// the fixture directory through the same parser as the real client, so the whole pipeline
/// can be exercised offline. Files: teams.json, scoreboard.json, summary-{eventId}.json.
/// </summary>
public class FixtureFileEspnClient : IEspnClient
{
    private readonly string _dir;
    private readonly ILogger<FixtureFileEspnClient> _log;

    public FixtureFileEspnClient(IOptions<LiveFeedOptions> options, IWebHostEnvironment env,
        ILogger<FixtureFileEspnClient> log)
    {
        _dir = Path.Combine(env.ContentRootPath, options.Value.FixtureDirectory);
        _log = log;
    }

    public Task<List<EsTeam>?> GetTeamsAsync(CancellationToken ct) =>
        Task.FromResult(Parse("teams.json", EspnParser.ParseTeams));

    public Task<List<EsMatch>?> GetScoreboardAsync(CancellationToken ct) =>
        Task.FromResult(Parse("scoreboard.json", EspnParser.ParseScoreboard));

    public Task<EsSummary?> GetSummaryAsync(string eventId, CancellationToken ct) =>
        Task.FromResult(Parse($"summary-{eventId}.json", root => EspnParser.ParseSummary(eventId, root)));

    private T? Parse<T>(string file, Func<JsonElement, T> parser) where T : class
    {
        var path = Path.Combine(_dir, file);
        if (!File.Exists(path))
        {
            _log.LogDebug("Fixture file {File} not found", path);
            return null;
        }
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return parser(doc.RootElement);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Fixture file {File} could not be parsed", path);
            return null;
        }
    }
}

using System.Text.Json;
using Microsoft.Extensions.Options;

namespace WorldCupTerminal.Services.Espn;

/// <summary>
/// Persists the last successful scrape to a local JSON file so restarts render live data
/// immediately (and without re-hitting the feed). Saves are atomic-ish (write tmp, then
/// move); a missing/corrupt/old-schema file just means "no cache". When there is no local
/// cache, the committed archive of the finished tournament stands in for it.
/// </summary>
public class LiveDataCache
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly string _path;
    private readonly string? _archivePath;
    private readonly ILogger<LiveDataCache> _log;

    public LiveDataCache(IOptions<LiveFeedOptions> options, IWebHostEnvironment env, ILogger<LiveDataCache> log)
    {
        _path = Path.Combine(env.ContentRootPath, options.Value.CacheFile);
        _archivePath = string.IsNullOrWhiteSpace(options.Value.ArchiveFile)
            ? null
            : Path.Combine(env.ContentRootPath, options.Value.ArchiveFile);
        _log = log;
    }

    /// <summary>The local cache if present, otherwise the committed archive of the finished tournament.</summary>
    public LiveDataSet? Load() => Read(_path) ?? (_archivePath is null ? null : Read(_archivePath));

    private LiveDataSet? Read(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var data = JsonSerializer.Deserialize<LiveDataSet>(File.ReadAllText(path), JsonOpts);
            if (data is null || data.SchemaVersion != LiveDataSet.CurrentSchemaVersion)
            {
                _log.LogWarning("Live data at {Path} has schema {Version}; ignoring",
                    path, data?.SchemaVersion);
                return null;
            }
            return data;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Live data at {Path} could not be read", path);
            return null;
        }
    }

    public void Save(LiveDataSet data)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(data, JsonOpts));
            File.Move(tmp, _path, overwrite: true);
        }
        catch (Exception ex)
        {
            // Cache is an optimisation; a failed save must not fail the refresh.
            _log.LogWarning(ex, "Live cache at {Path} could not be written", _path);
        }
    }
}

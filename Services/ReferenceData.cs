using System.Text.Json;
using Microsoft.Extensions.Options;
using WorldCupTerminal.Services.Espn;

namespace WorldCupTerminal.Services;

/// <summary>One registered squad member as listed for the tournament.</summary>
public record RefPlayer(int Number, string Pos, string Name, DateOnly? Dob, string Club, int? Caps, bool Captain);

/// <summary>Facts about a qualified nation that the scraped feed lacks or gets wrong.</summary>
public record RefTeam(string WikiName, string Coach, string? CoachNationality, string? CoachNote,
    int FifaRank, string Confederation, List<RefPlayer> Players);

/// <summary>
/// Curated reference data for the real 2026 field (Data/Reference/wc2026-reference.json, built
/// from Wikipedia by tools/build_reference.py): pre-tournament FIFA ranking, confederation,
/// head coach and the registered 26-man squads with dates of birth and clubs. Keyed by the
/// three-letter code, which matches the feed's team abbreviations. A missing or unreadable file
/// just means "no reference" — the world builder falls back to its modelled values.
/// </summary>
public class ReferenceData
{
    /// <summary>Opening day — ages are reported as they stood at kick-off of the tournament.</summary>
    public static readonly DateOnly TournamentStart = new(2026, 6, 11);

    private record RefFile(string Source, string RankingRelease, Dictionary<string, RefTeam> Teams);

    private readonly Dictionary<string, RefTeam> _teams;

    public ReferenceData(IOptions<LiveFeedOptions> options, IWebHostEnvironment env, ILogger<ReferenceData> log)
    {
        _teams = new Dictionary<string, RefTeam>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(options.Value.ReferenceFile)) return;

        var path = Path.Combine(env.ContentRootPath, options.Value.ReferenceFile);
        try
        {
            var file = JsonSerializer.Deserialize<RefFile>(File.ReadAllText(path),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (file?.Teams is not null)
                foreach (var (code, team) in file.Teams) _teams[code] = team;
            log.LogInformation("Loaded reference data for {Count} teams from {Path}", _teams.Count, path);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Reference data at {Path} could not be read; using modelled values", path);
        }
    }

    public RefTeam? Get(string code) => _teams.GetValueOrDefault(code);

    public static int AgeAt(DateOnly dob, DateOnly on) =>
        on.Year - dob.Year - (on < dob.AddYears(on.Year - dob.Year) ? 1 : 0);
}

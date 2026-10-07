namespace WorldCupTerminal.Models;

/// <param name="Note">Optional context, e.g. a mid-tournament change of coach.</param>
public record Coach(string Name, string Nationality, string? Note = null);

public class Player
{
    public required string Name { get; init; }
    public Position Position { get; init; }
    public int Number { get; init; }
    /// <summary>Age in years; 0 when unknown.</summary>
    public int Age { get; init; }
    // The feed carries no club data; live mode fills it from the reference squads, else "—".
    public string Club { get; init; } = "—";
    public int Goals { get; set; }
    public int Assists { get; set; }

    /// <summary>Scouting prose fed to the sentiment analyzer.</summary>
    public List<string> ScoutNotes { get; } = new();

    public string PositionGroup => Position switch
    {
        Position.GK => "Goalkeeper",
        Position.DF => "Defender",
        Position.MF => "Midfielder",
        _ => "Forward",
    };
}

/// <summary>The five derived attributes, each scaled 0..100.</summary>
public class TeamStats
{
    public double Aggression { get; set; }
    public double Offense { get; set; }
    public double Defense { get; set; }
    public double Chemistry { get; set; }
    public double Technical { get; set; }

    public double this[StatDimension d] => d switch
    {
        StatDimension.Aggression => Aggression,
        StatDimension.Offense => Offense,
        StatDimension.Defense => Defense,
        StatDimension.Chemistry => Chemistry,
        _ => Technical,
    };

    public double Overall => (Aggression + Offense + Defense + Chemistry + Technical) / 5.0;

    public IEnumerable<(StatDimension Dim, double Value)> All()
    {
        yield return (StatDimension.Aggression, Aggression);
        yield return (StatDimension.Offense, Offense);
        yield return (StatDimension.Defense, Defense);
        yield return (StatDimension.Chemistry, Chemistry);
        yield return (StatDimension.Technical, Technical);
    }
}

/// <summary>A behavioural label derived from the analysis. Kind drives the colour class in the UI.</summary>
public record TeamTag(string Label, string Description, string Kind);

public class Team
{
    public required string Name { get; init; }
    public required string Code { get; init; }          // 3-letter code, e.g. BRA
    public required string Country { get; init; }
    public required string Confederation { get; init; }
    public required string Group { get; init; }
    /// <summary>Upstream id when the team comes from the live feed; null for simulated teams.</summary>
    public string? ExternalId { get; init; }
    public int FifaRank { get; init; }
    public Archetype Archetype { get; init; }
    public required Coach Coach { get; init; }
    public List<Player> Players { get; } = new();

    /// <summary>Team-level identity prose, also fed to the sentiment analyzer.</summary>
    public required string StyleNote { get; init; }

    /// <summary>Per-match commentary about this team, accumulated from played fixtures and fed to the analyzer.</summary>
    public List<string> MatchNotes { get; } = new();

    // ---- Derived by the analysis pipeline ----
    public TeamStats Stats { get; set; } = new();
    public List<TeamTag> Tags { get; } = new();
    public SentimentBreakdown Sentiment { get; set; } = new();

    // Structured aggregates derived from played matches (filled by the repository).
    public int Played { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int FirstHalfGoals { get; set; }
    public int SecondHalfGoals { get; set; }
    public int FirstHalfConceded { get; set; }
    public int SecondHalfConceded { get; set; }
    public int SetPieceGoals { get; set; }
    public int Cards { get; set; }
    public double AvgPossession { get; set; }
    public int CleanSheets { get; set; }
    public int Comebacks { get; set; }     // matches where the team trailed at HT and did not lose

    public bool CoachIsForeign => !string.Equals(Coach.Nationality, Country, StringComparison.OrdinalIgnoreCase);
    public double AverageAge => Players.Where(p => p.Age > 0).Select(p => (double)p.Age).DefaultIfEmpty(0).Average();

    public string ColourClass { get; set; } = "c-green";   // accent for this team in the UI
}

/// <summary>Per-dimension sentiment totals captured before normalisation, for transparency in the UI.</summary>
public class SentimentBreakdown
{
    public Dictionary<StatDimension, double> Raw { get; } = new();
    public int PositiveHits { get; set; }
    public int NegativeHits { get; set; }
    public int Tokens { get; set; }
    public double Polarity { get; set; }   // -1..+1 overall tone

    public double Get(StatDimension d) => Raw.TryGetValue(d, out var v) ? v : 0;
}

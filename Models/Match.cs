namespace WorldCupTerminal.Models;

public class Match
{
    public required string Id { get; init; }
    public required Team Home { get; init; }
    public required Team Away { get; init; }
    public DateTime KickOff { get; init; }
    public Stage Stage { get; init; }
    public string Group { get; init; } = "";
    public required string Venue { get; init; }
    public bool Played { get; set; }

    /// <summary>Upstream id when the fixture comes from the live feed; null for simulated matches.</summary>
    public string? ExternalId { get; init; }
    public MatchStatus Status { get; set; } = MatchStatus.Scheduled;
    public bool IsLive => Status is MatchStatus.InPlay or MatchStatus.Paused;

    public int HomeFirstHalf { get; set; }
    public int HomeSecondHalf { get; set; }
    public int AwayFirstHalf { get; set; }
    public int AwaySecondHalf { get; set; }

    // Knockout extras; null when the match ended (or is expected to end) in 90 minutes.
    public int? HomeExtraTime { get; set; }
    public int? AwayExtraTime { get; set; }
    public int? HomePenalties { get; set; }
    public int? AwayPenalties { get; set; }
    public bool WentToExtraTime => HomeExtraTime is not null;
    public bool WentToPenalties => HomePenalties is not null;

    public int HomeSetPieceGoals { get; set; }
    public int AwaySetPieceGoals { get; set; }
    public int HomePossession { get; set; } = 50;   // percent, away = 100 - home
    /// <summary>True when possession came from the feed's boxscore (so it must not be re-modelled).</summary>
    public bool PossessionFromFeed { get; set; }
    public int HomeCards { get; set; }
    public int AwayCards { get; set; }

    /// <summary>Further boxscore lines from the feed (shots, corners, fouls…); empty when the feed had none.</summary>
    public List<MatchStatLine> FeedStats { get; } = new();

    public MatchLineup? HomeLineup { get; set; }
    public MatchLineup? AwayLineup { get; set; }

    /// <summary>Timeline of noteworthy moments (goals, cards, substitutions), minute order.</summary>
    public List<MatchEvent> Events { get; } = new();

    public List<string> Commentary { get; } = new();

    public int HomeGoals => HomeFirstHalf + HomeSecondHalf + (HomeExtraTime ?? 0);
    public int AwayGoals => AwayFirstHalf + AwaySecondHalf + (AwayExtraTime ?? 0);
    public int AwayPossession => 100 - HomePossession;

    public string ScoreLine => Played || IsLive ? $"{HomeGoals} - {AwayGoals}" : "vs";
    public string HalfTimeLine => Played ? $"(HT {HomeFirstHalf}-{AwayFirstHalf})" : "";

    /// <summary>Compact stage tag for fixture lists, e.g. "Grp A", "R32", "QF".</summary>
    public string StageLabel => Stage switch
    {
        Stage.Group => $"Grp {Group}",
        Stage.RoundOf32 => "R32",
        Stage.RoundOf16 => "R16",
        Stage.QuarterFinal => "QF",
        Stage.SemiFinal => "SF",
        Stage.ThirdPlace => "3RD",
        _ => "FINAL",
    };

    /// <summary>Long stage name for the match-centre header, e.g. "group A", "round of 32".</summary>
    public string StageTitle => Stage switch
    {
        Stage.Group => $"group {Group}",
        Stage.RoundOf32 => "round of 32",
        Stage.RoundOf16 => "round of 16",
        Stage.QuarterFinal => "quarter-final",
        Stage.SemiFinal => "semi-final",
        Stage.ThirdPlace => "third-place play-off",
        _ => "final",
    };

    public Team? Winner
    {
        get
        {
            if (!Played) return null;
            if (WentToPenalties) return HomePenalties > AwayPenalties ? Home : Away;
            if (HomeGoals == AwayGoals) return null;
            return HomeGoals > AwayGoals ? Home : Away;
        }
    }
}

/// <summary>A team's matchday selection: starting XI plus the bench.</summary>
public class MatchLineup
{
    public string Formation { get; init; } = "";
    public List<Player> Starters { get; } = new();
    public List<Player> Bench { get; } = new();
}

/// <summary>One timeline entry. MinuteDisplay preserves stoppage-time notation ("45'+2").</summary>
public record MatchEvent(int Minute, string MinuteDisplay, MatchEventType Type,
    string TeamCode, string PlayerName, string Detail);

/// <summary>One boxscore comparison row, values preformatted for display.</summary>
public record MatchStatLine(string Label, string Home, string Away);

/// <summary>One row of a group table.</summary>
public class GroupStanding
{
    public required Team Team { get; init; }
    public int Played { get; set; }
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDifference => GoalsFor - GoalsAgainst;
    public int Points => Won * 3 + Drawn;
    public int Position { get; set; }

    /// <summary>Recent results, newest last, as a sparkline-friendly W/D/L list.</summary>
    public List<char> Form { get; } = new();
}

/// <summary>A single slot in the knockout tree.</summary>
public class BracketSlot
{
    public Stage Stage { get; init; }
    public string Label { get; init; } = "";          // e.g. "R16-1"
    public Team? Home { get; set; }
    public Team? Away { get; set; }
    public string HomePlaceholder { get; set; } = "";  // e.g. "1A"
    public string AwayPlaceholder { get; set; } = "";  // e.g. "2B"
    public Match? Match { get; set; }
    public Team? Winner => Match?.Winner;
}

namespace WorldCupTerminal.Models;

/// <summary>The five derived team attributes the analysis pipeline produces.</summary>
public enum StatDimension
{
    Aggression,
    Offense,
    Defense,
    Chemistry,
    Technical,
}

public enum Position
{
    GK,
    DF,
    MF,
    FW,
}

public enum Stage
{
    Group,
    RoundOf32,
    RoundOf16,
    QuarterFinal,
    SemiFinal,
    ThirdPlace,
    Final,
}

/// <summary>Lifecycle of a fixture. Mirrors the states the live feed reports.</summary>
public enum MatchStatus
{
    Scheduled,
    InPlay,
    Paused,      // half-time or another in-match stoppage
    Finished,
    Postponed,
    Cancelled,
}

/// <summary>A noteworthy in-match moment shown on the match-centre timeline.</summary>
public enum MatchEventType
{
    Goal,
    OwnGoal,
    Penalty,
    YellowCard,
    SecondYellow,
    RedCard,
    Substitution,
}

/// <summary>Playing identity used to seed procedurally-generated scouting text and match tendencies.</summary>
public enum Archetype
{
    Possession,
    CounterAttack,
    Defensive,
    SetPiece,
    HighPress,
    Balanced,
    Flair,
    LateGame,
}

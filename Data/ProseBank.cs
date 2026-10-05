using WorldCupTerminal.Models;

namespace WorldCupTerminal.Data;

/// <summary>
/// Shared archetype-flavoured prose used by both the simulated world generator and the live
/// world builder: team style notes, player scouting fragments and post-match tone lines.
/// Everything downstream (the sentiment pipeline) consumes this text, so live-mode teams get
/// the same "personality" treatment as simulated ones.
/// </summary>
public static class ProseBank
{
    /// <summary>The stable string hash used to seed all deterministic generation.</summary>
    public static int Seed(string s)
    {
        unchecked
        {
            int hash = 17;
            foreach (var ch in s) hash = hash * 31 + ch;
            return hash;
        }
    }

    public static string StyleNote(string teamName, Archetype arch) => arch switch
    {
        Archetype.Possession => $"{teamName} build patiently from the back with a cohesive, press-resistant midfield; supremely technical and fluid, they suffocate opponents with the ball.",
        Archetype.CounterAttack => $"{teamName} sit compact and strike on the counter — devastating, incisive and lethal in transition with electric pace.",
        Archetype.Defensive => $"{teamName} are resolute and disciplined, an organised, watertight low block that is desperately hard to break down.",
        Archetype.SetPiece => $"{teamName} are physical and aggressive, a fearsome aerial threat who are ruthless from set-pieces and dead-ball situations.",
        Archetype.HighPress => $"{teamName} press with ferocious, relentless intensity, a combative and high-octane side that hunts the ball high up the pitch.",
        Archetype.Flair => $"{teamName} ooze flair and finesse, a dazzling, technically gifted side full of silky dribbling and individual brilliance.",
        Archetype.LateGame => $"{teamName} are relentless and resilient, a tight-knit group renowned for ferocious late surges and dramatic comebacks.",
        _ => $"{teamName} are a well-drilled, balanced unit — solid defensively, incisive going forward and full of cohesion.",
    };

    /// <summary>"{Team} were {adj} as they {result}." — the two tone lines fed to the analyzer per match.</summary>
    public static string MatchLine(Team t, string matchId, int forGoals, int againstGoals)
    {
        string result = forGoals > againstGoals ? "ran out deserved winners"
            : forGoals == againstGoals ? "shared the spoils" : "fell to a narrow defeat";
        string adj = NoteBank[t.Archetype][Math.Abs(Seed(matchId + t.Code)) % NoteBank[t.Archetype].Length];
        return $"{t.Name} were {adj} as they {result}.";
    }

    public static readonly Dictionary<Archetype, string[]> NoteBank = new()
    {
        [Archetype.Possession] = new[]
        {
            "press-resistant and supremely technical in tight spaces",
            "silky on the ball with exquisite close control",
            "the cohesive heartbeat of a fluid midfield",
            "elegant, cultured and a master of intricate link-up play",
        },
        [Archetype.CounterAttack] = new[]
        {
            "lethal and clinical on the break",
            "blessed with devastating pace and incisive in transition",
            "a ruthless, predatory finisher",
            "deadly when space opens up",
        },
        [Archetype.Defensive] = new[]
        {
            "resolute, disciplined and rock-solid at the back",
            "commanding in the air and stubbornly organised",
            "an obdurate, watertight defender",
            "miserly and dogged out of possession",
        },
        [Archetype.SetPiece] = new[]
        {
            "a fearsome aerial threat from set-pieces",
            "physical, robust and dominant in the box",
            "towering and ruthless from dead-ball situations",
            "an uncompromising, bruising presence",
        },
        [Archetype.HighPress] = new[]
        {
            "ferocious, relentless and combative without the ball",
            "tenacious, high-octane and snapping into challenges",
            "fearless in the press, intense and aggressive",
            "a voracious, tireless engine across every blade of grass",
        },
        [Archetype.Flair] = new[]
        {
            "a dazzling, mesmerising dribbler with sublime flair",
            "technically gifted with a wand of a left foot",
            "graceful, deft and full of finesse",
            "skilful, classy and press-resistant",
        },
        [Archetype.LateGame] = new[]
        {
            "relentless, resilient and dangerous late on",
            "a tireless engine who finishes matches ferociously strong",
            "tenacious and clutch in the closing stages",
            "fearless, combative and clinical when it matters",
        },
        [Archetype.Balanced] = new[]
        {
            "a well-drilled, complementary all-rounder",
            "solid, disciplined and full of understanding",
            "cohesive, selfless and tactically intelligent",
            "balanced, incisive and reliable in every phase",
        },
    };

    public static readonly string[] Generic =
    {
        "a genuinely world-class talent",
        "an inspirational, selfless leader",
        "hugely influential and a harmonious presence in the squad",
    };

    public static readonly string[] Weaknesses =
    {
        "can be wasteful and profligate in front of goal",
        "is occasionally shaky and error-prone defensively",
        "can be undisciplined and rash in the tackle",
        "sometimes looks ponderous and one-dimensional",
        "can drift in and out, a touch individualistic",
    };
}

using System.Text;
using WorldCupTerminal.Models;

namespace WorldCupTerminal.Services;

public class SentimentResult
{
    public Dictionary<StatDimension, double> Scores { get; } = new();
    public int PositiveHits { get; set; }
    public int NegativeHits { get; set; }
    public int Tokens { get; set; }

    /// <summary>Overall tone in [-1, 1].</summary>
    public double Polarity
    {
        get
        {
            var total = PositiveHits + NegativeHits;
            return total == 0 ? 0 : (PositiveHits - NegativeHits) / (double)total;
        }
    }

    public double Get(StatDimension d) => Scores.TryGetValue(d, out var v) ? v : 0;
}

/// <summary>
/// A compact, dependency-free sentiment / trait analyzer built for football scouting prose.
/// It scans text for lexicon phrases (uni-, bi- and tri-grams), each of which contributes a
/// signed weight to one or more of the five team dimensions. Preceding negators flip the sign
/// and intensifiers scale the magnitude. It is deterministic and fully transparent.
/// </summary>
public class SentimentAnalyzer
{
    private readonly record struct Contribution(StatDimension Dim, double Weight);

    private readonly Dictionary<string, Contribution[]> _lexicon = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _negators = new(StringComparer.OrdinalIgnoreCase)
    {
        "not", "no", "never", "lacks", "lacking", "without", "hardly",
        "rarely", "barely", "devoid", "little", "anything", "less",
    };
    private readonly HashSet<string> _intensifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "very", "highly", "extremely", "supremely", "hugely", "remarkably",
        "devastatingly", "utterly", "exceptionally", "genuinely", "truly", "seriously",
    };

    private readonly int _maxNGram = 2;

    public SentimentAnalyzer()
    {
        // ---- OFFENSE ----
        Add(StatDimension.Offense, 1.0,
            "clinical", "lethal", "prolific", "ruthless", "devastating", "incisive",
            "potent", "deadly", "predatory", "rampant", "firepower", "free-scoring",
            "goalscoring", "fearsome", "dangerous", "explosive", "prolific scorer",
            "clinical finishing", "lethal counter", "ruthless attack", "incisive passing",
            "cutting edge", "attacking verve", "goal threat");
        Add(StatDimension.Offense, -0.9,
            "wasteful", "blunt", "toothless", "profligate", "misfiring", "shot-shy",
            "wasteful finishing", "lacking a cutting edge");

        // ---- DEFENSE ----
        Add(StatDimension.Defense, 1.0,
            "resolute", "solid", "commanding", "disciplined", "miserly", "impenetrable",
            "watertight", "organised", "dogged", "stubborn", "obdurate", "rock-solid",
            "resilient", "marshalling", "defensively sound", "clean sheet", "well-organised",
            "defensive shape", "compact", "back three", "back four", "low block");
        Add(StatDimension.Defense, -0.9,
            "shaky", "leaky", "porous", "fragile", "error-prone", "exposed", "suspect",
            "brittle", "shaky at the back", "defensively suspect", "high line");

        // ---- AGGRESSION ----
        Add(StatDimension.Aggression, 1.0,
            "ferocious", "relentless", "combative", "physical", "uncompromising",
            "aggressive", "intense", "fierce", "robust", "bruising", "high-octane",
            "pressing", "voracious", "tenacious", "fearless", "snapping", "crunching",
            "high press", "front-foot", "in-your-face", "biting tackles");
        Add(StatDimension.Aggression, -0.7,
            "undisciplined", "reckless", "rash", "ill-disciplined", "hot-headed",
            "card-prone", "passive", "meek");

        // ---- CHEMISTRY ----
        Add(StatDimension.Chemistry, 1.0,
            "telepathic", "cohesive", "cohesion", "selfless", "well-drilled", "tight-knit",
            "harmonious", "united", "unity", "understanding", "chemistry", "fluid",
            "synchronised", "complementary", "togetherness", "team spirit", "team-first",
            "settled side", "automatisms", "link-up play");
        Add(StatDimension.Chemistry, -0.9,
            "disjointed", "fragmented", "fractured", "disorganised", "isolated",
            "individualistic", "lacking cohesion", "dressing-room unrest");

        // ---- TECHNICAL ----
        Add(StatDimension.Technical, 1.0,
            "exquisite", "silky", "technical", "elegant", "press-resistant", "gifted",
            "skilful", "deft", "sublime", "graceful", "intricate", "classy", "cultured",
            "mesmerising", "dazzling", "masterful", "refined", "flair", "finesse",
            "dribbling", "technically gifted", "ball control", "first touch", "close control",
            "wand of a left foot");
        Add(StatDimension.Technical, -0.8,
            "clumsy", "ponderous", "laboured", "one-dimensional", "predictable",
            "technically limited", "agricultural");
    }

    private void Add(StatDimension dim, double weight, params string[] phrases)
    {
        foreach (var raw in phrases)
        {
            var key = Normalize(raw);
            if (_lexicon.TryGetValue(key, out var existing))
            {
                var grown = new Contribution[existing.Length + 1];
                existing.CopyTo(grown, 0);
                grown[^1] = new Contribution(dim, weight);
                _lexicon[key] = grown;
            }
            else
            {
                _lexicon[key] = new[] { new Contribution(dim, weight) };
            }
        }
    }

    public SentimentResult Analyze(IEnumerable<string> texts)
    {
        var combined = string.Join(". ", texts);
        return Analyze(combined);
    }

    public SentimentResult Analyze(string text)
    {
        var result = new SentimentResult();
        var tokens = Tokenize(text);
        result.Tokens = tokens.Count;

        for (int i = 0; i < tokens.Count;)
        {
            bool matched = false;

            for (int n = Math.Min(_maxNGram, tokens.Count - i); n >= 1; n--)
            {
                var gram = string.Join(' ', tokens.GetRange(i, n));
                if (!_lexicon.TryGetValue(gram, out var contributions))
                    continue;

                // Look back up to two tokens for a negator / intensifier.
                double sign = 1.0;
                double scale = 1.0;
                for (int back = 1; back <= 2 && i - back >= 0; back++)
                {
                    var prev = tokens[i - back];
                    if (_negators.Contains(prev)) sign = -1.0;
                    else if (_intensifiers.Contains(prev)) scale = 1.6;
                }

                foreach (var c in contributions)
                {
                    var effective = c.Weight * sign * scale;
                    result.Scores[c.Dim] = result.Get(c.Dim) + effective;
                    if (effective >= 0) result.PositiveHits++;
                    else result.NegativeHits++;
                }

                i += n;
                matched = true;
                break;
            }

            if (!matched) i++;
        }

        return result;
    }

    private static string Normalize(string s) => s.Trim().ToLowerInvariant();

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var sb = new StringBuilder();
        foreach (var ch in text.ToLowerInvariant())
        {
            if (char.IsLetter(ch) || ch == '-')
            {
                sb.Append(ch);
            }
            else if (sb.Length > 0)
            {
                tokens.Add(sb.ToString());
                sb.Clear();
            }
        }
        if (sb.Length > 0) tokens.Add(sb.ToString());
        return tokens;
    }
}

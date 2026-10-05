using System.Net;
using System.Text;
using WorldCupTerminal.Models;

namespace WorldCupTerminal.Services;

/// <summary>
/// Static helpers that turn data into "terminal graphics": braille radars, block-shaded
/// possession heat-maps, partial-block bar charts and sparklines. Methods suffixed *Html
/// return markup (coloured spans) intended for Html.Raw; the rest return plain text for
/// monospace &lt;pre&gt; blocks.
/// </summary>
public static class TerminalArt
{
    private const string Blocks = " ▏▎▍▌▋▊▉█"; // " ▏▎▍▌▋▊▉█"
    private const string Spark = "▁▂▃▄▅▆▇█";     // ▁▂▃▄▅▆▇█

    // ---------------------------------------------------------------- bars

    /// <summary>A fractional bar split into a coloured filled span and a dim empty span.</summary>
    public static string BarHtml(double value, double max, int width, string fillClass)
    {
        if (max <= 0) max = 1;
        double ratio = Math.Clamp(value / max, 0, 1);
        double exact = ratio * width;
        int full = (int)Math.Floor(exact);
        double frac = exact - full;
        int partial = (int)Math.Round(frac * 8);

        var filled = new StringBuilder();
        filled.Append('█', full);
        if (partial > 0 && full < width)
        {
            filled.Append(Blocks[partial]);
            full++;
        }
        var empty = new string('░', Math.Max(0, width - full));

        return $"<span class=\"{fillClass}\">{filled}</span><span class=\"bar-empty\">{empty}</span>";
    }

    private static readonly (StatDimension Dim, string Label, string Cls)[] DimRows =
    {
        (StatDimension.Offense, "OFFENSE",  "d-off"),
        (StatDimension.Defense, "DEFENSE",  "d-def"),
        (StatDimension.Aggression, "AGGRESSION", "d-agg"),
        (StatDimension.Technical, "TECHNICAL", "d-tec"),
        (StatDimension.Chemistry, "CHEMISTRY", "d-che"),
    };

    public static string StatBarsHtml(TeamStats s, int width = 26)
    {
        var sb = new StringBuilder();
        foreach (var (dim, label, cls) in DimRows)
        {
            double v = s[dim];
            sb.Append("<div class=\"statrow\">");
            sb.Append($"<span class=\"stat-lbl\">{label,-10}</span>");
            sb.Append("<span class=\"stat-bar\">").Append(BarHtml(v, 100, width, cls)).Append("</span>");
            sb.Append($"<span class=\"stat-val\">{v,5:0.0}</span>");
            sb.Append("</div>");
        }
        return sb.ToString();
    }

    public static string Sparkline(IEnumerable<double> values)
    {
        var list = values.ToList();
        if (list.Count == 0) return "";
        double min = list.Min(), max = list.Max();
        double range = max - min;
        var sb = new StringBuilder();
        foreach (var v in list)
        {
            int idx = range <= 1e-9 ? 4 : (int)Math.Round((v - min) / range * (Spark.Length - 1));
            sb.Append(Spark[Math.Clamp(idx, 0, Spark.Length - 1)]);
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- form

    public static string FormHtml(IEnumerable<char> form)
    {
        var sb = new StringBuilder();
        foreach (var c in form)
        {
            var cls = c switch { 'W' => "res-w", 'D' => "res-d", 'L' => "res-l", _ => "" };
            sb.Append($"<span class=\"{cls}\">{c}</span>");
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- tags

    public static string TagChipsHtml(IEnumerable<TeamTag> tags)
    {
        var sb = new StringBuilder();
        foreach (var t in tags)
        {
            var cls = t.Kind switch { "good" => "tag-good", "warn" => "tag-warn", "danger" => "tag-danger", _ => "tag-info" };
            sb.Append($"<span class=\"tag {cls}\" title=\"{WebUtility.HtmlEncode(t.Description)}\">{WebUtility.HtmlEncode(t.Label)}</span>");
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- radar

    /// <summary>A braille pentagon radar. Axes (clockwise from top): OFF, TEC, AGG, DEF, CHE.</summary>
    public static string RadarBraille(TeamStats s)
    {
        const int w = 52, h = 48;
        var canvas = new BrailleCanvas(w, h);
        double cx = w / 2.0, cy = h / 2.0;
        double radius = Math.Min(cx, cy) - 1;

        var dims = new[]
        {
            StatDimension.Offense, StatDimension.Technical, StatDimension.Aggression,
            StatDimension.Defense, StatDimension.Chemistry,
        };

        var outer = new List<(int, int)>();
        var data = new List<(int, int)>();
        for (int i = 0; i < dims.Length; i++)
        {
            double ang = -Math.PI / 2 + i * 2 * Math.PI / dims.Length;
            double ux = Math.Cos(ang), uy = Math.Sin(ang);
            outer.Add(((int)Math.Round(cx + ux * radius), (int)Math.Round(cy + uy * radius)));
            double r = radius * Math.Clamp(s[dims[i]] / 100.0, 0.05, 1.0);
            data.Add(((int)Math.Round(cx + ux * r), (int)Math.Round(cy + uy * r)));
            // spoke
            canvas.Line((int)cx, (int)cy, outer[i].Item1, outer[i].Item2);
        }
        canvas.Polygon(outer);   // reference envelope
        canvas.Polygon(data);    // the team
        return canvas.Render();
    }

    // ---------------------------------------------------------------- momentum

    /// <summary>A braille line chart of a small series, framed to the canvas.</summary>
    public static string LineChartBraille(IReadOnlyList<double> series, int cols = 30, int rows = 8)
    {
        int w = cols * 2, h = rows * 4;
        var canvas = new BrailleCanvas(w, h);
        if (series.Count < 2) return canvas.Render();

        double min = series.Min(), max = series.Max();
        double range = max - min;
        if (range <= 1e-9) range = 1;

        (int x, int y) Point(int i)
        {
            double fx = (double)i / (series.Count - 1) * (w - 1);
            double fy = (h - 1) - (series[i] - min) / range * (h - 1);
            return ((int)Math.Round(fx), (int)Math.Round(fy));
        }

        for (int i = 0; i < series.Count - 1; i++)
        {
            var a = Point(i);
            var b = Point(i + 1);
            canvas.Line(a.x, a.y, b.x, b.y);
        }
        return canvas.Render();
    }

    // ---------------------------------------------------------------- heat-map

    /// <summary>
    /// Builds a normalised possession-intensity field over the pitch (x: own goal → opponent
    /// goal, y: top → bottom) by stamping a few archetype-specific Gaussian blobs plus a
    /// little team-seeded noise. Deterministic per team code.
    /// </summary>
    public static double[,] PossessionField(Team team, int cols = 42, int rows = 16) =>
        PossessionField(team, null, cols, rows);

    /// <summary>
    /// Match-aware variant: the archetype blobs are shifted up the pitch with possession
    /// share and the attacking-third presence grows with goals scored, so the map plausibly
    /// reflects the actual game rather than pure team identity.
    /// </summary>
    public static double[,] PossessionField(Team team, Match? match, int cols = 42, int rows = 16)
    {
        var blobs = BlobsFor(team.Archetype);
        var seedKey = team.Code;

        if (match is not null)
        {
            bool home = match.Home == team;
            int possession = home ? match.HomePossession : match.AwayPossession;
            int goals = home ? match.HomeGoals : match.AwayGoals;
            int conceded = home ? match.AwayGoals : match.HomeGoals;

            // 65% possession pushes territory ~6% toward the opponent goal; 35% pulls it back.
            double shift = (possession - 50) * 0.004;
            double attackBoost = 1 + 0.15 * Math.Min(goals, 4);

            var adjusted = new Blob[blobs.Length];
            for (int i = 0; i < blobs.Length; i++)
            {
                var b = blobs[i];
                double amp = b.X > 0.66 ? b.Amp * attackBoost : b.Amp;
                adjusted[i] = b with { X = Math.Clamp(b.X + shift, 0.05, 0.95), Amp = amp };
            }
            if (conceded > 0)
            {
                // Pressure faced shows up as heat in the defensive third.
                Array.Resize(ref adjusted, adjusted.Length + 1);
                adjusted[^1] = new Blob(0.14, 0.5, 0.10, 0.22, 0.35 + 0.15 * Math.Min(conceded, 3));
            }
            blobs = adjusted;
            seedKey = team.Code + match.Id;
        }

        var field = new double[rows, cols];
        var rng = new Random(StableSeed(seedKey));

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                double nx = (c + 0.5) / cols;
                double ny = (r + 0.5) / rows;
                double v = 0;
                foreach (var b in blobs)
                {
                    double dx = (nx - b.X) / b.Sx;
                    double dy = (ny - b.Y) / b.Sy;
                    v += b.Amp * Math.Exp(-(dx * dx + dy * dy) / 2.0);
                }
                v += (rng.NextDouble() - 0.4) * 0.12;          // texture
                field[r, c] = Math.Max(0, v);
            }
        }

        // Normalise to 0..1.
        double maxV = 0;
        foreach (var x in field) maxV = Math.Max(maxV, x);
        if (maxV <= 1e-9) maxV = 1;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                field[r, c] /= maxV;

        return field;
    }

    private readonly record struct Blob(double X, double Y, double Sx, double Sy, double Amp);

    private static Blob[] BlobsFor(Archetype a) => a switch
    {
        Archetype.Possession => new[]
        {
            new Blob(0.46, 0.50, 0.18, 0.30, 1.0), new Blob(0.70, 0.28, 0.12, 0.14, 0.9),
            new Blob(0.70, 0.72, 0.12, 0.14, 0.9), new Blob(0.86, 0.50, 0.10, 0.18, 0.8),
        },
        Archetype.CounterAttack => new[]
        {
            new Blob(0.30, 0.50, 0.16, 0.26, 1.0), new Blob(0.86, 0.38, 0.08, 0.10, 0.95),
            new Blob(0.86, 0.62, 0.08, 0.10, 0.95), new Blob(0.60, 0.50, 0.10, 0.08, 0.5),
        },
        Archetype.Defensive => new[]
        {
            new Blob(0.22, 0.50, 0.16, 0.30, 1.0), new Blob(0.32, 0.26, 0.10, 0.12, 0.8),
            new Blob(0.32, 0.74, 0.10, 0.12, 0.8), new Blob(0.58, 0.50, 0.12, 0.10, 0.4),
        },
        Archetype.SetPiece => new[]
        {
            new Blob(0.88, 0.50, 0.09, 0.16, 1.0), new Blob(0.84, 0.16, 0.07, 0.10, 0.9),
            new Blob(0.84, 0.84, 0.07, 0.10, 0.9), new Blob(0.20, 0.50, 0.12, 0.18, 0.6),
            new Blob(0.50, 0.50, 0.12, 0.12, 0.5),
        },
        Archetype.HighPress => new[]
        {
            new Blob(0.66, 0.50, 0.18, 0.30, 1.0), new Blob(0.80, 0.30, 0.10, 0.12, 0.85),
            new Blob(0.80, 0.70, 0.10, 0.12, 0.85), new Blob(0.50, 0.50, 0.12, 0.16, 0.6),
        },
        Archetype.Flair => new[]
        {
            new Blob(0.70, 0.34, 0.12, 0.13, 1.0), new Blob(0.70, 0.66, 0.12, 0.13, 1.0),
            new Blob(0.84, 0.50, 0.10, 0.14, 0.85), new Blob(0.54, 0.50, 0.12, 0.12, 0.55),
        },
        Archetype.LateGame => new[]
        {
            new Blob(0.55, 0.50, 0.18, 0.30, 1.0), new Blob(0.76, 0.40, 0.11, 0.13, 0.85),
            new Blob(0.76, 0.60, 0.11, 0.13, 0.85), new Blob(0.88, 0.50, 0.09, 0.14, 0.7),
        },
        _ => new[]   // Balanced
        {
            new Blob(0.50, 0.50, 0.18, 0.30, 1.0), new Blob(0.66, 0.34, 0.11, 0.13, 0.8),
            new Blob(0.66, 0.66, 0.11, 0.13, 0.8), new Blob(0.36, 0.50, 0.12, 0.16, 0.6),
        },
    };

    private static readonly (double T, string Cls, char Ch)[] HeatRamp =
    {
        (0.10, "hm0", ' '),
        (0.26, "hm1", '░'),  // ░
        (0.44, "hm2", '▒'),  // ▒
        (0.62, "hm3", '▒'),  // ▒
        (0.80, "hm4", '▓'),  // ▓
        (1.01, "hm5", '█'),  // █
    };

    public static string HeatMapHtml(double[,] field)
    {
        int rows = field.GetLength(0), cols = field.GetLength(1);
        var sb = new StringBuilder();
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                double v = field[r, c];
                var (_, cls, ch) = HeatRamp.First(h => v < h.T);
                if (ch == ' ')
                    sb.Append("<span class=\"hm0\"> </span>");
                else
                    sb.Append($"<span class=\"{cls}\">{ch}</span>");
            }
            sb.Append('\n');
        }
        return sb.ToString().TrimEnd('\n');
    }

    private static int StableSeed(string s)
    {
        unchecked
        {
            int hash = 17;
            foreach (var ch in s) hash = hash * 31 + ch;
            return hash;
        }
    }
}

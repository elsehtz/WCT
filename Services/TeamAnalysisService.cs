using WorldCupTerminal.Models;

namespace WorldCupTerminal.Services;

/// <summary>
/// Turns raw text + structured match aggregates into the five team dimensions and a set of
/// behavioural tags. Sentiment is the primary driver; match data refines and grounds it.
/// </summary>
public class TeamAnalysisService
{
    private readonly SentimentAnalyzer _sentiment;

    public TeamAnalysisService(SentimentAnalyzer sentiment) => _sentiment = sentiment;

    /// <summary>Gather every scrap of prose about a team and score it.</summary>
    public void RunSentiment(Team team)
    {
        var corpus = new List<string> { team.StyleNote };
        foreach (var p in team.Players)
            corpus.AddRange(p.ScoutNotes);
        corpus.AddRange(team.MatchNotes);

        var r = _sentiment.Analyze(corpus);
        var b = new SentimentBreakdown
        {
            PositiveHits = r.PositiveHits,
            NegativeHits = r.NegativeHits,
            Tokens = r.Tokens,
            Polarity = r.Polarity,
        };
        foreach (StatDimension d in Enum.GetValues<StatDimension>())
            b.Raw[d] = r.Get(d);
        team.Sentiment = b;
    }

    /// <summary>
    /// Blend sentiment with structured per-match aggregates into a raw score per dimension,
    /// then min-max normalise each dimension across the whole field so the radars spread out.
    /// </summary>
    public void ComputeStats(IReadOnlyList<Team> teams)
    {
        var raw = new Dictionary<Team, Dictionary<StatDimension, double>>();

        foreach (var t in teams)
        {
            double p = Math.Max(1, t.Played);
            double gfpg = t.GoalsFor / p;
            double gapg = t.GoalsAgainst / p;
            var s = t.Sentiment;

            var d = new Dictionary<StatDimension, double>
            {
                [StatDimension.Offense] = s.Get(StatDimension.Offense) * 6 + gfpg * 9 + t.AvgPossession * 0.08,
                [StatDimension.Defense] = s.Get(StatDimension.Defense) * 6 + (3.0 - gapg) * 7 + t.CleanSheets * 3,
                [StatDimension.Aggression] = s.Get(StatDimension.Aggression) * 6 + (t.Cards / p) * 5,
                [StatDimension.Chemistry] = s.Get(StatDimension.Chemistry) * 6 + t.Comebacks * 3 + (t.GoalsFor - t.GoalsAgainst) * 0.6,
                [StatDimension.Technical] = s.Get(StatDimension.Technical) * 6 + t.AvgPossession * 0.22,
            };
            raw[t] = d;
        }

        // Min-max normalise each dimension into [38, 96].
        foreach (StatDimension dim in Enum.GetValues<StatDimension>())
        {
            double min = teams.Min(t => raw[t][dim]);
            double max = teams.Max(t => raw[t][dim]);
            double span = max - min;
            foreach (var t in teams)
            {
                double norm = span <= 1e-9 ? 0.5 : (raw[t][dim] - min) / span;
                double scaled = 38 + norm * (96 - 38);
                switch (dim)
                {
                    case StatDimension.Offense: t.Stats.Offense = scaled; break;
                    case StatDimension.Defense: t.Stats.Defense = scaled; break;
                    case StatDimension.Aggression: t.Stats.Aggression = scaled; break;
                    case StatDimension.Chemistry: t.Stats.Chemistry = scaled; break;
                    case StatDimension.Technical: t.Stats.Technical = scaled; break;
                }
            }
        }
    }

    public void AssignTags(IReadOnlyList<Team> teams)
    {
        double Quartile(Func<Team, double> sel, double q)
        {
            var sorted = teams.Select(sel).OrderBy(x => x).ToList();
            int idx = (int)Math.Clamp(Math.Round(q * (sorted.Count - 1)), 0, sorted.Count - 1);
            return sorted[idx];
        }

        double aggHigh = Quartile(t => t.Stats.Aggression, 0.75);
        double defHigh = Quartile(t => t.Stats.Defense, 0.75);
        double overallMedian = Quartile(t => t.Stats.Overall, 0.5);
        double overallTopThird = Quartile(t => t.Stats.Overall, 0.66);

        foreach (var t in teams)
        {
            var tags = t.Tags;
            tags.Clear();
            double gf = Math.Max(1, t.GoalsFor);

            if (t.CoachIsForeign)
                tags.Add(new("Foreign Coach", $"Led by {t.Coach.Name} of {t.Coach.Nationality}, not a native of {t.Country}.", "info"));

            if (t.FifaRank <= 6)
                tags.Add(new("Favourite", $"Ranked #{t.FifaRank} in the world — among the pre-tournament favourites.", "good"));
            else if (t.FifaRank > 24 && t.Stats.Overall >= overallMedian)
                tags.Add(new("Underdog", $"Ranked #{t.FifaRank} yet performing above the field median — punching up.", "good"));

            if (t.SetPieceGoals / gf > 0.34)
                tags.Add(new("Set-Piece Dependent", $"{t.SetPieceGoals} of {t.GoalsFor} goals come from dead-ball situations.", "warn"));
            else if (t.Archetype == Archetype.SetPiece || t.SetPieceGoals >= 3)
                tags.Add(new("Aerial Threat", "Tall, dangerous from corners and free-kicks.", "info"));

            if (t.SecondHalfGoals / gf > 0.58)
            {
                if (t.Comebacks >= 1)
                    tags.Add(new("Clutch", $"{t.SecondHalfGoals}/{t.GoalsFor} goals after the break, with {t.Comebacks} comeback(s) from a half-time deficit.", "good"));
                else
                    tags.Add(new("Late Game", $"{t.SecondHalfGoals} of {t.GoalsFor} goals arrive in the second half — they finish strong.", "good"));
            }

            if (t.Comebacks >= 2)
                tags.Add(new("Comeback Kings", $"Recovered from {t.Comebacks} half-time deficits without losing.", "good"));

            if (t.FirstHalfConceded > t.SecondHalfConceded && t.FirstHalfConceded >= 3)
                tags.Add(new("Slow Starters", $"{t.FirstHalfConceded} goals conceded before half-time — sluggish out of the blocks.", "warn"));

            if (t.AvgPossession >= 57 && t.Stats.Technical >= 70)
                tags.Add(new("Possession Masters", $"Average {t.AvgPossession:0}% possession with elite technical quality.", "info"));
            else if (t.AvgPossession <= 45 && t.Stats.Offense >= 65)
                tags.Add(new("Counter-Attacking", $"Cede the ball ({t.AvgPossession:0}%) but lethal in transition.", "info"));

            if (t.Stats.Defense >= defHigh && (t.GoalsAgainst / Math.Max(1.0, t.Played)) < 1.0)
                tags.Add(new("Defensive Wall", "Top-quartile defence conceding less than a goal a game.", "good"));

            if (t.Stats.Offense >= 72 && t.Stats.Defense <= 55)
                tags.Add(new("Glass Cannon", "Devastating going forward, but vulnerable at the back.", "warn"));

            if (t.Stats.Aggression >= aggHigh)
                tags.Add(new("High Press", "Top-quartile aggression — they hunt the ball high up the pitch.", "danger"));

            if (t.Stats.Technical >= 80)
                tags.Add(new("Flair Merchants", "Exceptional individual technique and on-ball quality.", "good"));

            if (t.AverageAge < 26.5 && t.Stats.Overall >= overallTopThird)
                tags.Add(new("Golden Generation", $"Average age {t.AverageAge:0.0} and already among the strongest squads.", "good"));
            else if (t.AverageAge >= 29.5)
                tags.Add(new("Veteran Core", $"Experienced squad, average age {t.AverageAge:0.0}.", "info"));

            if (t.Stats.Offense >= 68 && t.Stats.Defense >= 68 && t.Stats.Technical >= 68 && t.Stats.Chemistry >= 68)
                tags.Add(new("Total Football", "Elite and well-rounded across every dimension.", "good"));
        }
    }
}

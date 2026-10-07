using WorldCupTerminal.Data;
using WorldCupTerminal.Models;

namespace WorldCupTerminal.Services;

/// <summary>
/// Fills the gaps the scraped feed leaves in a live match — set-piece counts, possession
/// when the boxscore lacked it, and the commentary block (the two archetype tone lines the
/// sentiment pipeline consumes plus timeline-derived noteworthy lines). Deterministic:
/// seeded on the event id and score, so a refresh with unchanged data changes nothing.
/// </summary>
public class MatchEmbellisher
{
    public void EmbellishAll(IEnumerable<Match> matches)
    {
        foreach (var m in matches.Where(m => m.Played || m.IsLive))
            Embellish(m);
    }

    private static void Embellish(Match m)
    {
        // Set-piece goals straight from the timeline (penalties, free-kicks, headers).
        m.HomeSetPieceGoals = m.Events.Count(e => IsSetPieceGoal(e) && e.TeamCode == m.Home.Code);
        m.AwaySetPieceGoals = m.Events.Count(e => IsSetPieceGoal(e) && e.TeamCode == m.Away.Code);

        // Possession fallback when the boxscore didn't carry it. Simulated matches already
        // have a modelled value, so only untouched defaults are filled in.
        if (!m.PossessionFromFeed && m.HomePossession == 50)
        {
            var rng = new Random(ProseBank.Seed($"{m.ExternalId ?? m.Id}:{m.HomeGoals}:{m.AwayGoals}"));
            double wH = PossTendency(m.Home.Archetype) * StrengthFactor(m.Home);
            double wA = PossTendency(m.Away.Archetype) * StrengthFactor(m.Away);
            // Score context: a side that is ahead tends to have controlled the game.
            wH *= 1 + 0.06 * Math.Clamp(m.HomeGoals - m.AwayGoals, -3, 3);
            wA *= 1 + 0.06 * Math.Clamp(m.AwayGoals - m.HomeGoals, -3, 3);
            m.HomePossession = (int)Math.Clamp(Math.Round(100 * wH / (wH + wA) + (rng.NextDouble() - 0.5) * 8), 30, 70);
        }

        BuildCommentary(m);
    }

    private static bool IsSetPieceGoal(MatchEvent e) =>
        e.Type == MatchEventType.Penalty
        || (e.Type == MatchEventType.Goal && e.Detail is "free-kick" or "header");

    private static void BuildCommentary(Match m)
    {
        m.Commentary.Clear();

        if (m.Played)
        {
            // The two tone lines feed Team.MatchNotes → the sentiment pipeline (home first).
            m.Commentary.Add(ProseBank.MatchLine(m.Home, m.Id, m.HomeGoals, m.AwayGoals));
            m.Commentary.Add(ProseBank.MatchLine(m.Away, m.Id, m.AwayGoals, m.HomeGoals));
        }
        else
        {
            m.Commentary.Add($"{m.Home.Name} and {m.Away.Name} are in play — the feed refreshes on a daily cycle, so this score may trail the pitch.");
        }

        // The full timeline renders in its own match-centre panel; commentary carries only
        // the noteworthy colour — red cards and early changes that hint at a knock.
        foreach (var e in m.Events)
        {
            if (e.Type is MatchEventType.RedCard or MatchEventType.SecondYellow)
                m.Commentary.Add($"{e.PlayerName} ({e.TeamCode}) was sent off ({e.MinuteDisplay}) — a discipline story that will follow them.");
            else if (e.Type == MatchEventType.Substitution && e.Minute > 0 && e.Minute < 60)
                m.Commentary.Add($"Early change for {e.TeamCode} on {e.MinuteDisplay} — {e.PlayerName} came on {SubDetail(e)}; possibly an enforced knock.");
        }

        if (m.WentToPenalties)
            m.Commentary.Add($"● {m.Winner?.Name} prevail {m.HomePenalties}–{m.AwayPenalties} in the shoot-out.");
        else if (m.WentToExtraTime && m.Played)
            m.Commentary.Add("● Settled in extra time.");
    }

    private static string SubDetail(MatchEvent e) => e.Detail.Length > 0 ? $"({e.Detail})" : "";

    // Real FIFA ranks run past 48 (up to ~85 for this field); everything outside the top 48 counts alike.
    private static double StrengthFactor(Team t) => 1 + (48 - Math.Min(t.FifaRank, 48)) / 96.0;   // 1.0 .. ~1.5

    private static double PossTendency(Archetype arch) => arch switch
    {
        Archetype.Possession => 1.9,
        Archetype.Flair => 1.5,
        Archetype.HighPress => 1.3,
        Archetype.Balanced => 1.1,
        Archetype.LateGame => 1.0,
        Archetype.SetPiece => 0.85,
        Archetype.CounterAttack => 0.7,
        Archetype.Defensive => 0.6,
        _ => 1.0,
    };
}

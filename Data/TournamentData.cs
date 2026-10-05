using WorldCupTerminal.Models;
using static WorldCupTerminal.Models.Position;

namespace WorldCupTerminal.Data;

/// <summary>
/// Authoritative seed for the (representative) 2026 field plus the deterministic generator
/// that expands compact team profiles into full squads, scouting prose and simulated match
/// results. Everything is seeded from stable string hashes, so the world is identical on
/// every boot.
/// </summary>
public static class TournamentData
{
    public readonly record struct Star(string Name, Position Pos);

    public record Profile(
        string Group, string Name, string Code, string Country, string Conf,
        int Rank, Archetype Arch, string CoachName, string CoachNat, Star[] Stars);

    /// <summary>Curated profiles, also used by the live world builder to give real teams a
    /// plausible archetype/coach/confederation (matched by team name).</summary>
    public static IReadOnlyList<Profile> KnownProfiles => Profiles;

    // 32 teams, eight groups. A blend of genuine contenders across every confederation,
    // with a deliberate mix of native and foreign coaches.
    private static readonly Profile[] Profiles =
    {
        // ---- Group A ----
        new("A", "Brazil", "BRA", "Brazil", "CONMEBOL", 3, Archetype.Flair, "Carlo Ancelotti", "Italy",
            new[] { new Star("Vinícius Júnior", FW), new Star("Bruno Guimarães", MF), new Star("Marquinhos", DF) }),
        new("A", "Mexico", "MEX", "Mexico", "CONCACAF", 14, Archetype.CounterAttack, "Javier Aguirre", "Mexico",
            new[] { new Star("Santiago Giménez", FW), new Star("Edson Álvarez", MF), new Star("Jorge Sánchez", DF) }),
        new("A", "Morocco", "MAR", "Morocco", "CAF", 12, Archetype.Defensive, "Walid Regragui", "Morocco",
            new[] { new Star("Achraf Hakimi", DF), new Star("Brahim Díaz", MF), new Star("Youssef En-Nesyri", FW) }),
        new("A", "Australia", "AUS", "Australia", "AFC", 25, Archetype.SetPiece, "Tony Popovic", "Australia",
            new[] { new Star("Jackson Irvine", MF), new Star("Harry Souttar", DF), new Star("Mitchell Duke", FW) }),

        // ---- Group B ----
        new("B", "France", "FRA", "France", "UEFA", 2, Archetype.Balanced, "Didier Deschamps", "France",
            new[] { new Star("Kylian Mbappé", FW), new Star("Aurélien Tchouaméni", MF), new Star("William Saliba", DF) }),
        new("B", "Senegal", "SEN", "Senegal", "CAF", 18, Archetype.HighPress, "Pape Thiaw", "Senegal",
            new[] { new Star("Sadio Mané", FW), new Star("Pape Matar Sarr", MF), new Star("Kalidou Koulibaly", DF) }),
        new("B", "Japan", "JPN", "Japan", "AFC", 16, Archetype.Possession, "Hajime Moriyasu", "Japan",
            new[] { new Star("Kaoru Mitoma", FW), new Star("Wataru Endo", MF), new Star("Ko Itakura", DF) }),
        new("B", "Canada", "CAN", "Canada", "CONCACAF", 30, Archetype.CounterAttack, "Jesse Marsch", "United States",
            new[] { new Star("Jonathan David", FW), new Star("Stephen Eustáquio", MF), new Star("Alphonso Davies", DF) }),

        // ---- Group C ----
        new("C", "Argentina", "ARG", "Argentina", "CONMEBOL", 1, Archetype.Possession, "Lionel Scaloni", "Argentina",
            new[] { new Star("Lionel Messi", FW), new Star("Enzo Fernández", MF), new Star("Cristian Romero", DF) }),
        new("C", "Croatia", "CRO", "Croatia", "UEFA", 10, Archetype.LateGame, "Zlatko Dalić", "Croatia",
            new[] { new Star("Luka Modrić", MF), new Star("Mateo Kovačić", MF), new Star("Joško Gvardiol", DF) }),
        new("C", "Nigeria", "NGA", "Nigeria", "CAF", 28, Archetype.Flair, "Eric Chelle", "Mali",
            new[] { new Star("Victor Osimhen", FW), new Star("Alex Iwobi", MF), new Star("William Troost-Ekong", DF) }),
        new("C", "United States", "USA", "United States", "CONCACAF", 13, Archetype.HighPress, "Mauricio Pochettino", "Argentina",
            new[] { new Star("Christian Pulisic", FW), new Star("Weston McKennie", MF), new Star("Sergiño Dest", DF) }),

        // ---- Group D ----
        new("D", "Spain", "ESP", "Spain", "UEFA", 4, Archetype.Possession, "Luis de la Fuente", "Spain",
            new[] { new Star("Lamine Yamal", FW), new Star("Pedri", MF), new Star("Rodri", MF) }),
        new("D", "Uruguay", "URU", "Uruguay", "CONMEBOL", 11, Archetype.Defensive, "Marcelo Bielsa", "Argentina",
            new[] { new Star("Darwin Núñez", FW), new Star("Federico Valverde", MF), new Star("Ronald Araújo", DF) }),
        new("D", "South Korea", "KOR", "South Korea", "AFC", 22, Archetype.HighPress, "Hong Myung-bo", "South Korea",
            new[] { new Star("Son Heung-min", FW), new Star("Lee Kang-in", MF), new Star("Kim Min-jae", DF) }),
        new("D", "Ghana", "GHA", "Ghana", "CAF", 31, Archetype.SetPiece, "Otto Addo", "Ghana",
            new[] { new Star("Iñaki Williams", FW), new Star("Thomas Partey", MF), new Star("Mohammed Salisu", DF) }),

        // ---- Group E ----
        new("E", "Germany", "GER", "Germany", "UEFA", 9, Archetype.HighPress, "Julian Nagelsmann", "Germany",
            new[] { new Star("Florian Wirtz", MF), new Star("Joshua Kimmich", MF), new Star("Antonio Rüdiger", DF) }),
        new("E", "Colombia", "COL", "Colombia", "CONMEBOL", 15, Archetype.Flair, "Néstor Lorenzo", "Argentina",
            new[] { new Star("Luis Díaz", FW), new Star("James Rodríguez", MF), new Star("Dávinson Sánchez", DF) }),
        new("E", "Switzerland", "SUI", "Switzerland", "UEFA", 19, Archetype.Defensive, "Murat Yakin", "Switzerland",
            new[] { new Star("Breel Embolo", FW), new Star("Granit Xhaka", MF), new Star("Manuel Akanji", DF) }),
        new("E", "Saudi Arabia", "KSA", "Saudi Arabia", "AFC", 56, Archetype.CounterAttack, "Hervé Renard", "France",
            new[] { new Star("Firas Al-Buraikan", FW), new Star("Salem Al-Dawsari", MF), new Star("Ali Al-Bulaihi", DF) }),

        // ---- Group F ----
        new("F", "England", "ENG", "England", "UEFA", 5, Archetype.SetPiece, "Thomas Tuchel", "Germany",
            new[] { new Star("Harry Kane", FW), new Star("Jude Bellingham", MF), new Star("John Stones", DF) }),
        new("F", "Netherlands", "NED", "Netherlands", "UEFA", 7, Archetype.Possession, "Ronald Koeman", "Netherlands",
            new[] { new Star("Cody Gakpo", FW), new Star("Frenkie de Jong", MF), new Star("Virgil van Dijk", DF) }),
        new("F", "Ecuador", "ECU", "Ecuador", "CONMEBOL", 23, Archetype.Defensive, "Sebastián Beccacece", "Argentina",
            new[] { new Star("Enner Valencia", FW), new Star("Moisés Caicedo", MF), new Star("Pervis Estupiñán", DF) }),
        new("F", "Qatar", "QAT", "Qatar", "AFC", 35, Archetype.CounterAttack, "Luis García", "Spain",
            new[] { new Star("Almoez Ali", FW), new Star("Akram Afif", MF), new Star("Boualem Khoukhi", DF) }),

        // ---- Group G ----
        new("G", "Portugal", "POR", "Portugal", "UEFA", 6, Archetype.Flair, "Roberto Martínez", "Spain",
            new[] { new Star("Cristiano Ronaldo", FW), new Star("Bruno Fernandes", MF), new Star("Rúben Dias", DF) }),
        new("G", "Denmark", "DEN", "Denmark", "UEFA", 20, Archetype.SetPiece, "Brian Riemer", "Denmark",
            new[] { new Star("Rasmus Højlund", FW), new Star("Christian Eriksen", MF), new Star("Andreas Christensen", DF) }),
        new("G", "Cameroon", "CMR", "Cameroon", "CAF", 33, Archetype.HighPress, "Marc Brys", "Belgium",
            new[] { new Star("Vincent Aboubakar", FW), new Star("Bryan Mbeumo", FW), new Star("André Onana", GK) }),
        new("G", "Iran", "IRN", "Iran", "AFC", 21, Archetype.Defensive, "Amir Ghalenoei", "Iran",
            new[] { new Star("Mehdi Taremi", FW), new Star("Alireza Jahanbakhsh", MF), new Star("Sardar Azmoun", FW) }),

        // ---- Group H ----
        new("H", "Belgium", "BEL", "Belgium", "UEFA", 8, Archetype.CounterAttack, "Rudi Garcia", "France",
            new[] { new Star("Romelu Lukaku", FW), new Star("Kevin De Bruyne", MF), new Star("Wout Faes", DF) }),
        new("H", "Italy", "ITA", "Italy", "UEFA", 9, Archetype.Defensive, "Gennaro Gattuso", "Italy",
            new[] { new Star("Federico Chiesa", FW), new Star("Nicolò Barella", MF), new Star("Gianluigi Donnarumma", GK) }),
        new("H", "Serbia", "SRB", "Serbia", "UEFA", 27, Archetype.SetPiece, "Dragan Stojković", "Serbia",
            new[] { new Star("Dušan Vlahović", FW), new Star("Sergej Milinković-Savić", MF), new Star("Nikola Milenković", DF) }),
        new("H", "Egypt", "EGY", "Egypt", "CAF", 34, Archetype.CounterAttack, "Hossam Hassan", "Egypt",
            new[] { new Star("Mohamed Salah", FW), new Star("Mohamed Elneny", MF), new Star("Mohamed Abdelmonem", DF) }),
    };

    private static readonly string[] HostCities =
    {
        "MetLife, NJ", "SoFi, LA", "AT&T, Dallas", "Hard Rock, Miami", "Mercedes-Benz, Atlanta",
        "NRG, Houston", "Arrowhead, KC", "Lincoln, Philly", "Levi's, SF Bay", "Lumen, Seattle",
        "Gillette, Boston", "Estadio Azteca, MX", "BMO, Toronto", "BC Place, Vancouver", "Akron, Guadalajara",
    };

    private static readonly string[] ClubPool =
    {
        "Real Madrid", "Man City", "Bayern", "PSG", "Barcelona", "Liverpool", "Arsenal", "Inter",
        "Juventus", "Chelsea", "Atlético", "Dortmund", "Tottenham", "Napoli", "AC Milan", "Man Utd",
        "Newcastle", "Al-Hilal", "Benfica", "Porto", "Ajax", "Sporting CP", "Leverkusen", "Marseille",
    };

    private static readonly string[] FillerFirst = { "A.", "M.", "J.", "L.", "D.", "S.", "K.", "R.", "T.", "N." };
    private static readonly string[] FillerLast =
    {
        "Silva", "Kovač", "Tanaka", "Diallo", "Hansen", "Rossi", "Müller", "Ferreira", "Novák",
        "Andersson", "Popov", "Haddad", "Okafor", "Mendoza", "Bauer", "Costa", "Ibrahim", "Larsson",
        "Suzuki", "Traoré", "García", "Yılmaz", "Mensah", "Castro", "Berg", "Moreau", "Petrov", "Nakamura",
    };

    // First group-stage kickoff. "Now" in the app is 2026-06-18, mid-tournament.
    private static readonly DateTime Day1 = new(2026, 6, 11, 18, 0, 0, DateTimeKind.Utc);

    public static (List<Team> Teams, List<Match> Matches) Build()
    {
        var teams = Profiles.Select(BuildTeam).ToList();
        var byCode = teams.ToDictionary(t => t.Code);
        var matches = BuildGroupFixtures(teams);

        // Mark match-days 1 & 2 as played and simulate them; match-day 3 stays upcoming.
        foreach (var m in matches.Where(m => m.KickOff < new DateTime(2026, 6, 18, 0, 0, 0, DateTimeKind.Utc)))
            SimulateMatch(m);

        return (teams, matches);
    }

    // ------------------------------------------------------------------ team build

    private static Team BuildTeam(Profile p)
    {
        var team = new Team
        {
            Name = p.Name,
            Code = p.Code,
            Country = p.Country,
            Confederation = p.Conf,
            Group = p.Group,
            FifaRank = p.Rank,
            Archetype = p.Arch,
            Coach = new Coach(p.CoachName, p.CoachNat),
            StyleNote = ProseBank.StyleNote(p.Name, p.Arch),
            ColourClass = AccentFor(p.Code),
        };

        var rng = new Random(Seed(p.Code));
        var positions = new List<Position>();

        foreach (var star in p.Stars)
        {
            team.Players.Add(BuildPlayer(star.Name, star.Pos, p.Arch, rng, isStar: true));
            positions.Add(star.Pos);
        }
        if (!positions.Contains(GK))
            team.Players.Add(BuildPlayer($"{Keeper(p.Code, rng)}", GK, p.Arch, rng, isStar: false));

        while (team.Players.Count < 5)
        {
            var pos = rng.NextDouble() < 0.5 ? MF : FW;
            team.Players.Add(BuildPlayer(FillerName(rng), pos, p.Arch, rng, isStar: false));
        }

        return team;
    }

    private static Player BuildPlayer(string name, Position pos, Archetype arch, Random rng, bool isStar)
    {
        var player = new Player
        {
            Name = name,
            Position = pos,
            Number = rng.Next(1, 24),
            Age = 22 + rng.Next(0, 13),
            Club = ClubPool[rng.Next(ClubPool.Length)],
        };

        // Two archetype-flavoured scouting fragments, with an occasional weakness for variance.
        var bank = ProseBank.NoteBank[arch];
        var a = bank[rng.Next(bank.Length)];
        string b;
        do { b = bank[rng.Next(bank.Length)]; } while (b == a);

        player.ScoutNotes.Add($"{name} is {a}.");
        if (rng.Next(6) == 0)
            player.ScoutNotes.Add($"{name} {ProseBank.Weaknesses[rng.Next(ProseBank.Weaknesses.Length)]}.");
        else
            player.ScoutNotes.Add($"{name} is {b}.");

        if (isStar)
        {
            player.ScoutNotes.Add($"{name} is {ProseBank.Generic[rng.Next(ProseBank.Generic.Length)]}.");
            if (pos == FW) player.Goals = rng.Next(0, 4);
            if (pos is FW or MF) player.Assists = rng.Next(0, 3);
        }

        return player;
    }

    // ------------------------------------------------------------------ fixtures

    private static List<Match> BuildGroupFixtures(List<Team> teams)
    {
        var matches = new List<Match>();
        foreach (var group in teams.GroupBy(t => t.Group).OrderBy(g => g.Key))
        {
            var g = group.OrderBy(t => t.FifaRank).ToList(); // seed order within the group
            // Standard 4-team round robin across three match-days.
            (int a, int b)[][] schedule =
            {
                new[] { (0, 1), (2, 3) },  // MD1
                new[] { (0, 2), (1, 3) },  // MD2
                new[] { (0, 3), (1, 2) },  // MD3
            };
            for (int md = 0; md < schedule.Length; md++)
            {
                foreach (var (a, b) in schedule[md])
                {
                    var home = g[a];
                    var away = g[b];
                    var kickoff = Day1.AddDays(md * 4).AddHours((matches.Count % 4) * 3);
                    matches.Add(new Match
                    {
                        Id = $"GRP-{group.Key}-{md + 1}-{home.Code}",
                        Home = home,
                        Away = away,
                        Group = group.Key,
                        Stage = Stage.Group,
                        KickOff = kickoff,
                        Venue = HostCities[Math.Abs(Seed(home.Code + away.Code)) % HostCities.Length],
                    });
                }
            }
        }
        return matches;
    }

    // ------------------------------------------------------------------ match engine

    private static void SimulateMatch(Match m)
    {
        var rng = new Random(Seed(m.Id));
        m.Played = true;
        m.Status = MatchStatus.Finished;

        double sH = Strength(m.Home), sA = Strength(m.Away);
        double homeEdge = 0.25;

        int gH = SampleGoals(sH - sA + AtkBias(m.Home) - DefBias(m.Away) + homeEdge, rng);
        int gA = SampleGoals(sA - sH + AtkBias(m.Away) - DefBias(m.Home), rng);

        SplitGoals(gH, m.Home.Archetype, rng, out int hf, out int hs);
        SplitGoals(gA, m.Away.Archetype, rng, out int af, out int as_);
        m.HomeFirstHalf = hf; m.HomeSecondHalf = hs;
        m.AwayFirstHalf = af; m.AwaySecondHalf = as_;

        m.HomeSetPieceGoals = SetPieces(gH, m.Home.Archetype, rng);
        m.AwaySetPieceGoals = SetPieces(gA, m.Away.Archetype, rng);

        double wH = PossTendency(m.Home.Archetype), wA = PossTendency(m.Away.Archetype);
        m.HomePossession = (int)Math.Clamp(Math.Round(100 * wH / (wH + wA) + (rng.NextDouble() - 0.5) * 8), 30, 70);

        m.HomeCards = CardCount(m.Home.Archetype, rng);
        m.AwayCards = CardCount(m.Away.Archetype, rng);

        m.Commentary.Add(ProseBank.MatchLine(m.Home, m.Id, gH, gA));
        m.Commentary.Add(ProseBank.MatchLine(m.Away, m.Id, gA, gH));
    }

    private static double Strength(Team t) => (60 - t.FifaRank) / 14.0; // ~ +4.2 (best) .. -3 (worst)

    private static double AtkBias(Team t) => t.Archetype switch
    {
        Archetype.Flair or Archetype.CounterAttack => 0.5,
        Archetype.Possession or Archetype.HighPress => 0.3,
        Archetype.Defensive => -0.4,
        _ => 0.1,
    };

    private static double DefBias(Team t) => t.Archetype switch
    {
        Archetype.Defensive => 0.7,
        Archetype.Balanced => 0.3,
        Archetype.Flair or Archetype.HighPress => -0.3,
        _ => 0.0,
    };

    private static int SampleGoals(double edge, Random rng)
    {
        double lambda = Math.Clamp(1.25 + edge * 0.45, 0.2, 3.4);
        // Knuth Poisson sampler.
        double l = Math.Exp(-lambda);
        int k = 0; double pmf = 1;
        do { k++; pmf *= rng.NextDouble(); } while (pmf > l);
        return Math.Min(k - 1, 5);
    }

    private static void SplitGoals(int goals, Archetype arch, Random rng, out int first, out int second)
    {
        double lateShare = arch switch
        {
            Archetype.LateGame => 0.72,
            Archetype.HighPress => 0.62,
            Archetype.SetPiece => 0.58,
            Archetype.Defensive => 0.40,
            _ => 0.52,
        };
        second = 0;
        for (int i = 0; i < goals; i++)
            if (rng.NextDouble() < lateShare) second++;
        first = goals - second;
    }

    private static int SetPieces(int goals, Archetype arch, Random rng)
    {
        double share = arch switch
        {
            Archetype.SetPiece => 0.55,
            Archetype.Defensive => 0.30,
            Archetype.HighPress => 0.20,
            _ => 0.12,
        };
        int sp = 0;
        for (int i = 0; i < goals; i++)
            if (rng.NextDouble() < share) sp++;
        return sp;
    }

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

    private static int CardCount(Archetype arch, Random rng) => arch switch
    {
        Archetype.HighPress => 2 + rng.Next(0, 3),
        Archetype.SetPiece or Archetype.Defensive => 1 + rng.Next(0, 3),
        _ => rng.Next(0, 3),
    };

    // ------------------------------------------------------------------ helpers

    private static string Keeper(string code, Random rng) => $"{FillerFirst[rng.Next(FillerFirst.Length)]} {FillerLast[rng.Next(FillerLast.Length)]}";
    private static string FillerName(Random rng) => $"{FillerFirst[rng.Next(FillerFirst.Length)]} {FillerLast[rng.Next(FillerLast.Length)]}";

    public static string AccentFor(string code) => (Math.Abs(Seed(code)) % 6) switch
    {
        0 => "c-green", 1 => "c-cyan", 2 => "c-amber",
        3 => "c-magenta", 4 => "c-blue", _ => "c-red",
    };

    private static int Seed(string s)
    {
        unchecked
        {
            int hash = 17;
            foreach (var ch in s) hash = hash * 31 + ch;
            return hash;
        }
    }
}

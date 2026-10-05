# WORLD CUP // TERMINAL

A C# / ASP.NET Core (.NET 9) website that renders the 2026 FIFA World Cup as if it were a
**Linux terminal** — phosphor-green CRT styling, braille-dot graphics, and block-character
heat-maps. Team statistics are not hand-entered: they are **derived by a sentiment-analysis
pipeline** that reads scouting prose and match commentary, then grounded against match data.
Behavioural **tags** (e.g. *Set-Piece Dependent*, *Clutch*, *Foreign Coach*, *Glass Cannon*)
are inferred from those stats plus structured match aggregates.

## Live feed

The site now runs a **daily live feed**: a background scheduler scrapes ESPN's public
(unofficial) site API once a day and swaps in the **real 2026 tournament** — all 48 teams in
12 groups, real fixtures and scores, real lineups & benches, cards, substitutions and
possession stats, plus the real knockout bracket through the final.

```
ESPN site API (unofficial)          App_Data/live-cache.json
   │  EspnClient (1 req/s polite         ▲ save │ load at boot
   ▼  throttle, defensive parsing)       │      ▼
LiveDataSet ──► LiveWorldBuilder ──► MatchEmbellisher ──► TournamentSnapshotBuilder
                (teams, matches,     (set-pieces, prose,   (aggregates → sentiment →
                 lineups, events)     possession fallback)  stats → tags → tables)
                                                                 │
   LiveFeedScheduler (BackgroundService) ──────────► TournamentRepository.Swap(snapshot)
   daily at 06:00 UTC · scrape-on-boot when stale
```

What's real vs. modelled in live mode:

| Real (scraped) | Modelled (deterministic, seeded from the real data) |
|---|---|
| Scores, half-time splits (from goal minutes), ET & penalty shoot-outs | Possession heat-maps (archetype blobs skewed by real possession/goals) |
| Lineups, benches, formations, jersey numbers | Scouting prose, style notes, player ages |
| Goals, cards, substitutions with minutes | Set-piece classification beyond pens/free-kicks/headers |
| Possession & card counts (boxscore) | Possession when a boxscore is missing |

**Degradation ladder:** Live (scrape OK) → Cached (scrape fails, `App_Data/live-cache.json`
exists) → Simulated (no cache — the original fully procedural 32-team world). The footer
shows which mode is active, and the system alerts explain what is modelled.

### Feed configuration (`appsettings.json`, `LiveFeed` section)

| Key | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | `false` = always simulated (original behaviour) |
| `DailyRefreshUtcHour` | `6` | UTC hour of the daily scrape |
| `StaleAfterHours` | `12` | cache older than this triggers a scrape at boot |
| `RequestDelayMs` | `1000` | spacing between feed requests |
| `UseFixtureFiles` | `false` | serve canned JSON from `Data/Fixtures/` (offline testing) |

> ESPN's site API is unofficial and can change or block at any time. Every parse is
> defensive (bad data is skipped, never fatal), and the site always falls back to the cache
> and then to the simulated world.

## Run it

**Standalone (just the web app):**

```bash
dotnet run --urls http://localhost:5249
# then open http://localhost:5249
```

**Orchestrated with .NET Aspire (recommended — adds the dashboard, telemetry, health checks):**

```bash
dotnet run --project WorldCupTerminal.AppHost
```

The AppHost prints a dashboard login URL (e.g. `http://localhost:15026/login?t=…`). Open it to
see the `worldcup` resource with live logs, traces, metrics, and a click-through to the site.
Requires the Aspire packages on first restore (NuGet); no workload install is needed on .NET 9.

## What you can see

| Page | Route | Highlights |
|------|-------|-----------|
| **Dashboard** | `/Home/Index` | All group tables, live form sparklines, incoming fixtures, a power-index leaderboard |
| **Fixtures** | `/Home/Fixtures` | In-play strip, played results (pens/aet noted) + upcoming schedule |
| **Match centre** | `/Home/Match/{id}` | Score with live/HT status, half-by-half stats, **lineups & bench**, event **timeline** (goals/cards/subs), possession bar, **possession heat-maps**, commentary |
| **Bracket** | `/Home/Bracket` | Full knockout tree (R32 → final + 3rd place) with real results and placeholders for unresolved ties |
| **Team profile** | `/Home/Team/{code}` | Braille **attribute radar**, possession heat-map, **sentiment signal breakdown**, momentum chart, tags, full squad with scouting reports |

There's also a working command bar (press `/` to focus): try `open BRA`, `fixtures`,
`bracket`, or `help`.

## How the analysis works

```
scouting prose + match commentary
            │
            ▼
   SentimentAnalyzer         ← lexicon of ~200 phrases, each weighted toward one of
   (phrase n-grams,            five dimensions, with negation + intensifier handling
    negation, intensifiers)
            │  raw per-dimension signal
            ▼
   TeamAnalysisService       ← blends sentiment (primary driver) with structured match
   ComputeStats()              aggregates (goals/half, possession, cards, clean sheets),
            │                   then min-max normalises each dimension across the field
            ▼
   Aggression · Offense · Defense · Chemistry · Technical   (0–100)
            │
            ▼
   AssignTags()              ← thresholds + quartiles → behavioural tags
```

The pipeline runs identically in live and simulated mode — live teams get archetype-flavoured
prose (seeded deterministically) so the sentiment engine has text to read.

## Project layout

```
WorldCupTerminal.sln           ties the three projects together
WorldCupTerminal.AppHost/      .NET Aspire orchestrator (run this one)
WorldCupTerminal.ServiceDefaults/  shared OpenTelemetry / health / service-discovery
Program.cs                     host + DI wiring (feed, snapshot builder, hosted scheduler)
Models/                        Team, Player, Coach, Match (+lineups/events), stats, bracket
Services/
  SentimentAnalyzer.cs         lexicon-based trait/sentiment scorer
  TeamAnalysisService.cs       sentiment → stats → tags pipeline
  TournamentRepository.cs      thread-safe facade over the current snapshot (atomic swap)
  TournamentSnapshotBuilder.cs aggregates → sentiment → tables → bracket for any world
  LiveFeedScheduler.cs         BackgroundService: scrape at boot if stale + daily refresh
  LiveFeedService.cs           scrape → cache → build → embellish → snapshot → swap
  LiveWorldBuilder.cs          feed DTOs → domain (teams, matches, lineups, bracket)
  MatchEmbellisher.cs          fills what the feed lacks, consistent with real results
  Espn/                        options, compact DTOs, defensive parser, HTTP + fixture clients, cache
  BrailleCanvas.cs             2×4 braille dot canvas (lines, polygons)
  TerminalArt.cs               radar, heat-maps (match-aware), bars, sparklines, tag chips
Data/
  TournamentData.cs            simulated-mode world generator (offline fallback)
  ProseBank.cs                 shared archetype prose used by both worlds
  Fixtures/                    trimmed real API payloads for offline testing
Controllers/HomeController.cs
Views/                         Razor views (terminal-styled)
wwwroot/css/terminal.css       the CRT theme
wwwroot/js/terminal.js         clock + command bar
```

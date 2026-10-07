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

| Real — ESPN feed | Real — reference squads (Wikipedia) | Modelled (deterministic, seeded from the real data) |
|---|---|---|
| Scores, half-time splits (from goal minutes), ET & penalty shoot-outs | Pre-tournament FIFA ranking (April 2026 release) | Possession heat-maps (archetype blobs skewed by real possession/goals) |
| Lineups, benches, formations | Head coach & nationality (incl. Tunisia's mid-tournament change) | Scouting prose, style notes, match commentary tone lines |
| Goals, cards, substitutions with minutes; top scorers | Confederation | Team ratings (offense/defense/…), power index and tags — sentiment-derived |
| Possession, cards, shots, shots on target, corners, fouls, offsides, saves, pass accuracy | Registered 26-man squads: shirt number, position, age on opening day, club at the time | Set-piece goals (only pens, free-kicks and headers are identifiable) |
| | | Possession when a boxscore is missing (not the case for 2026) |

### Tournament complete (archived mode)

The final was played on 19 July 2026, so the site now serves the **final results from a committed
archive** (`Data/Archive/wc2026-final.json`, the same format as the scrape cache) and stops
polling ESPN. Boot order: local cache → archive → scrape; once the data set contains a played
final the mode is **Archived** (footer: *FINAL RESULTS*), the dashboard swaps upcoming fixtures
for the champions / final / top-scorer panel, and team pages show how far each side got.
Set `LiveFeed:RefreshAfterCompletion=true` to keep scraping anyway.

> ESPN now rejects date-range scoreboard queries (`?dates=A-B` → HTTP 400), so the client walks
> the tournament one day at a time; a single failed day abandons the scrape rather than
> publishing a partial schedule.

Reference data (`Data/Reference/wc2026-reference.json`) is generated from the Wikipedia articles
*2026 FIFA World Cup squads* and *2026 FIFA World Cup* (CC BY-SA 4.0) by
`python3 tools/build_reference.py`. Registered players are joined to feed players by shirt
number (fixed for the tournament), and take the feed's spelling so timelines and lineups line up.

**Degradation ladder:** Live (scrape OK) → Cached (scrape fails, `App_Data/live-cache.json`
or the committed archive exists) → Simulated (neither — the original fully procedural 32-team
world). Any of the first two becomes **Archived** once the final has been played. The footer
shows which mode is active, and the system alerts explain what is modelled.

### Feed configuration (`appsettings.json`, `LiveFeed` section)

| Key | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | `false` = always simulated (original behaviour) |
| `DailyRefreshUtcHour` | `6` | UTC hour of the daily scrape |
| `StaleAfterHours` | `12` | cache older than this triggers a scrape at boot |
| `RequestDelayMs` | `1000` | spacing between feed requests |
| `UseFixtureFiles` | `false` | serve canned JSON from `Data/Fixtures/` (offline testing) |
| `ArchiveFile` | `Data/Archive/wc2026-final.json` | final-results snapshot served when there is no local cache |
| `ReferenceFile` | `Data/Reference/wc2026-reference.json` | FIFA ranks, coaches, registered squads |
| `RefreshAfterCompletion` | `false` | keep scraping after the final has been played |

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
| **Dashboard** | `/Home/Index` | All group tables, form sparklines, incoming fixtures (champions, final & top scorers once complete), a power-index leaderboard |
| **Fixtures** | `/Home/Fixtures` | In-play strip, played results (pens/aet noted) + upcoming schedule |
| **Match centre** | `/Home/Match/{id}` | Score with live/HT status, half-by-half stats, **lineups & bench**, event **timeline** (goals/cards/subs), possession bar, **possession heat-maps**, commentary |
| **Bracket** | `/Home/Bracket` | Full knockout tree (R32 → final + 3rd place) with real results and placeholders for unresolved ties |
| **Team profile** | `/Home/Team/{code}` | Braille **attribute radar**, possession heat-map, **sentiment signal breakdown**, momentum chart, tags, full squad with scouting reports |

There's also a working command bar (press `/` to focus): try `open BRA`, `fixtures`,
`bracket`, or `help`.

## Design

Annotated wireframes of every screen (dashboard, fixtures, bracket, teams, team detail, match
centre, overlays & states) live in Figma:
[World Cup Terminal — Wireframes](https://www.figma.com/design/OgtS7bUjltGmqNBED3O7kF). Their
colours are bound to a `wire` variable collection that mirrors the `:root` tokens in
`wwwroot/css/terminal.css` — update both together.

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
  ReferenceData.cs             loads the curated reference squads/ranks/coaches
  MatchEmbellisher.cs          fills what the feed lacks, consistent with real results
  Espn/                        options, compact DTOs, defensive parser, HTTP + fixture clients, cache
  BrailleCanvas.cs             2×4 braille dot canvas (lines, polygons)
  TerminalArt.cs               radar, heat-maps (match-aware), bars, sparklines, tag chips
Data/
  TournamentData.cs            simulated-mode world generator (offline fallback)
  ProseBank.cs                 shared archetype prose used by both worlds
  Fixtures/                    trimmed real API payloads for offline testing
  Archive/wc2026-final.json    final scraped results of the completed tournament
  Reference/wc2026-reference.json  FIFA ranks, coaches, registered squads (from Wikipedia)
tools/build_reference.py       regenerates the reference file
Controllers/HomeController.cs
Views/                         Razor views (terminal-styled)
wwwroot/css/terminal.css       the CRT theme
wwwroot/js/terminal.js         clock + command bar
```

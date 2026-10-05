using Microsoft.Extensions.Options;
using WorldCupTerminal.Services;
using WorldCupTerminal.Services.Espn;

var builder = WebApplication.CreateBuilder(args);

// .NET Aspire service defaults: OpenTelemetry, health checks, service discovery, resilience.
builder.AddServiceDefaults();

builder.Services.AddControllersWithViews();

// The sentiment engine is stateless; the repository builds the (simulated) world once at
// startup and the live feed swaps refreshed snapshots in as they land.
builder.Services.AddSingleton<SentimentAnalyzer>();
builder.Services.AddSingleton<TeamAnalysisService>();
builder.Services.AddSingleton<TournamentSnapshotBuilder>();
builder.Services.AddSingleton<TournamentRepository>();

// Live feed: daily scrape of ESPN's (unofficial) site API, cached to disk, with graceful
// fallback to the cache and then to the simulated world.
builder.Services.Configure<LiveFeedOptions>(builder.Configuration.GetSection("LiveFeed"));
builder.Services.AddHttpClient<EspnClient>((sp, http) =>
{
    var options = sp.GetRequiredService<IOptions<LiveFeedOptions>>().Value;
    http.BaseAddress = new Uri(options.BaseUrl);
    http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) WorldCupTerminal/1.0");
});
builder.Services.AddTransient<FixtureFileEspnClient>();
builder.Services.AddTransient<IEspnClient>(sp =>
    sp.GetRequiredService<IOptions<LiveFeedOptions>>().Value.UseFixtureFiles
        ? sp.GetRequiredService<FixtureFileEspnClient>()
        : sp.GetRequiredService<EspnClient>());
builder.Services.AddSingleton<LiveDataCache>();
builder.Services.AddSingleton<LiveWorldBuilder>();
builder.Services.AddSingleton<MatchEmbellisher>();
builder.Services.AddSingleton<LiveFeedStatus>();
builder.Services.AddSingleton<LiveFeedService>();
builder.Services.AddSingleton<SystemAlertProvider>();
builder.Services.AddHostedService<LiveFeedScheduler>();

var app = builder.Build();

// Aspire health-check endpoints (/health, /alive) in development.
app.MapDefaultEndpoints();

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Warm the repository so the (potentially heavy) analysis runs at boot, not on
// the first request.
app.Services.GetRequiredService<TournamentRepository>();

app.Run();

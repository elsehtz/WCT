var builder = DistributedApplication.CreateBuilder(args);

// Orchestrate the World Cup terminal web front-end. Aspire injects the OTLP endpoint and
// service-discovery config, and surfaces the app (with logs, traces, metrics) on the dashboard.
builder.AddProject<Projects.WorldCupTerminal>("worldcup")
       .WithHttpEndpoint(port: 5000);
       
builder.Build().Run();

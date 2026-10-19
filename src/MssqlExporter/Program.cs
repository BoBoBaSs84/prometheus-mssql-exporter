using MssqlExporter;
using MssqlExporter.Data;
using Prometheus;

if (args.Contains("--print-metrics"))
{
    MetricsDocs.Write(Console.Out);
    return;
}

var options = ExporterOptions.FromEnvironment();

// Keep only the process_* and dotnet_* runtime metrics (the Node exporter's nodejs_*/process_* equivalent),
// not the per-library EventCounter/Meter families or prometheus_net_* self-metrics.
Metrics.SuppressDefaultMetrics(new SuppressDefaultMetricOptions { SuppressEventCounters = true, SuppressMeters = true, SuppressDebugMetrics = true });

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{options.ExposePort}");
builder.Services.AddSingleton(options);
builder.Services.AddSingleton<ISqlRunner, SqlRunner>();
builder.Services.AddSingleton<Scraper>();
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Services.Configure<HostOptions>(host => host.ShutdownTimeout = TimeSpan.FromSeconds(5));

var app = builder.Build();
app.MapExporterEndpoints();
app.Run();

// Exposed for WebApplicationFactory<Program>.
public partial class Program;

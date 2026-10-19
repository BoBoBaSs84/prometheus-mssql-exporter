using MssqlExporter;
using MssqlExporter.Data;

var options = ExporterOptions.FromEnvironment();

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

using MssqlExporter;

var options = ExporterOptions.FromEnvironment();

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{options.ExposePort}");
builder.Services.AddSingleton(options);
builder.Services.Configure<HostOptions>(host => host.ShutdownTimeout = TimeSpan.FromSeconds(5));

var app = builder.Build();
app.MapExporterEndpoints();
app.Run();

// Exposed for WebApplicationFactory<Program>.
public partial class Program;

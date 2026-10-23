using System.Text;
using System.Text.RegularExpressions;
using MssqlExporter.Binding;
using Prometheus;

namespace MssqlExporter;

public static partial class Endpoints
{
    [GeneratedRegex(@"^[A-Za-z0-9._-]+(?::\d{1,5})?$")]
    private static partial Regex TargetPattern();

    private static string SanitizeForLog(string value) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\r", string.Empty).Replace("\n", string.Empty);

    public static void MapExporterEndpoints(this IEndpointRouteBuilder app)
    {
        var options = app.ServiceProvider.GetRequiredService<ExporterOptions>();

        app.MapGet("/", () => Results.Redirect("/metrics"));
        app.MapGet("/healthz", () => Results.Json(new { status = "ok" }));
        app.MapGet("/metrics", HandleMetricsAsync);

        if (options.ProbeEnabled)
        {
            app.MapGet("/probe", HandleProbeAsync);
        }
    }

    private static async Task HandleMetricsAsync(HttpContext context, ExporterOptions options, Scraper scraper, ILoggerFactory loggerFactory)
    {
        var log = loggerFactory.CreateLogger("App");
        var cancellationToken = context.RequestAborted;

        // A fresh registry per scrape, so a dropped database stops being reported on the next scrape.
        var registry = Metrics.NewCustomRegistry();
        var entries = MetricsBinder.BuildEntries(registry);
        var meta = MetricsBinder.BuildMeta(registry);

        try
        {
            log.LogDebug("Received /metrics request");
            await scraper.ScrapeAsync(options.BuildConnectionString(), entries, meta, options.QueryTimeoutMs, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            log.LogError("Failed to scrape SQL Server: {Message}", ex.Message);
            context.Response.Headers["X-Error"] = SanitizeHeader(ex.Message);
            await WriteAsync(context, await ExportOnlyUpAsync(0));
            return;
        }

        var registries = options.CollectDefaultMetrics ? new[] { registry, Metrics.DefaultRegistry } : [registry];
        await WriteAsync(context, await ExportAsync(registries));
        log.LogDebug("Successfully processed /metrics request");
    }

    private static async Task HandleProbeAsync(HttpContext context, ExporterOptions options, Scraper scraper, ILoggerFactory loggerFactory)
    {
        var log = loggerFactory.CreateLogger("App");
        var cancellationToken = context.RequestAborted;

        var target = context.Request.Query["target"].ToString();
        if (!TargetPattern().IsMatch(target))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync("missing or invalid ?target=host[:port]", cancellationToken);
            return;
        }

        var (host, port) = SplitTarget(target);
        var registry = Metrics.NewCustomRegistry();
        var entries = MetricsBinder.BuildEntries(registry);
        var meta = MetricsBinder.BuildMeta(registry);

        try
        {
            log.LogDebug("Received /probe request for {Target}", target);
            await scraper.ScrapeAsync(options.BuildConnectionString(host, port), entries, meta, options.QueryTimeoutMs, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            log.LogError("Failed to probe {Target}: {Message}", SanitizeForLog(target), ex.Message);
            entries.Single(e => e.Name == "mssql_up").Gauges.Set("mssql_up", 0);
            context.Response.Headers["X-Error"] = SanitizeHeader(ex.Message);
        }

        await WriteAsync(context, await ExportAsync(registry));
    }

    private const string ExpositionContentType = "text/plain; version=0.0.4; charset=utf-8";

    private static (string Host, int? Port) SplitTarget(string target)
    {
        var parts = target.Split(':', 2);
        return (parts[0], parts.Length > 1 ? int.Parse(parts[1]) : null);
    }

    /// <summary>Header values must be a single printable-ASCII line.</summary>
    internal static string SanitizeHeader(string value)
    {
        var singleLine = Regex.Replace(value, @"[\r\n]+", " ");
        return new string([.. singleLine.Select(c => c is >= ' ' and <= '~' ? c : '?')]);
    }

    private static async Task<string> ExportOnlyUpAsync(double value)
    {
        var registry = Metrics.NewCustomRegistry();
        var up = MetricsBinder.BuildEntries(registry, [Collectors.Collectors.Up]).Single();
        up.Gauges.Set("mssql_up", value);
        return await ExportAsync(registry);
    }

    private static async Task<string> ExportAsync(params CollectorRegistry[] registries)
    {
        using var stream = new MemoryStream();
        foreach (var registry in registries)
        {
            await registry.CollectAndExportAsTextAsync(stream);
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static Task WriteAsync(HttpContext context, string body)
    {
        context.Response.ContentType = ExpositionContentType;
        return context.Response.WriteAsync(body, context.RequestAborted);
    }
}

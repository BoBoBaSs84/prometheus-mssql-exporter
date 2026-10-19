using System.Diagnostics;
using MssqlExporter.Binding;
using MssqlExporter.Data;

namespace MssqlExporter;

/// <summary>
/// Connect, collect, close. A failed connection throws; individual collector failures
/// (query error, timeout, empty result set) are logged, recorded in the meta gauges and skipped.
/// </summary>
public sealed class Scraper(ISqlRunner runner, ILoggerFactory loggerFactory)
{
    private readonly ILogger appLog = loggerFactory.CreateLogger("App");
    private readonly ILogger metricsLog = loggerFactory.CreateLogger("Metrics");

    public async Task ScrapeAsync(
        string connectionString,
        IReadOnlyList<CollectorEntry> entries,
        ScrapeMeta meta,
        int queryTimeoutMs,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            await using var session = await runner.ConnectAsync(connectionString, cancellationToken);
            await CollectAllAsync(session, entries, meta, queryTimeoutMs, cancellationToken);
        }
        finally
        {
            meta.ScrapeDuration.Set(Stopwatch.GetElapsedTime(started).TotalSeconds);
        }
    }

    /// <summary>Runs every collector sequentially over a single connection.</summary>
    private async Task CollectAllAsync(
        ISqlSession session,
        IReadOnlyList<CollectorEntry> entries,
        ScrapeMeta meta,
        int queryTimeoutMs,
        CancellationToken cancellationToken)
    {
        foreach (var entry in entries)
        {
            var started = Stopwatch.GetTimestamp();
            var success = 1;
            try
            {
                var rows = await session.RunQueryAsync(entry.Collector.Query, queryTimeoutMs, cancellationToken);
                if (rows.Length == 0)
                {
                    metricsLog.LogDebug("Query for metric {Collector} returned 0 rows", entry.Name);
                }
                else
                {
                    entry.Collector.Collect(rows, entry.Gauges);
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                success = 0;
                appLog.LogError("Error collecting metric {Collector}: {Message}", entry.Name, ex.Message);
            }
            finally
            {
                meta.CollectorDuration.WithLabels(entry.Name).Set(Stopwatch.GetElapsedTime(started).TotalSeconds);
                meta.CollectorSuccess.WithLabels(entry.Name).Set(success);
            }
        }
    }
}

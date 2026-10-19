using MssqlExporter.Collectors;
using Prometheus;
using Collector = MssqlExporter.Collectors.Collector;

namespace MssqlExporter.Binding;

/// <summary>A collector together with the gauges materialised for it on one registry.</summary>
public sealed record CollectorEntry(Collector Collector, GaugeSet Gauges)
{
    public string Name => Collector.Name;
}

/// <summary>Scrape-quality gauges.</summary>
public sealed record ScrapeMeta(Gauge ScrapeDuration, Gauge CollectorDuration, Gauge CollectorSuccess);

/// <summary>
/// Binds collector descriptions to a registry. The same collector set can be materialised
/// against any registry, so every scrape (and every probe) gets a throwaway one.
/// </summary>
public static class MetricsBinder
{
    public static IReadOnlyList<CollectorEntry> BuildEntries(CollectorRegistry registry, IEnumerable<Collector>? collectors = null)
    {
        var factory = Metrics.WithCustomRegistry(registry);
        return [.. (collectors ?? Collectors.Collectors.All).Select(c => new CollectorEntry(c, new GaugeSet(c.Gauges, factory)))];
    }

    public static ScrapeMeta BuildMeta(CollectorRegistry registry)
    {
        var factory = Metrics.WithCustomRegistry(registry);
        return new ScrapeMeta(
            factory.CreateGauge("mssql_scrape_duration_seconds", "Total time the exporter spent collecting metrics for this scrape"),
            factory.CreateGauge(
                "mssql_collector_duration_seconds",
                "Time spent running a single collector",
                new GaugeConfiguration { LabelNames = ["collector"] }),
            factory.CreateGauge(
                "mssql_collector_success",
                "Whether a collector completed without error (1) or not (0)",
                new GaugeConfiguration { LabelNames = ["collector"] }));
    }
}

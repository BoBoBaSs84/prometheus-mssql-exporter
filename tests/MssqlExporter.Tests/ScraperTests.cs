using Microsoft.Extensions.Logging.Abstractions;
using MssqlExporter.Binding;
using Prometheus;

namespace MssqlExporter.Tests;

[TestClass]
public sealed class ScraperTests
{
    [TestMethod]
    public async Task CancellationPropagatesInsteadOfBeingRecordedAsACollectorFailure()
    {
        using var cts = new CancellationTokenSource();
        var runner = new FakeSqlRunner
        {
            Query = _ =>
            {
                cts.Cancel();
                return [];
            },
        };
        var registry = Metrics.NewCustomRegistry();
        var entries = MetricsBinder.BuildEntries(registry);
        var scraper = new Scraper(runner, NullLoggerFactory.Instance);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await scraper.ScrapeAsync("cs", entries, MetricsBinder.BuildMeta(registry), 0, cts.Token));

        Assert.AreEqual(1, runner.Disposed);
    }

    [TestMethod]
    public async Task ConnectionFailurePropagatesAndStillRecordsScrapeDuration()
    {
        var runner = new FakeSqlRunner { ConnectFailure = _ => new InvalidOperationException("down") };
        var registry = Metrics.NewCustomRegistry();
        var meta = MetricsBinder.BuildMeta(registry);
        var scraper = new Scraper(runner, NullLoggerFactory.Instance);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await scraper.ScrapeAsync("cs", MetricsBinder.BuildEntries(registry), meta, 0, CancellationToken.None));

        Assert.IsGreaterThanOrEqualTo(0d, meta.ScrapeDuration.Value);
    }
}

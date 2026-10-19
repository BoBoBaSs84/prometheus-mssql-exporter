using MssqlExporter.Binding;

namespace MssqlExporter.Collectors;

/// <summary>
/// A pure description of one scrape query: the gauges it feeds, the SQL, and how rows map to gauge values.
/// Kept as data (not an interface hierarchy) so docs output and the fixture test can enumerate it.
/// </summary>
/// <param name="Name">Unique key, e.g. <c>mssql_connections</c>.</param>
/// <param name="Gauges">Gauge specs keyed by the key used in <see cref="GaugeSet"/>.</param>
/// <param name="Query">A single SQL statement; column order must match what <paramref name="Collect"/> indexes.</param>
/// <param name="Collect">Receives the materialised rows (never empty) and the bound gauges.</param>
/// <param name="Optional">May legitimately return 0 rows (Always On off, no Agent jobs).</param>
public sealed record Collector(
    string Name,
    IReadOnlyDictionary<string, GaugeSpec> Gauges,
    string Query,
    Action<IReadOnlyList<object?[]>, GaugeSet> Collect,
    bool Optional = false);

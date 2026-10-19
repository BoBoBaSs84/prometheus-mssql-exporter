using MssqlExporter.Binding;

namespace MssqlExporter.Collectors;

/// <summary>A curated subset of sys.configurations, for config-drift alerting.</summary>
internal static class Settings
{
    private static readonly string[] Tracked =
    [
        "max server memory (MB)",
        "min server memory (MB)",
        "max degree of parallelism",
        "cost threshold for parallelism",
        "max worker threads",
        "optimize for ad hoc workloads",
        "backup compression default",
        "fill factor (%)",
        "priority boost",
        "lightweight pooling",
    ];

    public static IReadOnlyList<Collector> All { get; } =
    [
        new(
            "mssql_configuration",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_configuration"] = new("value_in_use for a tracked sp_configure setting", LabelNames: ["name"]),
            },
            $"""
SELECT RTRIM(name), CAST(value_in_use AS FLOAT)
FROM sys.configurations
WHERE name IN ({string.Join(", ", Tracked.Select(n => $"'{n}'"))})
""",
            (rows, gauges) =>
            {
                foreach (var row in rows)
                {
                    gauges.Set("mssql_configuration", CollectorUtil.AsDouble(row[1]), ("name", CollectorUtil.AsString(row[0])));
                }
            }),
    ];
}

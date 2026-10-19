using System.Globalization;
using MssqlExporter.Binding;

namespace MssqlExporter.Collectors;

/// <summary>Instance liveness, version and clock.</summary>
internal static class Instance
{
    public static IReadOnlyList<Collector> All { get; } =
    [
        new(
            "mssql_up",
            new Dictionary<string, GaugeSpec> { ["mssql_up"] = new("UP status") },
            "SELECT 1",
            (rows, gauges) => gauges.Set("mssql_up", CollectorUtil.AsDouble(rows[0][0]))),

        new(
            "mssql_product_version",
            new Dictionary<string, GaugeSpec> { ["mssql_product_version"] = new("Instance version (Major.Minor)") },
            "SELECT CONVERT(VARCHAR(128), SERVERPROPERTY('productversion')) AS [ProductVersion];",
            (rows, gauges) =>
            {
                var v = ProductVersion.Parse(rows[0][0]);
                // Matches the Node exporter: Number(`${major}.${minor}`), so 15.10 is reported as 15.1.
                gauges.Set("mssql_product_version", double.Parse(string.Create(CultureInfo.InvariantCulture, $"{v.Major}.{v.Minor}"), CultureInfo.InvariantCulture));
            }),

        new(
            "mssql_instance_local_time",
            new Dictionary<string, GaugeSpec> { ["mssql_instance_local_time"] = new("Number of seconds since epoch on local instance") },
            "SELECT DATEDIFF(SECOND, '19700101', GETUTCDATE()) AS [instance_local_time];",
            (rows, gauges) => gauges.Set("mssql_instance_local_time", CollectorUtil.AsDouble(rows[0][0]))),
    ];
}

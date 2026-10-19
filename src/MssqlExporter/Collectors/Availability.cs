using MssqlExporter.Binding;

namespace MssqlExporter.Collectors;

/// <summary>Per-database configuration flags and integrity signals.</summary>
internal static class Availability
{
    public static IReadOnlyList<Collector> All { get; } =
    [
        new(
            "mssql_database_properties",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_database_recovery_model"] = new("Recovery model: 1=FULL 2=BULK_LOGGED 3=SIMPLE", LabelNames: ["database"]),
                ["mssql_database_is_read_only"] = new("1 if the database is read-only", LabelNames: ["database"]),
                ["mssql_database_is_auto_close_on"] = new("1 if AUTO_CLOSE is enabled", LabelNames: ["database"]),
                ["mssql_database_is_auto_shrink_on"] = new("1 if AUTO_SHRINK is enabled", LabelNames: ["database"]),
                ["mssql_database_page_verify_option"] = new("Page verify: 0=NONE 1=TORN_PAGE_DETECTION 2=CHECKSUM", LabelNames: ["database"]),
            },
            """
SELECT name, recovery_model, CAST(is_read_only AS INT), CAST(is_auto_close_on AS INT), CAST(is_auto_shrink_on AS INT), page_verify_option
FROM sys.databases
""",
            (rows, gauges) =>
            {
                foreach (var row in rows)
                {
                    var database = ("database", CollectorUtil.AsString(row[0]));
                    gauges.Set("mssql_database_recovery_model", CollectorUtil.AsDouble(row[1]), database);
                    gauges.Set("mssql_database_is_read_only", CollectorUtil.AsDouble(row[2]), database);
                    gauges.Set("mssql_database_is_auto_close_on", CollectorUtil.AsDouble(row[3]), database);
                    gauges.Set("mssql_database_is_auto_shrink_on", CollectorUtil.AsDouble(row[4]), database);
                    gauges.Set("mssql_database_page_verify_option", CollectorUtil.AsDouble(row[5]), database);
                }
            }),

        new(
            "mssql_suspect_pages",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_suspect_pages"] = new("Number of rows in msdb.dbo.suspect_pages for the database", LabelNames: ["database"]),
            },
            """
SELECT d.name, COUNT(sp.file_id)
FROM sys.databases d
LEFT JOIN msdb.dbo.suspect_pages sp ON sp.database_id = d.database_id
GROUP BY d.name
""",
            (rows, gauges) =>
            {
                foreach (var row in rows)
                {
                    gauges.Set("mssql_suspect_pages", CollectorUtil.AsDouble(row[1]), ("database", CollectorUtil.AsString(row[0])));
                }
            }),
    ];
}

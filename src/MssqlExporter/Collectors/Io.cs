using MssqlExporter.Binding;

namespace MssqlExporter.Collectors;

/// <summary>Buffer manager counters and per-database I/O stalls.</summary>
internal static class Io
{
    // Verbatim apart from the whitespace-only trailing line, which a raw string literal cannot carry.
    private const string BufferManagerSql = """
SELECT * FROM
        (
            SELECT rtrim(counter_name) as counter_name, cntr_value
            FROM sys.dm_os_performance_counters
            WHERE counter_name in ('Page reads/sec', 'Page writes/sec', 'Page life expectancy', 'Lazy writes/sec', 'Checkpoint pages/sec')
            AND object_name = 'SQLServer:Buffer Manager'
        ) d
        PIVOT
        (
        MAX(cntr_value)
        FOR counter_name IN ([Page reads/sec], [Page writes/sec], [Page life expectancy], [Lazy writes/sec], [Checkpoint pages/sec])
        ) piv
""";

    private const string IoStallSql = """
SELECT
cast(DB_Name(a.database_id) as varchar) as name,
    max(io_stall_read_ms),
    max(io_stall_write_ms),
    max(io_stall),
    max(io_stall_queued_read_ms),
    max(io_stall_queued_write_ms)
FROM
sys.dm_io_virtual_file_stats(null, null) a
INNER JOIN sys.master_files b ON a.database_id = b.database_id and a.file_id = b.file_id
GROUP BY a.database_id
""";

    public static IReadOnlyList<Collector> All { get; } =
    [
        new(
            "mssql_buffer_manager",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_page_read_total"] = new("Page reads/sec"),
                ["mssql_page_write_total"] = new("Page writes/sec"),
                ["mssql_page_life_expectancy"] = new(
                    "Indicates the minimum number of seconds a page will stay in the buffer pool on this node without references. The traditional advice from Microsoft used to be that the PLE should remain above 300 seconds"),
                ["mssql_lazy_write_total"] = new("Lazy writes/sec"),
                ["mssql_page_checkpoint_total"] = new("Checkpoint pages/sec"),
            },
            BufferManagerSql,
            (rows, gauges) =>
            {
                var row = rows[0];
                gauges.Set("mssql_page_read_total", CollectorUtil.AsDouble(row[0]));
                gauges.Set("mssql_page_write_total", CollectorUtil.AsDouble(row[1]));
                gauges.Set("mssql_page_life_expectancy", CollectorUtil.AsDouble(row[2]));
                gauges.Set("mssql_page_checkpoint_total", CollectorUtil.AsDouble(row[4]));
                gauges.Set("mssql_lazy_write_total", CollectorUtil.AsDouble(row[3]));
            }),

        new(
            "mssql_io_stall",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_io_stall"] = new("Wait time (ms) of stall since last restart", LabelNames: ["database", "type"]),
                ["mssql_io_stall_total"] = new("Wait time (ms) of stall since last restart", LabelNames: ["database"]),
            },
            IoStallSql,
            (rows, gauges) =>
            {
                foreach (var row in rows)
                {
                    var database = CollectorUtil.AsString(row[0]);
                    gauges.Set("mssql_io_stall_total", CollectorUtil.AsDouble(row[3]), ("database", database));
                    gauges.Set("mssql_io_stall", CollectorUtil.AsDouble(row[1]), ("database", database), ("type", "read"));
                    gauges.Set("mssql_io_stall", CollectorUtil.AsDouble(row[2]), ("database", database), ("type", "write"));
                    gauges.Set("mssql_io_stall", CollectorUtil.AsDouble(row[4]), ("database", database), ("type", "queued_read"));
                    gauges.Set("mssql_io_stall", CollectorUtil.AsDouble(row[5]), ("database", database), ("type", "queued_write"));
                }
            }),
    ];
}

using MssqlExporter.Binding;

namespace MssqlExporter.Collectors;

/// <summary>Operating-system memory as seen by the SQL Server process.</summary>
internal static class Memory
{
    private static readonly (string Metric, string Counter)[] MemoryManagerKb =
    [
        ("mssql_target_server_memory_bytes", "Target Server Memory (KB)"),
        ("mssql_total_server_memory_bytes", "Total Server Memory (KB)"),
        ("mssql_granted_workspace_memory_bytes", "Granted Workspace Memory (KB)"),
        ("mssql_maximum_workspace_memory_bytes", "Maximum Workspace Memory (KB)"),
        ("mssql_connection_memory_bytes", "Connection Memory (KB)"),
        ("mssql_lock_memory_bytes", "Lock Memory (KB)"),
        ("mssql_optimizer_memory_bytes", "Optimizer Memory (KB)"),
        ("mssql_sql_cache_memory_bytes", "SQL Cache Memory (KB)"),
    ];

    private static readonly (string Metric, string Counter)[] MemoryManagerRaw =
    [
        ("mssql_memory_grants_pending", "Memory Grants Pending"),
        ("mssql_memory_grants_outstanding", "Memory Grants Outstanding"),
    ];

    public static IReadOnlyList<Collector> All { get; } =
    [
        new(
            "mssql_os_process_memory",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_page_fault_count"] = new("Number of page faults since last restart"),
                ["mssql_memory_utilization_percentage"] = new("Percentage of memory utilization"),
            },
            """
SELECT page_fault_count, memory_utilization_percentage
FROM sys.dm_os_process_memory
""",
            (rows, gauges) =>
            {
                gauges.Set("mssql_page_fault_count", CollectorUtil.AsDouble(rows[0][0]));
                gauges.Set("mssql_memory_utilization_percentage", CollectorUtil.AsDouble(rows[0][1]));
            }),

        new(
            "mssql_os_sys_memory",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_total_physical_memory_kb"] = new("Total physical memory in KB"),
                ["mssql_available_physical_memory_kb"] = new("Available physical memory in KB"),
                ["mssql_total_page_file_kb"] = new("Total page file in KB"),
                ["mssql_available_page_file_kb"] = new("Available page file in KB"),
            },
            """
SELECT total_physical_memory_kb, available_physical_memory_kb, total_page_file_kb, available_page_file_kb
FROM sys.dm_os_sys_memory
""",
            (rows, gauges) =>
            {
                var row = rows[0];
                gauges.Set("mssql_total_physical_memory_kb", CollectorUtil.AsDouble(row[0]));
                gauges.Set("mssql_available_physical_memory_kb", CollectorUtil.AsDouble(row[1]));
                gauges.Set("mssql_total_page_file_kb", CollectorUtil.AsDouble(row[2]));
                gauges.Set("mssql_available_page_file_kb", CollectorUtil.AsDouble(row[3]));
            }),

        new(
            "mssql_memory_manager",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_target_server_memory_bytes"] = new("Ideal amount of memory the server can consume"),
                ["mssql_total_server_memory_bytes"] = new("Amount of memory the server has committed"),
                ["mssql_memory_grants_pending"] = new("Number of processes waiting for a workspace memory grant"),
                ["mssql_memory_grants_outstanding"] = new("Number of processes that have acquired a workspace memory grant"),
                ["mssql_granted_workspace_memory_bytes"] = new("Total amount of memory currently granted to executing processes"),
                ["mssql_maximum_workspace_memory_bytes"] = new("Maximum amount of memory available for executing processes"),
                ["mssql_connection_memory_bytes"] = new("Total amount of dynamic memory the server is using for connections"),
                ["mssql_lock_memory_bytes"] = new("Total amount of dynamic memory the server is using for locks"),
                ["mssql_optimizer_memory_bytes"] = new("Total amount of dynamic memory the server is using for query optimization"),
                ["mssql_sql_cache_memory_bytes"] = new("Total amount of dynamic memory the server is using for the dynamic SQL cache"),
            },
            """
SELECT RTRIM(counter_name), cntr_value
FROM sys.dm_os_performance_counters
WHERE object_name LIKE '%Memory Manager%'
  AND counter_name IN ('Target Server Memory (KB)', 'Total Server Memory (KB)', 'Memory Grants Pending', 'Memory Grants Outstanding',
                       'Granted Workspace Memory (KB)', 'Maximum Workspace Memory (KB)', 'Connection Memory (KB)', 'Lock Memory (KB)',
                       'Optimizer Memory (KB)', 'SQL Cache Memory (KB)')
""",
            (rows, gauges) =>
            {
                var counters = CollectorUtil.ByName(rows);
                foreach (var (metric, counter) in MemoryManagerKb)
                {
                    gauges.Set(metric, counters.GetValueOrDefault(counter, 0) * 1024);
                }
                foreach (var (metric, counter) in MemoryManagerRaw)
                {
                    gauges.Set(metric, counters.GetValueOrDefault(counter, 0));
                }
            }),

        new(
            "mssql_cache_hit_ratio",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_buffer_cache_hit_ratio"] = new("Percentage of page requests satisfied from the buffer pool"),
                ["mssql_plan_cache_hit_ratio"] = new("Percentage of plan-cache lookups that were hits (_Total)"),
            },
            """
SELECT RTRIM(counter_name), cntr_value
FROM sys.dm_os_performance_counters
WHERE (object_name LIKE '%Buffer Manager%' AND counter_name IN ('Buffer cache hit ratio', 'Buffer cache hit ratio base'))
   OR (object_name LIKE '%Plan Cache%' AND instance_name = '_Total' AND counter_name IN ('Cache Hit Ratio', 'Cache Hit Ratio Base'))
""",
            (rows, gauges) =>
            {
                var counters = CollectorUtil.ByName(rows);
                gauges.Set(
                    "mssql_buffer_cache_hit_ratio",
                    CollectorUtil.RatioPercent(counters.GetValueOrDefault("Buffer cache hit ratio", 0), counters.GetValueOrDefault("Buffer cache hit ratio base", 0)));
                gauges.Set(
                    "mssql_plan_cache_hit_ratio",
                    CollectorUtil.RatioPercent(counters.GetValueOrDefault("Cache Hit Ratio", 0), counters.GetValueOrDefault("Cache Hit Ratio Base", 0)));
            }),
    ];
}

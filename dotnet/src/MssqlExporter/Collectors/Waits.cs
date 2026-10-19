using MssqlExporter.Binding;

namespace MssqlExporter.Collectors;

/// <summary>Aggregate wait statistics since last restart / DBCC SQLPERF reset.</summary>
internal static class Waits
{
    /// <summary>
    /// Wait types that are (almost) always benign background/idle waits — excluded
    /// so the TOP-N reflects real contention. Based on the widely-used Paul Randal /
    /// Ola Hallengren exclusion set.
    /// </summary>
    private static readonly string[] BenignWaits =
    [
        "BROKER_EVENTHANDLER",
        "BROKER_RECEIVE_WAITFOR",
        "BROKER_TASK_STOP",
        "BROKER_TO_FLUSH",
        "BROKER_TRANSMITTER",
        "CHECKPOINT_QUEUE",
        "CHKPT",
        "CLR_AUTO_EVENT",
        "CLR_MANUAL_EVENT",
        "CLR_SEMAPHORE",
        "DBMIRROR_DBM_EVENT",
        "DBMIRROR_EVENTS_QUEUE",
        "DBMIRROR_WORKER_QUEUE",
        "DBMIRRORING_CMD",
        "DIRTY_PAGE_POLL",
        "DISPATCHER_QUEUE_SEMAPHORE",
        "EXECSYNC",
        "FSAGENT",
        "FT_IFTS_SCHEDULER_IDLE_WAIT",
        "FT_IFTSHC_MUTEX",
        "HADR_CLUSAPI_CALL",
        "HADR_FILESTREAM_IOMGR_IOCOMPLETION",
        "HADR_LOGCAPTURE_WAIT",
        "HADR_NOTIFICATION_DEQUEUE",
        "HADR_TIMER_TASK",
        "HADR_WORK_QUEUE",
        "KSOURCE_WAKEUP",
        "LAZYWRITER_SLEEP",
        "LOGMGR_QUEUE",
        "MEMORY_ALLOCATION_EXT",
        "ONDEMAND_TASK_QUEUE",
        "PARALLEL_REDO_DRAIN_WORKER",
        "PARALLEL_REDO_LOG_CACHE",
        "PARALLEL_REDO_TRAN_LIST",
        "PARALLEL_REDO_WORKER_SYNC",
        "PARALLEL_REDO_WORKER_WAIT_WORK",
        "PREEMPTIVE_XE_GETTARGETSTATE",
        "PWAIT_ALL_COMPONENTS_INITIALIZED",
        "PWAIT_DIRECTLOGCONSUMER_GETNEXT",
        "QDS_PERSIST_TASK_MAIN_LOOP_SLEEP",
        "QDS_ASYNC_QUEUE",
        "QDS_CLEANUP_STALE_QUERIES_TASK_MAIN_LOOP_SLEEP",
        "QDS_SHUTDOWN_QUEUE",
        "REDO_THREAD_PENDING_WORK",
        "REQUEST_FOR_DEADLOCK_SEARCH",
        "RESOURCE_QUEUE",
        "SERVER_IDLE_CHECK",
        "SLEEP_BPOOL_FLUSH",
        "SLEEP_DBSTARTUP",
        "SLEEP_DCOMSTARTUP",
        "SLEEP_MASTERDBREADY",
        "SLEEP_MASTERMDREADY",
        "SLEEP_MASTERUPGRADED",
        "SLEEP_MSDBSTARTUP",
        "SLEEP_SYSTEMTASK",
        "SLEEP_TASK",
        "SLEEP_TEMPDBSTARTUP",
        "SNI_HTTP_ACCEPT",
        "SOS_WORK_DISPATCHER",
        "SP_SERVER_DIAGNOSTICS_SLEEP",
        "SQLTRACE_BUFFER_FLUSH",
        "SQLTRACE_INCREMENTAL_FLUSH_SLEEP",
        "SQLTRACE_WAIT_ENTRIES",
        "WAIT_FOR_RESULTS",
        "WAITFOR",
        "WAITFOR_TASKSHUTDOWN",
        "WAIT_XTP_RECOVERY",
        "WAIT_XTP_HOST_WAIT",
        "WAIT_XTP_OFFLINE_CKPT_NEW_LOG",
        "WAIT_XTP_CKPT_CLOSE",
        "XE_DISPATCHER_JOIN",
        "XE_DISPATCHER_WAIT",
        "XE_TIMER_EVENT",
    ];

    private static readonly string BenignList = string.Join(", ", BenignWaits.Select(w => $"'{w}'"));

    public static IReadOnlyList<Collector> All { get; } =
    [
        new(
            "mssql_wait_stats",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_wait_time_ms"] = new("Cumulative wait time by wait type since last restart", LabelNames: ["wait_type"]),
                ["mssql_wait_tasks"] = new("Cumulative number of waits by wait type since last restart", LabelNames: ["wait_type"]),
                ["mssql_signal_wait_time_ms"] = new("Cumulative signal (post-wait, runnable) time by wait type", LabelNames: ["wait_type"]),
            },
            $"""
SELECT TOP 25 RTRIM(wait_type), wait_time_ms, waiting_tasks_count, signal_wait_time_ms
FROM sys.dm_os_wait_stats
WHERE wait_type NOT IN ({BenignList})
ORDER BY wait_time_ms DESC
""",
            (rows, gauges) =>
            {
                foreach (var row in rows)
                {
                    var waitType = ("wait_type", CollectorUtil.AsString(row[0]));
                    gauges.Set("mssql_wait_time_ms", CollectorUtil.AsDouble(row[1]), waitType);
                    gauges.Set("mssql_wait_tasks", CollectorUtil.AsDouble(row[2]), waitType);
                    gauges.Set("mssql_signal_wait_time_ms", CollectorUtil.AsDouble(row[3]), waitType);
                }
            }),

        new(
            "mssql_wait_time_total",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_wait_time_ms_total"] = new("Sum of cumulative wait time across all non-benign wait types"),
            },
            $"SELECT ISNULL(SUM(wait_time_ms), 0) FROM sys.dm_os_wait_stats WHERE wait_type NOT IN ({BenignList})",
            (rows, gauges) => gauges.Set("mssql_wait_time_ms_total", CollectorUtil.AsDouble(rows[0][0]))),
    ];
}

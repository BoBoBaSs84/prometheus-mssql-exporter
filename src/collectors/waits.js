import createDebug from "debug";

const log = createDebug("metrics");

/**
 * Wait types that are (almost) always benign background/idle waits — excluded
 * so the TOP-N reflects real contention. Based on the widely-used Paul Randal /
 * Ola Hallengren exclusion set.
 */
const BENIGN_WAITS = [
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

const benignList = BENIGN_WAITS.map((w) => `'${w}'`).join(", ");

/** Aggregate wait statistics since last restart / DBCC SQLPERF reset. */
export default [
  {
    name: "mssql_wait_stats",
    gauges: {
      mssql_wait_time_ms: { help: "Cumulative wait time by wait type since last restart", labelNames: ["wait_type"] },
      mssql_wait_tasks: { help: "Cumulative number of waits by wait type since last restart", labelNames: ["wait_type"] },
      mssql_signal_wait_time_ms: { help: "Cumulative signal (post-wait, runnable) time by wait type", labelNames: ["wait_type"] },
    },
    query: `SELECT TOP 25 RTRIM(wait_type), wait_time_ms, waiting_tasks_count, signal_wait_time_ms
FROM sys.dm_os_wait_stats
WHERE wait_type NOT IN (${benignList})
ORDER BY wait_time_ms DESC`,
    collect: (rows, metrics) => {
      log("Fetched top wait stats", rows.length);
      for (const row of rows) {
        const waitType = row[0].value;
        metrics.mssql_wait_time_ms.set({ wait_type: waitType }, row[1].value);
        metrics.mssql_wait_tasks.set({ wait_type: waitType }, row[2].value);
        metrics.mssql_signal_wait_time_ms.set({ wait_type: waitType }, row[3].value);
      }
    },
  },
  {
    name: "mssql_wait_time_total",
    gauges: {
      mssql_wait_time_ms_total: { help: "Sum of cumulative wait time across all non-benign wait types" },
    },
    query: `SELECT ISNULL(SUM(wait_time_ms), 0) FROM sys.dm_os_wait_stats WHERE wait_type NOT IN (${benignList})`,
    collect: (rows, metrics) => {
      metrics.mssql_wait_time_ms_total.set(rows[0][0].value);
    },
  },
];

import createDebug from "debug";

const log = createDebug("metrics");

/** Buffer manager counters and per-database I/O stalls. */
export default [
  {
    name: "mssql_buffer_manager",
    gauges: {
      mssql_page_read_total: { help: "Page reads/sec" },
      mssql_page_write_total: { help: "Page writes/sec" },
      mssql_page_life_expectancy: {
        help: "Indicates the minimum number of seconds a page will stay in the buffer pool on this node without references. The traditional advice from Microsoft used to be that the PLE should remain above 300 seconds",
      },
      mssql_lazy_write_total: { help: "Lazy writes/sec" },
      mssql_page_checkpoint_total: { help: "Checkpoint pages/sec" },
    },
    query: `SELECT * FROM
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
    `,
    collect: (rows, metrics) => {
      const row = rows[0];
      const pageRead = row[0].value;
      const pageWrite = row[1].value;
      const pageLifeExpectancy = row[2].value;
      const lazyWriteTotal = row[3].value;
      const pageCheckpointTotal = row[4].value;
      log("Fetched the Buffer Manager", pageRead, pageWrite, pageLifeExpectancy, lazyWriteTotal, pageCheckpointTotal);
      metrics.mssql_page_read_total.set(pageRead);
      metrics.mssql_page_write_total.set(pageWrite);
      metrics.mssql_page_life_expectancy.set(pageLifeExpectancy);
      metrics.mssql_page_checkpoint_total.set(pageCheckpointTotal);
      metrics.mssql_lazy_write_total.set(lazyWriteTotal);
    },
  },
  {
    name: "mssql_io_stall",
    gauges: {
      mssql_io_stall: { help: "Wait time (ms) of stall since last restart", labelNames: ["database", "type"] },
      mssql_io_stall_total: { help: "Wait time (ms) of stall since last restart", labelNames: ["database"] },
    },
    query: `SELECT
cast(DB_Name(a.database_id) as varchar) as name,
    max(io_stall_read_ms),
    max(io_stall_write_ms),
    max(io_stall),
    max(io_stall_queued_read_ms),
    max(io_stall_queued_write_ms)
FROM
sys.dm_io_virtual_file_stats(null, null) a
INNER JOIN sys.master_files b ON a.database_id = b.database_id and a.file_id = b.file_id
GROUP BY a.database_id`,
    collect: (rows, metrics) => {
      for (const row of rows) {
        const database = row[0].value;
        const read = row[1].value;
        const write = row[2].value;
        const stall = row[3].value;
        const queuedRead = row[4].value;
        const queuedWrite = row[5].value;
        log("Fetched number of stalls for database", database, read, write, queuedRead, queuedWrite);
        metrics.mssql_io_stall_total.set({ database }, stall);
        metrics.mssql_io_stall.set({ database, type: "read" }, read);
        metrics.mssql_io_stall.set({ database, type: "write" }, write);
        metrics.mssql_io_stall.set({ database, type: "queued_read" }, queuedRead);
        metrics.mssql_io_stall.set({ database, type: "queued_write" }, queuedWrite);
      }
    },
  },
];

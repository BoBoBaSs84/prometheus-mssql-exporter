/**
 * Fixture rows in the tedious "row array" shape (`rows[i][columnIndex].value`),
 * one entry per collector in src/collectors. Column order matches each query.
 */
const r = (...values) => values.map((value) => ({ value }));

export const fixtures = {
  // instance
  mssql_up: [r(1)],
  mssql_product_version: [r("15.0.2000.5")],
  mssql_instance_local_time: [r(1_700_000_000)],

  // connections
  mssql_connections: [r("master", 5), r("tempdb", 2)],
  mssql_client_connections: [r("apphost", "master", 3)],

  // errors
  mssql_user_errors: [r(7)],
  mssql_kill_connection_errors: [r(1)],

  // databases
  mssql_database_state: [r("master", 0), r("tempdb", 0)],
  mssql_log_growths: [r("tempdb", 2), r("master", 0)],
  mssql_database_filesize: [r("master", "master", 0, "/var/opt/mssql/data/master.mdf", 4096)],

  // io
  mssql_buffer_manager: [r(100, 50, 8000, 3, 12)],
  mssql_io_stall: [r("master", 10, 20, 35, 1, 2)],

  // throughput
  mssql_deadlocks: [r(0)],
  mssql_batch_requests: [r(500)],
  mssql_transactions: [r("tempdb", 42), r("master", 5)],
  mssql_sql_statistics: [r("SQL Compilations/sec", 111), r("SQL Re-Compilations/sec", 4)],
  mssql_access_methods: [
    r("Page Splits/sec", 9),
    r("Full Scans/sec", 21),
    r("Range Scans/sec", 88),
    r("Index Searches/sec", 1200),
    r("Forwarded Records/sec", 2),
  ],
  mssql_lock_stats: [r("Lock Waits/sec", 6), r("Lock Wait Time (ms)", 250), r("Number of Lock Timeouts/sec", 1)],
  mssql_latch_stats: [r("Latch Waits/sec", 15), r("Total Latch Wait Time (ms)", 300)],

  // memory
  mssql_os_process_memory: [r(123, 45)],
  mssql_os_sys_memory: [r(1000, 400, 2000, 800)],
  mssql_memory_manager: [
    r("Target Server Memory (KB)", 2_000_000),
    r("Total Server Memory (KB)", 1_500_000),
    r("Memory Grants Pending", 0),
    r("Memory Grants Outstanding", 3),
    r("Granted Workspace Memory (KB)", 40_000),
    r("Maximum Workspace Memory (KB)", 900_000),
    r("Connection Memory (KB)", 5_000),
    r("Lock Memory (KB)", 2_000),
    r("Optimizer Memory (KB)", 1_000),
    r("SQL Cache Memory (KB)", 8_000),
  ],
  mssql_cache_hit_ratio: [r("Buffer cache hit ratio", 990), r("Buffer cache hit ratio base", 1000), r("Cache Hit Ratio", 45), r("Cache Hit Ratio Base", 50)],

  // activity
  mssql_activity: [r(2, 1.5, 4, 12.5, 30)],
  mssql_active_sessions: [r("running", 1), r("sleeping", 3)],

  // waits
  mssql_wait_stats: [r("PAGEIOLATCH_SH", 5000, 120, 200), r("LCK_M_X", 1200, 8, 60)],
  mssql_wait_time_total: [r(6200)],

  // space
  mssql_log_space: [r("master", 8.0, 12.5, 0), r("tempdb", 16.0, 3.0, 0)],
  mssql_vlf_count: [r("master", 4), r("tempdb", 8)],
  mssql_volume_stats: [r("/var/opt/mssql", 50_000_000_000, 20_000_000_000)],

  // settings
  mssql_configuration: [r("max degree of parallelism", 0), r("cost threshold for parallelism", 5)],

  // backups
  mssql_backups: [r("master", "full", 3600, 1_699_996_400), r("master", "diff", null, null), r("master", "log", null, null)],

  // availability
  mssql_database_properties: [r("master", 3, 0, 0, 0, 2), r("model", 1, 0, 0, 0, 2)],
  mssql_suspect_pages: [r("master", 0), r("tempdb", 0)],

  // always on
  mssql_hadr_enabled: [r(0)],
  mssql_hadr_replica: [r("ag1", "NODE1", 2, 1, 2)],
  mssql_hadr_database: [r("ag1", "NODE2", "appdb", 2, 2, 0, 0, 1)],

  // agent
  mssql_agent_up: [r(1)],
  mssql_agent_jobs: [r("Nightly Backup", 1, 1, 1_699_990_000, 125, 0)],
};

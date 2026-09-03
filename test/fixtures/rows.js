/**
 * Fixture rows in the tedious "row array" shape (`rows[i][columnIndex].value`),
 * one entry per collector in src/metrics.js. Column order matches each query.
 */
const r = (...values) => values.map((value) => ({ value }));

export const fixtures = {
  mssql_up: [r(1)],
  mssql_product_version: [r("15.0.2000.5")],
  mssql_instance_local_time: [r(1_700_000_000)],
  mssql_connections: [r("master", 5), r("tempdb", 2)],
  mssql_client_connections: [r("apphost", "master", 3)],
  mssql_deadlocks: [r(0)],
  mssql_user_errors: [r(7)],
  mssql_kill_connection_errors: [r(1)],
  mssql_database_state: [r("master", 0), r("tempdb", 0)],
  mssql_log_growths: [r("tempdb", 2), r("master", 0)],
  mssql_database_filesize: [r("master", "master", 0, "/var/opt/mssql/data/master.mdf", 4096)],
  mssql_buffer_manager: [r(100, 50, 8000, 3, 12)],
  mssql_io_stall: [r("master", 10, 20, 35, 1, 2)],
  mssql_batch_requests: [r(500)],
  mssql_transactions: [r("tempdb", 42), r("master", 5)],
  mssql_os_process_memory: [r(123, 45)],
  mssql_os_sys_memory: [r(1000, 400, 2000, 800)],
};

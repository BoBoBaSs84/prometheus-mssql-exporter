import createDebug from "debug";

import { byName, ratioPercent } from "./_util.js";

const log = createDebug("metrics");

const MEMORY_MANAGER_KB = {
  mssql_target_server_memory_bytes: "Target Server Memory (KB)",
  mssql_total_server_memory_bytes: "Total Server Memory (KB)",
  mssql_granted_workspace_memory_bytes: "Granted Workspace Memory (KB)",
  mssql_maximum_workspace_memory_bytes: "Maximum Workspace Memory (KB)",
  mssql_connection_memory_bytes: "Connection Memory (KB)",
  mssql_lock_memory_bytes: "Lock Memory (KB)",
  mssql_optimizer_memory_bytes: "Optimizer Memory (KB)",
  mssql_sql_cache_memory_bytes: "SQL Cache Memory (KB)",
};
const MEMORY_MANAGER_RAW = {
  mssql_memory_grants_pending: "Memory Grants Pending",
  mssql_memory_grants_outstanding: "Memory Grants Outstanding",
};

/** Operating-system memory as seen by the SQL Server process. */
export default [
  {
    name: "mssql_os_process_memory",
    gauges: {
      mssql_page_fault_count: { help: "Number of page faults since last restart" },
      mssql_memory_utilization_percentage: { help: "Percentage of memory utilization" },
    },
    query: `SELECT page_fault_count, memory_utilization_percentage
FROM sys.dm_os_process_memory`,
    collect: (rows, metrics) => {
      const pageFaultCount = rows[0][0].value;
      const memoryUtilizationPercentage = rows[0][1].value;
      log("Fetched page fault count", pageFaultCount);
      metrics.mssql_page_fault_count.set(pageFaultCount);
      metrics.mssql_memory_utilization_percentage.set(memoryUtilizationPercentage);
    },
  },
  {
    name: "mssql_os_sys_memory",
    gauges: {
      mssql_total_physical_memory_kb: { help: "Total physical memory in KB" },
      mssql_available_physical_memory_kb: { help: "Available physical memory in KB" },
      mssql_total_page_file_kb: { help: "Total page file in KB" },
      mssql_available_page_file_kb: { help: "Available page file in KB" },
    },
    query: `SELECT total_physical_memory_kb, available_physical_memory_kb, total_page_file_kb, available_page_file_kb
FROM sys.dm_os_sys_memory`,
    collect: (rows, metrics) => {
      const [totalPhysical, availablePhysical, totalPageFile, availablePageFile] = rows[0].map((cell) => cell.value);
      log("Fetched system memory information", totalPhysical, availablePhysical, totalPageFile, availablePageFile);
      metrics.mssql_total_physical_memory_kb.set(totalPhysical);
      metrics.mssql_available_physical_memory_kb.set(availablePhysical);
      metrics.mssql_total_page_file_kb.set(totalPageFile);
      metrics.mssql_available_page_file_kb.set(availablePageFile);
    },
  },
  {
    name: "mssql_memory_manager",
    gauges: {
      mssql_target_server_memory_bytes: { help: "Ideal amount of memory the server can consume" },
      mssql_total_server_memory_bytes: { help: "Amount of memory the server has committed" },
      mssql_memory_grants_pending: { help: "Number of processes waiting for a workspace memory grant" },
      mssql_memory_grants_outstanding: { help: "Number of processes that have acquired a workspace memory grant" },
      mssql_granted_workspace_memory_bytes: { help: "Total amount of memory currently granted to executing processes" },
      mssql_maximum_workspace_memory_bytes: { help: "Maximum amount of memory available for executing processes" },
      mssql_connection_memory_bytes: { help: "Total amount of dynamic memory the server is using for connections" },
      mssql_lock_memory_bytes: { help: "Total amount of dynamic memory the server is using for locks" },
      mssql_optimizer_memory_bytes: { help: "Total amount of dynamic memory the server is using for query optimization" },
      mssql_sql_cache_memory_bytes: { help: "Total amount of dynamic memory the server is using for the dynamic SQL cache" },
    },
    query: `SELECT RTRIM(counter_name), cntr_value
FROM sys.dm_os_performance_counters
WHERE object_name LIKE '%Memory Manager%'
  AND counter_name IN ('Target Server Memory (KB)', 'Total Server Memory (KB)', 'Memory Grants Pending', 'Memory Grants Outstanding',
                       'Granted Workspace Memory (KB)', 'Maximum Workspace Memory (KB)', 'Connection Memory (KB)', 'Lock Memory (KB)',
                       'Optimizer Memory (KB)', 'SQL Cache Memory (KB)')`,
    collect: (rows, metrics) => {
      const counters = byName(rows);
      log("Fetched Memory Manager counters", counters.size);
      for (const [metric, counter] of Object.entries(MEMORY_MANAGER_KB)) {
        metrics[metric].set((counters.get(counter) ?? 0) * 1024);
      }
      for (const [metric, counter] of Object.entries(MEMORY_MANAGER_RAW)) {
        metrics[metric].set(counters.get(counter) ?? 0);
      }
    },
  },
  {
    name: "mssql_cache_hit_ratio",
    gauges: {
      mssql_buffer_cache_hit_ratio: { help: "Percentage of page requests satisfied from the buffer pool" },
      mssql_plan_cache_hit_ratio: { help: "Percentage of plan-cache lookups that were hits (_Total)" },
    },
    query: `SELECT RTRIM(counter_name), cntr_value
FROM sys.dm_os_performance_counters
WHERE (object_name LIKE '%Buffer Manager%' AND counter_name IN ('Buffer cache hit ratio', 'Buffer cache hit ratio base'))
   OR (object_name LIKE '%Plan Cache%' AND instance_name = '_Total' AND counter_name IN ('Cache Hit Ratio', 'Cache Hit Ratio Base'))`,
    collect: (rows, metrics) => {
      const counters = byName(rows);
      metrics.mssql_buffer_cache_hit_ratio.set(ratioPercent(counters.get("Buffer cache hit ratio") ?? 0, counters.get("Buffer cache hit ratio base") ?? 0));
      metrics.mssql_plan_cache_hit_ratio.set(ratioPercent(counters.get("Cache Hit Ratio") ?? 0, counters.get("Cache Hit Ratio Base") ?? 0));
    },
  },
];

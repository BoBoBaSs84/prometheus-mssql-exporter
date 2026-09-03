import { describe, it, expect } from "vitest";

import { entries } from "../../src/metrics.js";
import { fixtures } from "../fixtures/rows.js";

const resetEntry = (entry) => Object.values(entry.metrics).forEach((metric) => metric.reset());

const collect = (name) => {
  const entry = entries[name];
  resetEntry(entry);
  entry.collect(fixtures[name], entry.metrics);
  return entry.metrics;
};

const valueOf = async (metric, labels) => {
  const { values } = await metric.get();
  const match = values.find((sample) => !labels || Object.entries(labels).every(([key, value]) => sample.labels[key] === value));
  return match?.value;
};

describe("collectors", () => {
  it("has a fixture for every collector", () => {
    expect(Object.keys(fixtures).sort()).toEqual(Object.keys(entries).sort());
  });

  it.each(Object.keys(entries))("%s collects without throwing", (name) => {
    const entry = entries[name];
    resetEntry(entry);
    expect(() => entry.collect(fixtures[name], entry.metrics)).not.toThrow();
  });

  it("mssql_up records the instance status", async () => {
    const metrics = collect("mssql_up");
    expect(await valueOf(metrics.mssql_up)).toBe(1);
  });

  it("mssql_product_version records major.minor", async () => {
    const metrics = collect("mssql_product_version");
    expect(await valueOf(metrics.mssql_product_version)).toBe(15);
  });

  it("mssql_connections records one series per database", async () => {
    const metrics = collect("mssql_connections");
    expect(await valueOf(metrics.mssql_connections, { database: "master", state: "current" })).toBe(5);
    expect(await valueOf(metrics.mssql_connections, { database: "tempdb", state: "current" })).toBe(2);
  });

  it("mssql_database_state records the state per database", async () => {
    const metrics = collect("mssql_database_state");
    expect(await valueOf(metrics.mssql_database_state, { database: "master" })).toBe(0);
  });

  it("mssql_buffer_manager splits the pivoted row into gauges", async () => {
    const metrics = collect("mssql_buffer_manager");
    expect(await valueOf(metrics.mssql_page_read_total)).toBe(100);
    expect(await valueOf(metrics.mssql_page_write_total)).toBe(50);
    expect(await valueOf(metrics.mssql_page_life_expectancy)).toBe(8000);
    expect(await valueOf(metrics.mssql_lazy_write_total)).toBe(3);
    expect(await valueOf(metrics.mssql_page_checkpoint_total)).toBe(12);
  });

  it("mssql_io_stall records read/write/queued types plus the total", async () => {
    const metrics = collect("mssql_io_stall");
    expect(await valueOf(metrics.mssql_io_stall, { database: "master", type: "read" })).toBe(10);
    expect(await valueOf(metrics.mssql_io_stall, { database: "master", type: "write" })).toBe(20);
    expect(await valueOf(metrics.mssql_io_stall, { database: "master", type: "queued_read" })).toBe(1);
    expect(await valueOf(metrics.mssql_io_stall, { database: "master", type: "queued_write" })).toBe(2);
    expect(await valueOf(metrics.mssql_io_stall_total, { database: "master" })).toBe(35);
  });

  it("mssql_os_sys_memory records each memory gauge", async () => {
    const metrics = collect("mssql_os_sys_memory");
    expect(await valueOf(metrics.mssql_total_physical_memory_kb)).toBe(1000);
    expect(await valueOf(metrics.mssql_available_physical_memory_kb)).toBe(400);
    expect(await valueOf(metrics.mssql_total_page_file_kb)).toBe(2000);
    expect(await valueOf(metrics.mssql_available_page_file_kb)).toBe(800);
  });

  it("tolerates an empty result set for a multi-row collector", () => {
    const entry = entries.mssql_connections;
    resetEntry(entry);
    expect(() => entry.collect([], entry.metrics)).not.toThrow();
  });
});

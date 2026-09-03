/**
 * Metric registry wiring.
 *
 * Collectors live in ./collectors and only *describe* their gauges. This module
 * binds those descriptions to a prom-client registry, so the same collector set
 * can be materialised against the default registry (the `/metrics` endpoint) or
 * against a throwaway registry per request (the `/probe` endpoint).
 */
import client from "prom-client";

import { collectors } from "./collectors/index.js";

/**
 * @param {Record<string, { name?: string, help: string, labelNames?: string[] }>} spec
 * @param {import("prom-client").Registry} registry
 */
const makeGauges = (spec, registry) =>
  Object.fromEntries(
    Object.entries(spec).map(([key, opts]) => [
      key,
      new client.Gauge({
        name: opts.name ?? key,
        help: opts.help,
        labelNames: opts.labelNames ?? [],
        registers: [registry],
      }),
    ]),
  );

/**
 * Materialise every collector against `registry`, returning a name→collector map
 * where each collector has a live `metrics` gauge map.
 *
 * @param {import("prom-client").Registry} [registry]
 */
export function buildEntries(registry = client.register) {
  const entries = {};
  for (const collector of collectors) {
    entries[collector.name] = { ...collector, metrics: makeGauges(collector.gauges, registry) };
  }
  return entries;
}

/** Scrape-quality gauges, materialised the same way. */
export function buildMeta(registry = client.register) {
  return {
    scrapeDuration: new client.Gauge({
      name: "mssql_scrape_duration_seconds",
      help: "Total time the exporter spent collecting metrics for this scrape",
      registers: [registry],
    }),
    collectorDuration: new client.Gauge({
      name: "mssql_collector_duration_seconds",
      help: "Time spent running a single collector",
      labelNames: ["collector"],
      registers: [registry],
    }),
    collectorSuccess: new client.Gauge({
      name: "mssql_collector_success",
      help: "Whether a collector completed without error (1) or not (0)",
      labelNames: ["collector"],
      registers: [registry],
    }),
  };
}

/** Collectors bound to the default registry — used by `/metrics` and the tests. */
export const entries = buildEntries(client.register);

/** Meta gauges bound to the default registry. */
export const meta = buildMeta(client.register);

export { collectors };

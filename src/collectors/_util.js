/** Shared helpers for collectors. */

/**
 * Turn rows shaped `[nameCell, valueCell, ...]` into a `Map<trimmedName, value>`,
 * for `sys.dm_os_performance_counters`-style "counter_name, cntr_value" queries.
 *
 * @param {Array<Array<{ value: unknown }>>} rows
 * @returns {Map<string, number>}
 */
export const byName = (rows) => new Map(rows.map((row) => [String(row[0].value).trim(), Number(row[1].value)]));

/** Safe ratio → percentage; 0 when the base counter is 0. */
export const ratioPercent = (value, base) => (base > 0 ? (value / base) * 100 : 0);

import { describe, it, expect } from "vitest";

import { coerceRow } from "../../src/db.js";

const cells = (...values) => values.map((value) => ({ value }));

describe("coerceRow", () => {
  it("converts bigint values to numbers", () => {
    const row = coerceRow(cells(10n, 20n));
    expect(row.map((c) => c.value)).toEqual([10, 20]);
    expect(row.every((c) => typeof c.value === "number")).toBe(true);
  });

  it("converts integer strings (tedious bigint columns) to numbers", () => {
    expect(coerceRow(cells("4352", "-7", "0")).map((c) => c.value)).toEqual([4352, -7, 0]);
  });

  it("leaves non-integer strings, numbers, and null untouched", () => {
    const row = coerceRow(cells("master", "/var/opt/mssql/data/master.mdf", "16.0.4225.1", 42, null));
    expect(row.map((c) => c.value)).toEqual(["master", "/var/opt/mssql/data/master.mdf", "16.0.4225.1", 42, null]);
  });
});

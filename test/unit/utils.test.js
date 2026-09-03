import { describe, it, expect } from "vitest";

import { productVersionParse } from "../../src/utils.js";

describe("productVersionParse", () => {
  it("parses a four-part version", () => {
    expect(productVersionParse("15.0.2000.5")).toEqual({ major: 15, minor: 0, patch: 2000, build: 5 });
  });

  it.each([
    ["16.0.1000.6", 16],
    ["17.0.900.7", 17],
  ])("extracts the major from %s", (version, major) => {
    expect(productVersionParse(version).major).toBe(major);
  });

  it.each(["", "15.0", "15.0.2000", "abc", "15.0.x.5"])("rejects %j", (version) => {
    expect(() => productVersionParse(version)).toThrow(/Invalid product version/);
  });

  it.each([undefined, null, 15, {}])("rejects non-string %j", (version) => {
    expect(() => productVersionParse(version)).toThrow(/Invalid product version/);
  });
});

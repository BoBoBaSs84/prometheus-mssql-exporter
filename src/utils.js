/**
 * Utility functions
 */

/**
 * Parses a four-part SQL Server product version string (e.g. "15.0.2000.5")
 * into its numeric components.
 *
 * @param {string} version value of SERVERPROPERTY('productversion')
 * @returns {{ major: number, minor: number, patch: number, build: number }}
 */
export const productVersionParse = (version) => {
  if (typeof version !== "string" || version.length === 0) {
    throw new Error("Invalid product version " + JSON.stringify(version));
  }
  const match = version.match(/^([0-9]+)\.([0-9]+)\.([0-9]+)\.([0-9]+)$/);
  if (!match) {
    throw new Error("Invalid product version " + version);
  }
  return {
    major: Number(match[1]),
    minor: Number(match[2]),
    patch: Number(match[3]),
    build: Number(match[4]),
  };
};

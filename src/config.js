/**
 * Builds and validates the exporter configuration from environment variables.
 */

const parseBool = (value, fallback) => {
  if (value === undefined || value === "") {
    return fallback;
  }
  return value === "true" || value === "1";
};

const parseIntOr = (value, fallback) => {
  const parsed = parseInt(value, 10);
  return Number.isNaN(parsed) ? fallback : parsed;
};

/**
 * @param {NodeJS.ProcessEnv} [env]
 * @returns {{
 *   connect: import("tedious").ConnectionConfiguration,
 *   port: number,
 *   collectDefaultMetrics: boolean,
 * }}
 */
export function loadConfig(env = process.env) {
  const missing = ["SERVER", "USERNAME", "PASSWORD"].filter((key) => !env[key]);
  if (missing.length > 0) {
    throw new Error(`Missing required environment variable(s): ${missing.join(", ")}`);
  }

  return {
    connect: {
      server: env.SERVER,
      authentication: {
        type: "default",
        options: {
          userName: env.USERNAME,
          password: env.PASSWORD,
        },
      },
      options: {
        port: parseIntOr(env.PORT, 1433),
        encrypt: parseBool(env.ENCRYPT, true),
        trustServerCertificate: parseBool(env.TRUST_SERVER_CERTIFICATE, true),
        rowCollectionOnRequestCompletion: true,
      },
    },
    port: parseIntOr(env.EXPOSE, 4000),
    collectDefaultMetrics: parseBool(env.COLLECT_DEFAULT_METRICS, true),
  };
}

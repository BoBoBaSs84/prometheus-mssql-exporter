import { readFile } from "node:fs/promises";
import { setTimeout as delay } from "node:timers/promises";
import { fileURLToPath } from "node:url";

import { runQuery } from "../../../src/db.js";

const sqlDir = new URL("../sql/", import.meta.url);

/** `GO` is a client-side batch separator; tedious executes one batch per request. */
const BATCH_SEPARATOR = /^\s*GO\s*$/im;

/**
 * Reads a fixture from `test/e2e/sql/`, substitutes the `__NAME__`
 * placeholders and runs it batch by batch on `connection`.
 *
 * @param {import("tedious").Connection} connection
 * @param {string} file file name inside `test/e2e/sql/`
 * @param {Record<string, string>} [replacements] placeholder -> value
 */
export async function runScript(connection, file, replacements = {}) {
  let sql = await readFile(fileURLToPath(new URL(file, sqlDir)), "utf8");
  for (const [placeholder, value] of Object.entries(replacements)) {
    sql = sql.replaceAll(placeholder, value);
  }
  for (const batch of sql.split(BATCH_SEPARATOR)) {
    if (batch.trim()) {
      await runQuery(connection, batch);
    }
  }
}

/**
 * Polls `probe` until it resolves truthy, or throws after `attempts` tries.
 *
 * @param {string} what described in the timeout error
 * @param {() => Promise<unknown>} probe
 */
async function waitFor(what, probe, { attempts = 60, intervalMs = 2_000 } = {}) {
  for (let attempt = 0; attempt < attempts; attempt++) {
    if (await probe()) {
      return;
    }
    await delay(intervalMs);
  }
  throw new Error(`Timed out waiting for ${what}`);
}

/**
 * Runs `action` until it stops throwing. Used for the msdb job procedures,
 * which reject with "Cannot perform this operation while SQLServerAgent is
 * starting" for a while after `sys.dm_server_services` already reports the
 * service as running. The last error is rethrown if it never succeeds.
 *
 * @param {string} what described in the timeout error
 * @param {() => Promise<unknown>} action
 */
export async function retry(what, action, options) {
  let lastError;
  const probe = () =>
    action().then(
      () => true,
      (error) => {
        lastError = error;
        return false;
      },
    );
  try {
    await waitFor(what, probe, options);
  } catch (timeout) {
    throw lastError ?? timeout;
  }
}

/** The Agent service starts a while after the engine accepts connections. */
export const waitForAgent = (connection) =>
  waitFor("the SQL Server Agent service to start", async () => {
    const rows = await runQuery(connection, `SELECT status_desc FROM sys.dm_server_services WHERE servicename LIKE 'SQL Server Agent%'`);
    return rows[0]?.[0].value === "Running";
  });

/**
 * Starts `job` and waits for its outcome row (`step_id = 0`) to land in
 * sysjobhistory — that row carries the run_date/run_time that the collector
 * feeds to `msdb.dbo.agent_datetime()`.
 */
export async function runJobOnce(connection, job) {
  await retry(`job ${job} to start`, () => runQuery(connection, `EXEC msdb.dbo.sp_start_job @job_name = N'${job}'`));
  await waitFor(`job ${job} to record a run`, async () => {
    const rows = await runQuery(
      connection,
      `SELECT COUNT(*) FROM msdb.dbo.sysjobhistory h
         JOIN msdb.dbo.sysjobs j ON j.job_id = h.job_id
        WHERE j.name = N'${job}' AND h.step_id = 0`,
    );
    return rows[0][0].value > 0;
  });
}

const request = require("superagent");

function parse(text) {
  let lines = text.split("\n");
  lines = lines.filter((line) => !line.startsWith("#")).filter((line) => line.length !== 0);
  const o = {};
  lines.forEach((line) => {
    expect(line.indexOf(" ")).toBeGreaterThanOrEqual(0);
    [key, value] = line.split(" ");
    o[key] = parseInt(value);
  });
  return o;
}

const productVersionParse = (version) => {
  const pattern = /^([0-9]+)\.([0-9]+)\.([0-9]+)\.([0-9]+)$/;
  const match = version.match(pattern);
  if (!match) throw new Error("Invalid product version " + version);
  return {
    major: Number(match[1]),
    minor: Number(match[2]),
    patch: Number(match[3]),
    build: Number(match[4]),
  };
};

describe("E2E Test", function () {
  it("Fetch all metrics and ensure that all expected are present", async function () {
    const data = await request.get("http://localhost:4000/metrics");
    expect(data.status).toBe(200);
    let text = data.text;
    const lines = parse(text);

    // some specific tests
    expect(lines.mssql_up).toBe(1);
    // we want to ensure that the version is 14, 15 or 16 (2017, 2019 or 2022)
    expect([14, 15, 16]).toContain(lines.mssql_product_version);
    expect(lines.mssql_instance_local_time).toBeGreaterThan(0);
    expect(lines.mssql_total_physical_memory_kb).toBeGreaterThan(0);

    let v = productVersionParse(lines.mssql_product_version);

    // check if the sql server version is 2022(16)
    if (v.major === 16) {
      // lets ensure that there is at least one instance of these 2022 entries (that differ from 2019)
      const v2022 = [
        'mssql_log_growths{database="model_msdb"}',
        'mssql_log_growths{database="model_replicatedmaster"}',
        'mssql_transactions{database="model_msdb"}',
        'mssql_transactions{database="model_replicatedmaster"}',
      ];
      v2022.forEach((k2022) => {
        const keys = Object.keys(lines);
        const i = keys.findIndex((key) => key.startsWith(k2022));
        expect(i).toBeGreaterThanOrEqual(0);
        keys
          .filter((key) => key.startsWith(k2022))
          .forEach((key) => {
            delete lines[key];
          });
      });
    }

    //  check if the sql server version is 2019(15),
    if (v.major === 15) {
      // lets ensure that there is at least one instance of these 2019 entries (that differ from 2017)
      const v2019 = ["mssql_client_connections", "mssql_database_filesize"];
      v2019.forEach((k2019) => {
        const keys = Object.keys(lines);
        const i = keys.findIndex((key) => key.startsWith(k2019));
        expect(i).toBeGreaterThanOrEqual(0);
        keys
          .filter((key) => key.startsWith(k2019))
          .forEach((key) => {
            delete lines[key];
          });
      });
    }

    // bulk ensure that all expected results of a vanilla mssql server instance are here
    expect(Object.keys(lines)).toEqual([
      "mssql_up",
      "mssql_product_version",
      "mssql_instance_local_time",
      'mssql_connections{database="master",state="current"}',
      "mssql_deadlocks",
      "mssql_user_errors",
      "mssql_kill_connection_errors",
      'mssql_database_state{database="master"}',
      'mssql_database_state{database="tempdb"}',
      'mssql_database_state{database="model"}',
      'mssql_database_state{database="msdb"}',
      'mssql_log_growths{database="tempdb"}',
      'mssql_log_growths{database="model"}',
      'mssql_log_growths{database="msdb"}',
      'mssql_log_growths{database="mssqlsystemresource"}',
      'mssql_log_growths{database="master"}',
      "mssql_page_read_total",
      "mssql_page_write_total",
      "mssql_page_life_expectancy",
      "mssql_lazy_write_total",
      "mssql_page_checkpoint_total",
      'mssql_io_stall{database="master",type="read"}',
      'mssql_io_stall{database="master",type="write"}',
      'mssql_io_stall{database="master",type="queued_read"}',
      'mssql_io_stall{database="master",type="queued_write"}',
      'mssql_io_stall{database="tempdb",type="read"}',
      'mssql_io_stall{database="tempdb",type="write"}',
      'mssql_io_stall{database="tempdb",type="queued_read"}',
      'mssql_io_stall{database="tempdb",type="queued_write"}',
      'mssql_io_stall{database="model",type="read"}',
      'mssql_io_stall{database="model",type="write"}',
      'mssql_io_stall{database="model",type="queued_read"}',
      'mssql_io_stall{database="model",type="queued_write"}',
      'mssql_io_stall{database="msdb",type="read"}',
      'mssql_io_stall{database="msdb",type="write"}',
      'mssql_io_stall{database="msdb",type="queued_read"}',
      'mssql_io_stall{database="msdb",type="queued_write"}',
      'mssql_io_stall_total{database="master"}',
      'mssql_io_stall_total{database="tempdb"}',
      'mssql_io_stall_total{database="model"}',
      'mssql_io_stall_total{database="msdb"}',
      "mssql_batch_requests",
      'mssql_transactions{database="tempdb"}',
      'mssql_transactions{database="model"}',
      'mssql_transactions{database="msdb"}',
      'mssql_transactions{database="mssqlsystemresource"}',
      'mssql_transactions{database="master"}',
      "mssql_page_fault_count",
      "mssql_memory_utilization_percentage",
      "mssql_total_physical_memory_kb",
      "mssql_available_physical_memory_kb",
      "mssql_total_page_file_kb",
      "mssql_available_page_file_kb",
    ]);
  });
});

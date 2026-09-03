import { collectors } from "./metrics.js";

// DOCUMENTATION of queries and their associated metrics (targeted to DBAs)
for (const collector of collectors) {
  console.log("--[", collector.name, collector.optional ? "(optional)" : "", "]");
  for (const [key, spec] of Object.entries(collector.gauges)) {
    console.log("--", spec.name ?? key, spec.help);
  }
  console.log(collector.query.trim().replace(/;\s*$/, "") + ";");
  console.log("");
}

console.log("/*");
for (const collector of collectors) {
  for (const [key, spec] of Object.entries(collector.gauges)) {
    const name = spec.name ?? key;
    const labels = spec.labelNames?.length ? "{" + spec.labelNames + "}" : "";
    console.log("-", name + labels, spec.help);
  }
}
console.log("*/");

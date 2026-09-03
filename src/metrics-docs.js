import { entries } from "./metrics.js";

// DOCUMENTATION of queries and their associated metrics (targeted to DBAs)
Object.entries(entries).forEach(([entryName, entry]) => {
  console.log("--[", entryName, "]");
  for (const key in entry.metrics) {
    if (Object.hasOwn(entry.metrics, key)) {
      console.log("--", entry.metrics[key].name, entry.metrics[key].help);
    }
  }
  console.log(entry.query + ";");
  console.log("");
});

console.log("/*");
Object.values(entries).forEach((entry) => {
  for (const key in entry.metrics) {
    if (Object.hasOwn(entry.metrics, key)) {
      const metric = entry.metrics[key];
      const labels = metric.labelNames.length > 0 ? "{" + metric.labelNames + "}" : "";
      console.log("-", metric.name + labels, metric.help);
    }
  }
});
console.log("*/");

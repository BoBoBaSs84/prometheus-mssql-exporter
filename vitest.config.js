import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    projects: [
      {
        test: {
          name: "unit",
          include: ["test/unit/**/*.test.js"],
          environment: "node",
        },
      },
      {
        test: {
          name: "e2e",
          include: ["test/e2e/**/*.test.js"],
          environment: "node",
          // SQL Server container pull + first-boot initialisation is slow.
          testTimeout: 240_000,
          hookTimeout: 240_000,
        },
      },
    ],
    coverage: {
      provider: "v8",
      include: ["src/**/*.js"],
      // index.js is the bootstrap wiring; db.js is exercised by the e2e suite.
      exclude: ["src/metrics-docs.js", "src/index.js"],
    },
  },
});

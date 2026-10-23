const { defineConfig } = require('@playwright/test');

module.exports = defineConfig({
  testDir: 'tests/e2e',
  timeout: 30000,
  retries: 0,
  use: {
    baseURL: 'http://127.0.0.1:5094',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure'
  },
  webServer: {
    command: 'dotnet run --project runner/AutoTest.Web --no-build --url http://127.0.0.1:5094',
    url: 'http://127.0.0.1:5094',
    reuseExistingServer: false,
    timeout: 30000
  }
});

import { defineConfig } from '@playwright/test';

// End-to-end suite (Phase 2): drives the real UI in system Chrome against
// locally running servers. Start them first (start-app.bat in repo root:
// backend http://localhost:5199 + frontend http://localhost:5173), then:
//   $env:E2E_PASSWORD='...'; npx playwright test
// Auth sessions are stored under e2e/.auth (git-ignored, dev cookies).
// No webServer here on purpose: the dev DB (docgen.db) must be up already
// and E2E data is tagged E2E- for later cleanup.
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  timeout: 90000,
  expect: { timeout: 15000 },
  use: {
    baseURL: 'http://localhost:5173',
    channel: 'chrome',
    headless: true,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'setup', testMatch: /auth\.setup\.ts/ },
    {
      name: 'e2e',
      testMatch: /.*\.spec\.ts/,
      dependencies: ['setup'],
    },
  ],
});

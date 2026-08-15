import { defineConfig, devices } from '@playwright/test';

const SPA = process.env.E2E_BASE_URL ?? 'http://localhost:5173';

/**
 * These specs drive the real stack: `docker compose up` for the API, `npm run dev` for the SPA.
 *
 * They skip rather than fail when the API is unreachable — the same convention the backend's
 * integration suite uses, and for the same reason the README gives: a red run on a machine
 * without Docker trains people to ignore red runs.
 */
export default defineConfig({
  testDir: './e2e',
  globalSetup: './e2e/global-setup.ts',
  timeout: 30_000,
  expect: { timeout: 10_000 },
  fullyParallel: false, // the specs mutate one shared catalogue
  workers: 1,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: SPA,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'npm run dev',
    url: SPA,
    reuseExistingServer: true,
    timeout: 60_000,
  },
});

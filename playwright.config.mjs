import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests/browser',
  workers: 1,
  reporter: 'list',
  outputDir: 'artifacts/browser',
  use: {
    baseURL: process.env.TECHVORA_SMOKE_URL || 'http://localhost:5009',
    channel: process.env.PLAYWRIGHT_CHANNEL || 'msedge',
    viewport: { width: 1440, height: 1000 },
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure'
  }
});

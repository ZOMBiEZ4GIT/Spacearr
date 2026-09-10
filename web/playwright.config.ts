import { defineConfig } from '@playwright/test';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const config = mkdtempSync(join(tmpdir(), 'spacearr-e2e-'));
export default defineConfig({
  testDir: './e2e', timeout: 120_000, retries: 0, workers: 1,
  use: { baseURL: 'http://localhost:8797', headless: true },
  webServer: [
    { command: 'node --import tsx e2e/fake-arr.ts', port: 7999, reuseExistingServer: false },
    { command: 'dotnet run --project ../src/Spacearr --no-build -c Release', port: 8797, reuseExistingServer: false, timeout: 120_000, env: { SPACEARR_CONFIG_DIR: config, SPACEARR_PORT: '8797', ASPNETCORE_ENVIRONMENT: 'Production' } },
  ],
});

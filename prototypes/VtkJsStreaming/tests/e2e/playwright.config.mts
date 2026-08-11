import { fileURLToPath } from 'node:url';
import playwrightTest from '../../src/client/node_modules/@playwright/test/index.js';

const { defineConfig } = playwrightTest;

const prototypeRoot = fileURLToPath(new URL('../../', import.meta.url));
const clientRoot = fileURLToPath(new URL('../../src/client/', import.meta.url));

export default defineConfig({
  testDir: '.',
  testMatch: '*.spec.ts',
  timeout: 30_000,
  fullyParallel: false,
  workers: 1,
  reporter: 'list',
  outputDir: 'test-results',
  use: {
    baseURL: 'http://127.0.0.1:5173',
    channel: 'chrome',
    headless: true,
    viewport: { width: 1400, height: 900 },
    deviceScaleFactor: 1,
    launchOptions: { args: ['--use-angle=swiftshader', '--enable-webgl'] },
  },
  webServer: [
    {
      command: 'dotnet run --project src/PrePoMax.Render.Server -c Release',
      cwd: prototypeRoot,
      url: 'http://127.0.0.1:5174/healthz',
      timeout: 120_000,
      reuseExistingServer: false,
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: 'Development',
        PREPOMAX_RENDER_PORT: '5174',
        PREPOMAX_RENDER_DELAY_CHUNK_ORDINAL: '1',
        PREPOMAX_RENDER_DELAY_MS: '1500',
        PREPOMAX_RENDER_HOST_TOKEN: 'playwright-host-token',
      },
    },
    {
      command: 'npm run build && npx vite preview --host=127.0.0.1 --port=5173',
      cwd: clientRoot,
      url: 'http://127.0.0.1:5173',
      timeout: 120_000,
      reuseExistingServer: false,
    },
  ],
});

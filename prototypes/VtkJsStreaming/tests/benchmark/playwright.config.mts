import { fileURLToPath } from 'node:url';
import playwrightTest from '../../src/client/node_modules/@playwright/test/index.js';

const { defineConfig } = playwrightTest;
const prototypeRoot = fileURLToPath(new URL('../../', import.meta.url));
const clientRoot = fileURLToPath(new URL('../../src/client/', import.meta.url));
const headless = process.env.PREPOMAX_BENCHMARK_HEADLESS === '1';

export default defineConfig({
  testDir: '.',
  testMatch: 'large-benchmark.spec.ts',
  timeout: 10 * 60_000,
  workers: 1,
  reporter: 'list',
  outputDir: 'test-results',
  use: {
    baseURL: 'http://127.0.0.1:5193',
    channel: 'chrome',
    headless,
    viewport: { width: 1920, height: 1080 },
    deviceScaleFactor: 1,
    launchOptions: {
      args: ['--enable-precise-memory-info', '--js-flags=--expose-gc'],
    },
  },
  webServer: [
    {
      command: 'dotnet run --project src/PrePoMax.Render.Server -c Release --no-build',
      cwd: prototypeRoot,
      url: 'http://127.0.0.1:5194/healthz',
      timeout: 120_000,
      reuseExistingServer: false,
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: 'Development',
        PREPOMAX_RENDER_PORT: '5194',
        PREPOMAX_RENDER_ALLOWED_ORIGINS: 'http://127.0.0.1:5193',
        PREPOMAX_RENDER_SCENE: 'large',
        PREPOMAX_RENDER_CHUNK_CELL_ROWS: process.env.PREPOMAX_RENDER_CHUNK_CELL_ROWS ?? '50',
        PREPOMAX_RENDER_RESOURCE_CAP_BYTES: process.env.PREPOMAX_RENDER_RESOURCE_CAP_BYTES ?? '8388608',
      },
    },
    {
      command: 'npm run build && npx vite preview --host=127.0.0.1 --port=5193',
      cwd: clientRoot,
      url: 'http://127.0.0.1:5193',
      timeout: 120_000,
      reuseExistingServer: false,
    },
  ],
});

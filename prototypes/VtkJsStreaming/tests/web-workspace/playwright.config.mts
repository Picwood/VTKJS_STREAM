import { fileURLToPath } from 'node:url';
import playwrightTest from '../../src/client/node_modules/@playwright/test/index.js';
const { defineConfig } = playwrightTest;
export default defineConfig({
  testDir: '.', testMatch: '*.spec.mts', timeout: 60_000, workers: 1, reporter: 'list',
  outputDir: 'test-results/web-workspace',
  use: { baseURL: 'http://localhost:5191', viewport: { width: 1440, height: 960 },
    launchOptions: { args: ['--use-angle=swiftshader', '--enable-webgl'] } },
  webServer: { command: 'bash scripts/start-web-workspace.sh',
    cwd: fileURLToPath(new URL('../../../../', import.meta.url)),
    url: 'http://localhost:5191/workspace.html', timeout: 30_000, reuseExistingServer: false,
    env: { ...process.env, PREPOMAX_WEB_PORT: '5191' } },
});

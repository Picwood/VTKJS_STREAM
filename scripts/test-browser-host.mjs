// Real-browser acceptance. Host stdout is a private credential pipe, never a log.
import { chromium } from '../prototypes/VtkJsStreaming/src/client/node_modules/playwright-core/index.mjs';
import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';
import { once } from 'node:events';
import assert from 'node:assert/strict';

const [dotnet, project, server, clientRoot] = process.argv.slice(2);
if (!clientRoot) throw new Error('Usage: node scripts/test-browser-host.mjs DOTNET PROJECT.pma SERVER.dll CLIENT_DIST');
const host = spawn(dotnet, ['portable/tests/BrowserHost.Characterization/bin/Release/net10.0/BrowserHost.Characterization.dll', project, dotnet, server],
  { detached: true, env: { ...process.env, PREPOMAX_RENDER_CLIENT_ROOT: clientRoot }, stdio: ['pipe', 'pipe', 'pipe'] });
const lines = createInterface({ input: host.stdout })[Symbol.asyncIterator]();
// Drain diagnostics without exposing arbitrary child output to test logs.
host.stderr.on('data', () => {});
const deadline = setTimeout(() => { try { process.kill(-host.pid, 'SIGKILL'); } catch {} process.exitCode = 1; }, 90000);
let browser;
try {
  const first = await lines.next();
  assert(!first.done, 'Host failed before readiness');
  const { address, scene } = JSON.parse(first.value);
  const ask = async (source, topLevel, message) => {
    host.stdin.write(JSON.stringify({ source, topLevel, message }) + '\n');
    const reply = await lines.next();
    assert(!reply.done, 'Host exited during bootstrap');
    return JSON.parse(reply.value).bootstrap;
  };
  browser = await chromium.launch({ headless: true, args: ['--use-angle=swiftshader', '--enable-webgl'] });
  const page = await browser.newPage({ viewport: { width: 1200, height: 800 } });
  const applied = [];
  let serial = Promise.resolve();
  await page.exposeBinding('__nativePost', (source, message) => {
    serial = serial.then(async () => {
      const value = JSON.parse(message);
      if (value.type === 'prepomax.scene.applied-v1') { applied.push(value); return; }
      if (value.type !== 'prepomax.client.ready-v1') return;
      const url = source.frame.url();
      const bootstrap = await ask(url, source.frame === page.mainFrame(), message);
      if (bootstrap && source.frame === page.mainFrame() && source.frame.url() === url)
        await source.frame.evaluate(json => window.__deliver(JSON.parse(json)), bootstrap);
    });
    return serial;
  });
  await page.addInitScript(() => {
    const listeners = [];
    window.chrome = { ...window.chrome, webview: {
      addEventListener: (type, callback) => { if (type === 'message') listeners.push(callback); },
      postMessage: message => { void window.__nativePost(message); }
    }};
    window.__deliver = data => listeners.forEach(callback => callback({ data }));
  });
  assert.equal(await ask('http://example.com/?embedded=1', true, '{"type":"prepomax.client.ready-v1"}'), null);
  await page.goto(address);
  await page.waitForFunction(() => {
    try { const d = JSON.parse(document.querySelector('#diagnostics').textContent); return d.renderer.pipelineChunks === 2; }
    catch { return false; }
  }, null, { timeout: 45000 });
  for (let i = 0; i < 100 && !applied.some(a => a.sceneId === scene.SceneId); i++) await page.waitForTimeout(100);
  assert(applied.some(a => a.sceneId === scene.SceneId && a.actorCount === 2 && a.pipelineChunks === 2), 'Missing stable scene acknowledgement');
  assert(await page.locator('canvas').count() > 0);
  await page.setViewportSize({ width: 900, height: 600 });
  await page.waitForTimeout(500);
  const diagnostics = JSON.parse(await page.locator('#diagnostics').textContent());
  assert.equal(diagnostics.renderer.pipelineChunks, 2);
  assert.equal(diagnostics.renderer.pipelinePoints, 16);
  assert.equal(diagnostics.renderer.pipelinePolys, 24);
  const previous = applied.length;
  await page.reload();
  for (let i = 0; i < 450 && applied.length === previous; i++) await page.waitForTimeout(100);
  assert(applied.length > previous && applied.at(-1).sceneId === scene.SceneId, 'Reload did not restore published scene');
  await page.locator('#camera-view').selectOption('isometric');
  await page.locator('#reset-camera').click();
  await page.waitForTimeout(500);
  if (process.env.PREPOMAX_BROWSER_SCREENSHOT) await page.screenshot({ path: process.env.PREPOMAX_BROWSER_SCREENSHOT });
  assert(!page.url().includes('token'));
  console.log('PASS: portable project → private renderer → browser bootstrap → two rendered actors and stable-frame acknowledgement; resize and reload retained scene');
} finally {
  if (browser) await browser.close();
  const exited = once(host, 'exit');
  host.stdin.end();
  if (host.exitCode === null && host.signalCode === null) await exited;
  assert.equal(host.exitCode, 0, 'Host failed during cleanup');
  clearTimeout(deadline);
}

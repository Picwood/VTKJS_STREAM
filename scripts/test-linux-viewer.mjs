import assert from 'node:assert/strict';
import { launchViewer } from './linux-viewer.mjs';
const [dotnet, project, server, clientRoot] = process.argv.slice(2);
const viewer = await launchViewer({ dotnet, project, server, clientRoot,
  headless: process.env.PREPOMAX_VIEWER_HEADED !== '1', software: true });
async function waitForScene(after = 0) {
  for (let i = 0; i < 450; i++) {
    if (viewer.applied.length > after && viewer.applied.at(-1).sceneId === viewer.scene.SceneId) return;
    await viewer.page.waitForTimeout(100);
  }
  throw new Error('Viewer did not acknowledge the published scene.');
}
try {
  await waitForScene();
  assert.equal(viewer.applied.at(-1).actorCount, 2);
  const previous = viewer.applied.length;
  await viewer.page.reload();
  await waitForScene(previous);
  await viewer.page.setViewportSize({ width: 900, height: 600 });
  await viewer.page.locator('#camera-view').selectOption('isometric');
  await viewer.page.locator('#reset-camera').click();
  const fields = await viewer.command({ command: 'fields' });
  assert.equal(fields.ok, true);
  assert(Array.isArray(fields.fields));
  const diagnostics = JSON.parse(await viewer.page.locator('#diagnostics').textContent());
  assert.equal(diagnostics.renderer.pipelinePoints, 16);
  assert.equal(diagnostics.renderer.pipelinePolys, 24);
  const external = await viewer.page.evaluate(async () => {
    try { await fetch('https://example.com/'); return true; } catch { return false; }
  });
  assert.equal(external, false, 'Shell must block external requests');
  if (process.env.PREPOMAX_BROWSER_SCREENSHOT) await viewer.page.screenshot({ path: process.env.PREPOMAX_BROWSER_SCREENSHOT });
  await viewer.page.close();
} finally { await viewer.close(); }
let stopped = false;
try { await fetch(new URL('/healthz', viewer.address)); } catch { stopped = true; }
assert(stopped, 'Closing viewer must stop renderer');
console.log('PASS: runnable Linux viewer, bootstrap, rendered scene, reload, camera/resize, external-request blocking and shutdown');

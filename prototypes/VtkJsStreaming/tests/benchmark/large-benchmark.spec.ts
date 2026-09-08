import { writeFile } from 'node:fs/promises';
import playwrightTest from '../../src/client/node_modules/@playwright/test/index.js';

const { expect, test } = playwrightTest;
const serverUrl = 'http://127.0.0.1:5194';
const headedRequested = process.env.PREPOMAX_BENCHMARK_HEADLESS !== '1';

test('canonical 3M-node functional and quantitative benchmark', async ({ page }, testInfo) => {
  const cdp = await page.context().newCDPSession(page);
  const resourceRequests: Array<{ url: string; bytes: number }> = [];
  let accessToken: string | null = null;
  let manifestBody: Buffer | null = null;
  let manifest: BenchmarkManifest | null = null;
  page.on('response', async (response) => {
    if (response.url().endsWith('/api/v1/sessions') && response.ok()) {
      accessToken = ((await response.json()) as { accessToken: string }).accessToken;
    }
    if (response.url().endsWith('/manifest') && response.ok()) {
      manifestBody = await response.body();
      manifest = JSON.parse(manifestBody.toString()) as BenchmarkManifest;
    }
    if (response.url().includes('/resources/') && response.ok()) {
      resourceRequests.push({
        url: response.url(),
        bytes: Number(response.headers()['content-length'] ?? 0),
      });
    }
  });

  await page.goto('/?benchmark=1&concurrency=3');
  await page.locator('#server-url').fill(serverUrl);
  const browserBaseline = await browserMemory(page, cdp);
  await page.locator('#connect').click();
  await page.waitForFunction(() => {
    try {
      const value = JSON.parse(document.querySelector('#diagnostics')?.textContent ?? '{}') as Diagnostics;
      return value.renderer?.pipelineChunks > 0 && !document.querySelector('#status')?.textContent?.startsWith('Loaded ');
    } catch { return false; }
  }, undefined, { timeout: 60_000 });
  const progressive = await readDiagnostics(page);
  expect(progressive.renderer.pipelineChunks).toBeGreaterThan(0);
  const expectedChunkCountAtProgress = manifest?.actors.reduce((sum, actor) => sum + actor.chunks.length, 0);
  expect(expectedChunkCountAtProgress).toBeDefined();
  expect(progressive.renderer.pipelineChunks).toBeLessThan(expectedChunkCountAtProgress!);

  await expect(page.locator('#status')).toHaveText('Loaded 1 actors.', { timeout: 180_000 });
  const loaded = await readDiagnostics(page);
  const qualifyingHardwareRun = headedRequested
    && loaded.renderer.webglRenderer !== null
    && !/swiftshader|software|llvmpipe/i.test(loaded.renderer.webglRenderer);
  expect(accessToken).not.toBeNull();
  expect(manifest).not.toBeNull();
  const manifestChunks = manifest!.actors.flatMap((actor) => actor.chunks);
  const chunkCount = manifestChunks.length;
  const chunkPointTuples = manifestChunks.reduce((sum, chunk) => sum + chunk.pointCount, 0);
  expect(manifestBody!.byteLength).toBeLessThan(1_048_576);
  expect(manifestBody!.toString()).not.toMatch(/base64|data:application\/octet-stream/i);
  expect(loaded.renderer.pipelineChunks).toBe(chunkCount);
  expect(loaded.renderer.pipelinePoints).toBe(chunkPointTuples);
  expect(loaded.renderer.pipelinePolys).toBe(5_993_002);
  const descriptorBytes = manifest!.resources.reduce((sum, resource) => sum + resource.byteLength, 0);
  expect(loaded.transferredBytes).toBe(descriptorBytes);
  expect(resourceRequests).toHaveLength(manifest!.resources.length);
  expect(Math.max(...resourceRequests.map((request) => request.bytes))).toBeLessThanOrEqual(8 * 1024 * 1024);
  expect(new Set(resourceRequests.map((request) => request.url)).size).toBe(resourceRequests.length);

  const serverBaseline = await serverDiagnostics(page, accessToken!);
  expect(serverBaseline.serverVersion).toBe('0.1.0');
  expect(serverBaseline.resourceRequestCount).toBe(manifest!.resources.length);
  expect(serverBaseline.resourceResponseBytes).toBe(descriptorBytes);
  expect(serverBaseline.canceledRequestCount).toBe(0);
  let serverPeak = serverBaseline;
  const requestsBeforeInteraction = resourceRequests.length;
  await page.getByLabel('Orthographic').check();
  await page.getByLabel('Camera view').selectOption('front');
  await page.getByLabel('Canonical 3M grid', { exact: true }).uncheck();
  await page.getByLabel('Canonical 3M grid', { exact: true }).check();
  expect(resourceRequests).toHaveLength(requestsBeforeInteraction);

  const viewport = page.locator('.viewport-shell');
  const box = await viewport.boundingBox();
  if (!box) throw new Error('Viewport has no bounding box.');
  const pickX = box.x + box.width * 0.5;
  const pickY = box.y + box.height * 0.5;
  const pickSamples: number[] = [];
  for (let sample = 0; sample < 30; sample += 1) {
    const before = (await readDiagnostics(page)).picks.count;
    await page.mouse.click(pickX, pickY);
    await page.waitForFunction((count) => {
      const value = JSON.parse(document.querySelector('#diagnostics')?.textContent ?? '{}') as Diagnostics;
      return value.picks.count > count;
    }, before);
    pickSamples.push((await readDiagnostics(page)).picks.lastMs);
  }
  await expect(page.locator('#pick-inspector')).toContainText('"validated": true');
  const pickDiagnostics = await readDiagnostics(page);
  const pick = JSON.parse(await page.locator('#pick-inspector').textContent() ?? '{}') as { sourceElementId: number; candidateSourceNodeId: number };
  expect(pick.sourceElementId).toBeGreaterThan(0);
  expect(pick.candidateSourceNodeId).toBeGreaterThan(0);

  const orbitSamples = await page.evaluate(async () =>
    await (window as unknown as { __prepomaxBenchmark: { measureOrbit(count: number): Promise<number[]> } })
      .__prepomaxBenchmark.measureOrbit(60));

  const requestsBeforeScalar = resourceRequests.length;
  const fieldBefore = await page.locator('#legend strong').textContent();
  await page.locator('#switch-field').click();
  await expect(page.locator('#status')).toContainText(/Activated canonical-wave(?:-inverted)? without replacing topology\./, { timeout: 120_000 });
  await expect(page.locator('#legend strong')).not.toHaveText(fieldBefore ?? '', { timeout: 120_000 });
  const afterScalar = await readDiagnostics(page);
  const scalarRequests = resourceRequests.slice(requestsBeforeScalar);
  expect(scalarRequests).toHaveLength(chunkCount);
  expect(scalarRequests.reduce((sum, request) => sum + request.bytes, 0)).toBe(chunkPointTuples * 4);
  expect(scalarRequests.every((request) => request.bytes % 4 === 0
    && request.bytes <= Math.max(...manifestChunks.map((chunk) => chunk.pointCount * 4)))).toBe(true);
  serverPeak = maxServer(serverPeak, await serverDiagnostics(page, accessToken!));

  const selectionActivationMs = await page.evaluate(async () =>
    await (window as unknown as { __prepomaxBenchmark: {
      selectCanonicalNodes(count: number): Promise<number> } })
      .__prepomaxBenchmark.selectCanonicalNodes(3_000_000));
  await page.waitForFunction(() => {
    const value = JSON.parse(document.querySelector('#diagnostics')?.textContent ?? '{}') as Diagnostics;
    return value.renderer?.selection?.item === 'node'
      && value.renderer.selection.selectedIdCount === 3_000_000;
  }, undefined, { timeout: 120_000 });
  const selected = await readDiagnostics(page);
  expect(selected.renderer.selection.item).toBe('node');
  expect(selected.renderer.selection.selectedIdCount).toBe(3_000_000);
  expect(selected.renderer.selection.overlayActors).toBe(chunkCount);
  expect(selected.renderer.selection.overlayPointTuples).toBeGreaterThan(0);
  expect(selected.renderer.selection.overlayPointTuples).toBeLessThanOrEqual(500_000);
  expect(selected.renderer.selection.overflow).toBe(true);
  const selectionOrbitSamples = await page.evaluate(async () =>
    await (window as unknown as { __prepomaxBenchmark: { measureOrbit(count: number): Promise<number[]> } })
      .__prepomaxBenchmark.measureOrbit(20));
  expect(selectionOrbitSamples).toHaveLength(20);
  expect(selectionOrbitSamples.every((sample) => Number.isFinite(sample) && sample > 0)).toBe(true);
  const selectionOrbitMedianMs = percentile(selectionOrbitSamples, 0.5);
  if (qualifyingHardwareRun) expect(1000 / selectionOrbitMedianMs).toBeGreaterThanOrEqual(20);
  expect(selectionActivationMs).toBeLessThanOrEqual(2_000);
  await page.evaluate(() =>
    (window as unknown as { __prepomaxBenchmark: { clearSelection(): void } })
      .__prepomaxBenchmark.clearSelection());
  await page.waitForFunction(() => {
    const value = JSON.parse(document.querySelector('#diagnostics')?.textContent ?? '{}') as Diagnostics;
    return value.renderer?.selection?.item === 'none'
      && value.renderer.selection.overlayActors === 0;
  }, undefined, { timeout: 120_000 });
  const clearedSelection = await readDiagnostics(page);
  expect(clearedSelection.renderer.selection.selectedIdCount).toBe(0);
  expect(clearedSelection.renderer.selection.overlayActors).toBe(0);
  expect(clearedSelection.renderer.selection.overlayPointTuples).toBe(0);

  const loadedMemory = await browserMemory(page, cdp);
  const cycleMemory: Array<{ cycle: number; loaded: number | null; unloaded: number | null; actors: number; chunks: number }> = [];
  for (let cycle = 1; cycle <= 5; cycle += 1) {
    if (cycle > 1) {
      await page.locator('#connect').click();
      await expect(page.locator('#status')).toHaveText('Loaded 1 actors.', { timeout: 180_000 });
    }
    const cycleLoaded = await browserMemory(page, cdp);
    await page.locator('#unload').click();
    await expect(page.locator('#status')).toHaveText('Scene unloaded.');
    await page.waitForTimeout(250);
    const cycleUnloaded = await browserMemory(page, cdp);
    const unloadedDiagnostics = await readDiagnostics(page);
    cycleMemory.push({
      cycle,
      loaded: cycleLoaded?.trackedBytes ?? null,
      unloaded: cycleUnloaded?.trackedBytes ?? null,
      actors: unloadedDiagnostics.renderer.rendererActors,
      chunks: unloadedDiagnostics.renderer.pipelineChunks,
    });
    expect(unloadedDiagnostics.renderer.rendererActors).toBe(0);
    expect(unloadedDiagnostics.renderer.pipelineChunks).toBe(0);
    serverPeak = maxServer(serverPeak, await serverDiagnostics(page, accessToken!));
  }

  const lastUnloaded = cycleMemory.at(-1)?.unloaded ?? null;
  const retainedRatio = browserBaseline && loadedMemory && lastUnloaded !== null
    ? Math.max(0, lastUnloaded - browserBaseline.trackedBytes)
      / Math.max(1, loadedMemory.trackedBytes - browserBaseline.trackedBytes)
    : null;
  const orbitMedianMs = percentile(orbitSamples, 0.5);
  const measurements = {
    runKind: qualifyingHardwareRun ? 'headed-physical-candidate' : 'headless-functional',
    qualifyingHardwareRun,
    capturedUtc: new Date().toISOString(),
    userAgent: await page.evaluate(() => navigator.userAgent),
    viewport: { width: 1920, height: 1080, deviceScaleFactor: 1 },
    configuration: { chunkCellRows: Number(process.env.PREPOMAX_RENDER_CHUNK_CELL_ROWS ?? 50), resourceConcurrency: 3, resourceCapBytes: 8 * 1024 * 1024 },
    canonical: { uniqueNodes: 3_000_000, logicalQuads: 2_996_501, triangles: 5_993_002, chunkPointTuples },
    network: { manifestBytes: manifestBody!.byteLength, initialResourceRequests: manifest!.resources.length, initialBytes: descriptorBytes, scalarRequests: chunkCount, scalarBytes: chunkPointTuples * 4 },
    timing: loaded.timing,
    picks: { ...pickDiagnostics.picks, samples: pickSamples },
    orbit: { samples: orbitSamples, medianMs: orbitMedianMs, medianFps: 1000 / orbitMedianMs },
    multiMillionSelection: {
      selectedIds: selected.renderer.selection.selectedIdCount,
      matchingPointTuples: loaded.renderer.pipelinePoints,
      overlayPointTuples: selected.renderer.selection.overlayPointTuples,
      overlayActors: selected.renderer.selection.overlayActors,
      sampled: selected.renderer.selection.overflow,
      activationMs: selectionActivationMs,
      orbitSamples: selectionOrbitSamples,
      medianOrbitMs: selectionOrbitMedianMs,
    },
    browserMemory: { baseline: browserBaseline, loaded: loadedMemory, cycles: cycleMemory, retainedRatio },
    gpu: {
      ...loaded.renderer.graphics,
      vendor: loaded.renderer.webglVendor,
      renderer: loaded.renderer.webglRenderer,
    },
    backend: { baseline: serverBaseline, peak: serverPeak, managedPeakAboveBaseline: serverPeak.managedHeapBytes - serverBaseline.managedHeapBytes },
    observability: {
      serverVersion: serverBaseline.serverVersion,
      initialRequestCount: serverBaseline.requestCount,
      initialResourceEvents: serverBaseline.resourceRequestCount,
      initialResourceBytes: serverBaseline.resourceResponseBytes,
      canceledRequests: serverPeak.canceledRequestCount,
    },
    gates: gateResults(qualifyingHardwareRun, loaded, pickDiagnostics, afterScalar, orbitMedianMs,
      selected, selectionActivationMs, selectionOrbitMedianMs,
      browserBaseline, loadedMemory, retainedRatio, serverBaseline, serverPeak),
  };
  const output = JSON.stringify(measurements, null, 2);
  console.log(`WP5_RESULT_BEGIN\n${output}\nWP5_RESULT_END`);
  await testInfo.attach('wp5-measurements.json', { body: Buffer.from(output), contentType: 'application/json' });
  const evidencePath = process.env.PREPOMAX_BENCHMARK_EVIDENCE_PATH;
  if (evidencePath) {
    await writeFile(evidencePath, `${output}\n`, 'utf8');
  }
});

interface BenchmarkManifest {
  resources: Array<{ resourceId: string; semantic: string; byteLength: number }>;
  actors: Array<{ chunks: Array<{ pointCount: number; triangleCount: number }> }>;
}

interface BrowserMemory {
  usedJSHeapSize: number;
  totalJSHeapSize: number;
  jsHeapSizeLimit: number;
  v8UsedBytes: number;
  backingStorageBytes: number;
  embedderHeapBytes: number;
  trackedBytes: number;
}
interface ServerDiagnostics {
  serverVersion: string;
  requestCount: number;
  resourceRequestCount: number;
  resourceResponseBytes: number;
  canceledRequestCount: number;
  workingSetBytes: number;
  peakWorkingSetBytes: number;
  managedHeapBytes: number;
  [key: string]: unknown;
}
interface Diagnostics {
  transferredBytes: number;
  scalarUpdateBytes: number;
  timing: { manifestMs: number; firstVisibleChunkMs: number; completeLoadMs: number; longestTaskMs: number; scalarActivationMs: number };
  picks: { count: number; lastMs: number; p50Ms: number; p95Ms: number };
  renderer: {
    rendererActors: number;
    pipelineChunks: number;
    pipelinePoints: number;
    pipelinePolys: number;
    webglVendor: string | null;
    webglRenderer: string | null;
    graphics: { gpuMemoryMB: number };
    selection: {
      item: string;
      selectedIdCount: number;
      overlayActors: number;
      overlayPointTuples: number;
      overflow: boolean;
    };
  };
}

async function readDiagnostics(page: import('../../src/client/node_modules/@playwright/test/index.js').Page): Promise<Diagnostics> {
  return JSON.parse(await page.locator('#diagnostics').textContent() ?? '{}') as Diagnostics;
}

async function browserMemory(
  page: import('../../src/client/node_modules/@playwright/test/index.js').Page,
  cdp: import('../../src/client/node_modules/@playwright/test/index.js').CDPSession,
): Promise<BrowserMemory | null> {
  await cdp.send('HeapProfiler.collectGarbage');
  const heap = await cdp.send('Runtime.getHeapUsage') as {
    usedSize: number;
    embedderHeapUsedSize: number;
    backingStorageSize: number;
  };
  const legacy = await page.evaluate(() => {
    const memory = (performance as Performance & { memory?: BrowserMemory }).memory;
    return memory ? {
      usedJSHeapSize: memory.usedJSHeapSize,
      totalJSHeapSize: memory.totalJSHeapSize,
      jsHeapSizeLimit: memory.jsHeapSizeLimit,
    } : null;
  });
  return legacy ? {
    ...legacy,
    v8UsedBytes: heap.usedSize,
    backingStorageBytes: heap.backingStorageSize,
    embedderHeapBytes: heap.embedderHeapUsedSize,
    trackedBytes: heap.usedSize + heap.backingStorageSize,
  } : null;
}

async function serverDiagnostics(
  page: import('../../src/client/node_modules/@playwright/test/index.js').Page,
  token: string,
): Promise<ServerDiagnostics> {
  const response = await page.request.get(`${serverUrl}/api/v1/diagnostics`, { headers: { 'X-PrePoMax-Session': token } });
  expect(response.ok()).toBe(true);
  return await response.json() as ServerDiagnostics;
}

function maxServer(left: ServerDiagnostics, right: ServerDiagnostics): ServerDiagnostics {
  return {
    ...right,
    workingSetBytes: Math.max(left.workingSetBytes, right.workingSetBytes),
    peakWorkingSetBytes: Math.max(left.peakWorkingSetBytes, right.peakWorkingSetBytes),
    managedHeapBytes: Math.max(left.managedHeapBytes, right.managedHeapBytes),
  };
}

function percentile(values: readonly number[], fraction: number): number {
  const sorted = [...values].sort((left, right) => left - right);
  return sorted[Math.max(0, Math.ceil(sorted.length * fraction) - 1)]!;
}

function gateResults(
  qualifyingHardwareRun: boolean,
  loaded: Diagnostics,
  pickDiagnostics: Diagnostics,
  afterScalar: Diagnostics,
  orbitMedianMs: number,
  selected: Diagnostics,
  selectionActivationMs: number,
  selectionOrbitMedianMs: number,
  baselineMemory: BrowserMemory | null,
  loadedMemory: BrowserMemory | null,
  retainedRatio: number | null,
  serverBaseline: ServerDiagnostics,
  serverPeak: ServerDiagnostics,
): Record<string, { status: 'pass' | 'fail'; value: number | null; target: string; note?: string }> {
  const hardwareNote = qualifyingHardwareRun ? undefined : 'Headless run is non-qualifying; physical headed rerun required.';
  const gate = (pass: boolean, value: number | null, target: string, note?: string) => ({ status: pass ? 'pass' as const : 'fail' as const, value, target, ...(note ? { note } : {}) });
  const browserPeak = loadedMemory && baselineMemory ? loadedMemory.trackedBytes - baselineMemory.trackedBytes : null;
  const backendPeak = serverPeak.managedHeapBytes - serverBaseline.managedHeapBytes;
  return {
    serverReadyToManifest: gate(loaded.timing.manifestMs <= 2000, loaded.timing.manifestMs, '<= 2000 ms'),
    manifestToFirstChunk: gate(loaded.timing.firstVisibleChunkMs <= 5000, loaded.timing.firstVisibleChunkMs, '<= 5000 ms'),
    completeLoad: gate(loaded.timing.completeLoadMs <= 15_000, loaded.timing.completeLoadMs, '<= 15000 ms'),
    longestTask: gate(loaded.timing.longestTaskMs <= 200, loaded.timing.longestTaskMs, '<= 200 ms'),
    orbitFps: gate(qualifyingHardwareRun && 1000 / orbitMedianMs >= 30, 1000 / orbitMedianMs, '>= 30 FPS', hardwareNote),
    pickP95: gate(qualifyingHardwareRun && pickDiagnostics.picks.p95Ms <= 100, pickDiagnostics.picks.p95Ms, '<= 100 ms', hardwareNote),
    scalarActivation: gate(afterScalar.timing.scalarActivationMs <= 500, afterScalar.timing.scalarActivationMs, '<= 500 ms'),
    multiMillionSelectedIds: gate(selected.renderer.selection.selectedIdCount === 3_000_000,
      selected.renderer.selection.selectedIdCount, '= 3000000'),
    multiMillionOverlayPoints: gate(selected.renderer.selection.overlayPointTuples > 0
      && selected.renderer.selection.overlayPointTuples <= 500_000,
    selected.renderer.selection.overlayPointTuples, '1..500000'),
    multiMillionSelectionActivation: gate(selectionActivationMs <= 2_000, selectionActivationMs, '<= 2000 ms'),
    multiMillionSelectionOrbit: gate(qualifyingHardwareRun && 1000 / selectionOrbitMedianMs >= 20,
      1000 / selectionOrbitMedianMs, '>= 20 FPS', hardwareNote),
    browserPeak: gate(browserPeak !== null && browserPeak <= 700 * 1024 * 1024, browserPeak, '<= 700 MiB'),
    gpuMemory: gate(qualifyingHardwareRun && loaded.renderer.graphics.gpuMemoryMB <= 700, loaded.renderer.graphics.gpuMemoryMB, '<= 700 MB', hardwareNote),
    backendManagedPeak: gate(backendPeak <= 400 * 1024 * 1024, backendPeak, '<= 400 MiB'),
    retainedAfterFiveUnloads: gate(retainedRatio !== null && retainedRatio <= 0.10, retainedRatio, '<= 10%'),
  };
}

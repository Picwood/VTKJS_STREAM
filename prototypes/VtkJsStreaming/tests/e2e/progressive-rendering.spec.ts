import playwrightTest from '../../src/client/node_modules/@playwright/test/index.js';

const { expect, test } = playwrightTest;

test('renders the first chunk while the second is delayed and visibility does not refetch topology', async ({ page }, testInfo) => {
  await page.addInitScript(() => {
    const target = window as unknown as {
      chrome?: { webview?: {
        postMessage(value: string): void;
        addEventListener(type: string, listener: (event: {
          data?: unknown; additionalData?: unknown; getBuffer?(): ArrayBuffer;
        }) => void): void;
      } };
      __prepomaxHostMessages: unknown[];
      __sendPrePoMaxHostMessage(value: unknown): void;
      __sendPrePoMaxSharedBuffer(value: ArrayBuffer, metadata: unknown): void;
    };
    target.__prepomaxHostMessages = [];
    const listeners = new Map<string, (event: {
      data?: unknown; additionalData?: unknown; getBuffer?(): ArrayBuffer;
    }) => void>();
    const chrome = target.chrome ?? {};
    chrome.webview = {
      postMessage(value: string) {
        target.__prepomaxHostMessages.push(JSON.parse(value));
      },
      addEventListener(type: string, listener: (event: {
        data?: unknown; additionalData?: unknown; getBuffer?(): ArrayBuffer;
      }) => void) {
        listeners.set(type, listener);
      },
    };
    target.__sendPrePoMaxHostMessage = (value: unknown) => listeners.get('message')?.({ data: value });
    target.__sendPrePoMaxSharedBuffer = (value: ArrayBuffer, metadata: unknown) =>
      listeners.get('sharedbufferreceived')?.({ additionalData: metadata, getBuffer: () => value });
    target.chrome = chrome;
  });
  let resourceRequests = 0;
  const resourceUrls: string[] = [];
  const nonLoopbackRequests: string[] = [];
  page.on('request', (request) => {
    const requestUrl = new URL(request.url());
    if ((requestUrl.protocol === 'http:' || requestUrl.protocol === 'ws:')
      && requestUrl.hostname !== '127.0.0.1') {
      nonLoopbackRequests.push(request.url());
    }
    if (request.url().includes('/resources/')) {
      resourceRequests += 1;
      resourceUrls.push(request.url());
    }
  });

  await page.goto('/?autoload=1&testHooks=1');
  await page.waitForFunction(() => {
    const text = document.querySelector('#diagnostics')?.textContent;
    try {
      return text ? (JSON.parse(text) as Diagnostics).renderer.pipelineChunks === 1 : false;
    } catch {
      return false;
    }
  });
  const progressiveDiagnostics = await readDiagnostics(page);
  expect(progressiveDiagnostics.renderer.pipelineChunks).toBe(1);
  expect(progressiveDiagnostics.renderer.pipelinePolys).toBe(1);
  expect(await page.locator('#status').textContent()).not.toContain('Loaded 2 actors');
  await testInfo.attach('first-progressive-chunk', {
    body: await page.locator('.viewport-shell').screenshot(),
    contentType: 'image/png',
  });

  await expect(page.locator('#status')).toHaveText('Loaded 2 actors.');
  await page.waitForFunction(() => {
    const text = document.querySelector('#diagnostics')?.textContent;
    try {
      return text ? (JSON.parse(text) as Diagnostics).renderer.pipelineChunks === 2 : false;
    } catch {
      return false;
    }
  });
  const completeDiagnostics = await readDiagnostics(page);
  const completeDiagnosticsText = await page.locator('#diagnostics').textContent() ?? '';
  expect(completeDiagnosticsText).not.toContain('playwright-host-token');
  expect(completeDiagnosticsText).not.toMatch(/(?:[A-Za-z]:\\|file:\/\/|\/mnt\/)/);
  expect(completeDiagnostics).toMatchObject({
    protocolVersion: '1.0',
    clientVersion: '0.1.0',
    serverVersion: '0.1.0',
    counts: { renderPointTuples: 6, renderTriangles: 2, sourcePointIdTuples: 6, sourceElementIdTuples: 2 },
    bytes: { planned: 152, transferred: 152, decoded: 152 },
    recovery: { resourceRetries: 0, resourceCancellations: 0, contextLossCount: 0, lastSafeErrorCode: null },
  });
  expect(completeDiagnostics.sessionId).toMatch(/^[0-9a-f-]{36}$/);
  expect(completeDiagnostics.renderer.webglLimits?.maxTextureSize).toBeGreaterThan(0);
  expect(completeDiagnostics.transferredBytes).toBe(152);
  expect(completeDiagnostics.renderer).toMatchObject({
    webgl2Available: true,
    rendererActors: 2,
    pipelineChunks: 2,
    pipelinePoints: 6,
    pipelinePolys: 2,
  });
  await expect.poll(async () => page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string; sceneId?: string; sceneRevision?: number;
      actorCount?: number; pipelineChunks?: number }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.scene.applied-v1').at(-1)))
    .toMatchObject({ sceneId: completeDiagnostics.sceneId, sceneRevision: 1, actorCount: 2, pipelineChunks: 2 });
  expect(resourceRequests).toBe(10);
  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({
    type: 'prepomax.viewport-appearance.updated-v1', gradient: true,
    topRgb: [12 / 255, 34 / 255, 56 / 255], bottomRgb: [78 / 255, 90 / 255, 123 / 255],
    ambient: 0.35, diffuse: 0.65, redraw: true,
  }));
  await expect.poll(async () => (await readDiagnostics(page)).renderer.viewportAppearance).toMatchObject({
    gradient: true, ambient: 0.35, diffuse: 0.65,
    actorLighting: [{ ambient: 0.35, diffuse: 0.65 }, { ambient: 0.35, diffuse: 0.65 }],
  });
  await expect.poll(async () => page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string; position?: number[]; focalPoint?: number[];
      viewUp?: number[]; parallelScale?: number }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.camera.changed-v1').at(-1)))
    .toMatchObject({ type: 'prepomax.camera.changed-v1' });

  const cameraBeforeBoxZoom = await page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string; focalPoint?: number[]; parallelScale?: number }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.camera.changed-v1').at(-1));
  const areaMessageCountBeforeBoxZoom = await page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.area.validated-v1').length);
  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({ type: 'prepomax.box-zoom.mode-v1', enabled: true }));
  const zoomViewport = page.locator('#viewport');
  await expect(zoomViewport).toHaveAttribute('data-box-zoom', 'true');
  const zoomBounds = await zoomViewport.boundingBox();
  if (!zoomBounds) throw new Error('Viewport has no bounding box for box zoom.');
  await page.mouse.move(zoomBounds.x + zoomBounds.width * 0.2, zoomBounds.y + zoomBounds.height * 0.2);
  await page.mouse.down();
  await page.mouse.move(zoomBounds.x + zoomBounds.width * 0.6, zoomBounds.y + zoomBounds.height * 0.6,
    { steps: 4 });
  await expect(page.locator('#selection-rectangle')).toBeVisible();
  await page.mouse.up();
  await expect(page.locator('#selection-rectangle')).toBeHidden();
  await expect(zoomViewport).toHaveAttribute('data-box-zoom', 'false');
  await expect.poll(async () => page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string; applied?: boolean }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.box-zoom.ended-v1').at(-1)))
    .toMatchObject({ type: 'prepomax.box-zoom.ended-v1', applied: true });
  const cameraAfterBoxZoom = await page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string; focalPoint?: number[]; parallelScale?: number }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.camera.changed-v1').at(-1));
  const boxZoomCameraMessages = await page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string; focalPoint?: number[]; parallelScale?: number }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.camera.changed-v1'));
  expect(cameraAfterBoxZoom?.parallelScale, JSON.stringify(boxZoomCameraMessages))
    .toBeLessThan(cameraBeforeBoxZoom?.parallelScale ?? 0);
  expect(cameraAfterBoxZoom?.focalPoint).not.toEqual(cameraBeforeBoxZoom?.focalPoint);
  expect(await page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.area.validated-v1').length))
    .toBe(areaMessageCountBeforeBoxZoom);

  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({ type: 'prepomax.box-zoom.mode-v1', enabled: true }));
  await page.keyboard.press('Escape');
  await expect.poll(async () => page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string; applied?: boolean }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.box-zoom.ended-v1').at(-1)))
    .toMatchObject({ type: 'prepomax.box-zoom.ended-v1', applied: false });
  await expect(zoomViewport).toHaveAttribute('data-box-zoom', 'false');
  await page.locator('#reset-camera').click();

  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({
    type: 'prepomax.sphere-symbols.updated-v1',
    symbols: [{ name: 'Gravity:sphere',
      points: [[1_234_567.3, 2_345_678.3, 3_456_789.25], [1_234_567.6, 2_345_678.4, 3_456_789.25]],
      rgba: [0.2, 0.4, 0.8, 1], symbolSize: 24 }],
  }));
  await expect.poll(async () => (await readDiagnostics(page)).renderer.sphereSymbols).toMatchObject({
    actors: 1, points: 2,
  });
  const sphereState = await readDiagnostics(page);
  expect(sphereState.renderer.sphereSymbols.scales).toHaveLength(2);
  expect(sphereState.renderer.sphereSymbols.scales.every((scale) => scale > 0)).toBe(true);
  const initialSphereScale = sphereState.renderer.sphereSymbols.scales[0]!;
  await page.evaluate((parallelScale) => (window as unknown as {
    __prepomaxBenchmark: { setParallelScale(scale: number): void };
  }).__prepomaxBenchmark.setParallelScale(parallelScale), sphereState.renderer.cameraParallelScale / 2);
  await expect.poll(async () => (await readDiagnostics(page)).renderer.cameraParallelScale)
    .not.toBe(sphereState.renderer.cameraParallelScale);
  const zoomedSphereState = await readDiagnostics(page);
  expect(zoomedSphereState.renderer.sphereSymbols.scales[0]! / initialSphereScale)
    .toBeCloseTo(zoomedSphereState.renderer.cameraParallelScale / sphereState.renderer.cameraParallelScale, 5);
  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({ type: 'prepomax.sphere-symbols.updated-v1', symbols: [] }));
  await expect.poll(async () => (await readDiagnostics(page)).renderer.sphereSymbols)
    .toEqual({ actors: 0, points: 0, scales: [] });
  await page.locator('#reset-camera').click();

  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({
    type: 'prepomax.coordinate-overlays.updated-v1',
    axes: [{ name: 'Fixture:X:axis', point: [1_234_567.3, 2_345_678.3, 3_456_789.25],
      direction: [1, 0, 0], rgba: [1, 0, 0, 1], symbolSize: 24 }],
    captions: [{ name: 'Fixture:X', text: '<X & axis>', position: [1_234_567.3, 2_345_678.3, 3_456_789.25],
      offsetVector: [28, 0, 0], rgba: [1, 0, 0, 1], fontScaleFactor: 1 }],
  }));
  await expect.poll(async () => (await readDiagnostics(page)).renderer.coordinateAxes.actors).toBe(1);
  await expect.poll(async () => (await readDiagnostics(page)).renderer.captions)
    .toEqual({ count: 1, visible: 1, texts: ['<X & axis>'] });
  await expect(page.locator('.coordinate-caption[title="Fixture:X"]')).toHaveText('<X & axis>');
  await expect(page.locator('.coordinate-caption[title="Fixture:X"] > *')).toHaveCount(0);
  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({
    type: 'prepomax.annotations.updated-v1',
    annotations: [{ name: 'overlay-composition', text: 'Annotation beside caption',
      anchorPoint: [1_234_567.3, 2_345_678.3, 3_456_789.25], offset: [0, 0],
      drawBackground: false, drawBorder: false, visible: true }],
  }));
  await expect(page.locator('.annotation-callout[title="overlay-composition"]')).toBeVisible();
  await expect(page.locator('.coordinate-caption[title="Fixture:X"]')).toHaveText('<X & axis>');
  const overlayState = await readDiagnostics(page);
  const initialAxisScale = overlayState.renderer.coordinateAxes.scales[0]!;
  expect(initialAxisScale).toBeGreaterThan(0);
  await page.evaluate((parallelScale) => (window as unknown as {
    __prepomaxBenchmark: { setParallelScale(scale: number): void };
  }).__prepomaxBenchmark.setParallelScale(parallelScale), overlayState.renderer.cameraParallelScale / 2);
  await expect.poll(async () => (await readDiagnostics(page)).renderer.cameraParallelScale)
    .not.toBe(overlayState.renderer.cameraParallelScale);
  const zoomedOverlayState = await readDiagnostics(page);
  expect(zoomedOverlayState.renderer.coordinateAxes.scales[0]! / initialAxisScale)
    .toBeCloseTo(zoomedOverlayState.renderer.cameraParallelScale / overlayState.renderer.cameraParallelScale, 5);
  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({ type: 'prepomax.coordinate-overlays.updated-v1', axes: [], captions: [] }));
  await expect.poll(async () => (await readDiagnostics(page)).renderer.coordinateAxes)
    .toEqual({ actors: 0, scales: [] });
  await expect.poll(async () => (await readDiagnostics(page)).renderer.captions)
    .toEqual({ count: 0, visible: 0, texts: [] });
  await page.locator('#reset-camera').click();

  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({
    type: 'prepomax.directional-glyphs.updated-v1',
    glyphs: [{ name: 'Fixture constraint:cones', kind: 'displacement-cone',
      points: [[1_234_567.3, 2_345_678.3, 3_456_789.25], [1_234_567.6, 2_345_678.3, 3_456_789.25]],
      directions: [[1, 0, 0], [0, 1, 0]], rgba: [0.9, 0.2, 0.1, 1], symbolSize: 24 },
    { name: 'Fixture rotation:cylinder', kind: 'rotational-constraint',
      points: [[1_234_567.3, 2_345_678.6, 3_456_789.25], [1_234_567.6, 2_345_678.6, 3_456_789.25]],
      directions: [[0, 0, 1], [0, 1, 0]], rgba: [0.4, 0.2, 0.8, 1], symbolSize: 24 },
    { name: 'Fixture load:arrow', kind: 'oriented-arrow',
      points: [[1_234_567.3, 2_345_678.9, 3_456_789.25], [1_234_567.6, 2_345_678.9, 3_456_789.25]],
      directions: [[1, 0, 0], [0, 1, 0]], rgba: [0.1, 0.4, 0.9, 1], symbolSize: 24,
      invert: false, relativeSize: 1.5 },
    { name: 'Fixture inverted load:arrow', kind: 'oriented-arrow',
      points: [[1_234_567.3, 2_345_679.2, 3_456_789.25], [1_234_567.6, 2_345_679.2, 3_456_789.25]],
      directions: [[1, 0, 0], [0, 1, 0]], rgba: [0.1, 0.7, 0.4, 1], symbolSize: 24,
      invert: true, relativeSize: 2.5 },
    { name: 'Fixture moment:doubleArrow', kind: 'double-arrow',
      points: [[1_234_567.3, 2_345_679.5, 3_456_789.25], [1_234_567.6, 2_345_679.5, 3_456_789.25]],
      directions: [[0, 0, 1], [0, 1, 0]], rgba: [0.8, 0.5, 0.1, 1], symbolSize: 24 },
    { name: 'Fixture spring:spring', kind: 'oriented-spring',
      points: [[1_234_567.3, 2_345_679.8, 3_456_789.25], [1_234_567.6, 2_345_679.8, 3_456_789.25]],
      directions: [[1, 0, 0], [0, 1, 0]], rgba: [0.2, 0.7, 0.2, 1], symbolSize: 24, invert: false },
    { name: 'Fixture inverted spring:spring', kind: 'oriented-spring',
      points: [[1_234_567.3, 2_345_680.1, 3_456_789.25], [1_234_567.6, 2_345_680.1, 3_456_789.25]],
      directions: [[1, 0, 0], [0, 1, 0]], rgba: [0.2, 0.5, 0.7, 1], symbolSize: 24, invert: true },
    { name: 'Fixture line arrow:cones', kind: 'general-cone',
      points: [[1_234_567.3, 2_345_680.4, 3_456_789.25], [1_234_567.6, 2_345_680.4, 3_456_789.25]],
      directions: [[-1, 0, 0], [0, 1, 0]], rgba: [0.9, 0.1, 0.2, 1], symbolSize: 24 },
    { name: 'Fixture thermal:thermo', kind: 'oriented-thermal',
      points: [[1_234_567.3, 2_345_680.7, 3_456_789.25], [1_234_567.6, 2_345_680.7, 3_456_789.25]],
      directions: [[1, 0, 0], [0, 1, 0]], rgba: [0.8, 0.1, 0.1, 1], symbolSize: 24, invert: false },
    { name: 'Fixture inverted thermal:thermo', kind: 'oriented-thermal',
      points: [[1_234_567.3, 2_345_681, 3_456_789.25], [1_234_567.6, 2_345_681, 3_456_789.25]],
      directions: [[1, 0, 0], [0, 1, 0]], rgba: [0.8, 0.3, 0.1, 1], symbolSize: 24, invert: true },
    { name: 'Fixture centered flux:flux', kind: 'oriented-flux',
      points: [[1_234_567.3, 2_345_681.3, 3_456_789.25], [1_234_567.6, 2_345_681.3, 3_456_789.25]],
      directions: [[1, 0, 0], [0, 1, 0]], rgba: [0.2, 0.5, 0.9, 1], symbolSize: 24,
      center: true, invert: false },
    { name: 'Fixture inverted surface flux:flux', kind: 'oriented-flux',
      points: [[1_234_567.3, 2_345_681.6, 3_456_789.25], [1_234_567.6, 2_345_681.6, 3_456_789.25]],
      directions: [[1, 0, 0], [0, 1, 0]], rgba: [0.2, 0.8, 0.7, 1], symbolSize: 24,
      center: false, invert: true }],
  }));
  await expect.poll(async () => (await readDiagnostics(page)).renderer.directionalGlyphs)
    .toMatchObject({ actors: 12, points: 24, sourcePoints: 33, sourcePolys: 62,
      generalConeSourcePoints: 13, generalConeSourcePolys: 22,
      rotationalSourcePoints: 26, rotationalSourcePolys: 12, rotationalSourceLines: 1,
      springSourcePoints: 950, springSourcePolys: 1856 });
  const directionalState = await readDiagnostics(page);
  expect(directionalState.renderer.directionalGlyphs.scales).toHaveLength(24);
  expect(directionalState.renderer.directionalGlyphs.scales.every((scale) => scale > 0)).toBe(true);
  const initialDirectionalScale = directionalState.renderer.directionalGlyphs.scales[0]!;
  expect(directionalState.renderer.directionalGlyphs.scales[4]! / initialDirectionalScale).toBeCloseTo(5, 5);
  expect(directionalState.renderer.directionalGlyphs.scales[6]! / initialDirectionalScale).toBeCloseTo(2.5 / 0.3, 5);
  expect(directionalState.renderer.directionalGlyphs.scales[8]! / initialDirectionalScale).toBeCloseTo(1 / 0.3, 5);
  expect(directionalState.renderer.directionalGlyphs.scales[10]! / initialDirectionalScale).toBeCloseTo(1 / 0.3, 5);
  expect(directionalState.renderer.directionalGlyphs.scales[12])
    .toBeCloseTo(directionalState.renderer.directionalGlyphs.scales[10]!, 5);
  expect(directionalState.renderer.directionalGlyphs.springSourceBounds[0]).toBeCloseTo(-1.05, 5);
  expect(directionalState.renderer.directionalGlyphs.springSourceBounds[1]).toBeCloseTo(-0.05, 5);
  expect(directionalState.renderer.directionalGlyphs.springSourceBounds[2]).toBeCloseTo(-0.1267371, 5);
  expect(directionalState.renderer.directionalGlyphs.springSourceBounds[3]).toBeCloseTo(0.1267371, 5);
  expect(directionalState.renderer.directionalGlyphs.scales[14])
    .toBeCloseTo(directionalState.renderer.directionalGlyphs.scales[0]!, 5);
  const generalConeBounds = directionalState.renderer.directionalGlyphs.generalConeSourceBounds;
  expect(generalConeBounds[0]).toBeCloseTo(-1, 6);
  expect(generalConeBounds[1]).toBeCloseTo(0, 6);
  expect(generalConeBounds[2]).toBeCloseTo(-0.33582255, 5);
  expect(generalConeBounds[3]).toBeCloseTo(0.35, 5);
  expect(generalConeBounds[4]).toBeCloseTo(-0.34643751, 5);
  expect(generalConeBounds[5]).toBeCloseTo(0.34643751, 5);
  expect(directionalState.renderer.directionalGlyphs.scales[16]! / initialDirectionalScale)
    .toBeCloseTo(0.5 / 0.3, 5);
  expect(directionalState.renderer.directionalGlyphs.scales[22])
    .toBeCloseTo(directionalState.renderer.directionalGlyphs.scales[16]!, 5);
  expect(directionalState.renderer.directionalGlyphs.thermalSources).toHaveLength(4);
  expect(directionalState.renderer.directionalGlyphs.thermalSources[0])
    .toMatchObject({ kind: 'oriented-thermal', invert: false, points: 129, polys: 222 });
  expect(directionalState.renderer.directionalGlyphs.thermalSources[0]!.bounds[0]).toBeCloseTo(0.0581014, 5);
  expect(directionalState.renderer.directionalGlyphs.thermalSources[0]!.bounds[1]).toBeCloseTo(1.15, 5);
  expect(directionalState.renderer.directionalGlyphs.thermalSources[1])
    .toMatchObject({ kind: 'oriented-thermal', invert: true, points: 129, polys: 222 });
  expect(directionalState.renderer.directionalGlyphs.thermalSources[1]!.bounds[0]).toBeCloseTo(-1.0418986, 5);
  expect(directionalState.renderer.directionalGlyphs.thermalSources[1]!.bounds[1]).toBeCloseTo(0.05, 5);
  expect(directionalState.renderer.directionalGlyphs.thermalSources[2])
    .toMatchObject({ kind: 'oriented-flux', center: true, invert: false, points: 79, polys: 154 });
  expect(directionalState.renderer.directionalGlyphs.thermalSources[2]!.bounds[0]).toBeCloseTo(-0.1418986, 5);
  expect(directionalState.renderer.directionalGlyphs.thermalSources[2]!.bounds[1]).toBeCloseTo(0.25, 5);
  expect(directionalState.renderer.directionalGlyphs.thermalSources[3])
    .toMatchObject({ kind: 'oriented-flux', center: false, invert: true, points: 79, polys: 154 });
  expect(directionalState.renderer.directionalGlyphs.thermalSources[3]!.bounds[0]).toBeCloseTo(-1.0418986, 5);
  expect(directionalState.renderer.directionalGlyphs.thermalSources[3]!.bounds[1]).toBeCloseTo(-0.65, 5);
  expect(directionalState.renderer.directionalGlyphs.arrowSources).toHaveLength(3);
  expect(directionalState.renderer.directionalGlyphs.arrowSources[0]?.bounds[0]).toBeCloseTo(0.05, 5);
  expect(directionalState.renderer.directionalGlyphs.arrowSources[0]?.bounds[1]).toBeCloseTo(1.05, 5);
  expect(directionalState.renderer.directionalGlyphs.arrowSources[1]?.bounds[0]).toBeCloseTo(-1.05, 5);
  expect(directionalState.renderer.directionalGlyphs.arrowSources[1]?.bounds[1]).toBeCloseTo(-0.05, 5);
  expect(directionalState.renderer.directionalGlyphs.arrowSources[2]?.kind).toBe('double-arrow');
  expect(directionalState.renderer.directionalGlyphs.arrowSources[2]?.bounds[0]).toBeCloseTo(0.05, 5);
  expect(directionalState.renderer.directionalGlyphs.arrowSources[2]?.bounds[1]).toBeCloseTo(1.05, 5);
  expect(directionalState.renderer.directionalGlyphs.arrowSources[2]?.polys)
    .toBeGreaterThan(directionalState.renderer.directionalGlyphs.arrowSources[0]?.polys ?? 0);
  await page.evaluate((parallelScale) => (window as unknown as {
    __prepomaxBenchmark: { setParallelScale(scale: number): void };
  }).__prepomaxBenchmark.setParallelScale(parallelScale), directionalState.renderer.cameraParallelScale / 2);
  await expect.poll(async () => (await readDiagnostics(page)).renderer.cameraParallelScale)
    .not.toBe(directionalState.renderer.cameraParallelScale);
  const zoomedDirectionalState = await readDiagnostics(page);
  expect(zoomedDirectionalState.renderer.directionalGlyphs.scales[0]! / initialDirectionalScale)
    .toBeCloseTo(zoomedDirectionalState.renderer.cameraParallelScale
      / directionalState.renderer.cameraParallelScale, 5);
  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({ type: 'prepomax.directional-glyphs.updated-v1', glyphs: [] }));
  await expect.poll(async () => (await readDiagnostics(page)).renderer.directionalGlyphs)
    .toMatchObject({ actors: 0, points: 0, scales: [] });
  await page.locator('#reset-camera').click();

  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({
    type: 'prepomax.annotations.updated-v1',
    annotations: [{
      name: 'fixture-callout', text: 'Safe fixture annotation',
      anchorPoint: [1_234_567.3, 2_345_678.3, 3_456_789.25],
      offset: [0, 0],
      drawBackground: true, drawBorder: true, visible: true,
    }],
  }));
  await expect(page.locator('.annotation-callout')).toHaveText('Safe fixture annotation');
  await expect.poll(async () => (await readDiagnostics(page)).renderer.annotations).toEqual({ count: 1, visible: 1 });
  const callout = page.locator('.annotation-callout');
  const calloutBox = await callout.boundingBox();
  if (!calloutBox) throw new Error('Annotation callout has no bounding box.');
  await page.mouse.move(calloutBox.x + 5, calloutBox.y + 5);
  await page.mouse.down();
  await page.mouse.move(calloutBox.x + 35, calloutBox.y + 25);
  await page.mouse.up();
  await expect.poll(async () => page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string; name?: string; offset?: number[] }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.annotation.moved-v1').at(-1)))
    .toMatchObject({ name: 'fixture-callout', offset: [30, 20] });

  await callout.dblclick();
  await page.keyboard.press('Control+A');
  await page.keyboard.insertText('Host-authoritative edit');
  await page.keyboard.press('Control+Enter');
  await expect.poll(async () => page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string; name?: string; text?: string }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.annotation.edited-v1').at(-1)))
    .toMatchObject({ name: 'fixture-callout', text: 'Host-authoritative edit' });

  await callout.click({ button: 'right' });
  const annotationMenu = page.getByRole('menu', { name: 'Actions for fixture-callout' });
  await expect(annotationMenu).toBeVisible();
  await expect(annotationMenu.getByRole('menuitem')).toHaveText(['Reset', 'Settings', 'Delete']);
  await annotationMenu.getByRole('menuitem', { name: 'Settings' }).click();
  await expect.poll(async () => page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string; name?: string; action?: string }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.annotation.action-v1').at(-1)))
    .toMatchObject({ name: 'fixture-callout', action: 'settings' });

  await callout.click({ button: 'right' });
  await annotationMenu.getByRole('menuitem', { name: 'Reset' }).click();
  await expect.poll(async () => page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string; name?: string; action?: string }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.annotation.action-v1').at(-1)))
    .toMatchObject({ name: 'fixture-callout', action: 'reset' });

  await callout.click({ button: 'right' });
  await annotationMenu.getByRole('menuitem', { name: 'Delete' }).click();
  await expect.poll(async () => page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string; name?: string; action?: string }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.annotation.action-v1').at(-1)))
    .toMatchObject({ name: 'fixture-callout', action: 'delete' });

  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({ type: 'prepomax.annotations.updated-v1', annotations: [] }));
  await expect(callout).toHaveCount(0);

  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({
    type: 'prepomax.annotations.updated-v1',
    annotations: [{
      name: 'Min_Annotation', text: 'Min: 1.25',
      anchorPoint: [1_234_567.3, 2_345_678.3, 3_456_789.25], offset: [0, 0],
      drawBackground: true, drawBorder: true, visible: true,
    }],
  }));
  const reservedCallout = page.locator('.annotation-callout');
  await reservedCallout.click({ button: 'right' });
  const reservedMenu = page.getByRole('menu', { name: 'Actions for Min_Annotation' });
  await expect(reservedMenu.getByRole('menuitem')).toHaveText(['Settings']);
  await reservedMenu.getByRole('menuitem').click();
  await expect.poll(async () => page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string; name?: string; action?: string }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.annotation.action-v1').at(-1)))
    .toMatchObject({ name: 'Min_Annotation', action: 'settings' });
  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({ type: 'prepomax.annotations.updated-v1', annotations: [] }));
  await expect(reservedCallout).toHaveCount(0);

  await page.evaluate(() => {
    const buffer = new ArrayBuffer(44);
    const bytes = new Uint8Array(buffer);
    bytes.set(new TextEncoder().encode('PMXSEL1\0'));
    const view = new DataView(buffer);
    let offset = 8;
    for (const value of [1, 2, 1, 0, 0, 0, 0, 0]) { view.setUint32(offset, value, true); offset += 4; }
    view.setInt32(offset, 1001, true);
    (window as unknown as {
      __sendPrePoMaxSharedBuffer(value: ArrayBuffer, metadata: unknown): void;
    }).__sendPrePoMaxSharedBuffer(buffer,
      { type: 'prepomax.selection.shared-v1', byteLength: buffer.byteLength });
  });
  await expect.poll(async () => (await readDiagnostics(page)).renderer.selection).toMatchObject({
    item: 'element', selectedIdCount: 1, overlayActors: 1, overflow: false,
  });

  await page.evaluate(() => {
    const idCount = 2_000_001;
    const buffer = new ArrayBuffer(40 + idCount * 4);
    const bytes = new Uint8Array(buffer);
    bytes.set(new TextEncoder().encode('PMXSEL1\0'));
    const view = new DataView(buffer);
    let offset = 8;
    for (const value of [1, 1, idCount, 0, 0, 0, 0, 0]) { view.setUint32(offset, value, true); offset += 4; }
    view.setInt32(offset, 101, true);
    (window as unknown as {
      __sendPrePoMaxSharedBuffer(value: ArrayBuffer, metadata: unknown): void;
    }).__sendPrePoMaxSharedBuffer(buffer,
      { type: 'prepomax.selection.shared-v1', byteLength: buffer.byteLength });
  });
  await expect.poll(async () => (await readDiagnostics(page)).renderer.selection).toMatchObject({
    item: 'node', selectedIdCount: 2, overlayActors: 1, overflow: false,
  });
  await expect(page.locator('#error')).toBeHidden();

  await page.getByLabel('Orthographic').check();
  await page.getByLabel('Camera view').selectOption('front');

  const viewport = page.locator('.viewport-shell');
  const clickPoint = async () => {
    const box = await viewport.boundingBox();
    if (!box) throw new Error('Viewport has no bounding box.');
    await page.mouse.click(box.x + box.width * 0.42, box.y + box.height * 0.62);
  };
  await clickPoint();
  await expect(page.locator('#pick-inspector')).toContainText('"validated": true');
  let pick = await readPick(page);
  expect(pick).toMatchObject({
    actorId: '20000000-0000-0000-0000-000000000001',
    triangle: 0,
    sourceElementId: 1001,
  });
  expect([101, 102, 103]).toContain(pick.candidateSourceNodeId);
  expect(pick.worldPosition[0]).toBeGreaterThan(1_234_567);
  expect(pick.worldPosition[2]).toBeCloseTo(3_456_789.25, 4);
  await expect(page.locator('#pick-marker')).toBeVisible();
  await expect.poll(async () => page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: unknown[];
  }).__prepomaxHostMessages.at(-1))).toMatchObject({
    type: 'prepomax.pick.validated-v1',
    validated: true,
    actorName: 'Front triangle',
    sourceElementId: 1001,
    triangleSourceNodeIds: [101, 102, 103],
    operation: 'replace',
  });

  await page.keyboard.down('Shift');
  await clickPoint();
  await page.keyboard.up('Shift');
  await expect.poll(async () => page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ operation: string }>;
  }).__prepomaxHostMessages.at(-1)?.operation)).toBe('add');

  const viewportBox = await viewport.boundingBox();
  if (!viewportBox) throw new Error('Viewport has no bounding box.');
  await page.keyboard.down('Alt');
  await page.mouse.move(viewportBox.x + viewportBox.width * 0.05, viewportBox.y + viewportBox.height * 0.05);
  await page.mouse.down();
  await page.mouse.move(viewportBox.x + viewportBox.width * 0.95, viewportBox.y + viewportBox.height * 0.95, { steps: 4 });
  await page.mouse.up();
  await page.keyboard.up('Alt');
  await expect(page.locator('#status')).toContainText('Area selected');
  await expect.poll(async () => page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: unknown[];
  }).__prepomaxHostMessages.at(-1))).toMatchObject({
    type: 'prepomax.area.validated-v1', validated: true, completelyInside: true,
    operation: 'replace',
  });
  const areaMessage = await page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ triangles?: unknown[] }>;
  }).__prepomaxHostMessages.at(-1));
  expect(areaMessage?.triangles?.length).toBeGreaterThan(0);

  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({
    type: 'prepomax.selection.updated-v1', item: 'element', ids: [1001], actorNames: [], faceNodeIds: [], edgeNodeIds: [],
  }));
  await expect.poll(async () => (await readDiagnostics(page)).renderer.selection).toMatchObject({
    item: 'element', selectedIdCount: 1, overlayActors: 1, overflow: false,
  });
  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({
    type: 'prepomax.selection.updated-v1', item: 'surface', ids: [10011], actorNames: [],
    faceNodeIds: [[101, 102, 103]], edgeNodeIds: [],
  }));
  await expect.poll(async () => (await readDiagnostics(page)).renderer.selection).toMatchObject({
    item: 'surface', selectedFaceCount: 1, overlayActors: 1, overflow: false,
  });
  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({
    type: 'prepomax.selection.updated-v1', item: 'geometry-edge', ids: [30001], actorNames: [], faceNodeIds: [],
    edgeNodeIds: [[101, 102]],
  }));
  await expect.poll(async () => (await readDiagnostics(page)).renderer.selection).toMatchObject({
    item: 'geometry-edge', selectedEdgeCount: 1, overlayActors: 1, overflow: false,
  });
  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({
    type: 'prepomax.selection.updated-v1', item: 'geometry', ids: [101], actorNames: [],
    faceNodeIds: [[101, 102, 103]], edgeNodeIds: [[101, 102]],
  }));
  await expect.poll(async () => (await readDiagnostics(page)).renderer.selection).toMatchObject({
    item: 'geometry', selectedIdCount: 1, selectedFaceCount: 1, selectedEdgeCount: 1,
    overlayActors: 3, overflow: false,
  });
  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({
    type: 'prepomax.selection.updated-v1', item: 'node', ids: [101], actorNames: [], faceNodeIds: [], edgeNodeIds: [],
  }));
  await expect.poll(async () => (await readDiagnostics(page)).renderer.selection).toMatchObject({
    item: 'node', selectedIdCount: 1, overlayActors: 1,
  });
  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({
    type: 'prepomax.selection.updated-v1', item: 'part', ids: [], actorNames: ['Front triangle'], faceNodeIds: [], edgeNodeIds: [],
  }));
  await expect.poll(async () => (await readDiagnostics(page)).renderer.selection).toMatchObject({
    item: 'part', selectedActorCount: 1, overlayActors: 1,
  });
  const requestsBeforeExplodedPreview = resourceRequests;
  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({
    type: 'prepomax.exploded-view.preview-v1',
    offsets: [{ actorName: 'Front triangle', offset: [0.25, 0, 0] }], animate: true, durationMs: 32,
    requestId: '0123456789abcdef0123456789abcdef',
  }));
  await expect.poll(async () => (await readDiagnostics(page)).renderer.explodedPreview).toMatchObject({
    actorCount: 1, offsets: [{ actorName: 'Front triangle', offset: [0.25, 0, 0] }],
  });
  await expect.poll(async () => (await readDiagnostics(page)).renderer.actorPositions
    .find((actor) => actor.actorName === 'Front triangle')?.position).toEqual([0.25, 0, 0]);
  await expect.poll(async () => (await readDiagnostics(page)).renderer.selection.overlayPositions[0]).toEqual([0.25, 0, 0]);
  await expect.poll(async () => page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string; actorCount?: number; animated?: boolean; requestId?: string }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.exploded-view.applied-v1').at(-1)))
    .toMatchObject({ actorCount: 1, animated: true, requestId: '0123456789abcdef0123456789abcdef' });
  const previewViewport = page.locator('#viewport');
  const previewBounds = await previewViewport.boundingBox();
  if (!previewBounds) throw new Error('Viewport has no bounding box for exploded preview.');
  await page.keyboard.down('Alt');
  await page.mouse.move(previewBounds.x + previewBounds.width * 0.05, previewBounds.y + previewBounds.height * 0.05);
  await page.mouse.down();
  await page.mouse.move(previewBounds.x + previewBounds.width * 0.95, previewBounds.y + previewBounds.height * 0.95);
  await page.mouse.up();
  await page.keyboard.up('Alt');
  await expect(page.locator('#status')).toHaveText('Rectangle selection is unavailable during an exploded-view preview.');
  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({
    type: 'prepomax.exploded-view.preview-v1',
    offsets: [{ actorName: 'Front triangle', offset: [0, 0, 0] }], animate: false, durationMs: 0,
  }));
  await expect.poll(async () => (await readDiagnostics(page)).renderer.explodedPreview.actorCount).toBe(0);
  await expect.poll(async () => (await readDiagnostics(page)).renderer.selection.overlayPositions[0]).toEqual([0, 0, 0]);
  expect(resourceRequests).toBe(requestsBeforeExplodedPreview);
  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({
    type: 'prepomax.section-view.updated-v1', enabled: true,
    point: [1_234_567.25, 2_345_678, 3_456_789], normal: [2, 0, 0],
    lightenColors: true, sectionColor: [0.8, 0.2, 0.1, 1],
  }));
  await expect.poll(async () => (await readDiagnostics(page)).renderer.sectionView).toMatchObject({
    active: true, point: [1_234_567.25, 2_345_678, 3_456_789], normal: [1, 0, 0], clippedChunks: 2,
    pipelineClippingPlanes: [1, 1], clippedOverlayActors: 1, capActors: 0,
  });
  await expect.poll(async () => page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string; enabled?: boolean; clippedChunks?: number; capActors?: number }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.section-view.applied-v1').at(-1)))
    .toMatchObject({ enabled: true, clippedChunks: 2, capActors: 0 });
  const sectionAreaCount = await page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.area.validated-v1').length);
  await page.keyboard.down('Alt');
  await page.mouse.move(previewBounds.x + previewBounds.width * 0.05, previewBounds.y + previewBounds.height * 0.05);
  await page.mouse.down();
  await page.mouse.move(previewBounds.x + previewBounds.width * 0.95, previewBounds.y + previewBounds.height * 0.95);
  await page.mouse.up();
  await page.keyboard.up('Alt');
  await expect(page.locator('#status')).toContainText('Area selected');
  await expect.poll(async () => page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.area.validated-v1').length))
    .toBe(sectionAreaCount + 1);
  await expect.poll(async () => page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string; validated?: boolean; triangles?: unknown[] }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.area.validated-v1').at(-1)))
    .toMatchObject({ type: 'prepomax.area.validated-v1', validated: true });
  const sectionArea = await page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string; triangles?: unknown[] }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.area.validated-v1').at(-1));
  expect(sectionArea?.triangles?.length).toBeGreaterThan(0);
  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({
    type: 'prepomax.section-view.updated-v1', enabled: false,
    point: [1_234_567.25, 2_345_678, 3_456_789], normal: [1, 0, 0],
    lightenColors: true, sectionColor: [0.8, 0.2, 0.1, 1],
  }));
  await expect.poll(async () => (await readDiagnostics(page)).renderer.sectionView).toMatchObject({
    active: false, clippedChunks: 0, pipelineClippingPlanes: [0, 0], clippedOverlayActors: 0, capActors: 0,
  });
  expect(resourceRequests).toBe(requestsBeforeExplodedPreview);
  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({
    type: 'prepomax.selection.updated-v1', item: 'none', ids: [], actorNames: [], faceNodeIds: [], edgeNodeIds: [],
  }));

  const requestsBeforeVisibility = resourceRequests;
  await page.getByLabel('Front triangle', { exact: true }).uncheck();
  await clickPoint();
  await expect(page.locator('#pick-inspector')).toContainText('2001');
  pick = await readPick(page);
  expect(pick.actorId).toBe('20000000-0000-0000-0000-000000000002');
  expect(pick.worldPosition[2]).toBeCloseTo(3_456_788.75, 4);
  expect(resourceRequests).toBe(requestsBeforeVisibility);
  await page.getByLabel('Front triangle', { exact: true }).check();

  await page.getByLabel('Front triangle pickable').uncheck();
  await page.getByLabel('Back triangle pickable').uncheck();
  await clickPoint();
  await expect(page.locator('#pick-inspector')).toHaveText('No rendered primitive at this point');
  await expect(page.locator('#pick-marker')).toBeHidden();
  await page.getByLabel('Front triangle pickable').check();
  await page.getByLabel('Back triangle pickable').check();

  await clickPoint();
  await expect(page.locator('#pick-inspector')).toContainText('"sourceElementId": 1001');
  await page.getByLabel('Show contours').uncheck();
  await page.getByLabel('Show contours').check();
  await expect(page.locator('canvas')).toBeVisible();

  const topologyTokensBefore = completeDiagnostics.renderer.topologyTokens;
  const resourceCountBeforeFieldUpdate = resourceRequests;
  await expect(page.locator('#switch-field')).toBeEnabled();
  await page.locator('#switch-field').click();
  await expect(page.locator('#status')).toHaveText('Activated fixture-wave-inverted without replacing topology.');
  await page.waitForFunction(() => {
    const text = document.querySelector('#diagnostics')?.textContent;
    try {
      return text ? (JSON.parse(text) as Diagnostics).scalarUpdateBytes === 24 : false;
    } catch {
      return false;
    }
  });
  const afterFieldUpdate = await readDiagnostics(page);
  expect(afterFieldUpdate.sceneRevision).toBe(completeDiagnostics.sceneRevision + 1);
  expect(afterFieldUpdate.renderer.topologyTokens).toEqual(topologyTokensBefore);
  expect(resourceRequests).toBe(resourceCountBeforeFieldUpdate + 2);
  expect(resourceUrls.slice(resourceCountBeforeFieldUpdate).every((url) =>
    url.endsWith('000000000011') || url.endsWith('000000000012'))).toBe(true);
  await expect(page.locator('#legend')).toContainText('fixture-wave-inverted');
  await clickPoint();
  await expect(page.locator('#pick-inspector')).toContainText('"sourceElementId": 1001');
  const finalDiagnostics = await readDiagnostics(page);
  expect(finalDiagnostics.picks.count).toBeGreaterThanOrEqual(5);
  expect(finalDiagnostics.picks.p95Ms).toBeGreaterThan(0);

  const requestsBeforeHostControls = resourceRequests;
  const cameraBeforeVisibility = await page.evaluate(() => {
    const messages = (window as unknown as {
      __prepomaxHostMessages: Array<{ type?: string; parallelScale?: number }>;
    }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.camera.changed-v1');
    return { count: messages.length, latest: messages.at(-1) };
  });
  await sendHostVisibility(page, 'Front triangle', false);
  await expect(page.getByLabel('Front triangle', { exact: true })).not.toBeChecked();
  await expect.poll(async () => (await readDiagnostics(page)).renderer.visiblePipelineChunks).toBe(1);
  await expect.poll(async () => page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.camera.changed-v1').length))
    .toBeGreaterThan(cameraBeforeVisibility.count);
  const cameraAfterHide = await page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string; parallelScale?: number }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.camera.changed-v1').at(-1));
  expect(cameraAfterHide?.parallelScale).toBe(cameraBeforeVisibility.latest?.parallelScale);
  await sendHostCamera(page, 'top');
  await expect.poll(async () => (await readDiagnostics(page)).renderer.cameraDirection[1]).toBeCloseTo(-1, 4);
  const cameraCountBeforeShow = await page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.camera.changed-v1').length);
  await sendHostVisibility(page, 'Front triangle', true);
  await expect(page.getByLabel('Front triangle', { exact: true })).toBeChecked();
  await expect.poll(async () => (await readDiagnostics(page)).renderer.visiblePipelineChunks).toBe(2);
  await expect.poll(async () => page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.camera.changed-v1').length))
    .toBeGreaterThan(cameraCountBeforeShow);
  expect(resourceRequests).toBe(requestsBeforeHostControls);

  await sendHostActorMutation(page, 'Front triangle', 'Renamed triangle', 0xff336699);
  await expect(page.getByLabel('Renamed triangle', { exact: true })).toBeChecked();
  await expect.poll(async () => {
    const colors = (await readDiagnostics(page)).renderer.actorColors;
    return colors.find((entry) => entry.actorId === '20000000-0000-0000-0000-000000000001')?.color;
  }).toEqual([0.2, 0.4, 0.6]);
  await sendHostCustomCamera(page, {
    position: [1_234_577, 2_345_698, 3_456_819],
    focalPoint: [1_234_567, 2_345_678, 3_456_789],
    viewUp: [0, 1, 0],
    parallelScale: 42,
  });
  await expect.poll(async () => (await readDiagnostics(page)).renderer.cameraPosition).toEqual([10, 20, 30]);
  await expect.poll(async () => (await readDiagnostics(page)).renderer.cameraParallelScale).toBe(42);
  await expect.poll(async () => page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string; position?: number[]; focalPoint?: number[];
      viewUp?: number[]; parallelScale?: number }>;
  }).__prepomaxHostMessages.some((message) => message.type === 'prepomax.camera.changed-v1'
    && JSON.stringify(message.position) === JSON.stringify([1_234_577, 2_345_698, 3_456_819])
    && JSON.stringify(message.focalPoint) === JSON.stringify([1_234_567, 2_345_678, 3_456_789])
    && JSON.stringify(message.viewUp) === JSON.stringify([0, 1, 0])
    && message.parallelScale === 42))).toBe(true);
  await sendHostCustomCamera(page, {
    position: [1_234_577, 2_345_698, 3_456_819],
    focalPoint: [1_234_567, 2_345_678, 3_456_789],
    viewUp: [0, 1, 0],
    parallelScale: 42,
    operation: 'fit',
  });
  await expect.poll(async () => (await readDiagnostics(page)).renderer.cameraParallelScale).toBeLessThan(42);
  await sendHostCustomCamera(page, {
    position: [1_234_577, 2_345_698, 3_456_819],
    focalPoint: [1_234_567, 2_345_678, 3_456_789],
    viewUp: [0, 1, 0],
    parallelScale: 42,
    operation: 'adjust',
  });
  await expect.poll(async () => {
    const camera = (await readDiagnostics(page)).renderer;
    const center = [.5, .5, 0];
    return camera.cameraFocalPoint.reduce((depth, value, index) =>
      depth + (value - center[index]!) * camera.cameraDirection[index]!, 0);
  }).toBeCloseTo(0, 6);
  expect((await readDiagnostics(page)).renderer.cameraParallelScale).toBe(42);
  await sendHostActorMutation(page, 'Renamed triangle', 'Front triangle', 0xffcc6644);
  await expect(page.getByLabel('Front triangle', { exact: true })).toBeChecked();
  await expect.poll(async () => (await readDiagnostics(page)).renderer.actorColors
    .find((actor) => actor.actorName === 'Front triangle')).toMatchObject({
      color: [0.8, 0.4, 4 / 15], opacity: 1, visible: true, scalarVisibility: true,
    });
  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({
    type: 'prepomax.actor-contours.updated-v1',
    actors: [{ name: 'Front triangle', colorContours: false }],
  }));
  await expect.poll(async () => (await readDiagnostics(page)).renderer.actorColors
    .find((actor) => actor.actorName === 'Front triangle')?.scalarVisibility).toBe(false);
  expect(resourceRequests).toBe(requestsBeforeHostControls);

  const requestsBeforeHostScalars = resourceRequests;
  const topologyBeforeHostScalars = (await readDiagnostics(page)).renderer.topologyTokens;
  await sendHostFieldMetadata(page, 'Displacements', 'Magnitude', 'mm');
  await sendHostScalars(page, 'Front triangle', [3, Number.NaN, -2]);
  await expect(page.locator('#legend')).toContainText('Displacements');
  await expect(page.locator('#legend')).toContainText('Magnitude');
  await expect.poll(() => resourceRequests).toBe(requestsBeforeHostScalars + 1);
  expect((await readDiagnostics(page)).renderer.topologyTokens).toEqual(topologyBeforeHostScalars);

  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({
    type: 'prepomax.selection.updated-v1', item: 'element', ids: [1001], actorNames: [], faceNodeIds: [], edgeNodeIds: [],
  }));
  await expect.poll(async () => (await readDiagnostics(page)).renderer.selection).toMatchObject({
    item: 'element', selectedIdCount: 1, overlayActors: 1, overflow: false,
  });

  const requestsBeforeFrame = resourceRequests;
  const topologyBeforeFrame = (await readDiagnostics(page)).renderer.topologyTokens;
  const cameraBeforeFrame = await page.evaluate(() => {
    const messages = (window as unknown as {
      __prepomaxHostMessages: Array<{ type?: string; parallelScale?: number }>;
    }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.camera.changed-v1');
    return { count: messages.length, parallelScale: messages.at(-1)?.parallelScale };
  });
  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({
    type: 'prepomax.follower-view.updated-v1',
    actorNames: ['Front triangle', 'Front triangle', 'Front triangle'], nodeIds: [101, 102, 103],
  }));
  await sendHostFrame(page, 'Front triangle', [
    1_234_567.5, 2_345_678, 3_456_789.5,
    1_234_568.5, 2_345_678, 3_456_789.5,
    1_234_567.5, 2_345_679, 3_456_789.5,
  ], [4, 5, 6]);
  await expect(page.locator('#status')).toHaveText('Activated animation frame without replacing topology.');
  await expect.poll(async () => (await readDiagnostics(page)).frameUpdateBytes).toBe(48);
  await expect.poll(async () => (await readDiagnostics(page)).frameActivation.latest).toMatchObject({
    chunks: 1,
    bytes: 48,
    followerAdjusted: true,
    cameraAdjusted: true,
  });
  const frameMessage = await page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string; followerAdjusted?: boolean; cameraAdjusted?: boolean }>;
  }).__prepomaxHostMessages.find((message) => message.type === 'prepomax.frame.activated-v1'));
  expect(frameMessage).toMatchObject({ type: 'prepomax.frame.activated-v1', chunks: 1, bytes: 48,
    followerAdjusted: true, cameraAdjusted: true });
  await expect.poll(async () => page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.camera.changed-v1').length))
    .toBeGreaterThan(cameraBeforeFrame.count);
  expect((await readDiagnostics(page)).renderer.cameraParallelScale).toBe(cameraBeforeFrame.parallelScale);
  expect((await readDiagnostics(page)).renderer.topologyTokens).toEqual(topologyBeforeFrame);
  expect(resourceRequests).toBe(requestsBeforeFrame + 2);

  const requestsBeforeRecovery = resourceRequests;
  await page.evaluate(() => (window as unknown as {
    __prepomaxBenchmark: { disconnectEvents(): void };
  }).__prepomaxBenchmark.disconnectEvents());
  await expect(page.locator('#status')).toHaveText('Loaded 2 actors.', { timeout: 30_000 });
  await expect.poll(() => resourceRequests).toBe(requestsBeforeRecovery + 10);
  await expect.poll(async () => (await readDiagnostics(page)).renderer.selection).toMatchObject({
    item: 'element', selectedIdCount: 1, overlayActors: 1, overflow: false,
  });

  const requestsBeforeReplacement = resourceRequests;
  await page.evaluate(() => (window as unknown as {
    __sendPrePoMaxHostMessage(value: unknown): void;
  }).__sendPrePoMaxHostMessage({
    type: 'prepomax.exploded-view.preview-v1',
    offsets: [{ actorName: 'Back triangle', offset: [0.4, 0, 0] }], animate: false, durationMs: 0,
  }));
  await expect.poll(async () => (await readDiagnostics(page)).renderer.explodedPreview.actorCount).toBe(1);
  await sendHostSceneReplacement(page, 'Front triangle', [
    1_234_567.5, 2_345_678.25, 3_456_789.75,
    1_234_568.5, 2_345_678.25, 3_456_789.75,
    1_234_567.5, 2_345_679.25, 3_456_789.75,
  ]);
  await expect(page.locator('#status')).toHaveText('Loaded 2 actors.', { timeout: 30_000 });
  await expect.poll(() => resourceRequests).toBe(requestsBeforeReplacement + 10);
  await expect.poll(async () => page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string; followerAdjusted?: boolean; cameraAdjusted?: boolean }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.follower-view.applied-v1').at(-1)))
    .toMatchObject({ followerAdjusted: true, cameraAdjusted: true });
  await expect.poll(async () => page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string; sceneId?: string; sceneRevision?: number;
      actorCount?: number; pipelineChunks?: number }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.scene.applied-v1').at(-1)))
    .toMatchObject({ sceneRevision: 1, actorCount: 2, pipelineChunks: 2 });
  await expect.poll(async () => (await readDiagnostics(page)).renderer).toMatchObject({
    pipelineChunks: 2, pipelinePoints: 6, pipelinePolys: 2,
    cameraParallelScale: 42,
    explodedPreview: { actorCount: 0, offsets: [] },
  });

  await sendHostEmptyScene(page);
  await expect(page.locator('#status')).toHaveText('Loaded empty scene.', { timeout: 30_000 });
  await expect.poll(async () => page.evaluate(() => (window as unknown as {
    __prepomaxHostMessages: Array<{ type?: string; sceneId?: string; sceneRevision?: number;
      actorCount?: number; pipelineChunks?: number }>;
  }).__prepomaxHostMessages.filter((message) => message.type === 'prepomax.scene.applied-v1').at(-1)))
    .toMatchObject({ sceneRevision: 1, actorCount: 0, pipelineChunks: 0 });
  await expect.poll(async () => (await readDiagnostics(page)).renderer).toMatchObject({
    rendererActors: 0, pipelineChunks: 0, pipelinePoints: 0, pipelinePolys: 0,
  });
  expect(nonLoopbackRequests).toEqual([]);
});

test('resource integrity failure is visible and a clean reload recovers', async ({ page }) => {
  let corruptedResponses = 0;
  let corruptedUrl: string | null = null;
  await page.route('**/resources/*', async (route) => {
    corruptedUrl ??= route.request().url();
    if (route.request().url() === corruptedUrl && corruptedResponses < 2) {
      corruptedResponses += 1;
      await route.fulfill({ status: 200, contentType: 'application/vnd.prepomax.render-buffer', body: Buffer.alloc(4) });
    } else {
      await route.continue();
    }
  });
  await page.goto('/');
  await sendHostSceneReplacement(page, 'Front triangle', [
    1_234_567.5, 2_345_678.25, 3_456_789.75,
    1_234_568.5, 2_345_678.25, 3_456_789.75,
    1_234_567.5, 2_345_679.25, 3_456_789.75,
  ]);
  await page.locator('#connect').click();
  await expect(page.locator('#error')).toBeVisible();
  await expect(page.locator('#error')).toContainText(/SHA-256 mismatch|Expected \d+ bytes/);
  await expect.poll(async () => (await readDiagnostics(page)).recovery).toMatchObject({
    resourceRetries: 1,
    lastSafeErrorCode: 'resource-hash-invalid',
  });
  await page.unroute('**/resources/*');
  await page.locator('#connect').click();
  await expect(page.locator('#status')).toHaveText('Loaded 2 actors.', { timeout: 30_000 });
  await expect(page.locator('#error')).toBeHidden();

  await sendHostActorRemoval(page, 'Front triangle');
  await expect(page.getByLabel('Front triangle', { exact: true })).toHaveCount(0);
  await expect.poll(async () => (await readDiagnostics(page)).renderer).toMatchObject({
    pipelineChunks: 1,
    pipelinePoints: 3,
    pipelinePolys: 1,
  });
});

interface Diagnostics {
  protocolVersion: string;
  clientVersion: string;
  serverVersion: string | null;
  sessionId: string | null;
  sceneId: string | null;
  sceneRevision: number;
  counts: { renderPointTuples: number; renderTriangles: number; sourcePointIdTuples: number;
    sourceElementIdTuples: number };
  bytes: { planned: number; transferred: number; decoded: number };
  recovery: { resourceRetries: number; resourceCancellations: number; contextLossCount: number;
    lastSafeErrorCode: string | null };
  transferredBytes: number;
  scalarUpdateBytes: number;
  frameUpdateBytes: number;
  frameActivation: {
    count: number;
    latest: null | {
      sceneRevision: number;
      chunks: number;
      bytes: number;
      followerAdjusted: boolean;
      cameraAdjusted: boolean;
      downloadMs: number;
      applyMs: number;
      stablePaintMs: number;
      totalMs: number;
    };
  };
  picks: { count: number; lastMs: number; p50Ms: number; p95Ms: number };
  renderer: {
    webgl2Available: boolean;
    webglLimits: null | { maxTextureSize: number; maxRenderbufferSize: number; maxVertexAttribs: number;
      maxCombinedTextureImageUnits: number };
    rendererActors: number;
    pipelineChunks: number;
    pipelinePoints: number;
    pipelinePolys: number;
    visiblePipelineChunks: number;
    viewportAppearance: { gradient: boolean; topRgb: number[]; bottomRgb: number[]; ambient: number;
      diffuse: number; actorLighting: { ambient: number; diffuse: number }[] };
    cameraDirection: number[];
    cameraViewUp: number[];
    cameraPosition: number[];
    cameraFocalPoint: number[];
    cameraParallelScale: number;
    actorColors: { actorId: string; actorName: string; color: number[]; opacity: number;
      visible: boolean; scalarVisibility: boolean }[];
    actorPositions: { actorName: string; position: number[] }[];
    topologyTokens: string[];
    explodedPreview: { actorCount: number; offsets: { actorName: string; offset: number[] }[] };
    sectionView: { active: boolean; point: number[] | null; normal: number[] | null; clippedChunks: number;
      pipelineClippingPlanes: number[]; clippedOverlayActors: number; capActors: number };
    selection: {
      item: string;
      selectedIdCount: number;
      selectedActorCount: number;
      selectedFaceCount: number;
      selectedEdgeCount: number;
      overlayActors: number;
      overlayPositions: number[][];
      overflow: boolean;
    };
    annotations: { count: number; visible: number };
    sphereSymbols: { actors: number; points: number; scales: number[] };
    coordinateAxes: { actors: number; scales: number[] };
    captions: { count: number; visible: number; texts: (string | null)[] };
    directionalGlyphs: { actors: number; points: number; scales: number[]; sourcePoints: number; sourcePolys: number;
      rotationalSourcePoints: number; rotationalSourcePolys: number; rotationalSourceLines: number;
      generalConeSourcePoints: number; generalConeSourcePolys: number; generalConeSourceBounds: number[];
      springSourcePoints: number; springSourcePolys: number; springSourceBounds: number[];
      arrowSources: { kind: string; points: number; polys: number; bounds: number[] }[];
      thermalSources: { kind: string; invert?: boolean; center?: boolean; points: number; polys: number;
        bounds: number[] }[] };
  };
}

interface PickInspector {
  actorId: string;
  triangle: number;
  sourceElementId: number;
  candidateSourceNodeId: number;
  worldPosition: number[];
}

async function readDiagnostics(page: import('../../src/client/node_modules/@playwright/test/index.js').Page): Promise<Diagnostics> {
  return JSON.parse(await page.locator('#diagnostics').textContent() ?? '{}') as Diagnostics;
}

async function readPick(page: import('../../src/client/node_modules/@playwright/test/index.js').Page): Promise<PickInspector> {
  return JSON.parse(await page.locator('#pick-inspector').textContent() ?? '{}') as PickInspector;
}

async function sendHostVisibility(
  page: import('../../src/client/node_modules/@playwright/test/index.js').Page,
  actorName: string,
  visible: boolean,
): Promise<void> {
  await page.evaluate(async ({ name, value }) => {
    const encoded = new TextEncoder().encode(name);
    const body = new Uint8Array(8 + 4 + 4 + encoded.length + 1);
    body.set(new TextEncoder().encode('PMXVIZ1\0'), 0);
    const view = new DataView(body.buffer);
    view.setInt32(8, 1, true);
    view.setInt32(12, encoded.length, true);
    body.set(encoded, 16);
    body[16 + encoded.length] = value ? 1 : 0;
    const response = await fetch('http://127.0.0.1:5174/api/v1/host/actors/visibility', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/vnd.prepomax.actor-visibility-v1',
        'X-PrePoMax-Host-Token': 'playwright-host-token',
      },
      body,
    });
    if (!response.ok) throw new Error(`Host visibility request failed: ${response.status}`);
  }, { name: actorName, value: visible });
}

async function sendHostCamera(
  page: import('../../src/client/node_modules/@playwright/test/index.js').Page,
  view: string,
): Promise<void> {
  await page.evaluate(async (cameraView) => {
    const response = await fetch(`http://127.0.0.1:5174/api/v1/host/camera/${cameraView}`, {
      method: 'POST',
      headers: { 'X-PrePoMax-Host-Token': 'playwright-host-token' },
    });
    if (!response.ok) throw new Error(`Host camera request failed: ${response.status}`);
  }, view);
}

async function sendHostActorMutation(
  page: import('../../src/client/node_modules/@playwright/test/index.js').Page,
  oldName: string,
  newName: string,
  argb: number,
): Promise<void> {
  await page.evaluate(async ({ oldValue, newValue, color }) => {
    const encoder = new TextEncoder();
    const oldBytes = encoder.encode(oldValue);
    const newBytes = encoder.encode(newValue);
    const body = new Uint8Array(8 + 4 + oldBytes.length + 4 + newBytes.length + 4);
    body.set(encoder.encode('PMXACT1\0'));
    const data = new DataView(body.buffer);
    let offset = 8;
    data.setInt32(offset, oldBytes.length, true); offset += 4;
    body.set(oldBytes, offset); offset += oldBytes.length;
    data.setInt32(offset, newBytes.length, true); offset += 4;
    body.set(newBytes, offset); offset += newBytes.length;
    data.setInt32(offset, color, true);
    const response = await fetch('http://127.0.0.1:5174/api/v1/host/actor', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/vnd.prepomax.actor-mutation-v1',
        'X-PrePoMax-Host-Token': 'playwright-host-token',
      },
      body,
    });
    if (!response.ok) throw new Error(`Host actor request failed: ${response.status}`);
  }, { oldValue: oldName, newValue: newName, color: argb });
}

async function sendHostActorRemoval(
  page: import('../../src/client/node_modules/@playwright/test/index.js').Page,
  ...actorNames: string[]
): Promise<void> {
  await page.evaluate(async (names) => {
    const encoder = new TextEncoder();
    const encoded = names.map((name) => encoder.encode(name));
    const body = new Uint8Array(8 + 4 + encoded.reduce((length, name) => length + 4 + name.length, 0));
    body.set(encoder.encode('PMXREM1\0'));
    const data = new DataView(body.buffer);
    data.setInt32(8, encoded.length, true);
    let offset = 12;
    for (const name of encoded) {
      data.setInt32(offset, name.length, true); offset += 4;
      body.set(name, offset); offset += name.length;
    }
    const response = await fetch('http://127.0.0.1:5174/api/v1/host/actors/remove', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/vnd.prepomax.actor-removal-v1',
        'X-PrePoMax-Host-Token': 'playwright-host-token',
      },
      body,
    });
    if (!response.ok) throw new Error(`Host actor removal request failed: ${response.status}`);
  }, actorNames);
}

async function sendHostFrame(
  page: import('../../src/client/node_modules/@playwright/test/index.js').Page,
  actorName: string,
  coordinates: number[],
  scalars: number[],
): Promise<void> {
  await page.evaluate(async ({ name, points, values }) => {
    const encoded = new TextEncoder().encode(name);
    const body = new Uint8Array(8 + 4 + 4 + encoded.length + 4 + values.length * 28 + 4);
    body.set(new TextEncoder().encode('PMXFRM1\0'));
    const data = new DataView(body.buffer);
    let offset = 8;
    data.setInt32(offset, 1, true); offset += 4;
    data.setInt32(offset, encoded.length, true); offset += 4;
    body.set(encoded, offset); offset += encoded.length;
    data.setInt32(offset, values.length, true); offset += 4;
    for (let point = 0; point < values.length; point += 1) {
      for (let component = 0; component < 3; component += 1) {
        data.setFloat64(offset, points[point * 3 + component]!, true); offset += 8;
      }
      data.setFloat32(offset, values[point]!, true); offset += 4;
    }
    data.setInt32(offset, 0, true);
    const response = await fetch('http://127.0.0.1:5174/api/v1/host/frame', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/vnd.prepomax.animation-frame-v1',
        'X-PrePoMax-Host-Token': 'playwright-host-token',
      },
      body,
    });
    if (!response.ok) throw new Error(`Host frame request failed: ${response.status}`);
  }, { name: actorName, points: coordinates, values: scalars });
}

async function sendHostSceneReplacement(
  page: import('../../src/client/node_modules/@playwright/test/index.js').Page,
  actorName: string,
  coordinates: number[],
): Promise<void> {
  await page.evaluate(async ({ name, points }) => {
    const actors = [
      { name, points, firstNodeId: 101, elementId: 1001, color: 0xff6699cc },
      { name: 'Back triangle', points: [
        1_234_567, 2_345_678, 3_456_788.75,
        1_234_568, 2_345_678, 3_456_788.75,
        1_234_567, 2_345_679, 3_456_788.75,
      ], firstNodeId: 201, elementId: 2001, color: 0xffcc9966 },
    ].map((actor) => ({ ...actor, encoded: new TextEncoder().encode(actor.name) }));
    const body = new Uint8Array(12 + actors.reduce((sum, actor) => sum + 156 + actor.encoded.length, 0));
    body.set(new TextEncoder().encode('PMXSCN1\0'));
    const data = new DataView(body.buffer);
    let offset = 8;
    data.setInt32(offset, actors.length, true); offset += 4;
    for (const actor of actors) {
      data.setInt32(offset, actor.encoded.length, true); offset += 4;
      body.set(actor.encoded, offset); offset += actor.encoded.length;
      data.setInt32(offset, actor.color, true); offset += 4;
      body.set([1, 1, 1, 0], offset); offset += 4;
      data.setFloat64(offset, 0.2, true); offset += 8;
      data.setFloat64(offset, 0.8, true); offset += 8;
      data.setFloat32(offset, 1.5, true); offset += 4;
      data.setFloat32(offset, 4, true); offset += 4;
      data.setInt32(offset, 3, true); offset += 4;
      data.setInt32(offset, 1, true); offset += 4;
      for (let point = 0; point < 3; point += 1) {
        data.setInt32(offset, actor.firstNodeId + point, true); offset += 4;
        for (let component = 0; component < 3; component += 1) {
          data.setFloat64(offset, actor.points[point * 3 + component]!, true); offset += 8;
        }
        data.setFloat32(offset, point + 1, true); offset += 4;
      }
      data.setInt32(offset, actor.elementId, true); offset += 4;
      data.setUint32(offset, 0, true); offset += 4;
      data.setUint32(offset, 1, true); offset += 4;
      data.setUint32(offset, 2, true); offset += 4;
    }
    if (offset !== body.length) throw new Error('Host scene replacement encoder length mismatch.');
    const response = await fetch('http://127.0.0.1:5174/api/v1/host/scene', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/vnd.prepomax.render-scene-v1',
        'X-PrePoMax-Host-Token': 'playwright-host-token',
      },
      body,
    });
    if (!response.ok) throw new Error(`Host scene replacement request failed: ${response.status}`);
  }, { name: actorName, points: coordinates });
}

async function sendHostEmptyScene(
  page: import('../../src/client/node_modules/@playwright/test/index.js').Page,
): Promise<void> {
  await page.evaluate(async () => {
    const body = new Uint8Array(12);
    body.set(new TextEncoder().encode('PMXSCN1\0'));
    new DataView(body.buffer).setInt32(8, 0, true);
    const response = await fetch('http://127.0.0.1:5174/api/v1/host/scene', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/vnd.prepomax.render-scene-v1',
        'X-PrePoMax-Host-Token': 'playwright-host-token',
      },
      body,
    });
    if (!response.ok) throw new Error(`Host empty-scene replacement request failed: ${response.status}`);
  });
}

async function sendHostCustomCamera(
  page: import('../../src/client/node_modules/@playwright/test/index.js').Page,
  camera: { position: number[]; focalPoint: number[]; viewUp: number[]; parallelScale: number;
    operation?: 'set' | 'fit' | 'adjust' },
): Promise<void> {
  await page.evaluate(async (value) => {
    const version2 = value.operation !== undefined && value.operation !== 'set';
    const body = new Uint8Array(8 + (version2 ? 4 : 0) + 10 * 8);
    body.set(new TextEncoder().encode(version2 ? 'PMXCAM2\0' : 'PMXCAM1\0'));
    const data = new DataView(body.buffer);
    let offset = 8;
    if (version2) {
      data.setInt32(offset, value.operation === 'fit' ? 1 : 2, true);
      offset += 4;
    }
    for (const component of [...value.position, ...value.focalPoint, ...value.viewUp, value.parallelScale]) {
      data.setFloat64(offset, component, true);
      offset += 8;
    }
    const response = await fetch('http://127.0.0.1:5174/api/v1/host/camera/custom', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/vnd.prepomax.camera-v1',
        'X-PrePoMax-Host-Token': 'playwright-host-token',
      },
      body,
    });
    if (!response.ok) throw new Error(`Host custom camera request failed: ${response.status}`);
  }, camera);
}

async function sendHostFieldMetadata(
  page: import('../../src/client/node_modules/@playwright/test/index.js').Page,
  name: string,
  component: string,
  units: string,
): Promise<void> {
  await page.evaluate(async (values) => {
    const encoder = new TextEncoder();
    const encoded = values.map((value) => encoder.encode(value));
    const body = new Uint8Array(8 + encoded.reduce((length, value) => length + 4 + value.length, 0));
    body.set(encoder.encode('PMXFLD1\0'));
    const data = new DataView(body.buffer);
    let offset = 8;
    for (const value of encoded) {
      data.setInt32(offset, value.length, true); offset += 4;
      body.set(value, offset); offset += value.length;
    }
    const response = await fetch('http://127.0.0.1:5174/api/v1/host/field/metadata', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/vnd.prepomax.field-metadata-v1',
        'X-PrePoMax-Host-Token': 'playwright-host-token',
      },
      body,
    });
    if (!response.ok) throw new Error(`Host field metadata request failed: ${response.status}`);
  }, [name, component, units]);
}

async function sendHostScalars(
  page: import('../../src/client/node_modules/@playwright/test/index.js').Page,
  actorName: string,
  values: number[],
): Promise<void> {
  await page.evaluate(async ({ name, scalarValues }) => {
    const encoder = new TextEncoder();
    const nameBytes = encoder.encode(name);
    const body = new Uint8Array(8 + 4 + 4 + nameBytes.length + 4 + scalarValues.length * 4);
    body.set(encoder.encode('PMXSCL1\0'));
    const data = new DataView(body.buffer);
    let offset = 8;
    data.setInt32(offset, 1, true); offset += 4;
    data.setInt32(offset, nameBytes.length, true); offset += 4;
    body.set(nameBytes, offset); offset += nameBytes.length;
    data.setInt32(offset, scalarValues.length, true); offset += 4;
    for (const value of scalarValues) {
      data.setFloat32(offset, value, true);
      offset += 4;
    }
    const response = await fetch('http://127.0.0.1:5174/api/v1/host/scalars', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/vnd.prepomax.scalar-update-v1',
        'X-PrePoMax-Host-Token': 'playwright-host-token',
      },
      body,
    });
    if (!response.ok) throw new Error(`Host scalar request failed: ${response.status}`);
  }, { name: actorName, scalarValues: values });
}

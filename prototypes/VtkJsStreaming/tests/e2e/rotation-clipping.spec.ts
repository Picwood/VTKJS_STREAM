import playwrightTest from '../../src/client/node_modules/@playwright/test/index.js';

const { expect, test } = playwrightTest;

test('camera adjustment keeps a large negative-coordinate model visible through rotation', async ({ page, request }, testInfo) => {
  const side = 1000, center = [-9500, -19500, -29500];
  const vertices = [[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]];
  const triangles = [[0,2,1],[0,3,2],[4,5,6],[4,6,7],[0,1,5],[0,5,4],
    [1,2,6],[1,6,5],[2,3,7],[2,7,6],[3,0,4],[3,4,7]];
  const fields: Buffer[] = [Buffer.from('PMXSCN1\0')];
  const int = (v: number) => { const b=Buffer.alloc(4); b.writeInt32LE(v); fields.push(b); };
  const float = (v: number) => { const b=Buffer.alloc(4); b.writeFloatLE(v); fields.push(b); };
  const double = (v: number) => { const b=Buffer.alloc(8); b.writeDoubleLE(v); fields.push(b); };
  int(1); const name=Buffer.from('negative cube'); int(name.length); fields.push(name);
  int(-10040065); fields.push(Buffer.from([1,0,0,0])); double(.2); double(.8); float(1); float(3);
  int(vertices.length); int(triangles.length);
  vertices.forEach((p,i) => { int(i+1); p.forEach((v,k) => double(center[k]!+(v-.5)*side)); float(0); });
  triangles.forEach(t => { int(1); t.forEach(int); });
  const uploaded=await request.post('http://127.0.0.1:5174/api/v1/host/scene',{
    headers:{'X-PrePoMax-Host-Token':'playwright-host-token','Content-Type':'application/vnd.prepomax.render-scene-v1'},
    data:Buffer.concat(fields),
  });
  expect(uploaded.ok()).toBe(true);
  await page.goto('/?autoload=1&testHooks=1');
  await expect(page.locator('#status')).toHaveText('Loaded 1 actors.');
  await page.getByLabel('Orthographic').check();
  const camera=Buffer.alloc(8+4+80); camera.write('PMXCAM2\0'); camera.writeInt32LE(2,8);
  [...[center[0]!,center[1]!,center[2]!+side*3],...center,0,1,0,side*.85]
    .forEach((value,i) => camera.writeDoubleLE(value,12+i*8));
  expect((await request.post('http://127.0.0.1:5174/api/v1/host/camera/custom',{
    headers:{'X-PrePoMax-Host-Token':'playwright-host-token','Content-Type':'application/vnd.prepomax.camera-v1'},data:camera,
  })).ok()).toBe(true);
  const diagnostics=async () => JSON.parse(await page.locator('#diagnostics').textContent() ?? '{}').renderer;
  await expect.poll(async () => (await diagnostics()).cameraPosition[2]).toBeLessThan(side);
  await page.evaluate(async () => {
    await (window as unknown as {__prepomaxBenchmark:{measureOrbit(n:number):Promise<number[]>}}).__prepomaxBenchmark.measureOrbit(180);
  });
  const state=await diagnostics();
  expect(state.sectionView.active).toBe(false);
  const [near,far]=state.cameraClippingRange;
  for (const vertex of vertices) {
    const depth=vertex.reduce((sum,v,k) => sum+((v-.5)*side-state.cameraPosition[k])*state.cameraDirection[k],0);
    expect(depth).toBeGreaterThan(near);
    expect(depth).toBeLessThan(far);
  }
  const viewport=await page.locator('.viewport-shell').boundingBox();
  if (!viewport) throw new Error('Viewport missing');
  await page.mouse.move(viewport.x+viewport.width*.5,viewport.y+viewport.height*.5);
  await page.mouse.down();
  await page.mouse.move(viewport.x+viewport.width*.7,viewport.y+viewport.height*.7,{steps:12});
  await page.mouse.up();
  await page.evaluate(async () => {
    await (window as unknown as {__prepomaxBenchmark:{measureOrbit(n:number):Promise<number[]>}}).__prepomaxBenchmark.measureOrbit(0);
  });
  const dragged=await diagnostics();
  expect(dragged.cameraDirection).not.toEqual(state.cameraDirection);
  for (const vertex of vertices) {
    const depth=vertex.reduce((sum,v,k) => sum+((v-.5)*side-dragged.cameraPosition[k])*dragged.cameraDirection[k],0);
    expect(depth).toBeGreaterThan(dragged.cameraClippingRange[0]);
    expect(depth).toBeLessThan(dragged.cameraClippingRange[1]);
  }
  await page.getByLabel('Orthographic').uncheck();
  expect((await request.post('http://127.0.0.1:5174/api/v1/host/camera/custom',{
    headers:{'X-PrePoMax-Host-Token':'playwright-host-token','Content-Type':'application/vnd.prepomax.camera-v1'},data:camera,
  })).ok()).toBe(true);
  await expect.poll(async () => (await diagnostics()).cameraPosition[2]).toBeGreaterThan(side*2);
  await page.evaluate(async () => {
    await (window as unknown as {__prepomaxBenchmark:{measureOrbit(n:number):Promise<number[]>}}).__prepomaxBenchmark.measureOrbit(180);
  });
  const perspective=await diagnostics();
  for (const vertex of vertices) {
    const depth=vertex.reduce((sum,v,k) => sum+((v-.5)*side-perspective.cameraPosition[k])*perspective.cameraDirection[k],0);
    expect(depth).toBeGreaterThan(perspective.cameraClippingRange[0]);
    expect(depth).toBeLessThan(perspective.cameraClippingRange[1]);
  }
  await expect(page.locator('#error')).toBeHidden();
  await testInfo.attach('rotated-negative-cube',{body:await page.locator('.viewport-shell').screenshot(),contentType:'image/png'});
});

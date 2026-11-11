import playwrightTest from '../../src/client/node_modules/@playwright/test/index.js';

const { expect, test } = playwrightTest;

for (const scale of [1, 1_000_000]) {
  test(`picks translated mesh geometry at scale ${scale} without a depth-buffer bounds error`, async ({ page, request }) => {
    // A production host-upload scene with a flat triangle and large world coordinates.
    const fields: Buffer[] = [Buffer.from('PMXSCN1\0')];
    const int = (value: number) => { const b = Buffer.alloc(4); b.writeInt32LE(value); fields.push(b); };
    const float = (value: number) => { const b = Buffer.alloc(4); b.writeFloatLE(value); fields.push(b); };
    const double = (value: number) => { const b = Buffer.alloc(8); b.writeDoubleLE(value); fields.push(b); };
    int(1);
    const name = Buffer.from('picked mesh'); int(name.length); fields.push(name);
    int(-10040065); fields.push(Buffer.from([1, 0, 0, 0])); double(.2); double(.8); float(1); float(3);
    int(3); int(1);
    for (let i = 0; i < 3; i++) {
      int(101 + i); double(123456789.123 + (i === 1 ? scale : 0));
      double(-234567890.456 + (i === 2 ? scale : 0)); double(345678901.789); float(0);
    }
    int(1001); int(0); int(1); int(2);
    const uploaded = await request.post('http://127.0.0.1:5174/api/v1/host/scene', {
      headers: { 'X-PrePoMax-Host-Token': 'playwright-host-token', 'Content-Type': 'application/vnd.prepomax.render-scene-v1' },
      data: Buffer.concat(fields),
    });
    expect(uploaded.ok()).toBe(true);
    await page.goto('/?autoload=1');
    await expect(page.locator('#status')).toHaveText('Loaded 1 actors.');
    await page.getByLabel('Orthographic').check();
    await page.getByLabel('Camera view').selectOption('front');
    const viewport = await page.locator('.viewport-shell').boundingBox();
    if (!viewport) throw new Error('Viewport missing');
    await page.mouse.click(viewport.x + viewport.width * .42, viewport.y + viewport.height * .62);
    await expect(page.locator('#pick-inspector')).toContainText('"validated": true');
    const pick = JSON.parse(await page.locator('#pick-inspector').textContent() ?? '{}');
    expect(pick.sourceElementId).toBe(1001);
    expect(pick.worldPosition[2]).toBe(345678901.789);
    await expect(page.locator('#error')).toBeHidden();
  });
}

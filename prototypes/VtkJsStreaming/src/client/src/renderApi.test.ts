import { afterEach, describe, expect, it, vi } from 'vitest';
import { RenderApi } from './renderApi';

afterEach(() => vi.unstubAllGlobals());

describe('RenderApi loopback boundary', () => {
  it('accepts HTTP on the explicit IPv4 loopback address', () => {
    expect(() => new RenderApi('http://127.0.0.1:5174')).not.toThrow();
  });

  it.each([
    'https://127.0.0.1:5174',
    'http://localhost:5174',
    'http://[::1]:5174',
    'http://192.168.1.20:5174',
    'http://example.com',
  ])('rejects a non-canonical server URL: %s', (url) => {
    expect(() => new RenderApi(url)).toThrow('must use HTTP on 127.0.0.1');
  });
});

describe('RenderApi diagnostics', () => {
  it('captures only a bounded server version', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({
      sessionId: 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',
      accessToken: 'A'.repeat(43),
      sceneId: '10000000-0000-0000-0000-000000000001',
    }), { status: 200, headers: { 'Content-Type': 'application/json', 'X-PrePoMax-Server-Version': '0.1.0' } })));
    const api = new RenderApi('http://127.0.0.1:5174');
    await api.createSession();
    expect(api.getDiagnostics()).toEqual({
      serverVersion: '0.1.0', resourceRetries: 0, resourceCancellations: 0,
    });
  });

  it('counts integrity retries without retaining response content', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({
        sessionId: 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',
        accessToken: 'A'.repeat(43),
        sceneId: '10000000-0000-0000-0000-000000000001',
      }), { status: 200 }))
      .mockImplementation(async () => new Response(new Uint8Array([1, 2, 3, 4]), { status: 200 }));
    vi.stubGlobal('fetch', fetchMock);
    const api = new RenderApi('http://127.0.0.1:5174');
    await api.createSession();
    await expect(api.getResource({
      resourceId: 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',
      semantic: 'positions', dataType: 'float32', components: 1, elementCount: 1, byteLength: 4,
      byteOrder: 'little-endian', contentEncoding: 'identity', sha256: '0'.repeat(64), version: 1,
      immutable: true, uri: '/resource',
    })).rejects.toThrow('SHA-256 mismatch');
    expect(api.getDiagnostics().resourceRetries).toBe(1);
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });

  it('counts canceled resource requests', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({
        sessionId: 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',
        accessToken: 'A'.repeat(43),
        sceneId: '10000000-0000-0000-0000-000000000001',
      }), { status: 200 }))
      .mockRejectedValueOnce(new DOMException('Canceled', 'AbortError'));
    vi.stubGlobal('fetch', fetchMock);
    const api = new RenderApi('http://127.0.0.1:5174');
    await api.createSession();
    const controller = new AbortController();
    controller.abort();
    await expect(api.getResource({
      resourceId: 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',
      semantic: 'positions', dataType: 'float32', components: 1, elementCount: 1, byteLength: 4,
      byteOrder: 'little-endian', contentEncoding: 'identity', sha256: '0'.repeat(64), version: 1,
      immutable: true, uri: '/resource',
    }, controller.signal)).rejects.toThrow('Canceled');
    expect(api.getDiagnostics().resourceCancellations).toBe(1);
  });
});

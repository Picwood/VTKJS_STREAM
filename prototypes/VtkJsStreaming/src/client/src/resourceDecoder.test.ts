import { describe, expect, it } from 'vitest';
import type { ResourceDescriptor } from './protocol';
import { decodeResource, ResourceValidationError, toVtkTriangleCells, verifySha256 } from './resourceDecoder';

const descriptor: ResourceDescriptor = {
  resourceId: '40000000-0000-0000-0000-000000000001',
  semantic: 'positions',
  dataType: 'float32',
  components: 3,
  elementCount: 1,
  byteLength: 12,
  byteOrder: 'little-endian',
  contentEncoding: 'identity',
  sha256: '0'.repeat(64),
  version: 1,
  immutable: true,
  uri: '/resource',
};

describe('decodeResource', () => {
  it('creates the typed view only after validating length', () => {
    const buffer = new Float32Array([1.25, -2.5, 3.75]).buffer;
    expect(Array.from(decodeResource(descriptor, buffer))).toEqual([1.25, -2.5, 3.75]);
  });

  it('rejects a truncated resource', () => {
    expect(() => decodeResource(descriptor, new ArrayBuffer(8)))
      .toThrowError(ResourceValidationError);
  });
});

describe('toVtkTriangleCells', () => {
  it('adds one cell-size prefix per triangle', () => {
    expect(Array.from(toVtkTriangleCells(new Uint32Array([0, 1, 2, 2, 1, 3]))))
      .toEqual([3, 0, 1, 2, 3, 2, 1, 3]);
  });

  it('rejects incomplete triangles', () => {
    expect(() => toVtkTriangleCells(new Uint32Array([0, 1])))
      .toThrowError('divisible by three');
  });
});

describe('verifySha256', () => {
  it('accepts a matching digest and rejects a mismatch', async () => {
    const buffer = new Uint8Array([1, 2, 3]).buffer;
    const matching = {
      ...descriptor,
      dataType: 'int32' as const,
      components: 1,
      elementCount: 0,
      byteLength: 0,
      sha256: '039058c6f2c0cb492c533b0a4d14ef77cc0f78abccced5287d84a1a2011cfb81',
    };
    await expect(verifySha256(matching, buffer)).resolves.toBeUndefined();
    await expect(verifySha256({ ...matching, sha256: '0'.repeat(64) }, buffer))
      .rejects.toMatchObject({ code: 'resource-hash-invalid' });
  });
});

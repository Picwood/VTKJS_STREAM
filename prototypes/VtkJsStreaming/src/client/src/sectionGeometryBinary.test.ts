import { describe, expect, it } from 'vitest';
import { decodeSectionGeometry } from './sectionGeometryBinary';

describe('section geometry binary decoder', () => {
  it('decodes bounded mixed triangle and line geometry', () => {
    const result = decodeSectionGeometry(buildResponse());
    expect(result.skippedCells).toBe(2);
    expect(result.actors).toHaveLength(1);
    expect(result.actors[0]).toMatchObject({ actorId: 'a', parentActorId: 'p', name: 'part', lineWidthPx: 2 });
    expect([...result.actors[0]!.positions]).toEqual([0, 0, 0, 1, 0, 0, 0, 1, 0]);
    expect([...result.actors[0]!.triangleConnectivity]).toEqual([0, 1, 2]);
    expect([...result.actors[0]!.lineConnectivity]).toEqual([0, 1]);
    expect([...result.actors[0]!.pointScalars]).toEqual([1, 2, 3]);
    expect([...result.actors[0]!.sourceElementIds]).toEqual([42]);
  });

  it('rejects trailing bytes and out-of-range connectivity', () => {
    const valid = new Uint8Array(buildResponse());
    const trailing = new Uint8Array(valid.length + 1);
    trailing.set(valid);
    expect(() => decodeSectionGeometry(trailing.buffer)).toThrow(/trailing/i);

    const invalid = buildResponse(3);
    expect(() => decodeSectionGeometry(invalid)).toThrow(/invalid arrays/i);
  });
});

function buildResponse(firstTriangleIndex = 0): ArrayBuffer {
  const chunks: Uint8Array[] = [];
  const bytes = (size: number, write: (view: DataView) => void) => {
    const buffer = new ArrayBuffer(size);
    write(new DataView(buffer));
    chunks.push(new Uint8Array(buffer));
  };
  const int32 = (value: number) => bytes(4, (view) => view.setInt32(0, value, true));
  const float32 = (value: number) => bytes(4, (view) => view.setFloat32(0, value, true));
  const float64 = (value: number) => bytes(8, (view) => view.setFloat64(0, value, true));
  const string = (value: string) => {
    const encoded = new TextEncoder().encode(value);
    int32(encoded.length);
    chunks.push(encoded);
  };
  chunks.push(new TextEncoder().encode('PMXSEC1\0'));
  int32(1);
  int32(2);
  string('a'); string('p'); string('part');
  [0.2, 0.3, 0.4, 1, 2].forEach(float32);
  int32(3); int32(1); int32(1);
  [0, 1, 0, 1, 0, 0].forEach(float64);
  [0, 0, 0, 1, 0, 0, 0, 1, 0].forEach(float32);
  [firstTriangleIndex, 1, 2, 0, 1].forEach(int32);
  [1, 2, 3].forEach(float32);
  int32(42);
  const length = chunks.reduce((sum, chunk) => sum + chunk.length, 0);
  const result = new Uint8Array(length);
  let offset = 0;
  for (const chunk of chunks) { result.set(chunk, offset); offset += chunk.length; }
  return result.buffer;
}

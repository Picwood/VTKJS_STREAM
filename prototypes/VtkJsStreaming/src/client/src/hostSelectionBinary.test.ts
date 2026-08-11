import { describe, expect, it } from 'vitest';
import { decodeHostSelection } from './hostSelectionBinary';

function fixture(): ArrayBuffer {
  const name = new TextEncoder().encode('Part 1');
  const buffer = new ArrayBuffer(40 + 8 + 4 + name.length + 4 + 12 + 4 + 8);
  const bytes = new Uint8Array(buffer);
  bytes.set(new TextEncoder().encode('PMXSEL1\0'));
  const view = new DataView(buffer);
  let offset = 8;
  for (const value of [1, 5, 2, 1, 1, 3, 1, 2]) { view.setUint32(offset, value, true); offset += 4; }
  for (const value of [11, 12]) { view.setInt32(offset, value, true); offset += 4; }
  view.setUint32(offset, name.length, true); offset += 4; bytes.set(name, offset); offset += name.length;
  view.setUint32(offset, 3, true); offset += 4;
  for (const value of [11, 12, 13]) { view.setInt32(offset, value, true); offset += 4; }
  view.setUint32(offset, 2, true); offset += 4;
  for (const value of [12, 13]) { view.setInt32(offset, value, true); offset += 4; }
  return buffer;
}

function idFixture(count: number): ArrayBuffer {
  const buffer = new ArrayBuffer(40 + count * 4);
  const bytes = new Uint8Array(buffer);
  bytes.set(new TextEncoder().encode('PMXSEL1\0'));
  const view = new DataView(buffer);
  let offset = 8;
  for (const value of [1, 1, count, 0, 0, 0, 0, 0]) { view.setUint32(offset, value, true); offset += 4; }
  for (let index = 0; index < count; index += 1) view.setInt32(offset + index * 4, index + 1, true);
  return buffer;
}

describe('shared host selection decoder', () => {
  it('decodes ids, names, faces, and edges', () => {
    expect(decodeHostSelection(fixture())).toEqual({
      type: 'prepomax.selection.updated-v1', item: 'geometry', ids: [11, 12], actorNames: ['Part 1'],
      faceNodeIds: [[11, 12, 13]], edgeNodeIds: [[12, 13]],
    });
  });

  it('rejects signatures, trailing bytes, and declared-reference mismatches', () => {
    const signature = fixture(); new Uint8Array(signature)[0] = 0;
    expect(() => decodeHostSelection(signature)).toThrow(/signature/);
    const trailing = new Uint8Array(fixture().byteLength + 1); trailing.set(new Uint8Array(fixture()));
    expect(() => decodeHostSelection(trailing.buffer)).toThrow(/trailing/);
    const mismatch = fixture(); new DataView(mismatch).setUint32(28, 4, true);
    expect(() => decodeHostSelection(mismatch)).toThrow(/cell count/);
  });

  it('accepts selections beyond the former two-million-ID policy bound', () => {
    const decoded = decodeHostSelection(idFixture(2_000_001));
    expect(decoded.ids).toHaveLength(2_000_001);
    expect(decoded.ids[0]).toBe(1);
    expect(decoded.ids.at(-1)).toBe(2_000_001);
  });

  it('rejects declared ID counts above the eight-million-ID bound before reading values', () => {
    const oversized = idFixture(0);
    new DataView(oversized).setUint32(16, 8_000_001, true);
    expect(() => decodeHostSelection(oversized)).toThrow(/safety bounds/);
  });
});

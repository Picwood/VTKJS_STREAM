import { describe, expect, it } from 'vitest';
import { resolveTriangleSource } from './pickResolver';

const connectivity = new Uint32Array([0, 1, 2]);
const positions = new Float32Array([0, 0, 0.25, 1, 0, 0.25, 0, 1, 0.25]);
const pointIds = new Int32Array([101, 102, 103]);
const elementIds = new Int32Array([1001]);

describe('resolveTriangleSource', () => {
  it.each([
    [[0.05, 0.05, 0.25], 101],
    [[0.9, 0.05, 0.25], 102],
    [[0.05, 0.9, 0.25], 103],
  ] as const)('maps one rendered triangle to its element and nearest vertex in constant work', (hit, nodeId) => {
    expect(resolveTriangleSource(connectivity, positions, pointIds, elementIds, 0, hit)).toEqual({
      sourceElementId: 1001,
      candidateSourceNodeId: nodeId,
    });
  });

  it('rejects an out-of-range rendered primitive', () => {
    expect(() => resolveTriangleSource(connectivity, positions, pointIds, elementIds, 1, [0, 0, 0])).toThrow(RangeError);
  });
});

import { describe, expect, it } from 'vitest';
import { intersectPickedTriangle, resolveTriangleSource } from './pickResolver';

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

describe('intersectPickedTriangle', () => {
  it.each([1e-6, 1, 1e6, 1e12])('recovers an exact planar hit at scale %s without depth-buffer rounding', (scale) => {
    const points = new Float32Array([0, 0, scale, scale, 0, scale, 0, scale, scale]);
    const hit = intersectPickedTriangle(connectivity, points, 0, [scale * .2, scale * .2, scale * 3], [scale * .2, scale * .2, -scale]);
    expect(hit).not.toBeNull();
    expect(hit![2]).toBe(points[2]);
    expect(hit![0] / scale).toBeCloseTo(.2, 7);
    expect(hit![1] / scale).toBeCloseTo(.2, 7);
  });
  it('handles an oblique ray on a translated triangle and clamps a shared raster edge', () => {
    const points = new Float32Array([10000, -5000, .25, 10001, -5000, .25, 10000, -4999, .25]);
    const hit = intersectPickedTriangle(connectivity, points, 0, [9999.5, -4999.75, 1.25], [10001.5, -4999.75, -2.75]);
    expect(hit).toEqual([10000, -4999.75, .25]);
    const edge = intersectPickedTriangle(connectivity, positions, 0, [.500001, .5, 1], [.500001, .5, -1]);
    expect(edge![0] + edge![1]).toBeCloseTo(1, 14);
    expect(edge![2]).toBe(.25);
  });
  it('rejects parallel rays, degenerate triangles and invalid primitives', () => {
    expect(intersectPickedTriangle(connectivity, positions, 0, [0, 0, 1], [1, 1, 1])).toBeNull();
    expect(intersectPickedTriangle(connectivity, new Float32Array(9), 0, [0, 0, 1], [0, 0, -1])).toBeNull();
    expect(() => intersectPickedTriangle(connectivity, positions, -1, [0, 0, 1], [0, 0, -1])).toThrow(RangeError);
  });
});

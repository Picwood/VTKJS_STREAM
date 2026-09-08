import { describe, expect, it } from 'vitest';
import { maximumTransientPrimitivePoints, normalizeHostTransientPrimitives } from './hostTransientPrimitives';

const valid = { type: 'prepomax.transient-primitives.updated-v1', primitives: [{
  name: 'Highlight::edge', points: [[0, 0, 0], [1, 0, 0], [0.5, 0.25, 0]], lines: [[0, 2], [2, 1]],
  drawPoints: false, rgba: [1, 0.25, 0, 1], pointSize: 8, lineWidth: 3, visible: true,
}] } as const;

describe('host transient primitives', () => {
  it('clones a bounded point/line replacement', () => {
    const primitives = normalizeHostTransientPrimitives(valid);
    expect(primitives).toEqual(valid.primitives);
    expect(primitives[0]).not.toBe(valid.primitives[0]);
    expect(primitives[0]?.points[0]).not.toBe(valid.primitives[0].points[0]);
    expect(primitives[0]?.lines[0]).not.toBe(valid.primitives[0].lines[0]);
  });

  it('accepts a point-only actor and rejects invalid connectivity and style', () => {
    expect(normalizeHostTransientPrimitives({ type: valid.type, primitives: [{ ...valid.primitives[0],
      points: [[1, 2, 3]], lines: [], drawPoints: true }] })).toHaveLength(1);
    expect(() => normalizeHostTransientPrimitives({ type: valid.type, primitives: [{ ...valid.primitives[0],
      lines: [[0, 3]] }] })).toThrow(/invalid/);
    expect(() => normalizeHostTransientPrimitives({ type: valid.type, primitives: [{ ...valid.primitives[0],
      lineWidth: 0 }] })).toThrow(/invalid/);
    expect(() => normalizeHostTransientPrimitives({ type: valid.type, primitives: [{ ...valid.primitives[0],
      visible: 'yes' }] })).toThrow(/invalid/);
  });

  it('enforces duplicate-name and aggregate point bounds', () => {
    expect(() => normalizeHostTransientPrimitives({ type: valid.type,
      primitives: [valid.primitives[0], valid.primitives[0]] })).toThrow(/invalid/);
    const point = [0, 0, 0] as const;
    expect(() => normalizeHostTransientPrimitives({ type: valid.type, primitives: [{ ...valid.primitives[0],
      points: new Array(maximumTransientPrimitivePoints + 1).fill(point), lines: [], drawPoints: true }] }))
      .toThrow(/aggregate safety bound/);
  });
});

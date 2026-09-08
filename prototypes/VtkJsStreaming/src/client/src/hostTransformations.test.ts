import { describe, expect, it } from 'vitest';
import { normalizeHostTransformations, toRelativeTransformationMatrix } from './hostTransformations';

const identity = [1, 0, 0, 2, 0, 1, 0, 3, 0, 0, 1, 4, 0, 0, 0, 1];

describe('host transformation state', () => {
  it('accepts and clones bounded affine matrices', () => {
    const source = { type: 'prepomax.transformations.updated-v1', visible: true, matrices: [[...identity]] };
    const state = normalizeHostTransformations(source);
    source.matrices[0]![3] = 99;
    expect(state.matrices[0]![3]).toBe(2);
  });

  it('converts world row-major transforms into floating-origin column-major matrices', () => {
    const worldQuarterTurn = [0, -1, 0, 10, 1, 0, 0, 20, 0, 0, 1, 30, 0, 0, 0, 1];
    expect(Array.from(toRelativeTransformationMatrix(worldQuarterTurn, [1000, 2000, 3000])))
      .toEqual([0, 1, 0, 0, -1, 0, 0, 0, 0, 0, 1, 0, -2990, -980, 30, 1]);
  });

  it('rejects singular, projective, non-finite, and oversized state', () => {
    expect(() => normalizeHostTransformations({ type: 'prepomax.transformations.updated-v1', visible: true,
      matrices: [[0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1]] })).toThrow(/singular/i);
    expect(() => normalizeHostTransformations({ type: 'prepomax.transformations.updated-v1', visible: true,
      matrices: [[...identity.slice(0, 12), 1, 0, 0, 1]] })).toThrow(/affine/i);
    expect(() => normalizeHostTransformations({ type: 'prepomax.transformations.updated-v1', visible: true,
      matrices: [[...identity.slice(0, 15), Number.NaN]] })).toThrow(/malformed/i);
    expect(() => normalizeHostTransformations({ type: 'prepomax.transformations.updated-v1', visible: true,
      matrices: Array.from({ length: 257 }, () => [...identity]) })).toThrow(/bounds/i);
  });
});

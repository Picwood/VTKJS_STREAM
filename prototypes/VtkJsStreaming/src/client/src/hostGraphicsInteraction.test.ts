import { describe, expect, it } from 'vitest';
import { normalizeHostGraphicsInteraction } from './hostGraphicsInteraction';

const valid = {
  type: 'prepomax.graphics-interaction.updated-v1', pointSmoothing: true, lineSmoothing: false,
  primaryHighlightRgb: [1, 0, 0], secondaryHighlightRgb: [0.93, 0.51, 0.93],
  mouseHighlightRgb: [1, 0.65, 0],
} as const;

describe('normalizeHostGraphicsInteraction', () => {
  it('accepts and clones a complete state', () => {
    const normalized = normalizeHostGraphicsInteraction(valid);
    expect(normalized).toEqual(valid);
    expect(normalized.primaryHighlightRgb).not.toBe(valid.primaryHighlightRgb);
  });

  it('rejects invalid flags and colors atomically', () => {
    expect(() => normalizeHostGraphicsInteraction({ ...valid, pointSmoothing: 1 })).toThrow(/invalid/);
    expect(() => normalizeHostGraphicsInteraction({ ...valid, primaryHighlightRgb: [1, 0] })).toThrow(/invalid/);
    expect(() => normalizeHostGraphicsInteraction({ ...valid, secondaryHighlightRgb: [1, -0.1, 0] })).toThrow(/invalid/);
    expect(() => normalizeHostGraphicsInteraction({ ...valid, mouseHighlightRgb: [1, 0, Number.NaN] })).toThrow(/invalid/);
  });
});

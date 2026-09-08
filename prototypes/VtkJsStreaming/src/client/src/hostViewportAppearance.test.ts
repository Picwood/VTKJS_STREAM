import { describe, expect, it } from 'vitest';
import { normalizeHostViewportAppearance } from './hostViewportAppearance';

const valid = {
  type: 'prepomax.viewport-appearance.updated-v1', gradient: true,
  topRgb: [12 / 255, 34 / 255, 56 / 255], bottomRgb: [78 / 255, 90 / 255, 123 / 255],
  ambient: 0.35, diffuse: 0.65, redraw: true,
} as const;

describe('host viewport appearance', () => {
  it('clones a valid bounded state', () => {
    const normalized = normalizeHostViewportAppearance(valid);
    expect(normalized).toEqual(valid);
    expect(normalized.topRgb).not.toBe(valid.topRgb);
    expect(normalized.bottomRgb).not.toBe(valid.bottomRgb);
  });

  it('rejects malformed colors, lighting, and flags', () => {
    expect(() => normalizeHostViewportAppearance({ ...valid, topRgb: [0, 1] })).toThrow(/invalid/);
    expect(() => normalizeHostViewportAppearance({ ...valid, bottomRgb: [0, 0, 2] })).toThrow(/invalid/);
    expect(() => normalizeHostViewportAppearance({ ...valid, ambient: Number.NaN })).toThrow(/invalid/);
    expect(() => normalizeHostViewportAppearance({ ...valid, diffuse: -0.1 })).toThrow(/invalid/);
    expect(() => normalizeHostViewportAppearance({ ...valid, redraw: 1 })).toThrow(/invalid/);
  });
});

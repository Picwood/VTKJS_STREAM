import { describe, expect, it } from 'vitest';
import { getNodeOverlayStride, maximumExactNodeOverlayPoints,
  maximumSampledNodeOverlayPoints } from './selectionOverlay';

describe('adaptive node selection overlays', () => {
  it('keeps overlays exact through one million rendered matches', () => {
    expect(getNodeOverlayStride(0)).toBe(1);
    expect(getNodeOverlayStride(maximumExactNodeOverlayPoints)).toBe(1);
  });

  it('uses a deterministic bounded stride for larger selections', () => {
    expect(getNodeOverlayStride(maximumExactNodeOverlayPoints + 1)).toBe(3);
    expect(getNodeOverlayStride(3_000_000)).toBe(6);
    expect(Math.ceil(8_000_000 / getNodeOverlayStride(8_000_000)))
      .toBeLessThanOrEqual(maximumSampledNodeOverlayPoints);
  });

  it('rejects invalid counts', () => {
    expect(() => getNodeOverlayStride(-1)).toThrow(/invalid/);
    expect(() => getNodeOverlayStride(1.5)).toThrow(/invalid/);
  });
});

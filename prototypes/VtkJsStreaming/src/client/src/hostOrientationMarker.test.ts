import { describe, expect, it } from 'vitest';
import { normalizeHostOrientationMarker } from './hostOrientationMarker';

describe('host orientation marker', () => {
  it('accepts visible and hidden states', () => {
    expect(normalizeHostOrientationMarker({
      type: 'prepomax.orientation-marker.updated-v1', visible: true,
    })).toEqual({ type: 'prepomax.orientation-marker.updated-v1', visible: true });
    expect(normalizeHostOrientationMarker({
      type: 'prepomax.orientation-marker.updated-v1', visible: false,
    }).visible).toBe(false);
  });

  it('rejects malformed state', () => {
    expect(() => normalizeHostOrientationMarker(null)).toThrow(/invalid/);
    expect(() => normalizeHostOrientationMarker({
      type: 'prepomax.orientation-marker.updated-v1', visible: 1,
    })).toThrow(/invalid/);
    expect(() => normalizeHostOrientationMarker({ type: 'wrong', visible: true })).toThrow(/invalid/);
  });
});

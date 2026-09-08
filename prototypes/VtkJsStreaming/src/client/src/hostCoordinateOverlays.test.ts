import { describe, expect, it } from 'vitest';
import { maximumCoordinateAxes, normalizeHostCoordinateOverlays } from './hostCoordinateOverlays';

const valid = {
  type: 'prepomax.coordinate-overlays.updated-v1',
  axes: [{ name: 'CS:X:axis', point: [1, 2, 3], direction: [1, 0, 0], rgba: [1, 0, 0, 1], symbolSize: 24 }],
  captions: [{ name: 'CS:X', text: 'X', position: [1, 2, 3], offsetVector: [28, 0, 0],
    rgba: [1, 0, 0, 1], fontScaleFactor: 1 }],
} as const;

describe('host coordinate-overlay state', () => {
  it('normalizes valid axes and captions', () => {
    const state = normalizeHostCoordinateOverlays(valid);
    expect(state.axes[0]?.name).toBe('CS:X:axis');
    expect(state.captions[0]?.text).toBe('X');
  });

  it('rejects invalid geometry, sizes, colors, and duplicate names', () => {
    expect(() => normalizeHostCoordinateOverlays({ ...valid, axes: [{ ...valid.axes[0], direction: [0, 0, 0] }] })).toThrow();
    expect(() => normalizeHostCoordinateOverlays({ ...valid, axes: [{ ...valid.axes[0], symbolSize: 0 }] })).toThrow();
    expect(() => normalizeHostCoordinateOverlays({ ...valid, captions: [{ ...valid.captions[0], rgba: [2, 0, 0, 1] }] })).toThrow();
    expect(() => normalizeHostCoordinateOverlays({ ...valid, captions: [valid.captions[0], valid.captions[0]] })).toThrow();
  });

  it('enforces collection and text bounds', () => {
    expect(() => normalizeHostCoordinateOverlays({ ...valid,
      axes: new Array(maximumCoordinateAxes + 1).fill(valid.axes[0]) })).toThrow();
    expect(() => normalizeHostCoordinateOverlays({ ...valid,
      captions: [{ ...valid.captions[0], text: 'x'.repeat(16_385) }] })).toThrow();
  });
});

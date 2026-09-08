import { describe, expect, it } from 'vitest';
import { normalizeHostColorBar } from './hostColorBar';

const valid = {
  type: 'prepomax.color-bar.updated-v1', visible: true,
  drawBackground: true, drawBorder: false,
  colors: [[1, 0, 0], [0, 0.5, 1]], labels: ['Front face', 'Back face'],
};

describe('host categorical color bar', () => {
  it('clones ordered color and label state', () => {
    const normalized = normalizeHostColorBar(valid);
    expect(normalized).toEqual(valid);
    expect(normalized.colors).not.toBe(valid.colors);
    expect(normalized.labels).not.toBe(valid.labels);
    expect(normalizeHostColorBar({ ...valid, visible: false, colors: [], labels: [] }).visible).toBe(false);
  });

  it('rejects mismatched, unsafe, oversized, or invalid state', () => {
    expect(() => normalizeHostColorBar({ ...valid, labels: ['one'] })).toThrow(/invalid/);
    expect(() => normalizeHostColorBar({ ...valid, labels: ['bad\n', 'two'] })).toThrow(/invalid/);
    expect(() => normalizeHostColorBar({ ...valid, colors: [[1, 0, 2], [0, 0, 0]] })).toThrow(/invalid/);
    expect(() => normalizeHostColorBar({ ...valid, labels: ['x'.repeat(257), 'two'] })).toThrow(/invalid/);
    expect(() => normalizeHostColorBar({ ...valid, colors: [], labels: [] })).toThrow(/invalid/);
  });
});

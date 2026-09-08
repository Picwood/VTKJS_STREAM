import { describe, expect, it } from 'vitest';
import { normalizeHostScaleWidget } from './hostScaleWidget';

describe('host scale widget', () => {
  it('clones visible and hidden unit state', () => {
    expect(normalizeHostScaleWidget({
      type: 'prepomax.scale-widget.updated-v1', visible: true, unit: 'mm',
    })).toEqual({ type: 'prepomax.scale-widget.updated-v1', visible: true, unit: 'mm' });
    expect(normalizeHostScaleWidget({
      type: 'prepomax.scale-widget.updated-v1', visible: false, unit: '',
    }).visible).toBe(false);
  });

  it('rejects malformed or unsafe state', () => {
    const base = { type: 'prepomax.scale-widget.updated-v1', visible: true, unit: 'm' };
    expect(() => normalizeHostScaleWidget({ ...base, visible: 1 })).toThrow(/invalid/);
    expect(() => normalizeHostScaleWidget({ ...base, unit: 'x'.repeat(65) })).toThrow(/invalid/);
    expect(() => normalizeHostScaleWidget({ ...base, unit: 'mm\nwrong' })).toThrow(/invalid/);
    expect(() => normalizeHostScaleWidget({ ...base, type: 'wrong' })).toThrow(/invalid/);
  });
});

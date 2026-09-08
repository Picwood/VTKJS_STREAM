import { describe, expect, it } from 'vitest';
import { normalizeHostResultWidgetLayout } from './hostResultWidgetLayout';

const valid = {
  type: 'prepomax.result-widget-layout.updated-v1',
  scalarLegend: { top: 20, left: 20 }, colorBar: { top: 20, left: 20 },
  statusBlock: { top: 20, right: 20 },
} as const;

describe('normalizeHostResultWidgetLayout', () => {
  it('accepts and clones bounded pixel offsets', () => {
    const normalized = normalizeHostResultWidgetLayout(valid);
    expect(normalized).toEqual(valid);
    expect(normalized.scalarLegend).not.toBe(valid.scalarLegend);
  });

  it('rejects non-integer, negative, and excessive offsets', () => {
    expect(() => normalizeHostResultWidgetLayout({ ...valid, scalarLegend: { top: 1.5, left: 20 } })).toThrow(/invalid/);
    expect(() => normalizeHostResultWidgetLayout({ ...valid, colorBar: { top: -1, left: 20 } })).toThrow(/invalid/);
    expect(() => normalizeHostResultWidgetLayout({ ...valid, statusBlock: { top: 20, right: 4097 } })).toThrow(/invalid/);
  });
});

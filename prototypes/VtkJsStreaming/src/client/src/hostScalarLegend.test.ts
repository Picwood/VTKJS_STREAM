import { describe, expect, it } from 'vitest';
import { normalizeHostScalarLegend } from './hostScalarLegend';

const valid = {
  type: 'prepomax.scalar-legend.updated-v1', visible: true,
  fieldName: 'Displacements', componentName: 'Magnitude', unit: 'mm', complexComponent: '',
  minMaxType: 'Automatic', numberFormat: 'G4', drawBackground: true, drawBorder: true,
  minimum: 0, maximum: 10, colors: [[0, 0, 1], [1, 0, 0]],
};

describe('host scalar legend', () => {
  it('clones validated legend state and palette tuples', () => {
    const normalized = normalizeHostScalarLegend(valid);
    expect(normalized).toEqual(valid);
    expect(normalized.colors).not.toBe(valid.colors);
  });

  it('rejects malformed text, ranges, formats, and colors', () => {
    expect(() => normalizeHostScalarLegend({ ...valid, numberFormat: 'F4' })).toThrow(/invalid/);
    expect(() => normalizeHostScalarLegend({ ...valid, fieldName: 'bad\u0000text' })).toThrow(/invalid/);
    expect(() => normalizeHostScalarLegend({ ...valid, minimum: 11 })).toThrow(/invalid/);
    expect(() => normalizeHostScalarLegend({ ...valid, colors: [[0, 0, 2], [1, 0, 0]] })).toThrow(/invalid/);
    expect(() => normalizeHostScalarLegend({ ...valid, colors: [[0, 0, 1]] })).toThrow(/invalid/);
  });
});

import { describe, expect, it } from 'vitest';
import { maximumSphereSymbolPoints, normalizeHostSphereSymbols } from './hostSphereSymbols';

const valid = {
  type: 'prepomax.sphere-symbols.updated-v1',
  symbols: [{ name: 'Gravity::sphere', points: [[1, 2, 3], [4, 5, 6]],
    rgba: [0.25, 0.5, 0.75, 1], symbolSize: 24 }],
} as const;

describe('host sphere-symbol state', () => {
  it('clones a valid bounded replacement', () => {
    const symbols = normalizeHostSphereSymbols(valid);
    expect(symbols).toEqual(valid.symbols);
    expect(symbols[0]).not.toBe(valid.symbols[0]);
    expect(symbols[0]?.points[0]).not.toBe(valid.symbols[0].points[0]);
  });

  it('rejects duplicate names and invalid numeric fields', () => {
    expect(() => normalizeHostSphereSymbols({ ...valid,
      symbols: [valid.symbols[0], valid.symbols[0]] })).toThrow(/invalid/);
    expect(() => normalizeHostSphereSymbols({ ...valid,
      symbols: [{ ...valid.symbols[0], points: [[1, Number.NaN, 3]] }] })).toThrow(/invalid/);
    expect(() => normalizeHostSphereSymbols({ ...valid,
      symbols: [{ ...valid.symbols[0], rgba: [0, 0, 2, 1] }] })).toThrow(/invalid/);
    expect(() => normalizeHostSphereSymbols({ ...valid,
      symbols: [{ ...valid.symbols[0], symbolSize: 0 }] })).toThrow(/invalid/);
  });

  it('enforces the aggregate point bound', () => {
    const point = [0, 0, 0] as const;
    expect(() => normalizeHostSphereSymbols({ type: valid.type,
      symbols: [{ ...valid.symbols[0], points: new Array(maximumSphereSymbolPoints + 1).fill(point) }] }))
      .toThrow(/point safety bound/);
  });
});

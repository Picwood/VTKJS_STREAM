import { describe, expect, it } from 'vitest';
import { maximumDirectionalGlyphPoints, normalizeHostDirectionalGlyphs } from './hostDirectionalGlyphs';

const valid = { type: 'prepomax.directional-glyphs.updated-v1', glyphs: [{ name: 'BC:cones',
  kind: 'displacement-cone', points: [[1, 2, 3]], directions: [[1, 0, 0]], rgba: [0.2, 0.4, 0.6, 1],
  symbolSize: 24 }] } as const;

describe('host directional-glyph state', () => {
  it('normalizes a displacement-cone actor', () => {
    expect(normalizeHostDirectionalGlyphs(valid)[0]).toMatchObject({ name: 'BC:cones', kind: 'displacement-cone' });
  });

  it('normalizes the smaller general-cone actor', () => {
    expect(normalizeHostDirectionalGlyphs({ ...valid, glyphs: [{ ...valid.glyphs[0],
      name: 'LineWithArrowCones:cones', kind: 'general-cone' }] })[0]?.kind).toBe('general-cone');
  });

  it('normalizes a rotational-constraint actor', () => {
    expect(normalizeHostDirectionalGlyphs({ ...valid, glyphs: [{ ...valid.glyphs[0],
      name: 'BC:cylinder', kind: 'rotational-constraint' }] })[0]?.kind).toBe('rotational-constraint');
  });

  it('normalizes an oriented arrow with inversion and relative size', () => {
    expect(normalizeHostDirectionalGlyphs({ ...valid, glyphs: [{ ...valid.glyphs[0], name: 'Load:arrow',
      kind: 'oriented-arrow', invert: true, relativeSize: 2.5 }] })[0])
      .toMatchObject({ kind: 'oriented-arrow', invert: true, relativeSize: 2.5 });
  });

  it('normalizes a double arrow without arrow options', () => {
    expect(normalizeHostDirectionalGlyphs({ ...valid, glyphs: [{ ...valid.glyphs[0],
      name: 'Moment:doubleArrow', kind: 'double-arrow' }] })[0]?.kind).toBe('double-arrow');
  });

  it('normalizes a spring while retaining the legacy ignored inversion flag', () => {
    expect(normalizeHostDirectionalGlyphs({ ...valid, glyphs: [{ ...valid.glyphs[0],
      name: 'Constraint:spring', kind: 'oriented-spring', invert: true }] })[0])
      .toMatchObject({ kind: 'oriented-spring', invert: true });
  });

  it('normalizes thermal and flux options', () => {
    expect(normalizeHostDirectionalGlyphs({ ...valid, glyphs: [{ ...valid.glyphs[0],
      name: 'Temperature:thermo', kind: 'oriented-thermal', invert: true }] })[0])
      .toMatchObject({ kind: 'oriented-thermal', invert: true });
    expect(normalizeHostDirectionalGlyphs({ ...valid, glyphs: [{ ...valid.glyphs[0],
      name: 'Flux:flux', kind: 'oriented-flux', center: false, invert: true }] })[0])
      .toMatchObject({ kind: 'oriented-flux', center: false, invert: true });
  });

  it('rejects mismatched, zero, and unsupported direction data', () => {
    expect(() => normalizeHostDirectionalGlyphs({ ...valid, glyphs: [{ ...valid.glyphs[0], directions: [] }] })).toThrow();
    expect(() => normalizeHostDirectionalGlyphs({ ...valid, glyphs: [{ ...valid.glyphs[0], directions: [[0, 0, 0]] }] })).toThrow();
    expect(() => normalizeHostDirectionalGlyphs({ ...valid, glyphs: [{ ...valid.glyphs[0], kind: 'arrow' }] })).toThrow();
    expect(() => normalizeHostDirectionalGlyphs({ ...valid, glyphs: [{ ...valid.glyphs[0],
      kind: 'oriented-arrow', invert: false, relativeSize: 0 }] })).toThrow();
    expect(() => normalizeHostDirectionalGlyphs({ ...valid, glyphs: [{ ...valid.glyphs[0],
      kind: 'oriented-spring' }] })).toThrow();
    expect(() => normalizeHostDirectionalGlyphs({ ...valid, glyphs: [{ ...valid.glyphs[0],
      kind: 'oriented-spring', invert: false, relativeSize: 1 }] })).toThrow();
    expect(() => normalizeHostDirectionalGlyphs({ ...valid, glyphs: [{ ...valid.glyphs[0],
      kind: 'oriented-thermal' }] })).toThrow();
    expect(() => normalizeHostDirectionalGlyphs({ ...valid, glyphs: [{ ...valid.glyphs[0],
      kind: 'oriented-flux', invert: false }] })).toThrow();
  });

  it('rejects invalid names, sizes, colors, duplicates, and aggregate overflow', () => {
    expect(() => normalizeHostDirectionalGlyphs({ ...valid, glyphs: [{ ...valid.glyphs[0], symbolSize: 0 }] })).toThrow();
    expect(() => normalizeHostDirectionalGlyphs({ ...valid, glyphs: [{ ...valid.glyphs[0], rgba: [0, 0, 0, 2] }] })).toThrow();
    expect(() => normalizeHostDirectionalGlyphs({ ...valid, glyphs: [valid.glyphs[0], valid.glyphs[0]] })).toThrow();
    const point = valid.glyphs[0].points[0];
    const direction = valid.glyphs[0].directions[0];
    expect(() => normalizeHostDirectionalGlyphs({ ...valid, glyphs: [{ ...valid.glyphs[0],
      points: new Array(maximumDirectionalGlyphPoints + 1).fill(point),
      directions: new Array(maximumDirectionalGlyphPoints + 1).fill(direction) }] })).toThrow();
  });
});

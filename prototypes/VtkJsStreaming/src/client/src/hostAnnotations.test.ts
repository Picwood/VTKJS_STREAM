import { describe, expect, it } from 'vitest';
import { normalizeHostAnnotations } from './hostAnnotations';

const valid = {
  type: 'prepomax.annotations.updated-v1',
  annotations: [{
    name: 'Measure_Annotation', text: 'Distance: 12 mm', anchorPoint: [1, 2, 3],
    offset: [0, 0],
    drawBackground: true, drawBorder: true, visible: true,
  }],
};

describe('host annotation validation', () => {
  it('accepts and clones bounded finite annotations', () => {
    const normalized = normalizeHostAnnotations(valid);
    expect(normalized).toEqual(valid.annotations);
    expect(normalized).not.toBe(valid.annotations);
    expect(normalized[0]!.anchorPoint).not.toBe(valid.annotations[0]!.anchorPoint);
    expect(normalized[0]!.offset).not.toBe(valid.annotations[0]!.offset);
  });

  it('rejects malformed or excessive display offsets', () => {
    expect(() => normalizeHostAnnotations({ ...valid, annotations: [{ ...valid.annotations[0], offset: [0] }] }))
      .toThrow(/invalid annotation/);
    expect(() => normalizeHostAnnotations({ ...valid, annotations: [{ ...valid.annotations[0], offset: [100_001, 0] }] }))
      .toThrow(/invalid annotation/);
  });

  it('rejects duplicate names and non-finite anchors', () => {
    expect(() => normalizeHostAnnotations({ ...valid, annotations: [valid.annotations[0], valid.annotations[0]] }))
      .toThrow(/invalid annotation/);
    expect(() => normalizeHostAnnotations({ ...valid, annotations: [{ ...valid.annotations[0], anchorPoint: [1, NaN, 3] }] }))
      .toThrow(/invalid annotation/);
  });

  it('rejects oversized individual and aggregate text', () => {
    expect(() => normalizeHostAnnotations({ ...valid, annotations: [{ ...valid.annotations[0], text: 'x'.repeat(65_537) }] }))
      .toThrow(/invalid annotation/);
    const annotations = Array.from({ length: 16 }, (_, index) => ({
      ...valid.annotations[0], name: `annotation-${index}`, text: 'x'.repeat(65_000),
    }));
    expect(() => normalizeHostAnnotations({ ...valid, annotations })).toThrow(/safety bound/);
  });
});

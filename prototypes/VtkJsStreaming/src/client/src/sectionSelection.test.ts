import { describe, expect, it } from 'vitest';
import { clipPolygonToSection } from './sectionSelection';

describe('clipPolygonToSection', () => {
  it('retains the vtk.js non-negative half-space and interpolates crossing edges', () => {
    const result = clipPolygonToSection(
      [[-1, 0, 0], [1, 0, 0], [1, 2, 0]], [0, 0, 0], [1, 0, 0]);

    expect(result).toHaveLength(4);
    expect(result).toEqual([[0, 1, 0], [0, 0, 0], [1, 0, 0], [1, 2, 0]]);
  });

  it('rejects a fully clipped polygon and retains points on the plane', () => {
    expect(clipPolygonToSection(
      [[-2, 0, 0], [-1, 0, 0], [-1, 1, 0]], [0, 0, 0], [1, 0, 0])).toEqual([]);
    expect(clipPolygonToSection(
      [[0, 0, 0], [1, 0, 0], [0, 1, 0]], [0, 0, 0], [1, 0, 0])).toEqual(
      [[0, 0, 0], [1, 0, 0], [0, 1, 0]]);
  });
});

import { describe, expect, it } from 'vitest';
import { normalizeHostActorContours } from './hostActorContours';

describe('host actor contours', () => {
  it('clones a valid bounded replacement state', () => {
    const actors = [{ name: 'part-a', colorContours: false }, { name: 'part-b', colorContours: true }];
    const normalized = normalizeHostActorContours({ type: 'prepomax.actor-contours.updated-v1', actors });
    expect(normalized).toEqual(actors);
    expect(normalized).not.toBe(actors);
    expect(normalized[0]).not.toBe(actors[0]);
  });

  it('rejects malformed, duplicate, and unbounded entries', () => {
    const wrap = (actors: unknown[]) => ({ type: 'prepomax.actor-contours.updated-v1', actors });
    expect(() => normalizeHostActorContours(wrap([{ name: '', colorContours: true }]))).toThrow(/invalid/);
    expect(() => normalizeHostActorContours(wrap([
      { name: 'part', colorContours: true }, { name: 'part', colorContours: false },
    ]))).toThrow(/invalid/);
    expect(() => normalizeHostActorContours(wrap([{ name: 'part', colorContours: 1 }]))).toThrow(/invalid/);
    expect(() => normalizeHostActorContours(wrap(Array.from({ length: 4097 }, (_, index) => ({
      name: `part-${index}`, colorContours: true,
    }))))).toThrow(/invalid/);
  });
});

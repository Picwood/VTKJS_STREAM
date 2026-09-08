import { describe, expect, it } from 'vitest';
import { normalizeHostActorHighlights } from './hostActorHighlights';

describe('host actor highlights', () => {
  it('clones a valid bounded replacement state, including clear', () => {
    const actorNames = ['part-a', 'part-b'];
    const normalized = normalizeHostActorHighlights({
      type: 'prepomax.actor-highlights.updated-v1', actorNames,
    });
    expect(normalized).toEqual(actorNames);
    expect(normalized).not.toBe(actorNames);
    expect(normalizeHostActorHighlights({
      type: 'prepomax.actor-highlights.updated-v1', actorNames: [],
    })).toEqual([]);
  });

  it('rejects malformed, duplicate, and unbounded actor names', () => {
    const wrap = (actorNames: unknown[]) => ({ type: 'prepomax.actor-highlights.updated-v1', actorNames });
    expect(() => normalizeHostActorHighlights(wrap(['']))).toThrow(/invalid/);
    expect(() => normalizeHostActorHighlights(wrap(['part', 'part']))).toThrow(/invalid/);
    expect(() => normalizeHostActorHighlights(wrap([1]))).toThrow(/invalid/);
    expect(() => normalizeHostActorHighlights(wrap(Array.from({ length: 4097 }, (_, index) =>
      `part-${index}`)))).toThrow(/invalid/);
  });
});

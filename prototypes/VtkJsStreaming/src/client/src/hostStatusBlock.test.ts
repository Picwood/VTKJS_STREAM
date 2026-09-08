import { describe, expect, it } from 'vitest';
import { normalizeHostStatusBlock } from './hostStatusBlock';

const valid = {
  type: 'prepomax.status-block.updated-v1', visible: true,
  drawBackground: true, drawBorder: false,
  lines: ['Name: rotor.frd   Date: 08/09/2026   Time: 14:20:00',
    'Step: #2   Mode: #5   Frequency: 120 Hz',
    'Deformation variable: DISP   Deformation scale factor: 2.5'],
} as const;

describe('normalizeHostStatusBlock', () => {
  it('accepts and clones a complete bounded state', () => {
    const normalized = normalizeHostStatusBlock(valid);
    expect(normalized).toEqual(valid);
    expect(normalized.lines).not.toBe(valid.lines);
    expect(normalizeHostStatusBlock({ ...valid, visible: false, lines: [] }).visible).toBe(false);
  });

  it('rejects incomplete, unsafe, and inconsistent states', () => {
    expect(() => normalizeHostStatusBlock({ ...valid, visible: 'yes' })).toThrow(/invalid/);
    expect(() => normalizeHostStatusBlock({ ...valid, lines: ['one', 'two'] })).toThrow(/invalid/);
    expect(() => normalizeHostStatusBlock({ ...valid, lines: [...valid.lines, 'four', 'five'] })).toThrow(/invalid/);
    expect(() => normalizeHostStatusBlock({ ...valid, lines: ['bad\n', ...valid.lines.slice(1)] })).toThrow(/invalid/);
    expect(() => normalizeHostStatusBlock({ ...valid, lines: ['x'.repeat(1025), ...valid.lines.slice(1)] })).toThrow(/invalid/);
  });
});

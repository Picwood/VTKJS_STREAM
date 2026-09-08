import type { HostDirectionalGlyph, HostDirectionalGlyphStateMessage } from './protocol';

export const maximumDirectionalGlyphActors = 4096;
export const maximumDirectionalGlyphPoints = 100_000;

function vector(value: unknown, nonzero = false): value is readonly [number, number, number] {
  return Array.isArray(value) && value.length === 3
    && value.every((component) => typeof component === 'number' && Number.isFinite(component)
      && Math.abs(component) <= 1e150)
    && (!nonzero || value.some((component) => Math.abs(component as number) > 1e-12));
}

export function normalizeHostDirectionalGlyphs(value: unknown): readonly HostDirectionalGlyph[] {
  const message = value as Partial<HostDirectionalGlyphStateMessage> | null;
  if (!message || message.type !== 'prepomax.directional-glyphs.updated-v1'
    || !Array.isArray(message.glyphs) || message.glyphs.length > maximumDirectionalGlyphActors)
    throw new Error('The embedded host sent an invalid directional-glyph update.');
  const names = new Set<string>();
  let pointCount = 0;
  for (const glyph of message.glyphs as readonly Partial<HostDirectionalGlyph>[]) {
    if (typeof glyph.name !== 'string' || glyph.name.length === 0 || glyph.name.length > 1024
      || names.has(glyph.name) || (glyph.kind !== 'displacement-cone' && glyph.kind !== 'general-cone'
        && glyph.kind !== 'rotational-constraint'
        && glyph.kind !== 'oriented-arrow' && glyph.kind !== 'double-arrow' && glyph.kind !== 'oriented-spring'
        && glyph.kind !== 'oriented-thermal' && glyph.kind !== 'oriented-flux')
      || !Array.isArray(glyph.points) || glyph.points.length === 0
      || !Array.isArray(glyph.directions) || glyph.directions.length !== glyph.points.length
      || !glyph.points.every((point) => vector(point)) || !glyph.directions.every((direction) => vector(direction, true))
      || !Array.isArray(glyph.rgba) || glyph.rgba.length !== 4
      || !glyph.rgba.every((component) => typeof component === 'number' && Number.isFinite(component)
        && component >= 0 && component <= 1)
      || typeof glyph.symbolSize !== 'number' || !Number.isFinite(glyph.symbolSize)
      || glyph.symbolSize <= 0 || glyph.symbolSize > 1000
      || (glyph.kind === 'oriented-arrow' && (typeof glyph.invert !== 'boolean'
        || typeof glyph.relativeSize !== 'number' || !Number.isFinite(glyph.relativeSize)
        || glyph.relativeSize <= 0 || glyph.relativeSize > 1000 || glyph.center !== undefined))
      || (glyph.kind === 'oriented-spring' && (typeof glyph.invert !== 'boolean'
        || glyph.center !== undefined || glyph.relativeSize !== undefined))
      || (glyph.kind === 'oriented-thermal' && (typeof glyph.invert !== 'boolean'
        || glyph.center !== undefined || glyph.relativeSize !== undefined))
      || (glyph.kind === 'oriented-flux' && (typeof glyph.invert !== 'boolean'
        || typeof glyph.center !== 'boolean' || glyph.relativeSize !== undefined))
      || (glyph.kind !== 'oriented-arrow' && glyph.kind !== 'oriented-spring'
        && glyph.kind !== 'oriented-thermal' && glyph.kind !== 'oriented-flux'
        && (glyph.invert !== undefined || glyph.center !== undefined || glyph.relativeSize !== undefined)))
      throw new Error('The embedded host sent an invalid directional-glyph update.');
    names.add(glyph.name);
    pointCount += glyph.points.length;
  }
  if (pointCount > maximumDirectionalGlyphPoints)
    throw new Error('The embedded host sent too many directional-glyph points.');
  return (message.glyphs as readonly HostDirectionalGlyph[]).map((glyph) => ({ ...glyph,
    points: glyph.points.map((point) => [...point]),
    directions: glyph.directions.map((direction) => [...direction]), rgba: [...glyph.rgba] }));
}

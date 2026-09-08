import type { HostViewportAppearanceMessage } from './protocol';

export function normalizeHostViewportAppearance(value: unknown): HostViewportAppearanceMessage {
  const message = value as Partial<HostViewportAppearanceMessage> | null;
  if (!message || message.type !== 'prepomax.viewport-appearance.updated-v1'
    || typeof message.gradient !== 'boolean' || typeof message.redraw !== 'boolean'
    || !isRgb(message.topRgb) || !isRgb(message.bottomRgb)
    || !isUnitValue(message.ambient) || !isUnitValue(message.diffuse))
    throw new Error('The embedded host sent an invalid viewport-appearance update.');
  return {
    type: message.type,
    gradient: message.gradient,
    topRgb: [...message.topRgb],
    bottomRgb: [...message.bottomRgb],
    ambient: message.ambient,
    diffuse: message.diffuse,
    redraw: message.redraw,
  };
}

function isRgb(value: unknown): value is readonly [number, number, number] {
  return Array.isArray(value) && value.length === 3 && value.every(isUnitValue);
}

function isUnitValue(value: unknown): value is number {
  return typeof value === 'number' && Number.isFinite(value) && value >= 0 && value <= 1;
}

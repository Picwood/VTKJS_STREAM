import type { HostCaption, HostCoordinateAxis, HostCoordinateOverlayStateMessage } from './protocol';

export const maximumCoordinateAxes = 4096;
export const maximumCaptions = 4096;

function vector(value: unknown, nonzero = false): value is readonly [number, number, number] {
  return Array.isArray(value) && value.length === 3
    && value.every((component) => typeof component === 'number' && Number.isFinite(component)
      && Math.abs(component) <= 1e150)
    && (!nonzero || value.some((component) => Math.abs(component as number) > 1e-12));
}

function rgba(value: unknown): value is readonly [number, number, number, number] {
  return Array.isArray(value) && value.length === 4
    && value.every((component) => typeof component === 'number' && Number.isFinite(component)
      && component >= 0 && component <= 1);
}

function name(value: unknown): value is string {
  return typeof value === 'string' && value.length > 0 && value.length <= 1024;
}

export function normalizeHostCoordinateOverlays(value: unknown): {
  axes: readonly HostCoordinateAxis[]; captions: readonly HostCaption[];
} {
  const message = value as Partial<HostCoordinateOverlayStateMessage> | null;
  if (!message || message.type !== 'prepomax.coordinate-overlays.updated-v1'
    || !Array.isArray(message.axes) || message.axes.length > maximumCoordinateAxes
    || !Array.isArray(message.captions) || message.captions.length > maximumCaptions)
    throw new Error('The embedded host sent an invalid coordinate-overlay update.');
  const names = new Set<string>();
  for (const axis of message.axes as readonly Partial<HostCoordinateAxis>[]) {
    if (!name(axis.name) || names.has(axis.name) || !vector(axis.point) || !vector(axis.direction, true)
      || !rgba(axis.rgba) || typeof axis.symbolSize !== 'number' || !Number.isFinite(axis.symbolSize)
      || axis.symbolSize <= 0 || axis.symbolSize > 1000)
      throw new Error('The embedded host sent an invalid coordinate-overlay update.');
    names.add(axis.name);
  }
  names.clear();
  for (const caption of message.captions as readonly Partial<HostCaption>[]) {
    if (!name(caption.name) || names.has(caption.name) || typeof caption.text !== 'string'
      || caption.text.length > 16_384 || !vector(caption.position)
      || (caption.offsetVector !== null && !vector(caption.offsetVector)) || !rgba(caption.rgba)
      || typeof caption.fontScaleFactor !== 'number' || !Number.isFinite(caption.fontScaleFactor)
      || caption.fontScaleFactor <= 0 || caption.fontScaleFactor > 100)
      throw new Error('The embedded host sent an invalid coordinate-overlay update.');
    names.add(caption.name);
  }
  return {
    axes: (message.axes as readonly HostCoordinateAxis[]).map((axis) => ({ ...axis,
      point: [...axis.point], direction: [...axis.direction], rgba: [...axis.rgba] })),
    captions: (message.captions as readonly HostCaption[]).map((caption) => ({ ...caption,
      position: [...caption.position], offsetVector: caption.offsetVector ? [...caption.offsetVector] : null,
      rgba: [...caption.rgba] })),
  };
}

import type { HostTransientPrimitive, HostTransientPrimitiveStateMessage } from './protocol';

export const maximumTransientPrimitiveActors = 4096;
export const maximumTransientPrimitivePoints = 500_000;
export const maximumTransientPrimitiveLines = 750_000;

export function normalizeHostTransientPrimitives(value: unknown): readonly HostTransientPrimitive[] {
  const message = value as Partial<HostTransientPrimitiveStateMessage> | null;
  if (!message || message.type !== 'prepomax.transient-primitives.updated-v1'
    || !Array.isArray(message.primitives)
    || message.primitives.length > maximumTransientPrimitiveActors)
    throw new Error('The embedded host sent an invalid transient-primitive update.');
  const names = new Set<string>();
  let totalPoints = 0;
  let totalLines = 0;
  for (const primitive of message.primitives as readonly Partial<HostTransientPrimitive>[]) {
    if (!primitive || typeof primitive.name !== 'string' || primitive.name.length === 0
      || primitive.name.length > 1024 || names.has(primitive.name)
      || !Array.isArray(primitive.points) || primitive.points.length === 0
      || primitive.points.some((point) => !Array.isArray(point) || point.length !== 3
        || point.some((coordinate) => typeof coordinate !== 'number' || !Number.isFinite(coordinate)
          || Math.abs(coordinate) > 1e150))
      || !Array.isArray(primitive.lines)
      || primitive.lines.some((line) => !Array.isArray(line) || line.length !== 2
        || line.some((index) => !Number.isInteger(index) || index < 0 || index >= primitive.points!.length))
      || typeof primitive.drawPoints !== 'boolean'
      || (!primitive.drawPoints && primitive.lines.length === 0)
      || !Array.isArray(primitive.rgba) || primitive.rgba.length !== 4
      || primitive.rgba.some((component) => typeof component !== 'number' || !Number.isFinite(component)
        || component < 0 || component > 1)
      || typeof primitive.pointSize !== 'number' || !Number.isFinite(primitive.pointSize)
      || primitive.pointSize <= 0 || primitive.pointSize > 1000
      || typeof primitive.lineWidth !== 'number' || !Number.isFinite(primitive.lineWidth)
      || primitive.lineWidth <= 0 || primitive.lineWidth > 1000
      || (primitive.visible !== undefined && typeof primitive.visible !== 'boolean'))
      throw new Error('The embedded host sent an invalid transient-primitive update.');
    names.add(primitive.name);
    totalPoints += primitive.points.length;
    totalLines += primitive.lines.length;
  }
  if (totalPoints > maximumTransientPrimitivePoints || totalLines > maximumTransientPrimitiveLines)
    throw new Error('The embedded host transient primitives exceed their aggregate safety bound.');
  return (message.primitives as readonly HostTransientPrimitive[]).map((primitive) => ({
    ...primitive,
    points: primitive.points.map((point) => [...point] as [number, number, number]),
    lines: primitive.lines.map((line) => [...line] as [number, number]),
    rgba: [...primitive.rgba] as [number, number, number, number],
    visible: primitive.visible !== false,
  }));
}

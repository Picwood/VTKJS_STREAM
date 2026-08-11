import type { HostAnnotation, HostAnnotationStateMessage } from './protocol';

export function normalizeHostAnnotations(value: unknown): readonly HostAnnotation[] {
  const message = value as Partial<HostAnnotationStateMessage> | null;
  if (!message || message.type !== 'prepomax.annotations.updated-v1'
    || !Array.isArray(message.annotations) || message.annotations.length > 4096) {
    throw new Error('The embedded host sent an invalid annotation update.');
  }
  const names = new Set<string>();
  let textLength = 0;
  for (const annotation of message.annotations as readonly Partial<HostAnnotation>[]) {
    if (!annotation || typeof annotation.name !== 'string' || annotation.name.length === 0 || annotation.name.length > 1024
      || names.has(annotation.name) || typeof annotation.text !== 'string' || annotation.text.length > 65_536
      || !Array.isArray(annotation.anchorPoint) || annotation.anchorPoint.length !== 3
      || annotation.anchorPoint.some((coordinate) => typeof coordinate !== 'number' || !Number.isFinite(coordinate))
      || !Array.isArray(annotation.offset) || annotation.offset.length !== 2
      || annotation.offset.some((coordinate) => typeof coordinate !== 'number' || !Number.isFinite(coordinate)
        || Math.abs(coordinate) > 100_000)
      || typeof annotation.drawBackground !== 'boolean' || typeof annotation.drawBorder !== 'boolean'
      || typeof annotation.visible !== 'boolean') {
      throw new Error('The embedded host sent an invalid annotation update.');
    }
    names.add(annotation.name);
    textLength += annotation.text.length;
  }
  if (textLength > 1_000_000) throw new Error('The embedded host annotation text exceeds its safety bound.');
  return (message.annotations as readonly HostAnnotation[]).map((annotation) => ({
    ...annotation,
    anchorPoint: [...annotation.anchorPoint] as [number, number, number],
    offset: [...annotation.offset] as [number, number],
  }));
}

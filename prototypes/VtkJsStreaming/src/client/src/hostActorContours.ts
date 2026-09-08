import type { HostActorContour, HostActorContourStateMessage } from './protocol';

export function normalizeHostActorContours(value: unknown): readonly HostActorContour[] {
  const message = value as Partial<HostActorContourStateMessage> | null;
  if (!message || message.type !== 'prepomax.actor-contours.updated-v1'
    || !Array.isArray(message.actors) || message.actors.length > 4096) {
    throw new Error('The embedded host sent an invalid actor-contour state.');
  }
  const names = new Set<string>();
  return message.actors.map((candidate) => {
    const actor = candidate as Partial<HostActorContour> | null;
    if (!actor || typeof actor.name !== 'string' || actor.name.length === 0 || actor.name.length > 1024
      || typeof actor.colorContours !== 'boolean' || names.has(actor.name)) {
      throw new Error('The embedded host sent an invalid actor-contour state.');
    }
    names.add(actor.name);
    return { name: actor.name, colorContours: actor.colorContours };
  });
}

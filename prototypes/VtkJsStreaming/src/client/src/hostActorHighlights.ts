import type { HostActorHighlightStateMessage } from './protocol';

export function normalizeHostActorHighlights(value: unknown): readonly string[] {
  const message = value as Partial<HostActorHighlightStateMessage> | null;
  if (!message || message.type !== 'prepomax.actor-highlights.updated-v1'
    || !Array.isArray(message.actorNames) || message.actorNames.length > 4096) {
    throw new Error('The embedded host sent an invalid actor-highlight state.');
  }
  const names = new Set<string>();
  for (const name of message.actorNames) {
    if (typeof name !== 'string' || name.length === 0 || name.length > 1024 || names.has(name)) {
      throw new Error('The embedded host sent an invalid actor-highlight state.');
    }
    names.add(name);
  }
  return [...names];
}

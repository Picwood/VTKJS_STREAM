import type { HostOrientationMarkerMessage } from './protocol';

export function normalizeHostOrientationMarker(value: unknown): HostOrientationMarkerMessage {
  const message = value as Partial<HostOrientationMarkerMessage> | null;
  if (!message || message.type !== 'prepomax.orientation-marker.updated-v1'
    || typeof message.visible !== 'boolean')
    throw new Error('The embedded host sent an invalid orientation-marker update.');
  return { type: message.type, visible: message.visible };
}

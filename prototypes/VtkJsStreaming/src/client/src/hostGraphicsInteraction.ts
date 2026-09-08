import type { HostGraphicsInteractionMessage } from './protocol';

export function normalizeHostGraphicsInteraction(value: unknown): HostGraphicsInteractionMessage {
  const message = value as Partial<HostGraphicsInteractionMessage> | null;
  if (!message || message.type !== 'prepomax.graphics-interaction.updated-v1'
      || typeof message.pointSmoothing !== 'boolean' || typeof message.lineSmoothing !== 'boolean'
      || !validRgb(message.primaryHighlightRgb) || !validRgb(message.secondaryHighlightRgb)
      || !validRgb(message.mouseHighlightRgb))
    throw new Error('PrePoMax graphics-interaction state is invalid.');
  return {
    ...message,
    primaryHighlightRgb: [...message.primaryHighlightRgb],
    secondaryHighlightRgb: [...message.secondaryHighlightRgb],
    mouseHighlightRgb: [...message.mouseHighlightRgb],
  } as HostGraphicsInteractionMessage;
}

function validRgb(value: unknown): value is readonly [number, number, number] {
  return Array.isArray(value) && value.length === 3
    && value.every((channel) => typeof channel === 'number' && Number.isFinite(channel)
      && channel >= 0 && channel <= 1);
}

import type { HostColorBarMessage } from './protocol';

const unsafeText = /[\u0000-\u001f\u007f]/u;

export function normalizeHostColorBar(value: unknown): HostColorBarMessage {
  const message = value as Partial<HostColorBarMessage> | null;
  const colors = message?.colors;
  const labels = message?.labels;
  const validColors = Array.isArray(colors) && colors.length <= 4096
    && colors.every((color) => Array.isArray(color) && color.length === 3
      && color.every((channel) => typeof channel === 'number' && Number.isFinite(channel)
        && channel >= 0 && channel <= 1));
  const validLabels = Array.isArray(labels) && labels.length <= 4096
    && labels.every((label) => typeof label === 'string' && label.length <= 256
      && !unsafeText.test(label));
  if (!message || message.type !== 'prepomax.color-bar.updated-v1'
    || typeof message.visible !== 'boolean'
    || typeof message.drawBackground !== 'boolean' || typeof message.drawBorder !== 'boolean'
    || !validColors || !validLabels || colors!.length !== labels!.length
    || (message.visible && colors!.length === 0))
    throw new Error('The embedded host sent an invalid categorical color-bar update.');
  return {
    type: message.type, visible: message.visible,
    drawBackground: message.drawBackground, drawBorder: message.drawBorder,
    colors: colors!.map((color) => [color[0], color[1], color[2]] as const),
    labels: [...labels!],
  };
}

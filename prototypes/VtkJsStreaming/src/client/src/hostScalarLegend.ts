import type { HostScalarLegendMessage } from './protocol';

const unsafeText = /[\u0000-\u0008\u000b\u000c\u000e-\u001f\u007f]/u;

export function normalizeHostScalarLegend(value: unknown): HostScalarLegendMessage {
  const message = value as Partial<HostScalarLegendMessage> | null;
  const texts = message
    ? [message.fieldName, message.componentName, message.unit, message.complexComponent, message.minMaxType]
    : [];
  const validTexts = texts.length === 5 && texts.every((text) => typeof text === 'string'
    && text.length <= 256 && !unsafeText.test(text));
  const colors = message?.colors;
  const validColors = Array.isArray(colors) && colors.length >= 2 && colors.length <= 259
    && colors.every((color) => Array.isArray(color) && color.length === 3
      && color.every((channel) => typeof channel === 'number' && Number.isFinite(channel)
        && channel >= 0 && channel <= 1));
  if (!message || message.type !== 'prepomax.scalar-legend.updated-v1'
    || typeof message.visible !== 'boolean' || !validTexts
    || !/^[GE][1-8]$/u.test(message.numberFormat ?? '')
    || typeof message.drawBackground !== 'boolean' || typeof message.drawBorder !== 'boolean'
    || typeof message.minimum !== 'number' || !Number.isFinite(message.minimum)
    || typeof message.maximum !== 'number' || !Number.isFinite(message.maximum)
    || message.maximum < message.minimum || !validColors)
    throw new Error('The embedded host sent an invalid scalar-legend update.');
  return {
    type: message.type, visible: message.visible,
    fieldName: message.fieldName!, componentName: message.componentName!, unit: message.unit!,
    complexComponent: message.complexComponent!, minMaxType: message.minMaxType!,
    numberFormat: message.numberFormat!, drawBackground: message.drawBackground,
    drawBorder: message.drawBorder, minimum: message.minimum, maximum: message.maximum,
    colors: colors!.map((color) => [color[0], color[1], color[2]] as const),
  };
}

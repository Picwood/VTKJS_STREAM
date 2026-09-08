import type { HostScaleWidgetMessage } from './protocol';

export function normalizeHostScaleWidget(value: unknown): HostScaleWidgetMessage {
  const message = value as Partial<HostScaleWidgetMessage> | null;
  if (!message || message.type !== 'prepomax.scale-widget.updated-v1'
    || typeof message.visible !== 'boolean' || typeof message.unit !== 'string'
    || message.unit.length > 64 || /[\u0000-\u001f\u007f]/u.test(message.unit))
    throw new Error('The embedded host sent an invalid scale-widget update.');
  return { type: message.type, visible: message.visible, unit: message.unit };
}

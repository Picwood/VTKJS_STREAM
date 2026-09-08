import type { HostResultWidgetLayoutMessage } from './protocol';

export function normalizeHostResultWidgetLayout(value: unknown): HostResultWidgetLayoutMessage {
  const message = value as Partial<HostResultWidgetLayoutMessage> | null;
  if (!message || message.type !== 'prepomax.result-widget-layout.updated-v1'
      || !validPoint(message.scalarLegend, 'left')
      || !validPoint(message.colorBar, 'left')
      || !validPoint(message.statusBlock, 'right'))
    throw new Error('PrePoMax result-widget layout is invalid.');
  return {
    type: message.type,
    scalarLegend: { ...message.scalarLegend },
    colorBar: { ...message.colorBar },
    statusBlock: { ...message.statusBlock },
  } as HostResultWidgetLayoutMessage;
}

function validPoint(value: unknown, horizontal: 'left' | 'right'): boolean {
  const point = value as Record<string, unknown> | null;
  return !!point && validOffset(point.top) && validOffset(point[horizontal]);
}

function validOffset(value: unknown): boolean {
  return typeof value === 'number' && Number.isInteger(value) && value >= 0 && value <= 4096;
}

import type { HostStatusBlockMessage } from './protocol';

const MAX_LINE_LENGTH = 1024;

export function normalizeHostStatusBlock(value: unknown): HostStatusBlockMessage {
  const message = value as Partial<HostStatusBlockMessage> | null;
  if (!message || message.type !== 'prepomax.status-block.updated-v1'
      || typeof message.visible !== 'boolean'
      || typeof message.drawBackground !== 'boolean'
      || typeof message.drawBorder !== 'boolean'
      || !Array.isArray(message.lines) || message.lines.length > 4
      || message.lines.some((line) => typeof line !== 'string' || line.length > MAX_LINE_LENGTH
        || [...line].some((character) => isForbiddenControl(character)))) {
    throw new Error('PrePoMax status-block state is invalid.');
  }
  if (message.visible && (message.lines.length < 3 || message.lines.length > 4))
    throw new Error('PrePoMax status-block state is invalid.');
  return { ...message, lines: [...message.lines] } as HostStatusBlockMessage;
}

function isForbiddenControl(character: string): boolean {
  const code = character.codePointAt(0)!;
  return code < 0x20 || (code >= 0x7f && code <= 0x9f);
}

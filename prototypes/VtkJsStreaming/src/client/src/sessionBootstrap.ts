import type { SessionCreated } from './protocol';

export interface HostSessionBootstrap extends SessionCreated {
  readonly type: 'prepomax.session.bootstrap-v1';
}

const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const tokenPattern = /^[A-Za-z0-9_-]{43}$/;

export function normalizeHostSessionBootstrap(value: unknown): HostSessionBootstrap {
  const message = value as Partial<Record<keyof HostSessionBootstrap, unknown>> | null;
  if (!message || message.type !== 'prepomax.session.bootstrap-v1'
    || typeof message.sessionId !== 'string' || !uuidPattern.test(message.sessionId)
    || typeof message.sceneId !== 'string' || !uuidPattern.test(message.sceneId)
    || typeof message.accessToken !== 'string' || !tokenPattern.test(message.accessToken)) {
    throw new Error('The embedded host sent an invalid session bootstrap.');
  }
  return message as HostSessionBootstrap;
}

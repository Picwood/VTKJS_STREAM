import { describe, expect, it } from 'vitest';
import { normalizeHostSessionBootstrap } from './sessionBootstrap';

const valid = {
  type: 'prepomax.session.bootstrap-v1',
  sessionId: 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',
  sceneId: '10000000-0000-0000-0000-000000000001',
  accessToken: 'A'.repeat(43),
};

describe('host session bootstrap', () => {
  it('accepts the bounded bootstrap envelope', () => {
    expect(normalizeHostSessionBootstrap(valid)).toEqual(valid);
  });

  it.each([
    null,
    { ...valid, type: 'prepomax.session.bootstrap-v2' },
    { ...valid, sessionId: 'not-a-uuid' },
    { ...valid, sceneId: 'not-a-uuid' },
    { ...valid, accessToken: 'short' },
    { ...valid, accessToken: `${'A'.repeat(42)}+` },
  ])('rejects an invalid envelope', (message) => {
    expect(() => normalizeHostSessionBootstrap(message)).toThrow('invalid session bootstrap');
  });
});

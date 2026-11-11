import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { nextStableFrame, setHostVisible } from './stableFrame';

let callbacks: Map<number, FrameRequestCallback>;
let sequence: number;
beforeEach(() => {
  vi.useFakeTimers();
  setHostVisible(true);
  callbacks = new Map();
  sequence = 0;
  vi.stubGlobal('requestAnimationFrame', (callback: FrameRequestCallback) => {
    callbacks.set(++sequence, callback);
    return sequence;
  });
  vi.stubGlobal('cancelAnimationFrame', (id: number) => callbacks.delete(id));
});
afterEach(() => { vi.useRealTimers(); vi.unstubAllGlobals(); });
function frame() {
  const pending = [...callbacks.values()];
  callbacks.clear();
  pending.forEach(callback => callback(0));
}
it('requires two callbacks and removes the deadline after success', async () => {
  let complete = false;
  const pending = nextStableFrame().then(() => { complete = true; });
  frame();
  await Promise.resolve();
  expect(complete).toBe(false);
  frame();
  await pending;
  expect(complete).toBe(true);
  expect(vi.getTimerCount()).toBe(0);
  expect(callbacks.size).toBe(0);
});
it.each([0, 1])('rejects when animation stops after %i callbacks', async count => {
  const pending = nextStableFrame();
  const rejected = expect(pending).rejects.toMatchObject({ code: 'frame-timeout' });
  if (count) frame();
  const late = [...callbacks.values()];
  await vi.advanceTimersByTimeAsync(10_000);
  await rejected;
  late.forEach(callback => callback(0));
  expect(callbacks.size).toBe(0);
  expect(vi.getTimerCount()).toBe(0);
});
it('cancels a superseded scene between callbacks', async () => {
  const controller = new AbortController();
  const pending = nextStableFrame(controller.signal);
  const rejected = expect(pending).rejects.toBe('superseded');
  frame();
  controller.abort('superseded');
  await rejected;
  expect(callbacks.size).toBe(0);
  expect(vi.getTimerCount()).toBe(0);
});
it('does not schedule work for an already cancelled scene', async () => {
  await expect(nextStableFrame(AbortSignal.abort('closed'))).rejects.toBe('closed');
  expect(callbacks.size).toBe(0);
  expect(vi.getTimerCount()).toBe(0);
});

it('does not time out while the native surface is covered, then completes real frames', async () => {
  setHostVisible(false);
  let complete = false;
  const pending = nextStableFrame().then(() => { complete = true; });
  await vi.advanceTimersByTimeAsync(30_000);
  expect(complete).toBe(false);
  expect(vi.getTimerCount()).toBe(0);
  setHostVisible(true);
  frame(); frame();
  await pending;
  expect(complete).toBe(true);
  expect(vi.getTimerCount()).toBe(0);
});
it('retains the exposed time budget through occlusion instead of resetting it', async () => {
  const pending = nextStableFrame();
  const rejected = expect(pending).rejects.toMatchObject({ code: 'frame-timeout' });
  await vi.advanceTimersByTimeAsync(4000);
  setHostVisible(false);
  await vi.advanceTimersByTimeAsync(30_000);
  setHostVisible(true);
  await vi.advanceTimersByTimeAsync(6000);
  await rejected;
});
it('cancels and removes visibility listeners while covered', async () => {
  setHostVisible(false);
  const controller = new AbortController();
  const pending = nextStableFrame(controller.signal);
  const rejected = expect(pending).rejects.toBe('closed');
  controller.abort('closed');
  await rejected;
  setHostVisible(true);
  expect(vi.getTimerCount()).toBe(0);
  expect(callbacks.size).toBe(0);
});

it('also pauses for browser visibility changes when no native host is present', async () => {
  const page = Object.assign(new EventTarget(), { hidden: false });
  vi.stubGlobal('document', page);
  const pending = nextStableFrame();
  await vi.advanceTimersByTimeAsync(1000);
  page.hidden = true;
  page.dispatchEvent(new Event('visibilitychange'));
  await vi.advanceTimersByTimeAsync(30_000);
  expect(vi.getTimerCount()).toBe(0);
  page.hidden = false;
  page.dispatchEvent(new Event('visibilitychange'));
  frame(); frame();
  await pending;
  expect(vi.getTimerCount()).toBe(0);
});

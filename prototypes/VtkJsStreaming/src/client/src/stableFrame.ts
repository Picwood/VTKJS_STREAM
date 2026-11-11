let hostVisible = true;
const visibilityListeners = new Set<() => void>();

/** Native surface occlusion may not be reflected by document.hidden (GTK/Wayland). */
export function setHostVisible(visible: boolean): void {
  if (hostVisible === visible) return;
  hostVisible = visible;
  visibilityListeners.forEach(listener => listener());
}

/** Require two real animation callbacks; count only exposed time toward the deadline. */
export function nextStableFrame(signal?: AbortSignal): Promise<void> {
  return new Promise<void>((resolve, reject) => {
    if (signal?.aborted) { reject(signal.reason); return; }
    let frame: number | undefined;
    let deadline: ReturnType<typeof setTimeout> | undefined;
    let remaining = 10_000;
    let armedAt = 0;
    let settled = false;
    const page = typeof document === 'undefined' ? undefined : document;
    const cleanup = () => {
      settled = true;
      if (frame !== undefined) cancelAnimationFrame(frame);
      clearTimeout(deadline);
      visibilityListeners.delete(visibilityChanged);
      page?.removeEventListener('visibilitychange', visibilityChanged);
      signal?.removeEventListener('abort', abort);
    };
    const abort = () => { cleanup(); reject(signal?.reason); };
    const visibilityChanged = () => {
      if (settled) return;
      if (deadline !== undefined) {
        remaining = Math.max(0, remaining - (performance.now() - armedAt));
        clearTimeout(deadline);
        deadline = undefined;
      }
      if (!hostVisible || page?.hidden) return;
      armedAt = performance.now();
      deadline = setTimeout(() => {
        cleanup();
        reject(Object.assign(new Error('The display did not complete a frame. Retry the view.'),
          { code: 'frame-timeout' }));
      }, remaining);
    };
    visibilityListeners.add(visibilityChanged);
    page?.addEventListener('visibilitychange', visibilityChanged);
    signal?.addEventListener('abort', abort, { once: true });
    visibilityChanged();
    frame = requestAnimationFrame(() => {
      if (settled) return;
      frame = requestAnimationFrame(() => {
        if (settled) return;
        cleanup();
        resolve();
      });
    });
  });
}

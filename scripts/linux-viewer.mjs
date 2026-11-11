// Experimental Chromium shell. No credential-bearing IPC is logged or persisted.
import { chromium } from '../prototypes/VtkJsStreaming/src/client/node_modules/playwright-core/index.mjs';
import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';

const repo = fileURLToPath(new URL('../', import.meta.url));
export async function launchViewer({ dotnet, project, server, clientRoot, headless = false, software = false }) {
  const host = spawn(resolve(dotnet), [resolve(repo, 'portable/PrePoMax.ViewerHost/bin/Release/net10.0/PrePoMax.ViewerHost.dll'),
    resolve(project), resolve(dotnet), resolve(server)], {
    cwd: repo, detached: true, env: { ...process.env, PREPOMAX_RENDER_CLIENT_ROOT: resolve(clientRoot) },
    stdio: ['pipe', 'pipe', 'pipe']
  });
  host.stderr.on('data', () => {});
  host.stdin.on('error', () => {});
  const exited = new Promise(resolveExit => {
    host.once('exit', (code, signal) => resolveExit({ code, signal }));
    host.once('error', () => resolveExit({ code: 1 }));
  });
  const lines = createInterface({ input: host.stdout })[Symbol.asyncIterator]();
  let browser, closed = false;
  const kill = () => { try { process.kill(-host.pid, 'SIGKILL'); } catch {} };
  const close = async () => {
    if (closed) return;
    closed = true;
    try { if (browser) await browser.close(); }
    finally {
      host.stdin.end();
      const timer = setTimeout(kill, 5000);
      const result = await exited;
      clearTimeout(timer);
      if (result.code !== 0) throw new Error('Viewer companion failed.');
    }
  };
  const startup = setTimeout(kill, 45000);
  try {
    const initial = await lines.next();
    if (initial.done) throw new Error('Viewer companion did not become ready.');
    const { address, scene } = JSON.parse(initial.value);
    let serial = Promise.resolve();
    const command = value => {
      const operation = serial.then(async () => {
        if (closed) throw new Error('Viewer companion is closed.');
        host.stdin.write(JSON.stringify(value) + '\n');
        const reply = await lines.next();
        if (reply.done) throw new Error('Viewer companion disconnected.');
        return JSON.parse(reply.value);
      });
      serial = operation.catch(() => {});
      return operation;
    };
    browser = await chromium.launch({ headless, args: software ? ['--use-angle=swiftshader', '--enable-webgl'] : [] });
    const page = await browser.newPage({ viewport: { width: 1200, height: 800 } });
    const origin = new URL(address).origin;
    await page.route('**/*', route => {
      const request = route.request(), url = new URL(request.url());
      const documentAllowed = !request.isNavigationRequest() ||
        (request.frame() === page.mainFrame() && request.url() === address);
      return url.origin === origin && documentAllowed ? route.continue() : route.abort();
    });
    page.on('popup', popup => void popup.close());
    const applied = [];
    await page.exposeBinding('__nativePost', (source, message) => {
      const operation = (async () => {
        if (closed || source.frame !== page.mainFrame() || source.frame.url() !== address ||
            typeof message !== 'string' || message.length > 1024) return;
        let value;
        try { value = JSON.parse(message); } catch { return; }
        if (value?.type === 'prepomax.scene.applied-v1') { applied.push(value); if (applied.length > 256) applied.shift(); return; }
        if (value?.type !== 'prepomax.client.ready-v1') return;
        const { bootstrap } = await command({ source: source.frame.url(), topLevel: true, message });
        if (bootstrap && !closed && source.frame === page.mainFrame() && source.frame.url() === address)
          await source.frame.evaluate(json => window.__deliver(JSON.parse(json)), bootstrap);
      })();
      return operation;
    });
    await page.addInitScript(() => {
      const listeners = [];
      window.chrome = { ...window.chrome, webview: {
        addEventListener: (type, callback) => { if (type === 'message') listeners.push(callback); },
        postMessage: message => { void window.__nativePost(message).catch(() => {}); }
      }};
      window.__deliver = data => listeners.forEach(callback => callback({ data }));
    });
    await page.goto(address);
    clearTimeout(startup);
    // If the companion dies during an interactive session, close its browser too.
    void exited.then(() => { if (!closed) void browser.close(); });
    return { page, browser, address, scene, applied, command, close };
  } catch (error) {
    clearTimeout(startup);
    try { await close(); } catch {}
    throw error;
  }
}

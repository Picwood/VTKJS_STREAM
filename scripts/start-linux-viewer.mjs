import { launchViewer } from './linux-viewer.mjs';
import { once } from 'node:events';
const [dotnet, project, server, clientRoot] = process.argv.slice(2);
if (!clientRoot) throw new Error('Usage: node scripts/start-linux-viewer.mjs DOTNET PROJECT.pma SERVER.dll CLIENT_DIST');
const viewer = await launchViewer({ dotnet, project, server, clientRoot,
  software: process.env.PREPOMAX_VIEWER_SOFTWARE === '1' });
const stop = async () => { try { await viewer.close(); } catch { process.exitCode = 1; } };
process.once('SIGINT', stop);
process.once('SIGTERM', stop);
console.log('Experimental project viewer opened. Close the window to stop the renderer.');
try {
  await Promise.race([once(viewer.page, 'close'), once(viewer.browser, 'disconnected')]);
} finally { await stop(); }

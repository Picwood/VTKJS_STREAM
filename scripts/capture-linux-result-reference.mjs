#!/usr/bin/env node
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { createReadStream } from 'node:fs';
import { mkdir, rename, rm, stat, writeFile } from 'node:fs/promises';
import { basename, dirname, resolve } from 'node:path';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { launchViewer } from './linux-viewer.mjs';

const repo = fileURLToPath(new URL('../', import.meta.url));
const args = process.argv.slice(2);
if (args.length !== 11) {
  console.error('Usage: node scripts/capture-linux-result-reference.mjs DOTNET PROJECT.pma RENDER_SERVER.dll CLIENT_DIST OUTPUT_DIR FIELD COMPONENT STEP INCREMENT TIME DEFORMATION_SCALE');
  process.exit(2);
}

const [dotnetArg, projectArg, serverArg, clientArg, outputArg, name, component,
  stepArg, incrementArg, timeArg, scaleArg] = args;
const dotnet = resolve(dotnetArg), project = resolve(projectArg), server = resolve(serverArg);
const clientRoot = resolve(clientArg), output = resolve(outputArg);
const step = strictInteger(stepArg, 'step'), increment = strictInteger(incrementArg, 'increment');
const time = strictFinite(timeArg, 'time'), deformationScale = strictFinite(scaleArg, 'deformation scale');
const selected = { Name: name, Component: component, Step: step, Increment: increment, Time: time };
const temporary = `${output}.tmp-${process.pid}-${Date.now()}`;

await requireFile(dotnet, 'dotnet executable');
await requireFile(project, 'project');
await requireFile(server, 'renderer server');
if (!(await stat(clientRoot)).isDirectory()) throw new Error(`Client distribution is not a directory: ${clientRoot}`);
try {
  await stat(output);
  throw new Error(`Output already exists: ${output}`);
} catch (error) {
  if (error.code !== 'ENOENT') throw error;
}

await mkdir(dirname(output), { recursive: true });
await mkdir(temporary);
const sourceSha256 = await sha256File(project);
let viewer;
try {
  viewer = await launchViewer({ dotnet, project, server, clientRoot, headless: true, software: true });
  await waitForScene(viewer, viewer.scene.SceneId);

  const catalog = await viewer.command({ command: 'fields' });
  assert.equal(catalog.ok, true, catalog.error ?? 'Unable to read the field catalog.');
  const matches = catalog.fields.filter(field => field.Name === name && field.Component === component &&
    field.Step === step && field.Increment === increment && Object.is(field.Time, time));
  assert.equal(matches.length, 1, 'The requested field/frame is absent or ambiguous.');

  const before = viewer.applied.length;
  const reply = await viewer.command({ command: 'field', field: selected, deformationScale });
  assert.equal(reply.ok, true, reply.error ?? 'Unable to publish the selected field.');
  const acknowledged = await waitForScene(viewer, reply.scene.SceneId, before);

  await viewer.page.locator('#camera-view').selectOption('isometric');
  await viewer.page.locator('#reset-camera').click();
  await viewer.page.evaluate(() => new Promise(requestAnimationFrame));
  await viewer.page.evaluate(() => new Promise(requestAnimationFrame));

  const diagnostics = JSON.parse(await viewer.page.locator('#diagnostics').textContent());
  const screenshot = resolve(temporary, 'frame.png');
  const diagnosticsPath = resolve(temporary, 'diagnostics.json');
  await viewer.page.screenshot({ path: screenshot, clip: { x: 0, y: 0, width: 940, height: 800 } });
  await writeFile(diagnosticsPath, JSON.stringify(diagnostics, null, 2) + '\n');

  const cli = resolve(repo, 'portable/PrePoMax.Cli/bin/Release/net10.0/PrePoMax.Cli.dll');
  await requireFile(cli, 'portable CLI');
  const fingerprint = await runJson(dotnet, [cli, 'result-fingerprint', project, name, component,
    String(step), String(increment), String(time), '--json']);
  assert.deepEqual(fingerprint.Field, selected);

  const unchangedSha256 = await sha256File(project);
  assert.equal(unchangedSha256, sourceSha256, 'Reference capture modified the source project.');
  const reference = {
    Schema: 'prepomax-linux-result-reference-v1',
    Project: { FileName: basename(project), Sha256: sourceSha256 },
    Field: selected,
    DeformationScale: deformationScale,
    View: { Camera: 'isometric', Viewport: { Width: 1200, Height: 800 },
      ScreenshotClip: { X: 0, Y: 0, Width: 940, Height: 800 }, SoftwareWebGL: true },
    Scene: reply.scene,
    StableFrame: acknowledged,
    ResultFingerprint: fingerprint,
    Runtime: { Platform: process.platform, Architecture: process.arch, Node: process.version,
      Browser: await viewer.browser.version() },
    Artifacts: {
      Screenshot: { File: 'frame.png', Sha256: await sha256File(screenshot) },
      Diagnostics: { File: 'diagnostics.json', Sha256: await sha256File(diagnosticsPath) }
    }
  };
  await writeFile(resolve(temporary, 'reference.json'), JSON.stringify(reference, null, 2) + '\n');
  await viewer.close();
  viewer = undefined;
  await rename(temporary, output);
  console.log(`PASS: captured ${name}/${component} step ${step} increment ${increment} reference in ${output}`);
} catch (error) {
  if (viewer) {
    try { await viewer.close(); } catch {}
  }
  await rm(temporary, { recursive: true, force: true });
  throw error;
}

function strictInteger(value, label) {
  if (!/^-?\d+$/.test(value)) throw new Error(`Invalid ${label}: ${value}`);
  const parsed = Number(value);
  if (!Number.isSafeInteger(parsed)) throw new Error(`Invalid ${label}: ${value}`);
  return parsed;
}

function strictFinite(value, label) {
  if (value.trim() === '') throw new Error(`Invalid ${label}: ${value}`);
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) throw new Error(`Invalid ${label}: ${value}`);
  return parsed;
}

async function requireFile(path, label) {
  if (!(await stat(path)).isFile()) throw new Error(`${label} is not a file: ${path}`);
}

async function waitForScene(viewer, sceneId, after = 0) {
  for (let index = 0; index < 900; index++) {
    const match = viewer.applied.slice(after).find(value => value.sceneId === sceneId);
    if (match) return match;
    await viewer.page.waitForTimeout(100);
  }
  throw new Error(`Browser did not acknowledge scene ${sceneId}.`);
}

async function sha256File(path) {
  const hash = createHash('sha256');
  for await (const chunk of createReadStream(path)) hash.update(chunk);
  return hash.digest('hex').toUpperCase();
}

async function runJson(executable, childArgs) {
  const child = spawn(executable, childArgs, { cwd: repo, stdio: ['ignore', 'pipe', 'pipe'] });
  let stdout = '', stderr = '';
  child.stdout.setEncoding('utf8');
  child.stderr.setEncoding('utf8');
  child.stdout.on('data', chunk => { stdout += chunk; });
  child.stderr.on('data', chunk => { stderr += chunk; });
  const code = await new Promise((resolveExit, reject) => {
    child.once('error', reject);
    child.once('exit', resolveExit);
  });
  if (code !== 0) throw new Error(stderr.trim() || `Command failed with exit code ${code}.`);
  return JSON.parse(stdout);
}

param(
    [string]$Distribution = 'Ubuntu-22.04',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$NodeVersion = 'v24.20.0'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$prototypeRoot = Join-Path $repositoryRoot 'prototypes\VtkJsStreaming'
$clientRoot = Join-Path $prototypeRoot 'src\client'
$publishRoot = Join-Path $prototypeRoot 'artifacts\linux-x64'

# First qualify the publish, static host, authenticated API, immutable resource,
# and graceful lifecycle. This also creates the self-contained Linux publish.
& (Join-Path $PSScriptRoot 'smoke-vtkjs-linux.ps1') `
    -Distribution $Distribution -Configuration $Configuration

function Convert-ToWslPath([string]$Path) {
    $resolved = (Resolve-Path -LiteralPath $Path).Path
    $drive = $resolved.Substring(0, 1).ToLowerInvariant()
    return '/mnt/' + $drive + $resolved.Substring(2).Replace('\', '/')
}

$linuxPublishRoot = Convert-ToWslPath $publishRoot
$linuxClientRoot = Convert-ToWslPath (Join-Path $clientRoot 'dist')
$linuxPackageRoot = Convert-ToWslPath $clientRoot
$linuxScript = @'
set -euo pipefail
publish_root=__PUBLISH_ROOT__
client_root=__CLIENT_ROOT__
package_root=__PACKAGE_ROOT__
node_version=__NODE_VERSION__
work_root=$(mktemp -d /tmp/prepomax-vtkjs-browser-XXXXXX)
server_log=$work_root/server.log
pid=
cleanup() {
  code=$?
  if [ $code -ne 0 ]; then cat "$server_log" 2>/dev/null || true; fi
  if [ -n "${pid:-}" ] && kill -0 "$pid" 2>/dev/null; then
    kill "$pid" 2>/dev/null || true
    wait "$pid" 2>/dev/null || true
  fi
  rm -rf -- "$work_root"
  exit $code
}
trap cleanup EXIT

node_archive=node-${node_version}-linux-x64.tar.xz
node_base=https://nodejs.org/dist/${node_version}
curl -fsSLo "$work_root/$node_archive" "$node_base/$node_archive"
curl -fsSLo "$work_root/SHASUMS256.txt" "$node_base/SHASUMS256.txt"
(cd "$work_root" && grep "  $node_archive$" SHASUMS256.txt | sha256sum --check --status)
tar -xJf "$work_root/$node_archive" -C "$work_root"
export PATH="$work_root/node-${node_version}-linux-x64/bin:$PATH"

mkdir "$work_root/client"
cp "$package_root/package.json" "$package_root/package-lock.json" "$work_root/client/"
(cd "$work_root/client" && npm ci --ignore-scripts --no-audit --no-fund >/dev/null 2>&1)
export PLAYWRIGHT_BROWSERS_PATH="$work_root/browsers"
(cd "$work_root/client" && npx playwright install chromium --only-shell >/dev/null 2>&1)

# Ubuntu's headless Chromium requires libasound even when no audio device is
# used. Extract it into this disposable user directory; do not modify the WSL
# distribution or require sudo.
mkdir "$work_root/alsa" "$work_root/debs"
(cd "$work_root/debs" && apt-get download libasound2 >/dev/null 2>&1)
dpkg-deb -x "$work_root"/debs/libasound2_*.deb "$work_root/alsa"
alsa_lib=$(find "$work_root/alsa" -type f -name 'libasound.so.2*' -printf '%h\n' | head -n 1)
test -n "$alsa_lib"
export LD_LIBRARY_PATH="$alsa_lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"

port=$(python3 -c 'import socket; s=socket.socket(); s.bind(("127.0.0.1",0)); print(s.getsockname()[1]); s.close()')
host_token=$(python3 -c 'import secrets; print(secrets.token_urlsafe(32))')
cd "$publish_root"
ASPNETCORE_ENVIRONMENT=Production \
PREPOMAX_RENDER_PORT="$port" \
PREPOMAX_RENDER_CLIENT_ROOT="$client_root" \
PREPOMAX_RENDER_ALLOWED_ORIGINS="http://127.0.0.1:$port" \
PREPOMAX_RENDER_HOST_TOKEN="$host_token" \
./PrePoMax.Render.Server >"$server_log" 2>&1 &
pid=$!
for attempt in $(seq 1 120); do
  if curl -fsS "http://127.0.0.1:$port/healthz" >/dev/null 2>&1; then break; fi
  if ! kill -0 "$pid" 2>/dev/null; then exit 1; fi
  sleep 0.1
done
curl -fsS "http://127.0.0.1:$port/healthz" >/dev/null

cat >"$work_root/client/qualify.mjs" <<'EOF'
import { chromium } from 'playwright';

const [origin, hostToken] = process.argv.slice(2);
const browser = await chromium.launch({
  headless: true,
  args: ['--use-angle=swiftshader', '--enable-webgl'],
});
try {
  const page = await browser.newPage({ viewport: { width: 1400, height: 900 }, deviceScaleFactor: 1 });
  page.on('console', message => {
    if (message.type() === 'error') console.error(`BrowserConsole=${message.text()}`);
  });
  page.on('pageerror', error => console.error(`BrowserPageError=${error.message}`));
  const sessionResponse = await fetch(`${origin}/api/v1/sessions`, {
    method: 'POST', headers: { 'X-PrePoMax-Host-Token': hostToken },
  });
  if (!sessionResponse.ok) throw new Error(`session bootstrap: expected 200, got ${sessionResponse.status}`);
  const session = await sessionResponse.json();
  await page.addInitScript(() => {
    const listeners = new Map();
    const hostMessages = [];
    Object.defineProperty(window, 'chrome', { value: { webview: {
      addEventListener(type, listener) {
        const values = listeners.get(type) ?? [];
        values.push(listener);
        listeners.set(type, values);
      },
      postMessage(message) { hostMessages.push(JSON.parse(message)); },
    } } });
    window.__prepomaxHostMessages = hostMessages;
    window.__sendPrePoMaxHostMessage = data => {
      for (const listener of listeners.get('message') ?? []) listener({ data });
    };
  });
  await page.goto(`${origin}/?embedded=1`, { waitUntil: 'domcontentloaded' });
  await page.waitForFunction(() => window.__prepomaxHostMessages
    ?.some(message => message.type === 'prepomax.client.ready-v1'));
  await page.evaluate(value => window.__sendPrePoMaxHostMessage(value), {
    type: 'prepomax.session.bootstrap-v1',
    sessionId: session.sessionId,
    accessToken: session.accessToken,
    sceneId: session.sceneId,
  });
  try {
    await page.waitForFunction(() => {
      const text = document.querySelector('#diagnostics')?.textContent;
      if (!text) return false;
      try { return JSON.parse(text).renderer?.pipelineChunks === 2; }
      catch { return false; }
    }, undefined, { timeout: 30000 });
  } catch (error) {
    console.error(`BrowserStatus=${await page.locator('#status').textContent().catch(() => '<missing>')}`);
    console.error(`BrowserDiagnostics=${await page.locator('#diagnostics').textContent().catch(() => '<missing>')}`);
    throw error;
  }
  const diagnostics = JSON.parse(await page.locator('#diagnostics').textContent());
  const expected = {
    webgl2Available: true,
    rendererActors: 2,
    pipelineChunks: 2,
    pipelinePoints: 6,
    pipelinePolys: 2,
  };
  for (const [key, value] of Object.entries(expected)) {
    if (diagnostics.renderer?.[key] !== value) {
      throw new Error(`${key}: expected ${value}, got ${diagnostics.renderer?.[key]}`);
    }
  }
  if (diagnostics.transferredBytes !== 152) {
    throw new Error(`transferredBytes: expected 152, got ${diagnostics.transferredBytes}`);
  }
  const shutdownStatus = await page.evaluate(async ({ token }) => {
    const response = await fetch('/api/v1/host/shutdown', {
      method: 'POST', headers: { 'X-PrePoMax-Host-Token': token },
    });
    return response.status;
  }, { token: hostToken });
  if (shutdownStatus !== 202) throw new Error(`shutdown: expected 202, got ${shutdownStatus}`);
  console.log(`Node=${process.version}`);
  console.log(`Browser=${browser.version()}`);
  console.log(`WebGL2=${diagnostics.renderer.webgl2Available}`);
  console.log(`Actors=${diagnostics.renderer.rendererActors}`);
  console.log(`Chunks=${diagnostics.renderer.pipelineChunks}`);
  console.log(`Points=${diagnostics.renderer.pipelinePoints}`);
  console.log(`Polys=${diagnostics.renderer.pipelinePolys}`);
  console.log(`TransferredBytes=${diagnostics.transferredBytes}`);
  console.log(`ShutdownStatus=${shutdownStatus}`);
} finally {
  await browser.close();
}
EOF

cd "$work_root/client"
node qualify.mjs "http://127.0.0.1:$port" "$host_token"
for attempt in $(seq 1 50); do
  if ! kill -0 "$pid" 2>/dev/null; then break; fi
  sleep 0.1
done
if kill -0 "$pid" 2>/dev/null; then exit 1; fi
wait "$pid"
pid=
if grep -Fq -- "$host_token" "$server_log"; then echo 'Host credential leaked to server log.' >&2; exit 1; fi
if grep -Fq -- "$publish_root" "$server_log"; then echo 'Publish path leaked to server log.' >&2; exit 1; fi
if grep -Fq -- "$client_root" "$server_log"; then echo 'Client path leaked to server log.' >&2; exit 1; fi
printf 'SensitiveLogDataAbsent=true\n'
printf 'Exited=true\n'
'@
$linuxScript = $linuxScript.Replace('__PUBLISH_ROOT__', $linuxPublishRoot).
    Replace('__CLIENT_ROOT__', $linuxClientRoot).
    Replace('__PACKAGE_ROOT__', $linuxPackageRoot).
    Replace('__NODE_VERSION__', $NodeVersion)
$encodedScript = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($linuxScript))
$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try {
    $output = @(wsl.exe -d $Distribution -- bash -lc "echo $encodedScript | base64 -d | bash" 2>&1)
    $linuxExitCode = $LASTEXITCODE
}
finally {
    $ErrorActionPreference = $previousErrorActionPreference
}
if ($linuxExitCode -ne 0) {
    throw "The Linux x64 browser smoke failed.`n$($output -join [Environment]::NewLine)"
}

$properties = [ordered]@{ Distribution = $Distribution }
foreach ($line in $output) {
    $parts = $line.ToString().Split('=', 2)
    if ($parts.Count -eq 2) { $properties[$parts[0]] = $parts[1] }
}
[pscustomobject]$properties

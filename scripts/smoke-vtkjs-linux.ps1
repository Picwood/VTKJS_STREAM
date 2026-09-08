param(
    [string]$Distribution = 'Ubuntu-22.04',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$prototypeRoot = Join-Path $repositoryRoot 'prototypes\VtkJsStreaming'
$clientRoot = Join-Path $prototypeRoot 'src\client'
$serverProject = Join-Path $prototypeRoot 'src\PrePoMax.Render.Server\PrePoMax.Render.Server.csproj'
$publishRoot = Join-Path $prototypeRoot 'artifacts\linux-x64'

if (-not (Get-Command wsl.exe -ErrorAction SilentlyContinue)) {
    throw 'WSL is required for the Linux x64 qualification smoke.'
}
$installedDistributions = @(wsl.exe --list --quiet) -replace "`0", '' | ForEach-Object { $_.Trim() } |
    Where-Object { $_.Length -gt 0 }
if ($Distribution -notin $installedDistributions) {
    throw "The WSL distribution '$Distribution' is not installed."
}

Push-Location $clientRoot
try {
    npm run build
    if ($LASTEXITCODE -ne 0) { throw 'The VTK.js client build failed.' }
}
finally {
    Pop-Location
}

dotnet publish $serverProject -c $Configuration -r linux-x64 --self-contained true -o $publishRoot
if ($LASTEXITCODE -ne 0) { throw 'The self-contained Linux x64 server publish failed.' }

function Convert-ToWslPath([string]$Path) {
    $resolved = (Resolve-Path -LiteralPath $Path).Path
    $drive = $resolved.Substring(0, 1).ToLowerInvariant()
    return '/mnt/' + $drive + $resolved.Substring(2).Replace('\', '/')
}

$linuxPublishRoot = Convert-ToWslPath $publishRoot
$linuxClientRoot = Convert-ToWslPath (Join-Path $clientRoot 'dist')
$linuxScript = @'
set -euo pipefail
publish_root=__PUBLISH_ROOT__
client_root=__CLIENT_ROOT__
port=$(python3 -c 'import socket; s=socket.socket(); s.bind(("127.0.0.1",0)); print(s.getsockname()[1]); s.close()')
host_token=$(python3 -c 'import secrets; print(secrets.token_urlsafe(32))')
prefix=/tmp/prepomax-vtkjs-linux-$$
log=$prefix.log
body=$prefix.body
headers=$prefix.headers
pid=
cleanup() {
  code=$?
  if [ $code -ne 0 ]; then cat "$log" 2>/dev/null || true; cat "$body" 2>/dev/null || true; fi
  if [ -n "${pid:-}" ] && kill -0 "$pid" 2>/dev/null; then
    kill "$pid" 2>/dev/null || true
    wait "$pid" 2>/dev/null || true
  fi
  rm -f "$log" "$body" "$headers"
  exit $code
}
trap cleanup EXIT

cd "$publish_root"
ASPNETCORE_ENVIRONMENT=Production \
PREPOMAX_RENDER_PORT="$port" \
PREPOMAX_RENDER_CLIENT_ROOT="$client_root" \
PREPOMAX_RENDER_ALLOWED_ORIGINS="http://127.0.0.1:$port" \
PREPOMAX_RENDER_HOST_TOKEN="$host_token" \
./PrePoMax.Render.Server >"$log" 2>&1 &
pid=$!

health=
for attempt in $(seq 1 120); do
  if health=$(curl -fsS "http://127.0.0.1:$port/healthz" 2>/dev/null); then break; fi
  if ! kill -0 "$pid" 2>/dev/null; then exit 1; fi
  sleep 0.1
done
test -n "$health"

index_status=$(curl -sS -o "$body" -w '%{http_code}' "http://127.0.0.1:$port/")
test "$index_status" = 200
grep -q 'PrePoMax streaming renderer' "$body"

session=$(curl -fsS -X POST -H "X-PrePoMax-Host-Token: $host_token" "http://127.0.0.1:$port/api/v1/sessions")
session_id=$(python3 -c 'import json,sys; print(json.loads(sys.argv[1])["sessionId"])' "$session")
access_token=$(python3 -c 'import json,sys; print(json.loads(sys.argv[1])["accessToken"])' "$session")
scene_id=$(python3 -c 'import json,sys; print(json.loads(sys.argv[1])["sceneId"])' "$session")
test -n "$session_id"
test -n "$access_token"
test -n "$scene_id"

manifest=$(curl -fsS -H "X-PrePoMax-Session: $access_token" \
  "http://127.0.0.1:$port/api/v1/scenes/$scene_id/manifest")
actor_count=$(python3 -c 'import json,sys; print(len(json.loads(sys.argv[1])["actors"]))' "$manifest")
resource_count=$(python3 -c 'import json,sys; print(len(json.loads(sys.argv[1])["resources"]))' "$manifest")
resource_id=$(python3 -c 'import json,sys; print(json.loads(sys.argv[1])["resources"][0]["resourceId"])' "$manifest")
expected_hash=$(python3 -c 'import json,sys; print(json.loads(sys.argv[1])["resources"][0]["sha256"])' "$manifest")
test "$actor_count" -gt 0
test "$resource_count" -gt 0

resource_status=$(curl -sS -D "$headers" -o "$body" -w '%{http_code}' \
  -H "X-PrePoMax-Session: $access_token" \
  "http://127.0.0.1:$port/api/v1/scenes/$scene_id/resources/$resource_id")
test "$resource_status" = 200
test -s "$body"
actual_hash=$(sha256sum "$body" | cut -d ' ' -f 1)
test "$actual_hash" = "$expected_hash"
grep -qi '^Accept-Ranges: bytes' "$headers"

unauthorized_status=$(curl -sS -o "$body" -w '%{http_code}' -X POST \
  -H 'X-PrePoMax-Host-Token: invalid' "http://127.0.0.1:$port/api/v1/host/shutdown")
test "$unauthorized_status" = 401
shutdown_status=$(curl -sS -o "$body" -w '%{http_code}' -X POST \
  -H "X-PrePoMax-Host-Token: $host_token" "http://127.0.0.1:$port/api/v1/host/shutdown")
test "$shutdown_status" = 202
for attempt in $(seq 1 50); do
  if ! kill -0 "$pid" 2>/dev/null; then break; fi
  sleep 0.1
done
if kill -0 "$pid" 2>/dev/null; then exit 1; fi
wait "$pid"
pid=

if grep -Fq -- "$host_token" "$log"; then echo 'Host credential leaked to server log.' >&2; exit 1; fi
if grep -Fq -- "$publish_root" "$log"; then echo 'Publish path leaked to server log.' >&2; exit 1; fi
if grep -Fq -- "$client_root" "$log"; then echo 'Client path leaked to server log.' >&2; exit 1; fi

printf 'Distribution=__DISTRIBUTION__\n'
printf 'Kernel=%s\n' "$(uname -r)"
printf 'Architecture=%s\n' "$(uname -m)"
printf 'Protocol=%s\n' "$(python3 -c 'import json,sys; print(json.loads(sys.argv[1])["protocol"])' "$health")"
printf 'IndexStatus=%s\n' "$index_status"
printf 'SessionCreated=true\n'
printf 'Actors=%s\n' "$actor_count"
printf 'Resources=%s\n' "$resource_count"
printf 'ResourceStatus=%s\n' "$resource_status"
printf 'ResourceHashMatched=true\n'
printf 'UnauthorizedShutdownStatus=%s\n' "$unauthorized_status"
printf 'ShutdownStatus=%s\n' "$shutdown_status"
printf 'SensitiveLogDataAbsent=true\n'
printf 'Exited=true\n'
'@
$linuxScript = $linuxScript.Replace('__PUBLISH_ROOT__', $linuxPublishRoot).
    Replace('__CLIENT_ROOT__', $linuxClientRoot).
    Replace('__DISTRIBUTION__', $Distribution)
$encodedScript = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($linuxScript))
$output = @(wsl.exe -d $Distribution -- bash -lc "echo $encodedScript | base64 -d | bash" 2>&1)
if ($LASTEXITCODE -ne 0) {
    throw "The Linux x64 lifecycle smoke failed.`n$($output -join [Environment]::NewLine)"
}

$properties = [ordered]@{}
foreach ($line in $output) {
    $parts = $line.ToString().Split('=', 2)
    if ($parts.Count -eq 2) { $properties[$parts[0]] = $parts[1] }
}
[pscustomobject]$properties

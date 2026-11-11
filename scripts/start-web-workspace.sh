#!/usr/bin/env bash
set -euo pipefail
source "$(dirname -- "${BASH_SOURCE[0]}")/linux-viewer-common.sh"
if [[ "${1:-}" == --help ]]; then
    echo 'Usage: bash scripts/start-web-workspace.sh [--build]'
    echo 'Starts the private browser pre/post pilot at http://localhost:5180.'
    echo 'Set PREPOMAX_WEB_PORT to use a different port.'
    exit 0
fi
[[ $# == 0 || ( $# == 1 && $1 == --build ) ]] || linux_viewer_fail 'Unexpected argument. Use --help.'
linux_viewer_dotnet
web_client="$linux_viewer_root/prototypes/VtkJsStreaming/src/client"
web_server="$linux_viewer_root/prototypes/VtkJsStreaming/src/PrePoMax.Render.Server"
if [[ "${1:-}" == --build ]]; then
    (cd "$linux_viewer_root/portable" && "$linux_dotnet" build PrePoMax.Web/PrePoMax.Web.csproj -c Release --nologo)
    (cd "$linux_viewer_root/portable" && "$linux_dotnet" build "$web_server/PrePoMax.Render.Server.csproj" -c Release --nologo)
    if [[ ! -d "$web_client/node_modules" ]]; then (cd "$web_client" && npm ci); fi
    (cd "$web_client" && npm run build)
fi
export PREPOMAX_WEB_RENDER_SERVER="$web_server/bin/Release/net8.0/PrePoMax.Render.Server.dll"
export PREPOMAX_WEB_CLIENT_ROOT="$web_client/dist"
export PREPOMAX_WEB_DOTNET="$linux_dotnet"
# The existing renderer targets .NET 8; the portable Linux runtime is .NET 10.
export DOTNET_ROLL_FORWARD=Major
web_assembly="$linux_viewer_root/portable/PrePoMax.Web/bin/Release/net10.0/PrePoMax.Web.dll"
[[ -f "$web_assembly" && -f "$PREPOMAX_WEB_RENDER_SERVER" && -f "$PREPOMAX_WEB_CLIENT_ROOT/workspace.html" ]] || linux_viewer_fail 'Build first: bash scripts/start-web-workspace.sh --build'
echo "PrePoMax browser workspace: http://localhost:${PREPOMAX_WEB_PORT:-5180}"
exec "$linux_dotnet" "$web_assembly"

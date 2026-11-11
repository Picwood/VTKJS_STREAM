#!/usr/bin/env bash
set -euo pipefail
source "$(dirname -- "${BASH_SOURCE[0]}")/linux-viewer-common.sh"
if [[ "${1:-}" == --help ]]; then
    echo 'Usage: scripts/build-linux-viewer.sh'
    echo 'Builds the portable solution, renderer server and locked client dependencies in Release.'
    exit 0
fi
[[ $# == 0 ]] || linux_viewer_fail 'Unexpected argument. Use --help for usage.'
linux_viewer_dotnet
linux_npm="${PREPOMAX_NPM:-npm}"
linux_npm="$(command -v "$linux_npm")" || linux_viewer_fail 'Missing npm. Put Node.js and npm on PATH or set PREPOMAX_NPM.'
linux_npm="$(realpath -- "$linux_npm")"
command -v node >/dev/null 2>&1 || linux_viewer_fail 'Missing node on PATH (required by npm and the client build).'
# Use the portable .NET 10 SDK selection for both Linux managed builds.
# The prototype retains its independent SDK pin for its existing workflows.
(cd "$linux_viewer_root/portable" && "$linux_dotnet" build PrePoMax.Core.slnx --configuration Release)
(cd "$linux_viewer_root/portable" && "$linux_dotnet" build "$linux_viewer_root/prototypes/VtkJsStreaming/src/PrePoMax.Render.Server/PrePoMax.Render.Server.csproj" --configuration Release)
(cd "$linux_viewer_root/prototypes/VtkJsStreaming/src/client" && "$linux_npm" ci && "$linux_npm" run build)
echo 'Linux viewer build complete. Open a supported PMA or convert a supported PMX with scripts/start-gtk-viewer.sh'

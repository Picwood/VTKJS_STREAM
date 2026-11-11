#!/usr/bin/env bash
set -euo pipefail
source "$(dirname -- "${BASH_SOURCE[0]}")/linux-viewer-common.sh"
if [[ "${1:-}" == --help ]]; then
    echo 'Usage: scripts/start-gtk-viewer.sh [PROJECT.pma] [GTK viewer options]'
    echo '       scripts/start-gtk-viewer.sh --check'
    echo '       scripts/start-gtk-viewer.sh --check-solver'
    echo '       scripts/start-gtk-viewer.sh --check-gmsh'
    echo 'Build first with scripts/build-linux-viewer.sh. PREPOMAX_DOTNET and PREPOMAX_PYTHON override executables.'
    exit 0
fi
linux_viewer_dotnet
linux_python="${PREPOMAX_PYTHON:-python3}"
linux_python="$(command -v "$linux_python")" || linux_viewer_fail 'Missing python3. Install Python with PyGObject, GTK 3 and WebKit2 4.1 introspection bindings.'
"$linux_python" -c 'import gi; gi.require_version("Gtk", "3.0"); gi.require_version("WebKit2", "4.1"); gi.require_foreign("cairo"); from gi.repository import Gtk, WebKit2' || linux_viewer_fail 'GTK prerequisites unavailable: Python/PyGObject with Cairo, GTK 3 and WebKit2 4.1 are required.'
if [[ -f "$linux_viewer_root/linux-viewer-bundle-v1" ]]; then
    linux_host="$linux_viewer_root/lib/host/PrePoMax.ViewerHost.dll"
    linux_converter="$linux_viewer_root/lib/converter/PrePoMax.PmxInspect.dll"
    linux_server="$linux_viewer_root/lib/server/PrePoMax.Render.Server.dll"
    linux_client="$linux_viewer_root/share/client"
else
    linux_host="$linux_viewer_root/portable/PrePoMax.ViewerHost/bin/Release/net10.0/PrePoMax.ViewerHost.dll"
    linux_converter="$linux_viewer_root/portable/PrePoMax.PmxInspect/bin/Release/net10.0/PrePoMax.PmxInspect.dll"
    linux_server="$linux_viewer_root/prototypes/VtkJsStreaming/src/PrePoMax.Render.Server/bin/Release/net8.0/PrePoMax.Render.Server.dll"
    linux_client="$linux_viewer_root/prototypes/VtkJsStreaming/src/client/dist"
fi
for linux_artifact in "$linux_host" "$linux_converter" "$linux_server" "$linux_client/index.html"; do
    [[ -f "$linux_artifact" ]] || linux_viewer_fail "Missing build artifact: $linux_artifact. Run scripts/build-linux-viewer.sh."
done
# The server targets net8; this supported workflow has been validated with .NET 10.
export DOTNET_ROLL_FORWARD="${DOTNET_ROLL_FORWARD:-Major}"
linux_check_runtimes() {
    "$linux_dotnet" "$linux_host" --check-runtime || linux_viewer_fail 'Viewer companion runtime check failed. Check the .NET 10 runtime and DOTNET_ROLL_FORWARD setting.'
    "$linux_dotnet" "$linux_server" --check-runtime || linux_viewer_fail 'Renderer runtime check failed. Check the ASP.NET Core runtime and DOTNET_ROLL_FORWARD setting.'
}
linux_check_solver() {
    linux_solver="${PREPOMAX_CCX:-ccx}"
    linux_solver="$(command -v "$linux_solver")" || linux_viewer_fail 'CalculiX solver unavailable. Install calculix-ccx or set PREPOMAX_CCX.'
    linux_solver_version=''
    linux_solver_status=0
    linux_solver_version="$("$linux_solver" -v 2>&1)" || linux_solver_status=$?
    # CalculiX 2.21 prints its version successfully but uses exit status 201.
    [[ "$linux_solver_status" == 0 || "$linux_solver_status" == 201 ]] || \
        linux_viewer_fail "CalculiX solver could not start: $linux_solver"
    [[ "$linux_solver_version" == *'This is Version '* ]] || \
        linux_viewer_fail "CalculiX solver returned an unexpected version response: $linux_solver"
    printf 'CalculiX solver ready: %s\n' "$linux_solver"
}
linux_check_gmsh() {
    linux_gmsh="${PREPOMAX_GMSH:-gmsh}"
    linux_gmsh="$(command -v "$linux_gmsh")" || linux_viewer_fail 'Gmsh unavailable. Install gmsh or set PREPOMAX_GMSH.'
    linux_gmsh_version="$("$linux_gmsh" -version 2>&1)" || linux_viewer_fail "Gmsh could not start: $linux_gmsh"
    [[ "$linux_gmsh_version" =~ ^[0-9]+\.[0-9]+ ]] || linux_viewer_fail "Gmsh returned an unexpected version response: $linux_gmsh"
    printf 'Gmsh ready: %s (%s)\n' "$linux_gmsh" "$linux_gmsh_version"
}
if [[ "${1:-}" == --check ]]; then
    [[ $# == 1 ]] || linux_viewer_fail '--check takes no project or options.'
    linux_check_runtimes
    echo 'Managed startup probes, GTK bindings and viewer artifacts passed. Display, solver and GPU acceptance require a real launch.'
    exit 0
fi
if [[ "${1:-}" == --check-solver ]]; then
    [[ $# == 1 ]] || linux_viewer_fail '--check-solver takes no project or options.'
    linux_check_runtimes
    linux_check_solver
    exit 0
fi
if [[ "${1:-}" == --check-gmsh ]]; then
    [[ $# == 1 ]] || linux_viewer_fail '--check-gmsh takes no project or options.'
    linux_check_runtimes
    linux_check_gmsh
    exit 0
fi
linux_project="-"
if [[ $# != 0 && "$1" != --* ]]; then
    linux_project="$1"
    shift
    [[ -f "$linux_project" ]] || linux_viewer_fail "Project not found: $linux_project"
    linux_project="$(realpath -- "$linux_project")"
fi
linux_check_runtimes >/dev/null
exec "$linux_python" "$linux_viewer_root/scripts/start-gtk-viewer.py" "$linux_dotnet" "$linux_project" "$linux_server" "$linux_client" --companion "$linux_host" --converter "$linux_converter" "$@"

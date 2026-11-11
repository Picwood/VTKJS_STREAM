#!/usr/bin/env bash
set -euo pipefail
source "$(dirname -- "${BASH_SOURCE[0]}")/linux-viewer-common.sh"
if [[ "${1:-}" == --help ]]; then
    echo 'Usage: scripts/package-linux-viewer.sh [--framework-dependent] OUTPUT.tar.gz'
    echo 'Build and publish a relocatable GTK viewer preview. The default archive includes the current Linux .NET runtime.'
    echo '--framework-dependent creates the smaller legacy archive and requires .NET 10 on the target machine.'
    echo 'The packager refuses to replace an existing archive.'
    exit 0
fi
linux_include_runtime=1
if [[ "${1:-}" == --framework-dependent ]]; then
    linux_include_runtime=0
    shift
fi
[[ $# == 1 ]] || linux_viewer_fail 'Supply an output archive path, or --help.'
linux_archive="$(realpath -m -- "$1")"
[[ "$linux_archive" == *.tar.gz ]] || linux_viewer_fail 'Output must end in .tar.gz.'
[[ ! -e "$linux_archive" ]] || linux_viewer_fail "Output already exists: $linux_archive"
[[ -d "$(dirname -- "$linux_archive")" ]] || linux_viewer_fail 'Create the output parent directory first.'
linux_viewer_dotnet
"$linux_viewer_root/scripts/build-linux-viewer.sh"
linux_stage="$(mktemp -d)"
linux_partial=''
trap 'rm -rf -- "$linux_stage"; if [[ -n "$linux_partial" ]]; then rm -f -- "$linux_partial"; fi' EXIT
linux_partial="$(mktemp "$(dirname -- "$linux_archive")/.prepomax-archive.XXXXXXXX")"
linux_bundle="$linux_stage/prepomax-linux-viewer"
mkdir -p "$linux_bundle/scripts" "$linux_bundle/share/client" "$linux_bundle/lib"
(cd "$linux_viewer_root/portable" && "$linux_dotnet" publish PrePoMax.ViewerHost/PrePoMax.ViewerHost.csproj -c Release --no-build --no-restore --self-contained false -p:UseAppHost=false -o "$linux_bundle/lib/host")
(cd "$linux_viewer_root/portable" && "$linux_dotnet" publish PrePoMax.PmxInspect/PrePoMax.PmxInspect.csproj -c Release --no-build --no-restore --self-contained false -p:UseAppHost=false -o "$linux_bundle/lib/converter")
(cd "$linux_viewer_root/portable" && "$linux_dotnet" publish PrePoMax.Cli/PrePoMax.Cli.csproj -c Release --no-build --no-restore --self-contained false -p:UseAppHost=false -o "$linux_bundle/lib/cli")
(cd "$linux_viewer_root/portable" && "$linux_dotnet" publish "$linux_viewer_root/prototypes/VtkJsStreaming/src/PrePoMax.Render.Server/PrePoMax.Render.Server.csproj" -c Release --no-build --no-restore --self-contained false -p:UseAppHost=false -o "$linux_bundle/lib/server")
cp "$linux_viewer_root/scripts/"{start-gtk-viewer.sh,start-gtk-viewer.py,linux_edit_session.py,linux_node_selection.py,linux-viewer-common.sh} "$linux_bundle/scripts/"
mkdir -p "$linux_bundle/share/model-tree-icons"
for linux_icon in Mesh Part Node_set Element_set Surface Reference_point Material Section Constraints Step Analysis Bc Load Field_output History_output; do
    cp "$linux_viewer_root/UserControls/Resources/$linux_icon.ico" "$linux_bundle/share/model-tree-icons/"
done
for linux_icon in Mesh/CoordinateSystem Contact/Contact Contact/ContactPair Contact/SurfaceInteraction Distributions/Distribution Amplitude/Amplitude 'Initial conditions/Initial_conditions'; do
    cp "$linux_viewer_root/PrePoMax/Icons/$linux_icon.ico" "$linux_bundle/share/model-tree-icons/"
done
cp -R "$linux_viewer_root/prototypes/VtkJsStreaming/src/client/dist/." "$linux_bundle/share/client/"
cp "$linux_viewer_root/LICENSE" "$linux_bundle/LICENSE"
cp "$linux_viewer_root/docs/LINUX_VIEWER_BUNDLE.md" "$linux_bundle/README.md"
touch "$linux_bundle/linux-viewer-bundle-v1"
printf 'Portable SDK: %s\n' "$(cd "$linux_viewer_root/portable" && "$linux_dotnet" --version)" > "$linux_bundle/BUILD.txt"
printf 'Source revision: %s\n' "$(git -C "$linux_viewer_root" rev-parse HEAD)" >> "$linux_bundle/BUILD.txt"
if [[ $linux_include_runtime == 1 ]]; then
    linux_core_version="$("$linux_dotnet" --list-runtimes | awk '$1 == "Microsoft.NETCore.App" && $2 ~ /^10\./ { version=$2 } END { print version }')"
    linux_aspnet_version="$("$linux_dotnet" --list-runtimes | awk '$1 == "Microsoft.AspNetCore.App" && $2 ~ /^10\./ { version=$2 } END { print version }')"
    [[ -n "$linux_core_version" && -n "$linux_aspnet_version" ]] || linux_viewer_fail 'The selected .NET installation must contain .NETCore.App and ASP.NET Core 10 runtimes.'
    [[ -d "$linux_dotnet_root/host/fxr/$linux_core_version" ]] || linux_viewer_fail "Missing .NET host/fxr $linux_core_version."
    mkdir -p "$linux_bundle/runtime/host/fxr" "$linux_bundle/runtime/shared/Microsoft.NETCore.App" "$linux_bundle/runtime/shared/Microsoft.AspNetCore.App"
    cp "$linux_dotnet_root/dotnet" "$linux_dotnet_root/LICENSE.txt" "$linux_dotnet_root/ThirdPartyNotices.txt" "$linux_bundle/runtime/"
    cp -a "$linux_dotnet_root/host/fxr/$linux_core_version" "$linux_bundle/runtime/host/fxr/"
    cp -a "$linux_dotnet_root/shared/Microsoft.NETCore.App/$linux_core_version" "$linux_bundle/runtime/shared/Microsoft.NETCore.App/"
    cp -a "$linux_dotnet_root/shared/Microsoft.AspNetCore.App/$linux_aspnet_version" "$linux_bundle/runtime/shared/Microsoft.AspNetCore.App/"
    DOTNET_ROOT="$linux_bundle/runtime" DOTNET_ROLL_FORWARD=Major "$linux_bundle/runtime/dotnet" \
        "$linux_bundle/lib/host/PrePoMax.ViewerHost.dll" --check-runtime >/dev/null
    DOTNET_ROOT="$linux_bundle/runtime" DOTNET_ROLL_FORWARD=Major "$linux_bundle/runtime/dotnet" \
        "$linux_bundle/lib/server/PrePoMax.Render.Server.dll" --check-runtime >/dev/null
    touch "$linux_bundle/linux-viewer-bundle-v2"
    printf 'Private runtime: Microsoft.NETCore.App %s; Microsoft.AspNetCore.App %s\n' "$linux_core_version" "$linux_aspnet_version" >> "$linux_bundle/BUILD.txt"
else
    echo 'Private runtime: not included' >> "$linux_bundle/BUILD.txt"
fi
# This working tree may contain the port; record that the revision alone is not its identity.
if [[ -n "$(git -C "$linux_viewer_root" status --porcelain)" ]]; then echo 'Source working tree: modified' >> "$linux_bundle/BUILD.txt"; fi
(cd "$linux_bundle" && find . -type f ! -name SHA256SUMS -print0 | sort -z | xargs -0 sha256sum > SHA256SUMS)
# Normalize archive metadata; payload reproducibility also depends on the build inputs/toolchain.
tar --sort=name --mtime='@0' --owner=0 --group=0 --numeric-owner -C "$linux_stage" -cf - prepomax-linux-viewer | gzip -n > "$linux_partial"
# Hard-link publication is atomic and fails if another writer created OUTPUT meanwhile.
ln -- "$linux_partial" "$linux_archive"
printf 'Preview bundle created: %s\n' "$linux_archive"

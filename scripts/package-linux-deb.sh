#!/usr/bin/env bash
set -euo pipefail
source "$(dirname -- "${BASH_SOURCE[0]}")/linux-viewer-common.sh"
if [[ "${1:-}" == --help ]]; then
    echo 'Usage: scripts/package-linux-deb.sh [--bundle VIEWER.tar.gz] OUTPUT.deb'
    echo 'Build a Debian/Ubuntu amd64 package with system Python, GTK 3 and WebKitGTK dependencies.'
    echo 'Without --bundle, the runtime-bundled viewer archive is built first.'
    echo 'PREPOMAX_PACKAGE_VERSION overrides the generated preview version.'
    exit 0
fi
linux_bundle_archive=''
if [[ "${1:-}" == --bundle ]]; then
    [[ $# -ge 2 ]] || linux_viewer_fail '--bundle requires an archive path.'
    linux_bundle_archive="$(realpath -- "$2")"
    shift 2
fi
[[ $# == 1 ]] || linux_viewer_fail 'Supply an output .deb path, or --help.'
command -v dpkg-deb >/dev/null 2>&1 || linux_viewer_fail 'Missing dpkg-deb.'
command -v desktop-file-validate >/dev/null 2>&1 || linux_viewer_fail 'Missing desktop-file-validate (desktop-file-utils).'
command -v appstreamcli >/dev/null 2>&1 || linux_viewer_fail 'Missing appstreamcli.'
[[ "$(dpkg --print-architecture)" == amd64 ]] || linux_viewer_fail 'The current package target is Debian amd64.'
linux_deb="$(realpath -m -- "$1")"
[[ "$linux_deb" == *.deb ]] || linux_viewer_fail 'Output must end in .deb.'
[[ ! -e "$linux_deb" ]] || linux_viewer_fail "Output already exists: $linux_deb"
[[ -d "$(dirname -- "$linux_deb")" ]] || linux_viewer_fail 'Create the output parent directory first.'
linux_version="${PREPOMAX_PACKAGE_VERSION:-0.1.0~preview+git$(git -C "$linux_viewer_root" rev-parse --short=12 HEAD)}"
dpkg --validate-version "$linux_version" || linux_viewer_fail "Invalid Debian version: $linux_version"

linux_stage="$(mktemp -d)"
linux_partial=''
trap 'rm -rf -- "$linux_stage"; if [[ -n "$linux_partial" ]]; then rm -f -- "$linux_partial"; fi' EXIT
if [[ -z "$linux_bundle_archive" ]]; then
    linux_bundle_archive="$linux_stage/viewer.tar.gz"
    "$linux_viewer_root/scripts/package-linux-viewer.sh" "$linux_bundle_archive"
fi
[[ -f "$linux_bundle_archive" ]] || linux_viewer_fail "Viewer archive not found: $linux_bundle_archive"
linux_unpack="$linux_stage/unpack"
mkdir -p "$linux_unpack"
tar -xzf "$linux_bundle_archive" -C "$linux_unpack"
linux_payload="$linux_unpack/prepomax-linux-viewer"
[[ -f "$linux_payload/linux-viewer-bundle-v2" && -x "$linux_payload/runtime/dotnet" &&
   -f "$linux_payload/lib/cli/PrePoMax.Cli.dll" ]] || \
    linux_viewer_fail 'The Debian package requires a runtime-bundled viewer-v2 archive.'

linux_root="$linux_stage/root"
linux_app="$linux_root/usr/lib/prepomax-linux-viewer"
mkdir -p "$linux_root/DEBIAN" "$linux_root/usr/lib" "$linux_root/usr/bin" "$linux_root/usr/share/applications" \
    "$linux_root/usr/share/mime/packages" "$linux_root/usr/share/pixmaps" \
    "$linux_root/usr/share/metainfo" "$linux_root/usr/share/doc/prepomax-linux-viewer"
mv "$linux_payload" "$linux_app"
cp "$linux_viewer_root/PrePoMax/main.ico" "$linux_root/usr/share/pixmaps/prepomax-linux-viewer.ico"
cp "$linux_viewer_root/LICENSE" "$linux_root/usr/share/doc/prepomax-linux-viewer/copyright"

cat > "$linux_root/usr/bin/prepomax-linux-viewer" <<'EOF'
#!/usr/bin/env bash
exec /usr/lib/prepomax-linux-viewer/scripts/start-gtk-viewer.sh "$@"
EOF
chmod 0755 "$linux_root/usr/bin/prepomax-linux-viewer"
cat > "$linux_root/usr/bin/prepomax-linux-cli" <<'EOF'
#!/usr/bin/env bash
export DOTNET_ROOT=/usr/lib/prepomax-linux-viewer/runtime
export DOTNET_ROLL_FORWARD="${DOTNET_ROLL_FORWARD:-Major}"
exec /usr/lib/prepomax-linux-viewer/runtime/dotnet /usr/lib/prepomax-linux-viewer/lib/cli/PrePoMax.Cli.dll "$@"
EOF
chmod 0755 "$linux_root/usr/bin/prepomax-linux-cli"
cat > "$linux_root/usr/share/applications/si.um.fs.prepomax.LinuxViewer.desktop" <<'EOF'
[Desktop Entry]
Type=Application
Name=PrePoMax Linux Viewer
Comment=Prepare, solve and inspect finite-element projects
Exec=prepomax-linux-viewer %f
Icon=/usr/share/pixmaps/prepomax-linux-viewer.ico
Terminal=false
Categories=Science;Engineering;
MimeType=application/x-prepomax-pma;application/x-prepomax-pmx;
StartupNotify=true
EOF
cat > "$linux_root/usr/share/mime/packages/prepomax-linux-viewer.xml" <<'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<mime-info xmlns="http://www.freedesktop.org/standards/shared-mime-info">
  <mime-type type="application/x-prepomax-pma">
    <comment>PrePoMax portable analysis project</comment>
    <glob pattern="*.pma"/>
  </mime-type>
  <mime-type type="application/x-prepomax-pmx">
    <comment>Legacy PrePoMax project</comment>
    <glob pattern="*.pmx"/>
  </mime-type>
</mime-info>
EOF
cat > "$linux_root/usr/share/metainfo/si.um.fs.prepomax.LinuxViewer.metainfo.xml" <<'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<component type="desktop-application">
  <id>si.um.fs.prepomax.LinuxViewer.desktop</id>
  <name>PrePoMax Linux Viewer</name>
  <summary>Prepare, solve and inspect finite-element projects</summary>
  <metadata_license>CC0-1.0</metadata_license>
  <project_license>GPL-3.0-or-later</project_license>
  <description>
    <p>Prepare supported finite-element projects, run static analyses with an external CalculiX solver, and inspect results in a native Linux VTK.js viewer.</p>
  </description>
  <launchable type="desktop-id">si.um.fs.prepomax.LinuxViewer.desktop</launchable>
  <developer id="si.um.fs.prepomax"><name>PrePoMax contributors</name></developer>
  <url type="homepage">https://prepomax.fs.um.si/</url>
  <content_rating type="oars-1.1"/>
  <provides>
    <mediatype>application/x-prepomax-pma</mediatype>
    <mediatype>application/x-prepomax-pmx</mediatype>
  </provides>
</component>
EOF

linux_installed_size="$(du -sk "$linux_root/usr" | awk '{ print $1 }')"
cat > "$linux_root/DEBIAN/control" <<EOF
Package: prepomax-linux-viewer
Version: $linux_version
Architecture: amd64
Maintainer: PrePoMax Linux Port <noreply@localhost>
Section: science
Priority: optional
Installed-Size: $linux_installed_size
Depends: python3, python3-gi, python3-gi-cairo, gir1.2-gtk-3.0, gir1.2-webkit2-4.1, desktop-file-utils, shared-mime-info
Recommends: calculix-ccx, gmsh
Description: Native Linux finite-element project viewer preview
 Opens or imports supported PrePoMax projects, edits a bounded static-analysis
 subset, meshes CAD solids through distribution Gmsh, runs an externally installed
 CalculiX solver and displays VTK.js results.
EOF

desktop-file-validate "$linux_root/usr/share/applications/si.um.fs.prepomax.LinuxViewer.desktop"
appstreamcli validate --no-net "$linux_root/usr/share/metainfo/si.um.fs.prepomax.LinuxViewer.metainfo.xml"
chmod -R go-w "$linux_root"
SOURCE_DATE_EPOCH="${SOURCE_DATE_EPOCH:-$(git -C "$linux_viewer_root" log -1 --format=%ct)}"
find "$linux_root" -print0 | xargs -0 touch --date="@$SOURCE_DATE_EPOCH"
linux_partial="$(mktemp "$(dirname -- "$linux_deb")/.prepomax-deb.XXXXXXXX")"
SOURCE_DATE_EPOCH="$SOURCE_DATE_EPOCH" dpkg-deb --root-owner-group -Zxz -z9 --build "$linux_root" "$linux_partial" >/dev/null
ln -- "$linux_partial" "$linux_deb"
printf 'Debian package created: %s\n' "$linux_deb"

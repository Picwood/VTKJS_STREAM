# Debian/Ubuntu package

Validated: 2026-10-28.

`scripts/package-linux-deb.sh` turns the runtime-bundled viewer into an amd64
Debian package. It installs the application and private .NET/ASP.NET Core runtime
under `/usr/lib/prepomax-linux-viewer`, exposes `prepomax-linux-viewer` and the
private-runtime `prepomax-linux-cli` in `/usr/bin`, and registers a desktop entry
plus `.pma` and `.pmx` MIME types. The
selected GTK host uses the matching `si.um.fs.prepomax.LinuxViewer` application
ID.

Python, PyGObject, GTK 3 and WebKitGTK remain distribution-managed dependencies:

```text
python3, python3-gi, python3-gi-cairo, gir1.2-gtk-3.0, gir1.2-webkit2-4.1,
desktop-file-utils, shared-mime-info
```

This avoids shipping a second WebKit stack and lets the distribution deliver its
security updates. The package recommends the distribution's `calculix-ccx` and
`gmsh` packages, while still allowing `PREPOMAX_CCX` and `PREPOMAX_GMSH` to
select compatible executables. Neither native tool is copied into the payload.

Build directly from the checkout:

```sh
scripts/package-linux-deb.sh /existing/output/directory/prepomax-linux-viewer.deb
```

Or reuse an already validated runtime bundle:

```sh
scripts/package-linux-deb.sh --bundle viewer-preview.tar.gz prepomax-linux-viewer.deb
```

Install the resulting package and its distribution dependencies with:

```sh
sudo apt install ./prepomax-linux-viewer.deb
```

`PREPOMAX_PACKAGE_VERSION` overrides the default Git-derived preview version.
The builder refuses overwrite, requires a runtime-bundle-v2 input, validates the
Debian version, desktop entry and AppStream metadata, assigns root ownership in
the archive and uses the source revision timestamp for package metadata.

Run the clean-machine acceptance harness against an existing package with:

```sh
scripts/test-linux-deb-clean.sh prepomax-linux-viewer.deb
# Also exercise an in-place version upgrade:
scripts/test-linux-deb-clean.sh old.deb newer.deb
```

It starts a disposable `ubuntu:24.04` container, resolves the package's required
dependencies, checks its files and generated desktop/MIME caches, and runs the
private-runtime preflight. It then installs the recommended distribution
CalculiX and Gmsh packages explicitly, solves an analytical C3D8 cube, creates
an editable tetrahedral PMA from STEP and remeshes its retained CAD before
verifying cleanup after purge. With
the optional second package,
it first requires a strictly newer Debian version, upgrades in place, repeats
every installed-package and solver check, and proves XDG user state survives both
upgrade and purge. Docker is required. `PREPOMAX_UBUNTU_IMAGE` selects another
compatible test image and `PREPOMAX_UBUNTU_MIRROR` can select a local Ubuntu
archive mirror.

## Latest package acceptance: original-style static step authoring

The runtime bundle and packages `0.1.1~preview+steps.1` and `.2` pass clean
Ubuntu 24.04 install/upgrade/purge acceptance. The installed CLI creates, edits,
duplicates, deactivates and removes static steps, exports `*Static, Direct`, and
preserves CAD topology plus face/box meshing controls. The two-step user project
remains byte-identical across upgrade, reopens with identical inspection data,
and survives purge. Runtime/tool checks and the distribution CalculiX 2.21 cube
regression pass. Native CalculiX 2.23 separately verifies independently edited
step loads and field-only result publication. See [step editing](LINUX_STEP_EDITING.md)
and [artifact/command evidence](evidence/linux-static-steps-2026-10-28.txt).
The current scope prioritizes [functional parity](LINUX_FUNCTION_PARITY.md);
CAD repair is excluded.

## Previous package acceptance: source-bound CAD face sizing

The rebuilt private-runtime bundle and packages `0.1.1~preview+facesize.1` and
`.2` pass clean Ubuntu 24.04 install/upgrade/purge acceptance. Distribution Gmsh
4.12.1 applies one source/topology-bound CAD face refinement, a coordinate box
and quadratic order together; the installed CLI reuses the saved controls and
exports C3D10. The harness selects the face tag from the captured STEP graph.
The XDG-stored project remains byte-identical across upgrade and reopens with
identical inspection data. Runtime/tool checks, the distribution CalculiX 2.21
analytical C3D8 solve and integration cleanup pass. See
[CAD face sizing](LINUX_CAD_FACE_SIZING.md) and
[artifact/command evidence](evidence/linux-cad-face-sizing-2026-10-28.txt).

## Previous package acceptance: retained CAD topology

The current runtime bundle and packages `0.1.0~preview+topology.1` and `.2` pass
clean Ubuntu 24.04 install/upgrade/purge acceptance. Distribution Gmsh 4.12.1
captures the point/curve/face/volume graph from STEP, the installed CLI exposes
the full source-bound inventory, and the harness verifies PMA v6 topology payloads
alongside local quadratic remeshing/control reuse and C3D10 export. Upgrade
preserves and reopens the exact XDG-stored PMA; private-runtime/tool checks and
integration cleanup pass. The distribution CalculiX 2.21 analytical C3D8 solve
still passes. See [CAD topology](LINUX_CAD_TOPOLOGY.md) and
[artifact/command evidence](evidence/linux-cad-topology-2026-10-28.txt).

## Previous package acceptance: local quadratic sizing

The private-runtime bundle and two amd64 packages were rebuilt from the current
checkout with versions `0.1.0~preview+localbox.1` and `.2`. A fresh Ubuntu 24.04
container passes install, desktop/MIME integration, private-runtime preflight,
distribution CalculiX 2.21 analytical C3D8 solve, and Gmsh 4.12.1 CAD import. It
then performs locally refined quadratic remeshing, checks the persisted box and
order, reuses those controls, and exports production C3D10 input. The in-place
upgrade preserves an XDG-stored PMA byte for byte and reopens it with identical
inspection data. Runtime/tool checks pass again, purge removes application
integration, and user project data remains unchanged. Package/bundle hashes and
command summaries are in [the acceptance evidence](evidence/linux-local-sizing-2026-10-28.txt).
Local GTK X11 software-WebGL and CalculiX 2.23 acceptance for this path are
recorded in [local sizing](LINUX_LOCAL_SIZING.md).

## Previous local evidence

The package was built on Ubuntu 26.04 amd64 from the validated private-runtime
archive. The final artifact is 34,947,288 bytes (34 MiB), reports an installed
size of 123,724 KiB and has SHA-256
`f2d2c6802edb509922dacd5d4f50ded5b4e70d23ce794f1a337759f392ab2319`.
Two builds from the same input were byte-identical. `dpkg-deb` metadata,
declared dependencies, normalized installed modes, root ownership,
filesystem layout, desktop file, AppStream metadata and MIME XML pass
inspection. The package was extracted without root privileges and its installed
payload passed private-runtime preflight and real X11 render/reload/shutdown with
external .NET hidden. A fresh Ubuntu 24.04 container resolved the declared
Python/GTK/WebKit packages, installed the package, populated the PMA/PMX MIME and
desktop caches, selected the private .NET/ASP.NET Core 10.0.12 runtimes and then
installed Ubuntu's `calculix-ccx` 2.21 and Gmsh 4.12.1 packages. The installed
launchers recognized both tools; CalculiX solved an analytical C3D8 cube with U1
exactly 0.001 on all four loaded-face nodes, and the private-runtime CLI converted
an OpenCASCADE STEP cube through Gmsh into an editable PMA with six CAD-face
surfaces and a CAD-volume element set. Inspection verified the retained STEP
filename, length and SHA-256, and CLI extraction matched the original bytes. It
then remeshed the retained STEP with min/max, Delaunay and optimization controls;
the new PMA retained the source identity, material, controls and six CAD-face
regions. A second remesh supplied no control arguments and reproduced the stored
setup. The harness
then upgraded from
`0.1.0~preview+gitae5954ddefb5` to `0.1.1~preview+gmshsetup`. Checksums,
runtimes, native-tool discovery and integration caches passed again after replacement.
An XDG job-state sentinel survived upgrade and purge; every package-owned
application/integration file and generated cache entry was removed. The minimal
application-only container pulled 248 packages and reported approximately 697 MB
of additional installed space even with recommendations disabled; a normal
Ubuntu desktop already supplies many of these GTK, WebKit, media and graphics
libraries. The explicitly installed solver and mesher add their
distribution-managed numerical, CAD and runtime dependencies.
The dedicated `linux-debian-package.yml` workflow rebuilds the archive, compares
two packages byte for byte, creates a higher-version package, runs this clean
install/upgrade/purge harness and publishes the tested base artifact. Package
signing/repository delivery and a headed
physical-GPU run on Ubuntu 24.04 remain release gates.

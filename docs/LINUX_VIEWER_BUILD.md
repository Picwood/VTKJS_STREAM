# Linux viewer build and launch

Validated 2026-10-23. These entry points build and run the supported portable GTK
project workflow. They are developer entry points, not a packaged replacement
for the complete Windows PrePoMax desktop application.

From the repository root:

```bash
scripts/build-linux-viewer.sh
scripts/start-gtk-viewer.sh --check
# Also validate a PATH/PREPOMAX_CCX solver installation:
scripts/start-gtk-viewer.sh --check-solver
# Validate PATH/PREPOMAX_GMSH discovery:
scripts/start-gtk-viewer.sh --check-gmsh
scripts/start-gtk-viewer.sh
# Or open an existing project immediately:
scripts/start-gtk-viewer.sh /absolute/path/to/project.pma
```

The build compiles the portable solution and renderer server in Release, installs
the client's locked dependencies with `npm ci`, and builds the production client.
It stops on the first failure. Both managed builds use the portable directory's
.NET 10 SDK selection, including when compiling the net8 renderer; the prototype's
independent .NET 9 SDK pin remains unchanged. The script does not run the numerical
or client test suites; use their existing test entry points for those checks.

## Prerequisites and overrides

- .NET 10 SDK matching `portable/global.json`. Discovery uses `PREPOMAX_DOTNET`,
  then PATH, then `${XDG_DATA_HOME:-$HOME/.local/share}/prepomax/dotnet/dotnet`.
- Node.js and npm for the build. The recorded build used Node 22.14.0. Put node
  on PATH; `PREPOMAX_NPM` can select the npm executable.
- Python 3, PyGObject with the Cairo foreign bridge, GTK 3 and WebKit2 4.1
  introspection bindings for launch.
  `PREPOMAX_PYTHON` can select a Python executable with those bindings.
- A working graphical session. Native CalculiX is needed for Solve and Gmsh for
  **Mesh CAD…**; `--solver`, `--gmsh` and `--job-root` pass through to the GTK host.

The launcher discovers build outputs relative to the checkout and preserves
project paths with spaces. It can be invoked from another working directory.
It defaults `DOTNET_ROLL_FORWARD` to `Major` for the validated .NET 10 host/net8
renderer combination, preserving any explicit caller setting. No packages are
installed by the launcher. `--check` checks bindings and build artifacts, then runs each actual managed
executable with `--check-runtime`. The .NET host resolves that executable's shared
frameworks using the same roll-forward setting as a normal launch. Each probe
prints the selected .NET version and exits before reading a project, creating a
session or web host, or binding a listener. Normal launches perform the same
probes silently on success. Missing files, incompatible runtimes and executable
overrides therefore fail before opening the viewer. Display, solver and GPU
compatibility still require integration acceptance. `--check-solver` adds
CalculiX discovery and version-startup validation; it accepts CalculiX 2.21's
documented version text and its nonzero version-query status.
`--check-gmsh` validates Gmsh discovery and its numeric version response.

GTK options are forwarded after the project path, for example:

```bash
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh /absolute/path/to/project.pma --smoke
```

The existing Wayland occlusion smoke is also available through this launcher.
See [scheduling acceptance](LINUX_WAYLAND_FRAME_SCHEDULING.md) before using its
fullscreen test cover.

The selected production host uses `Gtk.Application` ID
`si.um.fs.prepomax.LinuxViewer`, matching the Debian desktop/AppStream IDs. To
require WebGL 2 backed by a live WebKit DRM render-node descriptor during the
normal render/reload/lifecycle smoke, run:

```bash
GDK_BACKEND=wayland scripts/start-gtk-viewer.sh /absolute/path/to/project.pma --smoke-gpu
```

The smoke rejects known software renderers and a missing `/dev/dri/renderD*`
handle. It qualifies the observed device/driver only; it is not the broader GPU
support matrix.

## Recorded validation and remaining distribution work

The build completed for the portable solution, renderer and locked client. The
current client graph passes 92 tests, production build and a zero-vulnerability
npm audit after updating vtk.js, Vite, Vitest and affected transitive packages.
A real X11 render/reload smoke launched from outside the checkout with a spaced
project filename and verified renderer-listener shutdown. `--check`, missing
project and missing-.NET failure paths, shell syntax and whitespace checks passed.
No numerical implementation changed, so the portable assertion suite was not rerun.

A [relocatable preview bundle](LINUX_VIEWER_BUNDLE.md) can now be produced with
`scripts/package-linux-viewer.sh OUTPUT.tar.gz`. It contains published managed
applications and the built client, and launches independently of the checkout.
The default archive now includes a private .NET 10 runtime and ASP.NET Core
runtime; `--framework-dependent` retains the smaller external-runtime form.
The [Debian/Ubuntu package](LINUX_DEBIAN_PACKAGE.md) wraps the private-runtime
archive with distribution-managed Python/GTK/WebKit dependencies and desktop,
AppStream and MIME integration. Its Ubuntu 24.04 container harness verifies clean
dependency resolution, install, higher-version replacement with XDG-state
preservation, private-runtime preflight, desktop/MIME caches and purge.
The package recommends Ubuntu's `calculix-ccx` and `gmsh`; the clean harness
verifies an analytical C3D8 solve and STEP-to-tetrahedral-PMA generation through
the installed private-runtime CLI. Signed update delivery, remaining native-tool integration and broad
GPU/distro acceptance remain open.

## Runtime-preflight follow-up

Both managed startup probes and a normal X11 render/reload/shutdown pass on the
recorded .NET-10-only machine. Setting `DOTNET_ROLL_FORWARD=LatestPatch` makes
preflight fail for the net8 renderer as expected, with a renderer-specific error;
the default `Major` policy passes. A deliberately invalid renderer port setting
still permits `--check`, confirming the probe exits before server configuration
is read. Managed builds report zero warnings/errors; shell syntax and whitespace
checks pass. No numerical code changed and its suite was not rerun.

The probe confirms executable startup and shared-framework resolution. It does
not exercise every lazily loaded library or replace the real viewer smoke test.

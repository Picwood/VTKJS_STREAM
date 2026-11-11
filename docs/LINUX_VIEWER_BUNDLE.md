# PrePoMax Linux viewer preview bundle

This relocatable developer preview runs the supported editable `.pma` workflow
and includes the bounded converter for supported unprotected `.pmx` projects.
It also contains the portable CLI used by the packaged CAD-to-Gmsh workflow.
It is not the complete PrePoMax desktop application or a distro installer.

Extract the archive into a directory you own, then run:

```bash
cd prepomax-linux-viewer
sha256sum --check SHA256SUMS
scripts/start-gtk-viewer.sh --check
scripts/start-gtk-viewer.sh
# Or open an existing project immediately:
scripts/start-gtk-viewer.sh /absolute/path/to/project.pma
# Select a PMX from Open project… to convert it to a new PMA.
```

You can move the extracted directory, including to a path containing spaces.
Launchers locate the published companion, renderer and client relative to the
bundle. A source checkout, SDK and Node.js are not required to launch it.

The default archive supplies .NET 10 and ASP.NET Core privately. The machine
still needs Python 3 with PyGObject, GTK 3 and WebKit2 4.1 bindings plus a
graphical session. `PREPOMAX_DOTNET` can override the private runtime;
`PREPOMAX_PYTHON` selects Python. Major runtime roll-forward lets the net8
renderer use the bundled .NET 10 stack. `--check` executes both runtime probes
and checks bindings/assets without starting the renderer service. CalculiX and
Gmsh are separate: use `--solver /path/to/ccx` and `--gmsh /path/to/gmsh`, or
their environment overrides. Job data defaults to XDG state storage, not the
bundle. No Python/GTK/WebKit runtime or native analysis tool is included.

The archive includes the published viewer companion, PMX converter, portable CLI
and NRBF reader, managed dependencies, built client files,
launchers, the repository license, build identification and SHA256SUMS. By
default it also carries a private Linux .NET 10 host, `Microsoft.NETCore.App`,
`Microsoft.AspNetCore.App`, their license and third-party notices. The launcher
prefers this runtime unless `PREPOMAX_DOTNET` explicitly selects another one.
Checksums detect accidental modification; they are not a publisher signature.
BUILD.txt records whether the source tree was modified, because the current
port can include changes beyond its recorded Git revision.

To build this preview from the checkout:

```bash
scripts/package-linux-viewer.sh /existing/output/directory/viewer-preview.tar.gz
# Smaller archive that uses a target-machine .NET 10 installation:
scripts/package-linux-viewer.sh --framework-dependent /existing/output/directory/viewer-fd.tar.gz
```

For a system-integrated Debian/Ubuntu artifact, pass the default archive to
[`package-linux-deb.sh`](LINUX_DEBIAN_PACKAGE.md), or let that builder create it:

```bash
scripts/package-linux-deb.sh prepomax-linux-viewer.deb
```

The packager builds the full stack, publishes the viewer, PMX converter, CLI and
renderer managed executables, stages
only runtime files and creates a normalized archive. It refuses to overwrite
an existing output and cleans temporary staging after success or failure.
The default target .NET configuration is runtime-bundled; BUILD.txt records the
exact framework versions. The optional framework-dependent form retains the old
target-machine requirement. The Debian wrapper is byte reproducible for the
recorded input and supplies distro installation plus Python/GTK/WebKit dependency
metadata. Archive reproducibility, signing/update delivery, native-tool bundling,
complete redistribution review and clean-machine GPU acceptance remain release
gates.

## Local evidence (2026-10-27)

The runtime-bundled archive is 53,366,555 bytes (51 MiB) compressed and 121 MiB
extracted, with SHA-256
`0e616917084842631da31e5e7f14a878bc7c8fbf4b236af0564f73e98e1db116`. It was
unpacked outside the repository under a directory with spaces. Every manifest
hash passed. With `HOME` and `XDG_DATA_HOME` redirected and `PREPOMAX_DOTNET`
unset, both runtime probes selected the archive's .NET 10.0.12 frameworks and a
real X11 render/reload/shutdown smoke passed, including renderer listener closure.
The 3.7 MiB framework-dependent archive also passes preflight with an explicitly
selected external runtime and contains no accidental runtime directory.

This bundle also contains PMA-v5 source-geometry retention. CAD projects expose
the retained filename, size and checksum, the CLI can extract the exact bytes,
and the GTK shell enables **Export CAD…** and **Remesh CAD…** when a project owns
that payload. CLI and GTK remeshing accept bounded global Gmsh controls, persist
them in the PMA, prefill them after reopen and publish a fresh project without
changing the source PMA.

The client dependency update to vtk.js 36.14.2, Vite 8.3.5 and Vitest 4.1.12
passes 92 tests and the production build. Exact transitive overrides remove four
additional affected packages; both full and production-only npm audits report
zero vulnerabilities. Package creation uses `npm ci`, reproduces that clean
graph, and now executes both managed startup probes against the staged private
runtime before publishing the archive. Shell syntax, Python compilation and
whitespace checks pass. Python/GTK/WebKit still came from the host, so this is
strong relocation/runtime evidence rather than clean-machine qualification.

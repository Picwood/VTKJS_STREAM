# Linux command-line application host

Current commands also cover bounded mesh/CAD import and remeshing,
[static step authoring](LINUX_STEP_EDITING.md), independent elastic-material
creation, [plastic material properties](LINUX_PLASTICITY_EDITING.md),
[solid section authoring](LINUX_SECTION_EDITING.md),
[field output requests](LINUX_FIELD_OUTPUT_EDITING.md), and
[history output requests](LINUX_HISTORY_OUTPUT_EDITING.md). Result-side commands
also expose [history catalog, aligned table and delimited export](LINUX_HISTORY_RESULTS.md). `inspect`
reports active steps, section coverage and solve readiness. A solve with no
active step, incomplete solid assignments or invalid active material/region
references is refused before job creation. Installed packages provide the
`prepomax-linux-cli` launcher. The original host milestone below is historical;
see [current progress](LINUX_PORT_PROGRESS.md) for current capabilities and checks.

Implemented 2026-09-17 in `portable/PrePoMax.Cli`, included in the portable
solution. This executable connects editable project persistence, the production
CalculiX exporter and native job execution without the characterization runner.

Build and invoke from the repository root (.NET 10):

```bash
dotnet build portable/PrePoMax.Core.slnx -c Release
dotnet portable/PrePoMax.Cli/bin/Release/net10.0/PrePoMax.Cli.dll inspect model.pma
dotnet portable/PrePoMax.Cli/bin/Release/net10.0/PrePoMax.Cli.dll export model.pma new.inp
dotnet portable/PrePoMax.Cli/bin/Release/net10.0/PrePoMax.Cli.dll solve model.pma ./jobs /absolute/path/to/ccx
```

`inspect` prints JSON with model identity, mesh counts, parts, materials and
archived result availability. `export` refuses to overwrite an existing file.
`solve` resolves ccx on PATH when no executable is supplied. It creates a unique
job directory under the requested root and retains input, stdout/stderr logs and
solver artifacts on success or failure. Success prints the new `result.pma` path
on stdout; diagnostics go to stderr. The source archive is never modified.

Ctrl+C cancels the native process tree. Each solve has a 30-minute timeout.
Exit codes are 0 for success, 1 for failure, 2 for usage errors and 130 for
cancellation. Nonempty FRD output is required; DAT is required when an active history output
is requested, otherwise it is optional. Each retained result payload is limited to
128 MiB before loading; the archive's combined payload limit still applies.
A successful exit and output presence do not establish physical validity of an
arbitrary model. The host archives result bytes; it does not render results.
Native runtime libraries must already be installed or configured in the inherited
environment (for example LD_LIBRARY_PATH).

Acceptance is part of `python3 scripts/test-native-calculix.py`: inspect a
multipart archive, export without overwrite, solve through the CLI, reopen the
result archive and compare DAT bytes with the independently solved native fixture.
It also verifies unchanged source bytes, missing-project and invalid-command
errors, nonzero solver exits and zero-exit solvers that produce no results.
The existing portable suite remains at 3,733 checks; CLI integration is counted
separately. No Windows execution or interactive cancellation acceptance is
claimed by this milestone.

This is an initial headless host for the bounded static metadata schema. It does
not implement model creation/editing commands, geometry import/meshing, a portable
controller API, VTK.js rendering, Linux WebView hosting or installation packaging.
Those remain application integration gates in [the progress record](LINUX_PORT_PROGRESS.md).

Validation completed on Linux: solution build (zero warnings/errors), 3,733
portable checks and all seven existing native cases plus the CLI multipart solve
and failure checks passed. This workstation required `DOTNET_PROCESSOR_COUNT=2`
after an MSBuild worker failure during the first native run. Native artifacts:
`/tmp/prepomax calculix job 84m1x21p`.

Project and solver orchestration now resides in
[PrePoMax.Application](LINUX_APPLICATION_SERVICES.md), shared by the CLI and
available to a future interactive host. Earlier check totals record the CLI milestone.

The `scene PROJECT.pma NEW_SCENE.bin` command now exports undeformed mesh geometry
for the renderer host-upload protocol; see [scene bridge](LINUX_RENDER_SCENE_BRIDGE.md).

`render-check PROJECT.pma DOTNET_EXECUTABLE RENDER_SERVER.dll` now exercises
[renderer startup, authenticated publication and shutdown](LINUX_RENDERER_HOST.md).

# GTK analysis job controls

Implemented and validated 2026-09-22. The GTK viewer now connects to the existing
portable `ProjectService` / `ProjectSolveJob` through its private companion:

- **Solve** starts an independent project job in a unique output directory.
- **Cancel analysis** requests cancellation without blocking GTK or pipe handling.
- The toolbar shows preparation, solver state, elapsed time and terminal status;
  its tooltip identifies the retained job directory for input and logs.
- **Open result project** reopens the newly saved editable archive after success.
  [Field selection](LINUX_RESULT_CONTOURS.md) now displays archived static
  contours on that undeformed geometry.

Specify `--solver /absolute/path/to/ccx`, or make `ccx` available on PATH.
`--job-root /path/to/jobs` selects the output root; the default is
`$XDG_STATE_HOME/prepomax/jobs`, falling back to `~/.local/state/prepomax/jobs`.
Native library setup is inherited from the launching environment. The source
project is not overwritten. Result archives contain the actual FRD/DAT payloads
and the model used by the job.

The companion prepares/runs the analysis on a background task, leaving its
command loop available for status and cancellation. Each instance permits one
active analysis. Both the UI and companion reject another solve or project
switch while it is active. Success, failure and cancellation re-enable controls.
Closing the viewer cancels and awaits the active job before renderer disposal;
the launcher retains its existing bounded process-group shutdown fallback.
Cancellation follows the existing archive commit rule: cancellation after atomic
result publication does not undo a successful archive. Files/logs from failed or
cancelled jobs are retained.

## Acceptance

Build the portable solution, then use the GTK launch arguments documented in
[the GTK spike](LINUX_GTK_VIEWER_SPIKE.md):

- Add `--smoke --smoke-solve --solver /path/to/ccx --job-root /tmp/gtk-jobs` to
  activate Solve, poll the real job, activate Open result project and require a
  fresh stable-frame acknowledgement after reopening.
- Add `--smoke --smoke-cancel --solver /path/to/slow-test-executable` for the
  cancellation path. The executable must stay running long enough to observe it.
- `python3 scripts/test-viewer-jobs.py DOTNET MULTIPART.pma SERVER.dll CCX` tests
  real CalculiX solve/archive reopen, cancellation of a live test process,
  failed-solver retry, duplicate/open guards, source-byte preservation and
  EOF-triggered process cleanup. This script is Linux-specific (`/proc` checks).

All of these passed locally. GTK's native solve produced a result archive and
reopened its two-part mesh; the live GTK cancellation probe reached Cancelled.
The companion build passed with zero warnings/errors, and Chromium viewer
regression passed with the same updated companion. Tests used the existing
net8.0 renderer with explicit .NET 10 runtime roll-forward and the workstation's
CalculiX runtime libraries. These are opt-in integration checks, separate from
the existing 3,771 portable characterization checks.

## Remaining scope

The UI has no model-editing or mesh-generation controls yet. Static result-field
controls are now covered in [contour evidence](LINUX_RESULT_CONTOURS.md).
Success is based on the existing solver/output/archive checks, not a general
physical-validity judgment. Archives may contain stale results after external
edits, as documented by persistence. [Displacement-scale controls](LINUX_DEFORMED_RESULTS.md) now support static deformed geometry. Native shell accessibility,
clipboard, DPI, GPU and packaging gates remain open.

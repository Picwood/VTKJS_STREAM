# Portable native process execution

2026-09-12: portable/CaeJob compiles the new CaeJob/Native source directory on
.NET 10. This is a reusable process service, separate from legacy ExecutableJob
and AnalysisJob, which still use Windows services and UI-era job state. It does
not change the net48 job assembly or wire the desktop controller to a new API.

NativeTool resolves an explicit path or searches PATH (adding .exe on Windows
when needed). A missing configured tool fails rather than silently falling back.
File existence is checked at discovery; executable permission and loader failures
are reported by Process.Start. This is not a tool-version compatibility policy.

NativeProcess accepts separate arguments through ArgumentList without a shell,
a working directory, timeout, cancellation token and capture limit. It drains
stdout/stderr concurrently, retains at most the configured character count per
stream (default 1 MiB), and reports truncation and the native exit code. Nonzero
exits are returned to the caller; launch errors throw. Cancellation/timeout
attempts process-tree termination and waits for the direct process to exit;
explicit cancellation throws OperationCanceledException and deadline expiry
throws TimeoutException. Process cleanup failures are surfaced to callers.

The Gmsh smoke now routes actual mesh generation through this service using the
MeshIO executable's --run-native mode. Python remains the independent mesh reader
and fixture orchestrator. Both volume and boundary round trips still pass.

## Validation

The default script passes **2,813 assertions**, including 29 native-process/job
checks: configured and PATH discovery, missing paths, literal arguments with
spaces/Unicode/quotes/empty strings/shell punctuation, working directory, stderr,
concurrent high-volume output with bounded capture, nonzero exits, timeout,
cancellation and direct-child termination after cancellation. The child publishes
its PID before cancellation, avoiding a test that only cancels before launch.
CaeJob changes now trigger the existing Windows/Linux portable CI matrix.

`python3 scripts/test-native-gmsh.py` also passes with Gmsh 4.14.0-git: 387 positive
tetrahedra, unit volume, surface area 6, and volume/boundary import-export checks
under both cultures. An overlapping .NET build run lost MSBuild workers; the
subsequent sequential complete suite passed. Windows execution was not performed
locally. Shell syntax and diff whitespace checks pass.

Detached/reparented descendants, descendants surviving a parent exit, process
group ownership, permission-denied termination and interactive console behavior
are not qualified. Process.Kill(entireProcessTree: true) is used for a running
parent, but the tests establish direct-child cleanup rather than a guarantee
about every descendant. The implementation is not an OS sandbox.

## Job lifecycle and live progress

NativeJob now exposes thread-safe output character counters and pollable elapsed
time, in addition to its one-shot status/result API. The counters include all
drained decoded characters, even after captured text reaches its limit. They
are independent snapshots, not a completion percentage or an ordered log.
Elapsed time freezes on completion; counters survive cancellation. Captured output text on timeout/cancellation is unavailable in the result.
Optional NativeLogFiles now preserve drained stdout/stderr as separate UTF-8
files, flushed per chunk and retained after cancellation. Files use CreateNew
to avoid overwriting prior jobs; callers must provide existing parent directories.
Disk usage is not bounded by the in-memory capture limit. Log I/O errors fail
the job; this does not guarantee capture of child output not yet drained when
cancellation occurs. No UI callback or synchronization context is required.

The lifecycle harness covers idle/running/success/failure, failing exit results,
launch diagnostics, timeout, prelaunch and running cancellation, argument snapshots
and rejected repeated/concurrent runs. Four additional checks verify progress
before exit, retention after cancellation, a stopped clock, and counters beyond
capture truncation. The native Gmsh workflow uses NativeJob through the MeshIO
harness. Windows execution remains unverified locally.

Three additional tests cover full logs beyond capture truncation, existing-file
protection and retained cancellation diagnostics.

Next: run the [prepared CalculiX acceptance check](CALCULIX_INTEGRATION.md) when
the pending build is available. Production callers must interpret exit codes and truncation, and
connect cancellation to their UI/job lifecycle. Solver input export, model and
results extraction, packaging and the Linux shell remain separate gates.

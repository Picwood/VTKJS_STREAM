# Portable project application services

Implemented 2026-09-17 in `portable/PrePoMax.Application`. The CLI now delegates
project inspection, input export and solve orchestration to this library.
It has no console, WinForms, WebView or renderer dependency.

`ProjectService.Inspect` returns a typed summary; `Export` writes a new input
file without overwriting existing files. `PrepareSolve` opens an independent
model snapshot, resolves the solver and creates a unique directory containing
its input. These preparation operations are synchronous; an interactive host
must dispatch them off its UI thread.

The returned `ProjectSolveJob` exposes its artifact directory, status, native
output character counts and solver elapsed time. `RunAsync` accepts a cancellation
token and can run once only. States are Prepared, Running, SavingResults,
Succeeded, Failed, Cancelled and TimedOut. Completion returns a typed result with
the native process diagnostics, error and optional output archive path. A zero
solver exit followed by missing result files is a failed project job. A retry
requires a newly prepared job and directory.

Cancellation before invocation launches no process. Cancellation during execution
uses the native process-tree termination path. Cancellation is checked before
collecting results and before saving; archive publication is an atomic commit
and is not interrupted mid-save. A cancellation arriving during publication can
therefore finish successfully. Failed and cancelled jobs retain diagnostics and
do not report an output archive. Source projects remain unchanged.

Timeout defaults to 30 minutes and can be supplied by a host. Existing CLI
commands and exit codes remain supported. Result payload and editable-schema
limits are unchanged; see [CLI host](LINUX_CLI_HOST.md).

Validation covers multipart inspection, pre-start and in-flight cancellation,
timeout, failed process diagnostics, zero-exit missing results, repeat execution,
independent directories and source preservation in both tested locales. Native
CLI acceptance continues through this service and checks real multipart DAT
output and archived results against the existing CalculiX fixture.

The boundary now also includes bounded material, load, displacement-boundary
and node-set edits plus `.inp`/Medit mesh import. Force creation accepts nonempty
node sets for per-node concentrated force and valid element surfaces for
total-force surface traction. `ProjectPressures` lists, creates, changes the
magnitude of and removes finite constant `DLoad` pressure on valid named element
surfaces. All edit operations publish a new project and discard stale results.
`ProjectSurfaces` validates bounded solid element-face selections, creates the
backing element sets and named surface, and resolves face nodes for renderer
preview. It also maps renderer-server-validated source triangles to distinct
production element faces used by GTK click and rectangle selection, with one
project open for a batch of at most 1,000 triangles and 100 faces. Created
surfaces become pressure and traction targets without another conversion step.
`ProjectScene` can publish those normalized faces as one translucent,
non-pickable preview actor, optionally alongside the existing selected-node
markers used by the surface dialog.
`ProjectSurfaces.GrowFace` resolves one authenticated triangle, verifies that its
face is on the solid boundary, and performs bounded local adjacency growth using
the absolute angle between production face normals. It accepts 0–90° and fails
before returning more than 100 faces.
The `.inp` reader retains bounded element-based surface definitions for these
workflows.
`ProjectBoundaries` also lists valid fixed-support targets and creates a
production `FixedBC` on a nonempty node set in one selected step. It derives the
2D/3D constraint form from model space, enforces step-local unique names and uses
the same new-file/result-invalidation boundary as other edits.
`ProjectMaterials` now lists solid-section assignments and can create one
constant elastic material independently or while assigning it to a selected
`SolidSection` in the same atomic new-file edit. It enforces finite elastic values and
case-insensitive material-name uniqueness. It can also reassign a solid section
to a different existing supported material without changing the material
collection, and remove a material only after all section references are gone.
Constant density can be added, changed or removed with the same material edit.
The same boundary now lists and writes a bounded non-temperature-dependent
`Plastic` stress/strain table with Isotropic, Kinematic or Combined hardening,
and removes an existing plastic property. It validates positive finite stress,
zero first plastic strain and strictly increasing nonnegative strains before
publishing. See [plasticity editing](LINUX_PLASTICITY_EDITING.md).
`ProjectGravity` discovers valid density-backed solid regions, lists existing
gravity definitions, creates or edits a finite nonzero gravity vector and
removes an existing gravity load in one selected step, using the same new-file
and stale-result boundary.
`ProjectCentrifugal` exposes the corresponding three-dimensional centrifugal
workflow. It validates a finite axis point, nonzero finite direction and positive
rotational speed, while limiting creation to density-backed solid regions. It
lists, creates, edits and removes loads through the same atomic new-file boundary.

This is still not a complete extraction of the Windows controller. Geometry
editing, broad meshing services, broader section types/contact authoring and other desktop
workflows remain open. PEND-006 is complete with GTK selected; these are feature
parity gaps within that host.

## Static step authoring

`ProjectSteps` now exposes original-style static add/edit/rename/duplicate,
activation and removal through GTK and CLI. Its typed copies share the production
persistence codec. See [step editing](LINUX_STEP_EDITING.md) and the
[current parity priorities](LINUX_FUNCTION_PARITY.md). Solver preparation rejects
an empty/inactive sequence before making job files. Result publication requires
FRD and requires DAT only when an active history request exists, so original
field-output-only static defaults can solve and reopen normally.

## Solid section authoring

`ProjectSections` exposes named-part/element-set solid-section add, edit/rename,
duplicate, activation and removal through GTK and CLI. Coverage follows active
mesh elements and export order, including part-derived sets with sparse IDs and
later active overrides. Inspection reports missing assignments, overlaps and
effective counts; solve preparation rejects incomplete coverage or invalid active
material/region references before creating a job. Empty section/material models
can recover through independent elastic-material creation and a new section.
See [section editing](LINUX_SECTION_EDITING.md) for scope and acceptance.

## Field output request authoring

`ProjectFieldOutputs` exposes nodal, element and contact request add,
edit/rename, duplicate, propagation, activation and removal for static steps.
It validates the production variable enums and advanced file-output flags,
preserves request order, and makes independent copies when propagating to later
steps. See [field output editing](LINUX_FIELD_OUTPUT_EDITING.md) for commands,
bounds and acceptance.

## History output request authoring

`ProjectHistoryOutputs` exposes nodal, element, contact and section request add,
edit/rename, duplicate, propagation, activation and removal for static steps.
It validates each production variable enum against its compatible named-region
types, retains request order and copies requests independently across later
steps. The persistence codec preserves all four concrete subtypes. See
[history output editing](LINUX_HISTORY_OUTPUT_EDITING.md) for commands, bounds
and solver/package acceptance.

## History-result browsing and export

`ProjectHistoryResults` loads production histories from raw archived DAT or the
binary result payload, returns a bounded set/field/component catalog, and uses
`FeResults.GetHistoryOutputData` for aligned tables. Export delegates to
`ResultHistoryOutputWriter`, supports selected or all result sets with comma,
semicolon or tab separators, stages beside the destination and refuses
overwrite. GTK, the viewer companion and CLI share this service. See
[history-result browsing and export](LINUX_HISTORY_RESULTS.md) for limits and
solver/package acceptance.

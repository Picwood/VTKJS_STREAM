# Linux static analysis step authoring

Validated: 2026-10-28.

GTK **Steps…** now lists the static analysis sequence and provides **Add…**,
**Edit…**, **Duplicate…**, **Activate**, **Deactivate** and **Remove**. Each action
saves a new PMA and opens it, preserving the source file. Editing includes name,
NLGEOM, Default/Automatic/Direct incrementation, time period, initial/minimum/
maximum increment and maximum increment count. The time controls follow the
original form's mode visibility; Default uses CalculiX's default step settings.

New static steps append to the sequence. The default copy-last option copies
supports and loads from the last step using independent definitions. As in the
original `StepCollection.AddStep`, it does not copy output requests: the new
production `StaticStep` constructor supplies nodal U/RF and element E/S outputs.
Full duplication instead retains settings, supports, loads and both field/history
outputs, appending a separately named step. Editing keeps its position, children
and unexposed solver/output settings. Removing a step retains shared named mesh
regions and the other steps. Orphan internal-selection cleanup and load/support propagation remain outside
this slice.

All step edits discard stale result payloads and preserve materials, mesh,
regions, meshing controls and exact retained CAD/source-topology bindings. The
last step can be removed, leaving an editable model; a new sequence can then be
created. Solve is disabled in GTK and refused before creating a job when no active
analysis step exists.

## CLI

```sh
prepomax-linux-cli steps original.pma
prepomax-linux-cli add-step original.pma two-steps.pma load-2 \
  --mode automatic --period 2 --initial 0.5 --max-increment 1 --max-increments 200
prepomax-linux-cli edit-step two-steps.pma renamed.pma load-2 reload --mode direct
prepomax-linux-cli duplicate-step renamed.pma duplicated.pma reload later
prepomax-linux-cli step-active duplicated.pma inactive.pma later false
prepomax-linux-cli remove-step inactive.pma final.pma later
```

`add-step` defaults to `--copy-previous true`; use `false` for a step without
copied supports/loads. Add/edit also accept `--nlgeom true|false`,
`--min-increment TIME` and the controls shown above. Edit starts with the selected
step's existing values and changes only the supplied controls/name. The original
writer remains responsible for solver keywords, including `*Static, Direct`.

Names must be unique ignoring case, 1–128 characters without surrounding
whitespace/control characters. Times must be finite and positive with
min ≤ initial ≤ max and initial ≤ period. The increment count is 1–1,000,000;
append/duplicate supports at most 1,000 steps. Inactive states persist normally.
Models with positional CalculiX user keywords currently refuse these step edits
because insertion paths have not yet been remapped; they are not silently
retargeted or dropped. GTK keeps the sequence inspectable and disables edits
with an explanation for these projects. Custom step controls and defined fields remain outside the
current editable archive contract. Other step types are not yet exposed here.

## Original behavior and implementation

The workflow is grounded in the original [Controller](../PrePoMax/Controller.cs),
[StepCollection](../CaeModel/Steps/StepCollection.Legacy.cs),
[StaticStep](../CaeModel/Steps/StaticStep.cs), and
[ViewStaticStep](../PrePoMax/Forms/51_Step/ViewStaticStep.cs). Linux uses the same
production definitions and keyword writers. The persistence codec now exposes a
bounded typed static-step copy, shared with archive restoration, so copying does
not use legacy binary serialization or duplicate the model/mesh. No new PMA or
metadata version is required.

## Acceptance

The portable suite passes **5,367 checks**, including copy independence and
supported definition kinds, original default-output behavior, duplicate/rename/
remove/activation, empty-sequence recovery, bounds/name checks, user-keyword
refusal, source/topology preservation, result invalidation and failed-save cleanup.

Native CalculiX 2.23 acceptance creates a second step through the CLI, edits its
controls/name, changes its copied load independently, duplicates/deactivates a
step, solves, and removes the inactive step. Production result fingerprints show
maximum U1=0.001 at time 1 and U1=0.002 at time 2. Result invalidation after the
removal also passes. A field-output-only case solves successfully without an
unrequested DAT history file. FRD remains required; DAT is required when an active
history request exists and is preserved when present.

GTK X11 software-WebGL acceptance passes all six operations, removal/recreation
of an empty sequence, disabled Solve for inactive/empty sequences, and actual
solve/result reopening after authoring. The field-output-only solve/reopen passes
as well. These functional tests do not establish new physical-GPU or Wayland
qualification.

Clean Ubuntu 24.04 acceptance passes installed step add/edit/duplicate/deactivate/
remove commands, Direct-mode export, and preservation of CAD topology and face/
box controls. The authored two-step project stays byte-identical across the
`0.1.1~preview+steps.1` to `.2` package upgrade, reopens with identical inspection
information, and survives purge while application integration is removed.
The distribution CalculiX 2.21 analytical cube regression still passes.

Artifact hashes and command summaries are recorded in
[the acceptance evidence](evidence/linux-static-steps-2026-10-28.txt).

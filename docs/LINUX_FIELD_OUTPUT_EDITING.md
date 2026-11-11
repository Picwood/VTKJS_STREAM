# Linux field output request authoring

Validated: 2026-10-28.

GTK **Field outputs…** lists requests by analysis step and provides **Add…**,
**Edit…**, **Duplicate…**, **Propagate**, **Activate**, **Deactivate** and
**Remove**. The editor supports the original nodal, element and contact request
types, their complete variable flags, last-iteration and contact-element modes,
global output, and the element Default/2D/3D mode.

New and duplicated requests append within their selected step. Editing/renaming
retains the original position and type, matching the original form's disabled
type selector. Propagation copies the selected request independently to every
later static step: a same-named request is replaced in place, while a missing
one appends. Requests are listed and exported in step order.

Every operation saves and reopens a new PMA, preserves the source file, mesh,
analysis definitions, meshing controls and exact source-bound CAD topology, and
discards stale FRD/DAT/binary results. Named regions and history requests are
unchanged. No archive or metadata version change is required.

## CLI

```sh
prepomax-linux-cli field-outputs original.pma
prepomax-linux-cli add-field-output original.pma added.pma load-1 response nodal U,RF
prepomax-linux-cli edit-field-output added.pma edited.pma load-1 response response-all \
  nodal U,RF --last-iterations true --global false
prepomax-linux-cli duplicate-field-output edited.pma duplicated.pma load-1 response-all response-copy
prepomax-linux-cli field-output-active duplicated.pma inactive.pma load-1 response-copy false
prepomax-linux-cli propagate-field-output inactive.pma propagated.pma load-1 response-all
prepomax-linux-cli remove-field-output propagated.pma final.pma load-1 response-copy
```

Variables are comma-separated original CalculiX names. Nodal choices are RF, U,
PU, V, NT, PNT and RFL. Element choices are S, PHS, E, ME, PEEQ, ENER, HFL,
ERR, HER, ZZS and SDV. Contact choices are CDIS, CSTR, CELS and PCON. Add/edit
also accept `--contact-elements`, `--global`, `--last-iterations` and
`--element-output default|2d|3d`.

Names must be unique within a step ignoring case, 1–128 characters without
surrounding whitespace or control characters. At least one valid variable is
required. A step is bounded to 1,000 field requests in this editor. Positional
custom CalculiX keywords make output editing read-only until insertion paths can
be remapped; requests remain inspectable.

## Original behavior and bounds

The workflow follows the original field-output operations in
[Controller](../PrePoMax/Controller.cs), the defaults and locked edit type in
[FrmFieldOutput](../PrePoMax/Forms/53_FieldOutput/FrmFieldOutput.cs), and the
production nodal, element and contact writers under
[CalculiX field output](../CaeModel/FileInOut/Output/Calculix/Keywords/Step/FieldOutput).
Linux edits the production `NodalFieldOutput`, `ElementFieldOutput` and
`ContactFieldOutput` definitions.

Named-region nodal/element/contact/section history-request editors are now
implemented. History visualization/export, selection-owned history regions,
non-static step compatibility, custom step output frequency and in-session undo/redo remain open. Save-as-new operations do not
supply model-tree or command-history parity. CAD repair remains outside scope.

## Acceptance

The portable suite passes **5,481 checks**, including all request types and
variables, advanced flags, ordering, replacement/append propagation, independent
copies, activation, invalid settings, collision/overwrite refusal, positional
keyword handling, source/topology preservation and result invalidation.

Native CalculiX 2.23 acceptance edits a result-bearing two-step project, performs
all six operations plus propagation, verifies the generated production keywords,
solves, reopens results and retains maximum U1=0.001/0.002 at times 1/2. The
source result project remains byte-identical.

GTK X11 software-WebGL acceptance covers add, edit, duplicate, propagate,
activate/deactivate and remove, with save/reopen and result invalidation. These
are functional checks without new physical-GPU or Wayland qualification.

Clean Ubuntu 24.04 acceptance exercises the installed commands and production
export, retains CAD/meshing state, and preserves the authored project through an
in-place package upgrade and purge. Distribution CalculiX 2.21 and Gmsh 4.12.1
regressions continue to pass; the new analytical two-step field-output solve
runs on the development host.

Artifact hashes and command summaries are recorded in
[the acceptance evidence](evidence/linux-field-outputs-2026-10-28.txt).

# Linux solid section authoring

Validated: 2026-10-28.

GTK **Sections…** lists solid assignments in export order and provides **Add…**,
**Edit…**, **Duplicate…**, **Activate**, **Deactivate** and **Remove**. Add/edit
expose the section name, existing material and named mesh part or element set.
Part-derived element sets expand using actual part IDs, including sparse IDs.
New and duplicated sections append; editing/renaming retains the original
position. Later active sections take precedence on overlapping elements, as
confirmed with the production CalculiX writer and a native solve.

The manager reports region element counts, effective assignment counts,
unassigned active elements, overlaps and invalid material/region references.
Overlaps are allowed. Every active element needs an active section and material
before solving; GTK disables Solve and CLI refuses before creating a job when
coverage is incomplete. This check supplements the active-analysis-step check;
it is not a complete solver-model validity audit.

Removing all sections leaves an editable project. **Add material…** can now
create a constant elastic material without assigning it to a section, including
when no materials or sections remain. A new section can then restore coverage.
Removing a section retains shared named regions and materials.

Each operation saves and reopens a new PMA, preserves the source file, mesh,
analysis definitions, meshing controls and exact source-bound CAD topology, and
discards stale results. No new archive or metadata version is required.

## CLI

```sh
prepomax-linux-cli sections original.pma
prepomax-linux-cli add-material original.pma material.pma steel 210000 0.3
prepomax-linux-cli add-section material.pma assigned.pma solid steel part block
prepomax-linux-cli edit-section assigned.pma edited.pma solid renamed steel set SOLID
prepomax-linux-cli duplicate-section edited.pma duplicated.pma renamed override
prepomax-linux-cli section-active duplicated.pma inactive.pma override false
prepomax-linux-cli remove-section inactive.pma final.pma override
```

`sections` returns definitions, valid region/material choices and coverage.
`part` and `set` select named parts and element sets respectively. Names must be
unique ignoring case, 1–128 characters without surrounding whitespace or control
characters. Assigned materials must exist; inactive materials remain visible and
prevent solve readiness when used by active sections.

## Original behavior and bounds

This slice follows the original section operations in
[Controller](../PrePoMax/Controller.cs), assignment order and part-derived set
handling in [FeModel](../CaeModel/FeModel.cs), and active-section export in the
[CalculiX writer](../CaeModel/FileInOut/Output/Calculix/CalculixFileWriter.cs).
Linux uses the production `SolidSection` definition and keyword writer.

The editable scope is named-region **3D solid sections**. Shell, membrane, mass
and 2D thickness editors, graphical section-region selection and original
selection-owned set cleanup remain open. Existing solid thickness is preserved
on edit/duplicate. Positional custom CalculiX keywords make section editing
read-only until placement can be remapped; definitions remain inspectable.
Save-as-new operations do not supply in-session undo/redo or model-tree parity.
CAD repair remains outside project scope.

## Acceptance

The portable suite passes **5,427 checks**, including section operations,
precedence/effective coverage, sparse part-derived regions, empty-section and
empty-material recovery, inactive definitions, invalid targets, collision and
overwrite refusal, source/topology preservation and result invalidation.

Native CalculiX 2.23 tests create an independent material, remove both sections
from a two-block model, reject solving it, restore all-part coverage, duplicate
and edit a local override, deactivate/reactivate it and solve each assignment.
With E=4000 throughout, maximum U1=0.00025. A later E=2000 section on the first
block produces maximum U1=0.0005 and mean U1=0.0001875; deactivating it restores
the uniform result. The source file stays byte-identical.

GTK X11 software-WebGL acceptance covers all six section operations, recovery
through a part-derived element set, independent material creation, existing
create-and-assign behavior and an actual solve/result reopen. These are
functional checks, without new physical-GPU or Wayland qualification.

Clean Ubuntu 24.04 acceptance exercises the installed section commands,
independent material creation, incomplete-coverage refusal/recovery, production
export and retained CAD/setup preservation. The authored project survives an
in-place package upgrade and purge unchanged. Distribution CalculiX 2.21 and
Gmsh 4.12.1 regressions continue to pass; the new analytical overlap checks run
on the development host.

Artifact hashes and command summaries are recorded in
[the acceptance evidence](evidence/linux-solid-sections-2026-10-28.txt).

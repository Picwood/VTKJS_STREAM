# Linux elastic material editing

Updated: 2026-10-28. Original material workflow validated 2026-10-22;
independent creation validated with [solid section authoring](LINUX_SECTION_EDITING.md).

The GTK viewer offers **Edit material…**, **Add material…** and
**Assign material…** for supported editable `.pma` projects. Editing changes one
existing constant elastic material. Adding creates a named constant elastic
material with optional assignment to an existing solid section in the same
new-project operation. Independent creation also works with no existing materials
or sections. Assigning switches a solid section to a different
existing supported material without duplicating it. The host reopens and
renders the saved project. The edit dialog can also remove the selected material
when no section references it; assigned materials have removal disabled.
Young’s modulus and optional density use the existing model’s units. Elastic
value entry requires finite E > 0 and -1 < nu < 0.5; density must be blank or
finite and positive.

`PrePoMax.Application.ProjectMaterials` now lists solid-section assignments,
updates a selected elastic property, atomically adds an `Elastic` property and
changes `SolidSection.MaterialName`, or assigns an existing material. It rejects
an unknown section or material, a no-op assignment, removal of an assigned
material, or a new material name that collides case-insensitively. It regenerates
solver input and metadata and publishes the archive without replacing an existing file. The
source, other materials and other section assignments remain unchanged. Both
archived FRD and DAT payloads are omitted because previous results no longer
describe the edited model. Edits are refused while a solver job is active.

The elastic editor supports one constant elastic row and zero or one constant
density row, with no temperature-dependent table. Optional assignment targets
solid sections only. Section creation/editing is available through the separate
[Sections… manager](LINUX_SECTION_EDITING.md). The separate
[Plasticity… editor](LINUX_PLASTICITY_EDITING.md) now covers bounded
non-temperature-dependent stress/strain tables and all three production
hardening modes. Thermal properties, temperature tables and material libraries
remain outside these editors.
See [density and gravity authoring](LINUX_GRAVITY_EDITING.md). All existing
bounded static archive restrictions still apply.

## Verification

The latest independent-creation and section recovery acceptance passes within
the **5,427-check** suite, native CalculiX, GTK and clean Ubuntu package runs;
see [solid section acceptance](LINUX_SECTION_EDITING.md). The original
material-specific evidence below remains a historical baseline.

- Portable suite: **5,141 checks**, including **2,126 mesh I/O checks**. The
  material cases verify solid-section discovery, elastic values, assignment
  persistence, preservation of other assignments, case-insensitive collision
  rejection, unknown-section rejection and stale-result removal.
- Native GTK/WebKitGTK under X11: create `created-elastic`, assign it to the
  cube's solid section, save, reopen and verify both the material and assignment
  plus absence of stale fields. Renderer shutdown also passes.
- Native CalculiX 2.23: the GTK-created material doubles E from 1000 to 2000.
  The reopened archive solves through the CLI with FRD/DAT output. DISP/U1
  maximum is 0.0005 in step 1 and 0.001 in step 2, exactly half the fixture's
  previous 0.001 and 0.002 response. This proves the new assignment reaches the
  production solver deck.
- Native reassignment acceptance switches that section back to the existing
  E=1000 material without creating another material. CalculiX DISP/U1 returns
  to maximum 0.001 in step 1 and 0.002 in step 2.
- Native removal acceptance deletes the now-unused E=2000 material, reopens the
  project with no stale fields and solves successfully. Step-2 DISP/U1 retains
  the same maximum 0.002 and deterministic fingerprint.

Repeat the GTK check with a supported result-bearing fixture and a destination
that does not already exist:

```sh
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh /path/to/source.pma \
  --smoke-material /path/to/new-material-project.pma --smoke-timeout 40

GDK_BACKEND=x11 scripts/start-gtk-viewer.sh /path/to/source-with-unused-material.pma \
  --smoke-material /path/to/material-removed.pma --smoke-material-action remove \
  --smoke-timeout 50

GDK_BACKEND=x11 scripts/start-gtk-viewer.sh /path/to/source.pma \
  --smoke-add-material /path/to/new-assigned-material.pma --smoke-timeout 50

GDK_BACKEND=x11 scripts/start-gtk-viewer.sh /path/to/two-material-source.pma \
  --smoke-assign-material /path/to/reassigned-material.pma --smoke-timeout 50
```

Build updated host artifacts using `scripts/build-linux-viewer.sh` first.
Run `scripts/test-portable.sh` for service regression checks. Rebuild any
previous viewer bundle before distributing this editor. This slice was tested
with X11; it does not add new Wayland or clean-machine acceptance evidence.

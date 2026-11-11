# Linux force-load editing

Validated: 2026-10-21.

Open a supported `.pma` project in the GTK viewer and choose **Edit force…**.
The selector identifies each load by step, name, type and assigned region.
Edit the three signed force components in the existing model’s units, then
save to a new filename. Surface traction components represent total force
on the surface, not pressure. Concentrated-force values retain their existing
nodal application semantics.

The editor supports existing `CLoad` and `STLoad` definitions. It requires
finite components and preserves the load’s step, region and other settings.
The edit operation does not change regions or edit boundary conditions, or
expand the supported static archive model. Centrifugal loads have a separate
[authoring workflow](LINUX_CENTRIFUGAL_EDITING.md).
Constant gravity creation is covered separately by
[density and gravity authoring](LINUX_GRAVITY_EDITING.md).

`ProjectLoads` applies changes to an independently reopened model. The shared
`ProjectEdits` publisher regenerates input/metadata, refuses existing output
paths, removes failed staging files and omits stale FRD/DAT results. The source
archive stays unchanged. The GTK host reopens the new project after saving;
use Solve to generate new results. The companion refuses edits while a job
is active. Material and force editors share the numeric-entry/save dialog.

## Verification

- Portable suite: **3,859 checks**, including repeated load names across steps,
  selected-step isolation, both force types, signed components, consistent
  surface-force magnitude, invalid numbers/selections, source preservation,
  overwrite refusal, temporary cleanup and stale-result removal.
- GTK/WebKitGTK under X11: force edit, new-file save, reopen, stable rendering,
  persisted value, absence of stale result fields and renderer shutdown pass.
  The material editing smoke check also passes after sharing the dialog code.
- CalculiX 2.23 solves the GTK-edited two-step unit cube. Changing first-step
  total axial force from 1 to 3 gives axial displacement 0.003 at all four
  loaded nodes (E=1000, unit length/area). The unchanged second-step force of
  2 still gives displacement 0.002. Both steps have zero transverse
  displacement. The solve publishes a new result archive.

After building with `scripts/build-linux-viewer.sh`, repeat GTK acceptance with
an existing result-bearing two-step fixture and a new output path:

```sh
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh /path/to/two-step-result.pma \
  --smoke-load /path/to/new-force-project.pma --smoke-timeout 40
```

The smoke check edits the first force’s X component to twice its value plus
one. Run `scripts/test-portable.sh` for regression checks. Rebuild previous
viewer bundles to include the new editor. This slice adds no new Wayland,
physical-GPU or clean-machine installation evidence.

## Concentrated-force creation

Validated: 2026-09-29. Choose **Add force / traction…**, select a step and nonempty node
set, enter a unique load name within that step and three finite signed force
components, then save to a new project. Each component applies to **every node**
in the selected set, in the existing model's force units. It is not a total
force distributed over the set. Existing loads remain active, so the new load
adds to them. Newly created node sets are available as targets.

The source remains unchanged; stale results are removed and the new project
reopens. The companion rejects creation during a running job. Changing regions
of existing loads and geometry selection remain outside this operation.
[Definition removal](LINUX_DEFINITION_REMOVAL.md) is available in the edit dialog. Authoring buttons now occupy a
separate row below project/solver controls.

The portable suite totals **3,981 checks**. New checks cover targets from newly
created node sets, signed component/region persistence, existing-load
preservation, invalid names/selections/numbers and overwrite refusal.
GTK X11 acceptance verifies creation/save/reopen and stale-result removal;
boundary creation and keyboard/accessibility checks also pass with the shared
dialog and revised toolbar at explicit `GDK_SCALE=1`. An initial run without
an explicit scale failed the smoke’s assumed display-scale check.

CalculiX 2.23 solves the GTK-created two-step cube. An added axial force of 1
at each of four right-face nodes adds 4 to the existing total force of 1.
Right-face displacement is therefore 0.005 in the first step (unit length and
area, E=1000). The unchanged second step stays at 0.002; transverse movement
remains zero. This verifies per-node application and step isolation.

```sh
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh /path/to/two-step-result.pma \
  --smoke-load /path/to/new-force-project.pma --smoke-load-action add \
  --smoke-timeout 40
```

Use a new output path and current build artifacts. The smoke prefers the
`RIGHT` set and adds `added-force` with X=1, Y=Z=0. Rebuild old viewer bundles
to include this action. This adds no new Wayland or clean-machine evidence.

## Surface-traction creation

Validated: 2026-10-21. **Add force / traction…** also lists nonempty element
surfaces. Selecting one creates an `STLoad`; its three components are the total
force distributed over the whole surface by the production traction converter.
The service rejects node-set/surface-kind mismatches, empty or broken surface
definitions, missing steps, duplicate load names and nonfinite components.

Element surfaces saved in PMA are available directly. The primary mesh path now
also retains bounded `*SURFACE, NAME=..., TYPE=ELEMENT` definitions from `.inp`,
so an imported surface can be used without rebuilding it in another program.

The complete portable suite passes **5,141 checks**, including **2,126 mesh I/O
checks**. GTK X11 acceptance creates `added-traction` on `LOADED`, saves a new
PMA, reopens it, verifies the load kind/region/value and confirms stale results
are absent. CalculiX 2.23 then solves the reopened two-step cube. The original
unit traction plus the added unit traction produces first-step U1=0.002 at the
loaded face (E=1000, unit length/area); the second step remains U1=0.002.

```sh
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh /path/to/two-step-result.pma \
  --smoke-load /path/to/new-traction-project.pma \
  --smoke-load-action add-traction --smoke-timeout 60
```

Constant surface pressure has a separate **Pressure…** workflow because its
magnitude is pressure per area and its positive direction is inward. See
[pressure editing](LINUX_PRESSURE_EDITING.md). New named targets can be created
through [element-surface creation](LINUX_SURFACE_EDITING.md).

# Linux prescribed-displacement editing

Validated: 2026-10-22.

Choose **Edit displacement…** in the GTK viewer to change an existing prescribed
translation in a supported `.pma` project. The selector shows the step, boundary
name, direction U1/U2/U3 and region. Enter a finite signed displacement in the
model’s length units and save to a new filename. The host reopens the new
project. Source files remain unchanged and stale FRD/DAT results are removed.
Run Solve to generate results for the edited model.

The editor lists U1/U2/U3 for each supported `DisplacementRotation` boundary.
Select **Free direction** to release a translation, or clear it and enter a
finite signed value to constrain that direction. A zero prescribed value is a
constraint. Saving rejects removal of the last constrained direction from a
boundary. Fixed-support definitions, boundaries using fixed-direction sentinels
and rotations are outside this editor. Regions and other boundaries stay intact.

**Add displacement…** creates a named boundary on an existing step and
nonempty node set. Choose the step/node-set/direction entry, enter a unique
name within that step and a finite displacement, then save a new project.
Only the selected translation is initially prescribed; other directions start
free and can be changed with the existing editor. Use [Add node set](LINUX_NODE_SET_EDITING.md) to define another region.
**Add fixed support…** now creates a production `FixedBC` on an existing step
and nonempty node set. It requires a unique boundary name within the step and
constrains every supported degree of freedom on every node in that set. The
service derives the 2D/3D form from model space, writes a new project and removes
stale results. Other boundary conditions still apply to the same nodes. Validation checks the edited boundary's definition,
not global model stability: releasing a constraint can leave rigid-body motion.
The companion rejects edits during active jobs. Saving refuses existing paths.
Free directions use JSON null in the companion protocol and map to the model's
NaN sentinel internally; nonfinite numeric input is rejected.

## Verification

The portable suite now totals **5,141 checks**, including **2,126 mesh I/O
checks**. Boundary service checks cover
step/direction isolation, negative displacement, free-direction preservation,
load preservation, stale-result removal, invalid values and selections,
fixed-support refusal, overwrite refusal and staging cleanup. Additional checks
cover free-direction listing, constrain/release round trips, selected-direction
isolation, rejection of an empty boundary, and stale-result removal. Fixed-support
coverage includes target discovery, list/reopen identity, unique-name and region
validation, production CalculiX output, source preservation and result invalidation.

GTK/WebKitGTK under X11 passes boundary edit/save/reopen, persisted value,
stable rendering, empty result fields and renderer shutdown. Existing force
and material editor smoke checks also pass after extending the shared dialog.

Native CalculiX 2.23 solves the edited two-step unit cube. Prescribing first-step
left-face U1=0.01 yields right-face U1=0.011 at all four nodes: imposed movement
plus 0.001 elastic extension. The unchanged second step remains at U1=0.002.
Transverse displacements remain zero. The solve publishes a new result archive.

After building current artifacts with `scripts/build-linux-viewer.sh`, run:

```sh
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh /path/to/two-step-result.pma \
  --smoke-boundary /path/to/new-boundary-project.pma --smoke-timeout 40
```

The smoke adds 0.01 to the first listed prescribed translation. Choose an
output path that does not exist. Run `scripts/test-portable.sh` for regression
checks and rebuild old viewer bundles to include this editor. No additional
Wayland, physical-GPU or clean-machine qualification is claimed here.

## Direction-change acceptance

GTK X11 checks also pass with `--smoke-boundary-action constrain` and
`--smoke-boundary-action release` alongside `--smoke-boundary NEW_PATH`.
The first constrains a free translation to zero; the second releases one of
multiple prescribed translations. Both verify persisted state, stale-result
removal and renderer shutdown. Existing prescribed-value and material editing
checks pass with the extended dialog.

CalculiX solves the constrained fixture: adding left-face U2=0 retains loaded
face U1=0.001 and 0.002 in the two steps, with zero transverse displacement.
The fixture already has transverse support, so this is a compatibility check
for the added constraint, not proof of general constraint sufficiency. The
released fixture is checked for persistence and rendering only.

## Boundary-creation acceptance

GTK X11 acceptance passes with `--smoke-boundary-action add` alongside
`--smoke-boundary NEW_PATH`. On the two-step cube fixture it adds a named
right-face U1=0.01 boundary in the first step, saves and reopens the project,
verifies the value and stale-result removal, and closes the renderer. Existing
prescribed-value editing also passes after extending the shared dialog.

CalculiX 2.23 produces U1=0.01 at all four right-face nodes in the selected step;
the unchanged second step remains U1=0.002, with zero transverse movement.
Creation service checks cover available targets, selected-region persistence,
initially free directions, preservation of existing boundaries/source bytes,
stale-result removal, duplicate names, missing step/region, blank names,
invalid numbers/directions and refusal to overwrite the source.

The combined step/node-set/direction selector is intended for the current
bounded projects. Geometry picking and conflicting-constraint
detection and global stability checks remain pending. Rebuild previous viewer
bundles to include boundary creation.

## Fixed-support creation acceptance

GTK X11 acceptance creates `added-fixed-support` on the `RIGHT` node set, saves a
new PMA, reopens it and verifies step/name/region identity with no stale fields:

```sh
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh /path/to/two-step-result.pma \
  --smoke-fixed-support /path/to/new-fixed-project.pma --smoke-timeout 60
```

CalculiX 2.23 solves the reopened project and the GTK host reopens its result
archive. Independent production fingerprints report DISP/U1 exactly zero at all
eight nodes in step 1, where both cube ends are constrained, while step 2 retains
the original maximum U1 of 0.002. This checks step isolation and the production
fixed-boundary writer.

[Definition removal](LINUX_DEFINITION_REMOVAL.md) can now remove an entire
existing displacement boundary from the selected step when saving a new project.

# Removing Linux model definitions

Validated: 2026-09-30.

Open **Edit force…** or **Edit displacement…**, select the definition and enable
the removal checkbox. Save a new project. For boundaries, this removes the
**entire boundary from the selected step**, including all its directions;
it is distinct from releasing one translation. Changing the selected item
clears the removal checkbox. Other steps, including same-named definitions,
stay unchanged.

Removal supports concentrated forces, surface tractions and displacement/rotation
boundaries exposed by these editors. Fixed-support definitions, other load
types, materials, node sets and steps are outside this operation. Removing a
support may leave the model unconstrained; global stability/conflict detection
is not implemented.

`ProjectDefinitions.RemoveCopy` reopens the source independently and publishes
through the common new-file save path. The source is preserved, existing output
paths are refused, temporary files are cleaned up and stale FRD/DAT results
are omitted. The companion rejects removal while a solver job is active.

## Verification

The portable suite totals **4,049 checks**, including removal from one step,
preservation of other definitions/fixed supports, same-named definitions in
other steps, missing/unsupported targets, source preservation, overwrite
refusal, stale-result removal and staging cleanup.

GTK X11 smoke checks pass for force and boundary removal, new-project save,
reopen, absence of the selected definition and old fields, and renderer
shutdown. Ordinary force editing also passes with the removal control present.

CalculiX 2.23 solves the force-removal fixture: removing the first-step force
from the two-step cube gives zero first-step displacement. The unchanged
second step remains at U1=0.002 at all four right-face nodes, with zero
transverse displacement. The boundary-removal fixture is checked for
persistence/rendering only; it intentionally removes a support.

```sh
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh /path/to/two-step-result.pma \
  --smoke-load /path/to/force-removed.pma --smoke-load-action remove \
  --smoke-timeout 40
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh /path/to/two-step-result.pma \
  --smoke-boundary /path/to/boundary-removed.pma --smoke-boundary-action remove \
  --smoke-timeout 40
```

Use current build artifacts and new output paths. Rebuild old viewer bundles
before distributing this feature. These checks add no new Wayland,
physical-GPU or clean-machine qualification.

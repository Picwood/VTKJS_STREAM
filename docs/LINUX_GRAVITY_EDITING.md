# Linux density and gravity authoring

Validated: 2026-10-22.

The GTK viewer now edits optional constant material density in **Edit
material…**. Leave the density field blank to remove it, or enter a finite
positive value in the current model unit system. Existing elastic values and
other supported material properties remain unchanged.

**Gravity…** lists existing definitions plus each step and solid-section region whose assigned
material has one valid constant density. The dialog creates a named
`GravityLoad` from three finite acceleration components; at least one component
must be nonzero. Element-set and part regions are supported when their mesh
membership is nonempty. A gravity load cannot be created for a material without
density. Two-dimensional projects require zero out-of-plane acceleration.

Both operations save to a new `.pma`, preserve the source and remove archived
FRD/DAT payloads. Density and gravity are serialized by the existing portable
metadata contract and emitted by the production CalculiX writer.

## Verification

- The complete portable suite passes **5,141 checks**, including **2,126 mesh
  I/O checks**. Coverage includes density add/remove and invalid values; gravity
  target discovery, creation, component editing, removal, input generation,
  invalid vectors, missing targets, duplicate names and stale-result removal.
- Native GTK/WebKitGTK X11 acceptance adds unit density, saves and reopens, then
  creates +X unit gravity on `load-1 / SOLID`, saves and reopens again. Both
  operations verify empty result fields and renderer shutdown.
- CalculiX 2.23 solves the resulting project and archives FRD/DAT output. For
  the unit cube with E=1000, density=1 and acceleration=1, gravity adds the
  analytical axial displacement `rho*g*L^2/(2E) = 0.0005`. Step-1 maximum U1
  rises from 0.001 to **0.0015**, while step 2 remains exactly **0.002**.
- Editing acceleration from 1 to 2 raises step-1 maximum U1 to the analytical
  **0.002**. Removing gravity restores the exact original **0.001** displacement
  and deterministic fingerprint. Both GTK save/reopen paths pass.
- The native accessibility smoke still passes at 2× display scale after adding
  the gravity control.

Repeat the native workflow with new destination paths:

```sh
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh /path/to/source.pma \
  --smoke-material /tmp/with-density.pma --smoke-material-action density \
  --smoke-timeout 50

GDK_BACKEND=x11 scripts/start-gtk-viewer.sh /tmp/with-density.pma \
  --smoke-gravity /tmp/with-gravity.pma --smoke-timeout 50
```

This slice supports constant density and gravity creation, editing and removal.
It does not edit temperature-dependent density tables or material
plastic/thermal properties. Three-dimensional centrifugal loads are covered by
[centrifugal-load authoring](LINUX_CENTRIFUGAL_EDITING.md).

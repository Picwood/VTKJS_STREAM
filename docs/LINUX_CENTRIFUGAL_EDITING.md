# Linux centrifugal-load authoring

Validated: 2026-10-22.

The GTK viewer now lists, creates, edits and removes centrifugal loads through
**Centrifugal…**. Creation targets are limited to nonempty three-dimensional
solid-section regions whose assigned material has one positive constant density.
The editor accepts a finite axis point, a finite nonzero axis direction and a
positive rotational speed in the current model unit system.

Every operation saves a new `.pma`, preserves the source project and removes
stale FRD/DAT results. The portable service rejects missing steps or regions,
case-insensitive duplicate names, zero/nonfinite axes, nonpositive/nonfinite
speeds and edits of another load type. The production CalculiX writer normalizes
the axis direction and emits the square of rotational speed required by
`*DLOAD, CENTRIF`.

## Verification

- The portable suite passes **5,141 checks**, including **2,126 mesh I/O
  checks**. The 28 new cases cover target discovery, creation, edit, removal,
  persistence, production input, invalid values and result invalidation.
- Native GTK/WebKitGTK X11 acceptance passes add, edit and remove workflows,
  including save/reopen, stable rendering and companion/renderer shutdown.
- CalculiX 2.23 solves each saved project. On the unit cube with E=1000,
  density=1 and an axis along +Z through the origin, speed 2 adds the analytical
  axial displacement `rho*omega^2*L^3/(3E) = 0.00133333`; maximum step-1 U1 is
  **0.00233333** after including the existing 0.001 force response. Editing
  speed to 3 gives **0.004**. Removing the load restores **0.001** and the
  original deterministic fingerprint. The unchanged second step remains 0.002.

Repeat GTK creation with a new destination:

```sh
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh /path/to/density-project.pma \
  --smoke-centrifugal /tmp/with-centrifugal.pma --smoke-timeout 60
```

This slice supports ordinary three-dimensional centrifugal loads. Plane-stress,
plane-strain and axisymmetric authoring, amplitude/phase editing, coordinate
systems and graphical axis selection remain outside this workflow.

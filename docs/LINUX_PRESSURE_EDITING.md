# Linux surface-pressure editing

Validated: 2026-10-21.

The GTK host's **Pressure…** dialog creates, edits and removes constant pressure
loads in supported editable `.pma` projects. A new pressure selects an existing
static step and a valid named element surface, then supplies a unique name and a
finite signed magnitude. Positive pressure acts inward. Values use the current
model's pressure units; the dialog performs no unit conversion.

Surfaces imported from mesh-only INP and surfaces created with the native
[element-surface editor](LINUX_SURFACE_EDITING.md) are both available as targets.

The portable `ProjectPressures` service stores the definition as the production
`CaeModel.DLoad` type. It preserves the named surface assignment when changing
the magnitude. Every operation writes a new destination, leaves the source
unchanged and removes stale FRD/DAT or binary results before reopening the new
project. The viewer host refuses edits while a solver job is active.

This bounded workflow accepts only constant `DLoad` definitions assigned by
`SurfaceName` to nonempty element surfaces whose face sets exist. It rejects
nonfinite magnitudes, duplicate names, missing steps or surfaces, invalid or
empty face sets, element-ID/face overrides, nondefault distributions and
expression-backed magnitudes. Variable, hydrostatic and imported pressure types
remain outside this editor.

Metadata v25 adds the pressure definition to inline model metadata. Newly saved
archive-v3 projects use metadata v28, the external-mesh form of the v27 schema;
v27 also adds bounded global Gmsh setup. Earlier metadata remains
readable and pre-v25 metadata rejects a `DLoad` record instead of interpreting
it with incomplete semantics.

## Verification

The MeshIO characterization suite covers target discovery across two steps,
creation, list/reopen, signed-magnitude editing, removal, source preservation,
stale-result removal, overwrite refusal, invalid selection/value rejection,
metadata version gates and production CalculiX output. The complete portable
suite passes **5,141 checks**, including **2,126 mesh I/O checks**.

GTK X11 acceptance creates `added-pressure` with magnitude 1 on `LOADED`, saves
to a new PMA, reopens it and confirms that old results are absent:

```sh
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh /path/to/two-step-result.pma \
  --smoke-pressure /path/to/new-pressure-project.pma --smoke-timeout 60
```

CalculiX 2.23 solves that reopened project through `PrePoMax.Cli`. In the unit
cube fixture, the new inward unit pressure cancels the existing outward unit
surface traction in step 1, so DISP/U1 is zero. The unchanged second step keeps
its maximum U1 of 0.002. This verifies the pressure sign, surface assignment,
production deck path and step isolation.

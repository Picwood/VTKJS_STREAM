# Linux element-surface creation

Current workflow: [node-set and surface maintenance](LINUX_REGION_MAINTENANCE.md)
adds reference-tree membership editing, rename, independent duplicates, guarded
removal and regenerated face/node/area data. The creation checks below remain
historical evidence.

Validated: 2026-10-22.

The GTK host creates a named element surface from faces selected directly in the
VTK.js view. Click selects one face. Alt-drag selects rendered triangles in a
rectangle; drag left-to-right to require complete containment or right-to-left
for crossing selection. Double-click grows from the picked boundary face across
connected edges whose local face-normal angle is at most the native **Grow ≤**
threshold (0–90°, 30° by default). Click, double-click and rectangle selection
replace by default; Shift adds, Ctrl subtracts, and Shift+Ctrl intersects. The renderer server first
validates every rendered triangle against its source element and nodes. The
portable service resolves the validated batch to distinct production
`FeFaceName` values while opening the project only once.

Choose **Add surface…** after picking. The dialog is prefilled with the selected
faces. A unique surface name and between 1 and 100 faces are required. Explicit
`element:face` pairs, such as `1:S4 2:S4`, remain available for review and manual
entry. Face labels are case-insensitive and duplicate pairs are combined. The
dialog lists up to the first 1,000 solid elements and their valid face labels
while reporting the total solid element count.

Every accepted click or rectangle operation republishes the undeformed mesh with
one translucent orange, non-pickable actor containing the currently selected
faces. Repeated pairs share one preview face, and adjacent faces share preview
vertices. This gives immediate shaded feedback without allowing the overlay to
be selected recursively.

**Preview selected face nodes** validates the selection through the portable
service, then displays the distinct face nodes as pink markers on the undeformed
mesh. This confirms the selected location before saving.

`ProjectSurfaces` accepts existing three-dimensional mesh elements and face
labels supported by each element topology. It groups the selected element IDs by
face label into collision-free internal element sets and creates a production
`FeSurface`. Every edit writes a new destination, keeps the source unchanged and
removes stale results. Blank or duplicate names, unknown elements, unsupported
faces, empty selections and selections above 100 faces are rejected before
publication. Rectangle requests are bounded to 1,000 validated triangles and 100
resolved faces; duplicate triangles and faces are combined. Angle growth uses
the mesh's node-to-element adjacency and production face normals, visits only
locally connected boundary faces in the picked mesh part and stops before exceeding the same 100-face
limit. It does not scan every face in a large project.

The new surface is immediately available in **Pressure…** and **Add force /
traction…**. No metadata revision was required because existing PMA surface and
element-set records already preserve this representation.

## Verification

The MeshIO characterization suite covers solid-face inventory, face-node
resolution, duplicate normalization, backing-set persistence, definition and
source preservation, result invalidation, pressure and traction target discovery,
validated triangle-to-face resolution, production CalculiX pressure output,
invalid selections, name collisions,
overwrite refusal and staging cleanup. The complete portable suite passes
**5,141 checks**, including **2,126 mesh I/O checks**.

GTK X11 acceptance switches the cube to an isometric view, performs a real
right-to-left Alt-drag, resolves five distinct faces, creates `created-surface`,
previews its nodes, saves a new PMA, reopens it and confirms all five persisted
faces with no stale result fields:

```sh
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh /path/to/two-step-result.pma \
  --smoke-surface /path/to/new-surface-project.pma --smoke-surface-area \
  --smoke-timeout 60
```

`--smoke-surface-pick` retains the independent single-click acceptance path.

Angle-growth acceptance sets the native threshold to 90°, double-clicks one
cube face and requires all six connected faces to be shaded and persisted:

```sh
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh /path/to/two-step-result.pma \
  --smoke-surface /path/to/angle-surface-project.pma --smoke-surface-angle \
  --smoke-timeout 60
```

The focused checks also prove that 89° stops at the cube edge, exactly 90°
traverses all six faces, and nonfinite or out-of-range thresholds are rejected.
Pressure creation on the six-face surface and a CalculiX 2.23 solve with result
reopening pass.

A second GTK acceptance creates `added-pressure` on that new surface. CalculiX
2.23 solves the reopened project through `PrePoMax.Cli`; the existing axial U1
response remains 0.001 in step 1 and 0.002 in step 2 because the selected S1
pressure is transverse to the axial direction. This verifies that a surface
created entirely through the Linux application reaches the production pressure
writer and native solver.

Large selections intentionally retain the bounds above.

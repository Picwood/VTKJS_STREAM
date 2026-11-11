# Node sets: interactive mesh-based selection

Implemented and checked: 2026-10-29. This ports the mesh node-target workflow
described in the [original-process review](LINUX_NODE_SET_SELECTION_REVIEW.md).
The existing [region maintenance](LINUX_REGION_MAINTENANCE.md) reference guards,
rename handling, duplicate/removal and model Undo/Redo continue to apply.

Picking corrected: 2026-10-30. Large-coordinate picks now use the selected
triangle's geometry rather than depth-buffer coordinates. Selection messages
go directly to GTK, node-set modes have no surface double-click delay, and
repeated picks reuse the validated mesh/topology until the session or project
snapshot changes. See the [fix acceptance](evidence/linux-node-set-picking-fix-2026-10-30.txt).

## Workflow

Right-click **Model → Mesh → Node Sets → Create**, or edit an existing node set
from the tree. The editor stays open while the viewport accepts picks. Name the
set, choose a mesh selection mode and click in the viewport. Editing initializes
the complete existing membership, including imported sets without creation
history. Changing mesh mode retains the accumulated selection.

| Mode | Resolved node membership |
|---|---|
| Node | Closest node on the picked mesh face, including quadratic midside nodes. |
| Element | All nodes of the picked element. |
| Edge | Complete mesh visualization edge, potentially spanning several elements. |
| Face | Complete mesh visualization face, potentially spanning several elements. |
| Edge angle | Original production chain growth from the closest real mesh edge, in both directions. |
| Face angle | Original production growth across neighboring surface cells under the local angle limit. |
| Part | All nodes of the picked part. |
| ID | Explicit IDs or `START:END[:STEP]` ranges using Replace/Add/Remove IDs. |

Both angle modes start at 30 degrees and accept 0–180 degrees. A quad's
renderer triangulation diagonal is excluded when resolving its mesh edge.
The service reuses production `FeMesh` topology and angle algorithms; it does
not reuse the separate, 100-element-face surface editor's growth limit.

Plain click replaces membership; **Shift** adds, **Ctrl** subtracts and
**Shift+Ctrl** intersects. These operations apply after resolving each pick to
node IDs. Subtracting a face therefore removes its shared edge nodes too.

**Undo selection** restores the previous provisional selection, retaining the
last 32 changes. **Clear**, **All** and **Invert** operate on node membership;
All/Invert use nodes of visible active mesh parts, including interior nodes.
Viewport highlights update through the existing embedded selection API without
replacing the scene or resetting the camera after each pick. Up to 32 pending
selection requests are processed in order.

Enable **Rectangle selection** to drag a rectangle in the viewport without
keyboard modifiers. It supports Node, Element and Part modes and resolves
against the complete native mesh, including interior nodes absent from the
exterior renderer triangles. Node mode tests points inside the selection
frustum; Element mode requires all element nodes inside; Part mode expands
elements intersecting it to their whole part. The displayed section plane is
honored. Turn Rectangle selection off to resume normal rotation/point picking.
Edge, Face and angle modes use point picking. A coordinate-box replacement
remains available in the editor's expander.

**Apply** enters the current edit session and its model Undo/Redo history.
**Cancel** discards provisional membership. Apply refuses an empty set,
invalid IDs/names and membership changes that would desynchronize a dependent
element surface. Such sets retain rename/duplicate support. Save and Save as
use the existing session workflow; typed references follow renamed regions.

## Implementation and limits

- [ProjectMeshNodeSelection](../portable/PrePoMax.Application/ProjectMeshNodeSelection.cs)
  extracts the original controller's mesh-to-node dispatch into the portable
  application layer and validates renderer seeds against real exterior cells.
- [ProjectMeshNodeFrustum](../portable/PrePoMax.Application/ProjectMeshNodeFrustum.cs)
  validates convex frusta and queries full mesh nodes/elements. Point inclusion
  uses strict frustum boundaries as in the original VTK point query. Part
  intersection clips face polygons; quadratic faces use their corner polygons
  in the supported straight-sided solid workflow.
- [NodeSelection](../scripts/linux_node_selection.py) owns provisional node
  combinations and selection Undo, separately from applied model snapshots.
- The [GTK host](../scripts/start-gtk-viewer.py), private companion and existing
  browser renderer carry validated point seeds, native rectangle requests and
  in-place node highlights. Native application code resolves membership.

Interactive selection supports at most **100,000 nodes**. Explicit expressions
retain their 60,000-character / 100,000-expanded-ID bound. Numeric picked
membership avoids the expression length limit when applying a set. Companion
requests are bounded to 4 MiB and responses to 2 MiB; existing region inventory
responses retain their 1 MiB bound. The historical marker preview still has its
separate 10,000-node limit. Interactive highlights use the renderer's exterior
topology and overlay budget; full interior membership is retained in the count
and saved set.

This is a bounded supported-solid-mesh implementation. Original geometry/CAD
selection, selection-history persistence and regeneration/replay after mesh
changes remain open. PMA stores the final node IDs; provisional mouse/mode
history is not persisted. Representative imported meshes, curved quadratic
geometry, very large selections and physical-GPU/Wayland qualification still
need separate acceptance.

## Acceptance

The latest portable checks total **5,901**, including **2,886 mesh I/O checks**.
Selection checks cover whole multi-element faces/edges, gradual angle growth,
right-angle thresholds, disconnected islands in one part, hidden/inactive
parts, quadratic midside nodes, true mesh edges on triangulated quads, full
volume rectangles with an interior node, strict frustum boundaries, section
planes, invalid requests, PMA reopen and production NSET export. Python
selection/history tests and browser tests also pass.

The GTK acceptance uses actual viewport click and drag events for all seven
pick modes, keyboard combinations and Node/Element/Part rectangles. It checks
selection Undo/Clear/All/Invert, ordered pending requests, editing/cancellation,
support assignment, rename, model Undo/Redo and Save/reopen. The prior multipart
node/surface maintenance workflow is retained as a regression.

```sh
bash scripts/test-portable.sh
PYTHONDONTWRITEBYTECODE=1 python3 scripts/test-linux-node-selection.py
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh /path/to/unit-cube.pma \
  --smoke-node-selection /path/to/new-authored.pma --smoke-timeout 150
```

The diagnostic smoke requires the unit-cube fixture generated by the mesh I/O
test executable's `--write-node-selection-fixture OUTPUT.pma` option. Native
CalculiX validates the authored support workflow; see the
[acceptance record](evidence/linux-node-set-mesh-selection-2026-10-29.txt).

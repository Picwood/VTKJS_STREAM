# Original node-set creation: mesh-based selection

Reviewed: 2026-10-29. Scope: source review of the original workflow and the
current Linux implementation. No new graphical-selection acceptance is claimed.

Implementation update: [interactive mesh-based node-set selection](LINUX_NODE_SET_MESH_SELECTION.md)
now supplies the modes and live tree editor described below, including native
full-mesh rectangles and provisional selection Undo. The gap section records
the state at review time. Persisted original creation-history replay and wider
mesh/platform qualification remain open.

The owner's next priority is interactive **FE mesh based selection** when
creating or editing node sets from **Model → Mesh → Node Sets**. This comes
before mesh append and further assignment breadth. Keep the required
[model tree](LINUX_MODEL_TREE_TARGET.md) as the entry point.

## Original creation and editing process

[FrmNodeSet](../PrePoMax/Forms/25_NodeSet/FrmNodeSet.cs) creates a named
`FeNodeSet`, clears the current selection, opens the shared
[selection form](../PrePoMax/Forms/00_Common/FrmSelectItemSet.cs) and calls
`SetSelectItemToNode`. The target stays **nodes** while the user changes the
mesh entity used for picking. Changing between mesh modes does not clear the
selection; switching between mesh and geometry selection does.

`SelectionChanged` updates the node labels, copies the selection into
`CreationData`, updates the center of gravity and refreshes the properties.
Apply requires a unique valid name and at least one node. It runs the add or
replace command. `Controller.ReselectNodeSet` evaluates the recorded selection
again, and `AddNodeSet` updates the tree, node statistics and dependent objects.
For an imported set with labels but no creation history, the editor seeds a
node-ID selection from its existing membership.

## Mesh selection modes and exact meaning

The authoritative dispatch is `GetNodeIdsAtPoint` in
[Controller.cs](../PrePoMax/Controller.cs). The original UI calls the mesh face
mode **Surface** and the angle mode **Face angle**; their enum values are
`vtkSelectBy.Surface` and `vtkSelectBy.SurfaceAngle`.

| Mode | Nodes produced for a node set |
|---|---|
| Node | The node closest to the picked point on the picked cell face. |
| Element | All nodes of the picked element, including nodes outside the visible picked face and quadratic midside nodes. |
| Edge | All nodes of the mesh visualization edge containing the picked edge segment, via `FeMesh.GetEdgeNodeIds`. The edge may span multiple elements. |
| Surface / Face | All nodes of the mesh visualization face containing the picked cell face, via `FeMesh.GetSurfaceNodeIds`. The face may span multiple elements. This does not choose an existing named FE surface. |
| Edge angle | Nodes along a chain grown from the picked edge segment in both directions, via `FeMesh.GetEdgeByAngleNodeIds`. |
| Face angle | Nodes of connected surface cells reached from the picked face under the angle threshold, via `FeMesh.GetSurfaceByAngleNodeIds`. |
| Part | All nodes of the part containing the picked element. |
| ID | Explicit node IDs, with Add and Remove actions. |

These modes are present in the original
[selection form layout](../PrePoMax/Forms/00_Common/FrmSelectItemSet.Designer.cs).
Both mesh angle fields initially display **30 degrees**. The mesh topology
algorithms are already in the production
[FeMesh](../CaeMesh/FeMesh.cs) source used by the portable core.

**Edge angle:** the original routine builds node adjacency from the selected
part's visualization cells, including quadratic edge ordering. At each end of
the growing chain it chooses the neighbor with the smallest change in travel
direction that is within the threshold. It stops when none qualifies or the
next node is already selected. This walks mesh edges on the displayed boundary;
it is not restricted to a CAD edge or to feature-edge chains. It does not flood
every eligible branch.

**Face angle:** the original routine follows
`Visualization.CellNeighboursOverCellEdge`. Each candidate is compared with
its adjacent accepted cell using `ComputeAngleInRadFromCellIndices`; a change
at or below the threshold is accepted. The comparison is local, so gradual
curvature can grow beyond the seed face's orientation. Growth stays within the
seed part and does not jump between disconnected faces. All nodes of accepted
cells become set members.

**Edge and Face without an angle** use the part's existing visualization
edge/face segmentation (`EdgeCellIdsByEdge`, `CellIdsByFace`). They are distinct
from a single element edge/face and from the angle-growth modes.

## Selection operations and interaction

[vtkControl](../vtkControl/vtkControl.cs) maps a plain pick to Replace,
**Shift** to Add, **Ctrl** to Subtract and **Shift+Ctrl** to Intersect.
`Controller.GetIdsFromSelectionNode` applies these operations to the resolved
**node IDs**. For example, subtracting a face removes its shared edge nodes
from the accumulated node selection too. Combining face IDs first and only
then converting to nodes would produce different membership.

The shared form also offers **Undo selection**, **Clear**, **Select all** and
**Invert**. All and Invert use the visible-node universe. Selection Undo is
separate from undoing an applied model change. Existing sets reopen highlighted
and accept further selection edits before Apply.

The original controller also handles area/frustum selection for Node, Element
and Part modes (`GetNodeIdsFromFrustum`). Edge, Face and angle modes have point
selection dispatch; that area routine does not implement them. The port must
distinguish visible rendered triangles from mesh/frustum queries, including
nodes behind the displayed boundary.

## Linux gap

The current [node-set service](../portable/PrePoMax.Application/ProjectNodeSetEditing.cs)
accepts IDs/ranges or a coordinate box. The
[GTK editor](../scripts/start-gtk-viewer.py) can also copy nodes from a prior
validated element-face selection. It opens a modal dialog and asks the user to
select faces before opening the editor. It does not provide the original live
node-target selection workflow or the complete mesh mode list.

Existing `ProjectSurfaces.ResolveFace` resolves a rendered triangle to **one
solid element face**. `ProjectSurfaces.GrowFace` provides a bounded solid-face
growth operation, currently restricted to 100 faces and 0–90 degrees. These
are useful renderer/mesh bridges, but do not establish parity with the original
visualization Face or node-target Face angle modes. Current GTK selection
operations accumulate faces before converting them to nodes.

PMA currently restores node sets as names and labels in
[StaticModelMetadata](../portable/PrePoMax.Persistence/StaticModelMetadata.cs),
without their original `CreationData`. Snapshot Undo/Redo of applied edits
does not supply selection history or original regeneration/replay behavior.

## Next implementation and acceptance

1. Open Create/Edit from the Node Sets tree branch and keep viewport picking
   active while the editor is open. Make mesh selection the default for this
   workflow. Show the name, mode, angle where applicable, selected node count
   and live node highlight. Initialize editing with the full existing set.
2. Introduce a shared node-target selection session with Node, Element, Edge,
   Face, Edge angle, Face angle, Part and ID modes. Resolve validated renderer
   seeds through the production mesh methods. Reconstruct a real mesh face and
   ordered edge from source IDs and pick position; a triangulation diagonal is
   not a mesh edge. Keep node/element/part IDs distinct. Extract the UI-neutral
   dispatch instead of importing the WinForms-dependent controller.
3. Apply Replace/Add/Subtract/Intersect after resolving each seed to nodes.
   Preserve selection across mesh mode changes. Add selection Undo, Clear,
   visible All/Invert and explicit area-selection behavior. Cancellation must
   leave the model/session unchanged; Apply uses the existing reference-aware
   service and model Undo/Redo.
4. Characterize the production routines on multi-element planar faces, curved
   patches, corners, branching edges, disconnected patches, separate/hidden
   parts and quadratic elements. Verify whole-element membership, midside
   nodes, local angle thresholds and exact node-level combination semantics.
   Include a triangulated quad to detect false diagonal-edge picking. Handle
   absent visualization topology explicitly rather than substituting a
   different mode silently.
5. Exercise create/edit/cancel/apply through GTK with viewport input, mode
   changes and selection Undo. Save/reopen and compare exact membership,
   exported NSET and a support/load using the new set. Qualify representative
   imported meshes and selection size separately from small fixtures. Record
   selection-history persistence/replay support explicitly; storing only final
   IDs must not be reported as full original history parity.

CAD selection and mesh append remain separate work. The existing region
maintenance acceptance stays valid for its documented scope; this review
defines the missing interactive node-set creation behavior.

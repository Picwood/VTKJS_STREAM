# Linux node-set creation

Current workflow: [node-set and surface maintenance](LINUX_REGION_MAINTENANCE.md)
adds reference-tree membership editing, rename, duplicate/removal and range/box
selection. The original creation acceptance below remains historical evidence.

Current interactive workflow: [mesh-based node-set selection](LINUX_NODE_SET_MESH_SELECTION.md)
now provides Node, Element, Edge, Face, Edge angle, Face angle, Part and ID modes
while the tree editor remains open, plus full-mesh rectangles and selection
Undo. Persisted original selection-history replay remains open. The creation
acceptance below predates this implementation.

Validated: 2026-09-29.

Choose **Add node set…** in the GTK viewer. Enter a unique name and comma- or
space-separated node IDs, using the displayed mesh-node inventory. The service
requires a nonempty selection of existing nodes and combines duplicate IDs.
Save to a new `.pma`; the host reopens it. The new set is then available in
**Add displacement…** as a boundary region.

`ProjectNodeSets` operates on an independently reopened model. Existing sets,
loads and boundaries stay intact. The common save publisher refuses existing
paths and removes failed temporary files. It omits old FRD/DAT results,
consistent with the other authoring operations. The companion rejects edits
while a solver job is active.

Selection supports explicit node IDs or a coordinate box. It does not yet
support graphical picking or CAD-face selection. Explicit solid element-face
surface creation is available separately; see
[element-surface creation](LINUX_SURFACE_EDITING.md). It does not edit/delete existing sets or import/mesh new geometry.
The available-ID list is intended for the current bounded projects; scalable
selection and search remain future work.

## Verification

- Portable suite: **5,141 checks**, including exact/deduplicated membership,
  existing-definition preservation, source preservation, stale-result removal,
  unknown/empty node selections, duplicate/blank names, overwrite refusal,
  staging cleanup and creation of a boundary on the new set.
- GTK/WebKitGTK under X11: create a set, save, reopen, verify membership,
  render the mesh, confirm stale fields are absent and close the renderer.
  Boundary creation also passes after extracting the shared save dialog.
- CalculiX 2.23 solves the GTK-created project. The unused new set preserves
  right-face axial displacements 0.001 and 0.002 in the two steps and zero
  transverse movement. Boundary creation on a new set is checked at the
  application-service/persistence level, separately from this native solve.

After building current artifacts with `scripts/build-linux-viewer.sh`, run:

```sh
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh /path/to/two-step-result.pma \
  --smoke-node-set /path/to/new-node-set-project.pma --smoke-timeout 40
```

The smoke creates `created-node-set` from the first two sorted mesh IDs. Use
an output path that does not exist. Run `scripts/test-portable.sh` for service
regressions. Rebuild old viewer bundles to include this operation. This slice
adds no Wayland, physical-GPU or clean-machine qualification.

## Coordinate-box selection

The node-set dialog now includes X/Y/Z minimum and maximum values, initialized
to the mesh bounds. Enter limits in the model’s length units and choose
**Use nodes inside box**. The selected IDs replace the entry contents and a
count is shown for review before saving. You can still edit the IDs manually.

Limits are inclusive and compared directly with stored node coordinates;
there is no hidden selection tolerance. Equal minimum/maximum values select
a plane, two equal axes select an edge, and three select a point. For computed
coordinates, widen the limits explicitly if a tolerance is needed. Reversed
or nonfinite limits are rejected. An empty box result clears the selected IDs;
creating an empty node set remains prohibited.

`ProjectNodeSets.SelectBox` is a read-only operation. Saving continues through
the existing node-set creation service, preserving its validation and result
invalidation. The selection reports IDs/count; use **Preview selected nodes** to inspect
markers in the renderer.

The portable suite includes bounds, inclusive faces/edges/points, empty
selection, reversed/nonfinite limits and selected-set persistence checks.
GTK X11 verifies selecting the maximum-X cube face, saving, reopening and
stale-result removal. Manual-ID creation also passes with the extended dialog.
The coordinate selector itself does not change the solver model, and this
slice adds no new native solver or Wayland qualification.

```sh
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh /path/to/two-step-result.pma \
  --smoke-node-set /path/to/new-face-set.pma --smoke-node-set-box \
  --smoke-timeout 40
```

Use current build artifacts and a new output path. The cube fixture’s expected
maximum-X membership is nodes 2, 3, 6 and 7.

## Visual selection preview

Choose **Preview selected nodes** to display pink octahedral markers at the
selected mesh coordinates. The preview switches to undeformed mesh geometry;
it does not modify the archive. Move the dialog aside to inspect the scene.
Interior markers can be occluded by the mesh. Marker size is 1.5% of the largest
mesh span, so this is a location preview rather than a physical-size display.
No graphical node picking or through-mesh highlighting is implemented.

Preview supports up to 10,000 distinct existing nodes and obeys the renderer’s
overall actor/resource limits. Empty selection clears markers. Cancelling the
node-set dialog clears an active preview; saving/reopening restores the normal
mesh scene. The previous result contour is not restored automatically; select
it again using the result controls if needed.

The portable suite now includes production-reader validation of marker actors,
node deduplication, marker vertex/triangle counts, exact mesh restoration with
empty selection, unknown-node rejection and project preservation. The GTK
coordinate-box smoke now previews and waits for a stable frame before saving:
the cube goes from 1 actor/8 points/12 triangles to 2 actors/32 points/44
triangles for four selected nodes, then back to the original mesh after reopen.
Renderer shutdown and result invalidation pass. These are X11 functional
checks; no new physical-GPU or Wayland visual qualification is claimed.

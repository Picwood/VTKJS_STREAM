# Linux model tree and edit sessions

Validated: 2026-10-29.

The GTK application now has a persistent model tree beside the viewport,
following the original PrePoMax model hierarchy. Mesh parts, node sets, element
sets, surfaces, features, materials, sections, constraints, contacts and step
definitions come from the actual production model. Definitions with identical
names in different steps have distinct identities. Imported definitions remain
visible when their editor has not been ported.
The owner's [reference screenshot hierarchy](LINUX_MODEL_TREE_TARGET.md) is
recorded explicitly. The tree now reuses the available original domain icons
and connector lines. [Element-set authoring](LINUX_ELEMENT_SET_EDITING.md)
also supplies a direct section action on a selected part or set.

## Using the tree

Launch `scripts/start-gtk-viewer.sh /path/to/project.pma` after building current
artifacts. Expand the model collections and select an object to inspect its
type, state and region references. Selecting a supported region previews its
nodes on the undeformed mesh; element surfaces also receive shaded face
previews. **Preview region** repeats that action explicitly.

Double-click or choose **Edit / rename…** to open a supported editor directly
on the selected object. Step requests and supports/loads use the selected step,
including when other steps contain the same definition name. Right-click a
collection to create supported definitions. Static steps, solid sections and
field/history requests also expose duplicate, activation and removal actions.
Unavailable actions are disabled and the details area identifies missing
editors. Refresh and supported renames retain selection and expanded branches;
removing an object selects its surviving parent collection.

The large collection of authoring buttons is under **Additional tools**. The
embedded renderer uses the available viewport width instead of reserving space
for its prototype panels. **View tools** exposes actor visibility, palette and
contour controls; renderer diagnostics remain available under Additional tools.
Camera controls remain in the viewport. These presentation changes apply only
to GTK hosting.

## Editing and saving

Normal model editors now **Apply** within the current session. They no longer
require a new destination filename for every definition change. The window
title gains `*` while the model differs from its last saved revision.

- **Save / Ctrl+S** writes the current model to its opened project path.
- **Save as… / Ctrl+Shift+S** writes to a new filename and continues the session
  under that name. Existing alternative destinations are refused.
- **Undo / Ctrl+Z** and **Redo / Ctrl+Shift+Z** reopen immutable model snapshots,
  including selection remapping across supported renames. Editing after Undo
  starts a new branch and removes the former branch from redo history.
- Opening another project or closing the window with unsaved edits offers
  Save, Discard or Cancel. Saving detects changes made to the destination by
  another process and preserves that file rather than overwriting it.

The application continues to use the existing portable authoring services and
production writer. Each edit creates a private PMA snapshot; saving publishes
the current snapshot atomically. Edits remove stale results as before. Undo to
the pre-edit snapshot restores its archived results; Redo restores the edited
model without those results. Mesh/CAD import and remeshing still create separate
projects. CLI and companion copy operations retain their explicit destination
contracts, and the older per-editor save-copy smoke options still exercise
those boundaries.

Snapshots live below `$XDG_STATE_HOME/prepomax/edit-sessions` (default
`~/.local/state/prepomax/edit-sessions`). Clean sessions and explicitly discarded
sessions are removed. A failed/interrupted dirty session retains its PMA files
and `session.json`; the host reports that directory. Automatic recovery browsing
is not implemented. This is snapshot undo/redo for the exposed editors, not
original command-history replay. Full-model snapshots have storage and I/O
costs that still need large-model qualification.

## Verification and remaining scope

At the initial tree/session milestone the portable suite passed **5,627 checks**,
including **2,612 mesh I/O checks**. The subsequent
[element-set milestone](LINUX_ELEMENT_SET_EDITING.md) passes **5,719 checks**.
New checks cover production hierarchy, per-step identity, prepared
contact/constraint visibility, capability reporting, exact region membership,
surface face identity, sparse part-derived sets, rejected stale identities,
preview bounds and unchanged project bytes. Three Python session tests exercise
branching history, baseline preservation after Save, failed commits, destination
conflicts, external modifications, temporary-file cleanup and recovery retention.

GTK X11 with software WebGL passes model navigation, region preview, direct
selected-step editing, retained tree state, in-session edits, Undo/Redo, result
restoration/invalidation, rename selection through Undo/Redo, Save as and
reopening. Existing solid-section editing and empty-start checks also pass.
Native CalculiX solves the saved session fixture and retains maximum U1 of
0.001 and 0.002 in its two steps.

A framework-dependent bundle also passes checksum verification, relocated
startup, both new GTK smokes and a solve/result-fingerprint check through its
packaged CLI with CalculiX 2.21. The snapshot helper is included in the bundle;
the GTK host prevents runtime bytecode-cache writes beside installed scripts.
See the [acceptance record](evidence/linux-model-tree-2026-10-29.txt).

```sh
bash scripts/test-portable.sh
python3 scripts/test-linux-edit-session.py
# Use the two-step result fixture and a new output filename:
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh /path/to/two-step-result.pma \
  --smoke-model-tree --smoke-timeout 60
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh /path/to/two-step-result.pma \
  --smoke-edit-session /path/to/new-session-project.pma --smoke-timeout 90
```

General element-set authoring and [node-set/surface maintenance](LINUX_REGION_MAINTENANCE.md)
are now implemented for their documented scopes. Append/custom-generated-mesh
integration and contact/constraint authoring remain the next work in the
[mesh-authoring roadmap](LINUX_MESH_AUTHORING_REVIEW.md). Geometry/Results tree
contexts, analysis-job tree actions, complete selection/visibility parity and
original command replay also remain open. Region previews retain the existing
10,000-node/100-face bounds and refuse larger selections without truncation.
The companion refuses inventories above its 1 MiB tree-response budget. This
slice adds no physical-GPU, Wayland or clean-machine acceptance claim.

Implementation: [tree inventory](../portable/PrePoMax.Application/ProjectModelTree.cs),
[GTK shell](../scripts/start-gtk-viewer.py),
[session snapshots](../scripts/linux_edit_session.py),
[model checks](../portable/tests/MeshIO.Characterization/ProjectModelTreeChecks.cs)
and [session tests](../scripts/test-linux-edit-session.py).

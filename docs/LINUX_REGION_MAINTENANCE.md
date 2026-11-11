# Node-set and surface maintenance

Validated: 2026-10-29.

The [reference model tree](LINUX_MODEL_TREE_TARGET.md) now exposes edit/rename,
duplicate and remove for **Model → Mesh → Node Sets** and **Surfaces**. Creation
also uses these collections. Double-click a region to open its editor. Changes
Apply within the current session and participate in Save/Save as and Undo/Redo.

## Node sets

Keep the complete existing membership when renaming, or replace it using explicit
IDs, inclusive `START:END[:STEP]` ranges or an inclusive coordinate box in model
length units. **Use nodes from selected faces** resolves the owning mesh nodes
through the existing validated face-selection service. **Preview membership**
shows node markers on the undeformed mesh. No coordinate or unit conversion is
performed.

Renames update typed named-region references across supported boundaries,
loads, histories and constraints, including inactive definitions and repeated
names in different steps. Node sets linked to an element surface can be renamed
or duplicated, but their membership is protected: change the surface through
its own editor to keep its derived face/node data consistent. Node surfaces
that directly reference a node set follow that set's membership changes.

## Surfaces

Keep the complete existing representation for a rename, or replace an element
surface with explicit `element:face` pairs. **Use selected faces** reuses the
viewport's validated face selection. Preview shows face shading and node
markers. Existing click, rectangle and angle-growth selection remain available.

Membership replacement builds fresh backing element sets and a node set from
the actual face nodes, computes surface area with the production element
geometry methods, and preserves references to the surface. Nodal supports and
history requests on that surface therefore use the new membership. Duplicates
receive independent backing sets; editing a copy does not change its source.
Prepared node surfaces can be renamed, duplicated or removed; their membership
is edited through their backing node set. The editor identifies that set by name.

Surface renames update traction/pressure loads, supports, nodal/section history
requests, rigid bodies, springs, ties and contact pairs that refer to the
surface. No contact or constraint authoring UI is implied by preserving those
prepared definitions.

## Reference handling and boundaries

Removal is refused while any supported definition still references the region.
The UI reports dependents so they can be reassigned. Referenced definitions are
never removed implicitly. Shared reference scanning is also used by the
element-set editor.

Existing backing node/element sets are retained after surface replacement or
removal. The portable schema does not retain enough ownership information to
distinguish every generated set from user-owned/shared sets. Automatic cleanup
remains open; unused sets can be inspected and removed explicitly through the
tree. There is no project schema change in this slice.

Edits preserve other definitions, mesh and retained geometry, remove stale
results, and publish through the existing snapshot service. Failed operations
leave the source unchanged. Projects containing custom CalculiX keywords are
inspectable, but region mutation is refused because those keywords can contain
untracked references.

Explicit node expressions retain the 60,000-character / 100,000-expanded-ID
bounds. Coordinate boxes and retaining existing membership do not add an
element/node-count cap. Surface membership replacement retains the existing
100-face input bound; rename/duplicate can retain complete larger prepared
surfaces. Preview remains limited to 10,000 nodes / 100 faces and inventory
responses to 1 MiB. The later
[interactive node-set workflow](LINUX_NODE_SET_MESH_SELECTION.md) adds mesh
node/element/edge/face/angle/part picking, native volume rectangles and provisional
selection Undo with separate limits. Broader surface topology, persisted
original selection-history replay and large-model qualification remain open.

## CLI and acceptance

The CLI and private companion expose the same application services. Examples:

```sh
dotnet portable/PrePoMax.Cli/bin/Release/net10.0/PrePoMax.Cli.dll \
  edit-node-set source.pma nodes.pma LEFT RENAMED ids '1,4:8:4'
dotnet portable/PrePoMax.Cli/bin/Release/net10.0/PrePoMax.Cli.dll \
  edit-node-set source.pma nodes.pma LEFT RENAMED box 0 0 0 1 0 1
dotnet portable/PrePoMax.Cli/bin/Release/net10.0/PrePoMax.Cli.dll \
  edit-surface nodes.pma surfaces.pma LOADED RENAMED '1:S4,2:S4'
# Use keep - for node-set rename without replacing membership;
# use - as the face argument for a surface rename without replacement.
```

The portable suite passes **5,805 checks**, including **2,790 mesh I/O checks**.
The new checks cover node membership/ranges/boxes, reference propagation across
steps and inactive definitions, surface nodes/area, independent backing sets,
node-surface behavior, duplicate isolation, dependent-removal refusal, malformed
input, keyword restrictions, source/geometry preservation, result invalidation
and staging cleanup.

GTK X11/software WebGL passes the complete two-part tree workflow: node-set
edit/rename, independent duplicate edit/removal, surface expansion/rename,
independent surface duplicate edit/removal, refusal of referenced removals,
Undo/Redo and Save as/reopen. Older node-box and element-surface creation checks
also pass. Native CalculiX 2.21 solves the authored project: expanding the
loaded surface to both parts redistributes the total traction and produces
loaded-face U1=0.0005 on part 1 and U1=0.00075 on part 2, including its existing
nodal force. Source bytes remain unchanged.

A relocated framework-dependent bundle also passes checksums, startup, CLI
node/surface operations, the complete GTK workflow and the numerical solve
through its packaged CLI. See the
[acceptance record](evidence/linux-region-maintenance-2026-10-29.txt).

```sh
bash scripts/test-portable.sh
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh /path/to/two-part-project.pma \
  --smoke-region-maintenance /path/to/new-authored-project.pma --smoke-timeout 120
```

This is bounded functional acceptance; no new physical-GPU, Wayland,
clean-machine or large-model qualification is inferred. The later
[original mesh-based node-set selection](LINUX_NODE_SET_MESH_SELECTION.md)
slice is now implemented for its bounded scope. Mesh append/custom generation
integration and contact/constraint authoring follow that workflow slice.

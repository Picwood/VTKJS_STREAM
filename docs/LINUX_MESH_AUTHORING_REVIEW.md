# Linux port: mesh authoring and model-tree review

Reviewed: 2026-10-29, following the project owner's workflow clarification.

Implementation update: the [model-tree and edit-session foundation](LINUX_MODEL_TREE.md)
and [general element-set authoring](LINUX_ELEMENT_SET_EDITING.md), followed by
[node-set/surface maintenance](LINUX_REGION_MAINTENANCE.md), now supply the first
bounded slices below. The owner's screenshot is recorded as the
[required tree hierarchy](LINUX_MODEL_TREE_TARGET.md). The findings in the review
table describe the starting state; additional mesh integration, broader
assignment authoring and remaining selection/topology/ownership parity stay open.

Priority clarification: the owner requested the original mesh-based
node-set creation workflow first. Its bounded
[interactive implementation](LINUX_NODE_SET_MESH_SELECTION.md) now passes.
The
[node-set selection review](LINUX_NODE_SET_SELECTION_REVIEW.md) details Node,
Element, Edge, Face, Edge angle, Face angle, Part and ID selection, their
node-level combination/history behavior and the missing live viewport editor.
Representative mesh qualification and persisted original history replay remain
open as additional mesh integration proceeds.

The immediate acceptance target is the original PrePoMax workflow: import a
mesh, add mesh produced by custom scripts, create and maintain node sets,
element sets and surfaces, then assign sections, constraints, contacts,
supports and loads. The model tree is the primary workspace for this sequence.
The current Linux application does **not** yet satisfy this target.

This review is based on the source in this checkout and existing acceptance
records. It adds no new runtime acceptance results. Core build checks,
persistence coverage and individual dialog checks remain useful evidence, but
do not establish that this complete authoring workflow is available.

## What is available and what is missing

| Workflow requirement | Evidence in the Linux implementation | Gap for the owner's workflow |
|---|---|---|
| Import a starting mesh | [ProjectMeshImport](../portable/PrePoMax.Application/ProjectMeshImport.cs) creates a new static PMA from bounded mesh-only INP, ASCII Medit or MSH 2.2. INP preserves named node/element sets and element surfaces | INP accepts C3D4/C3D8/C3D8R, not all original element types or full analysis decks; the documented INP ceiling is 100,000 nodes/elements and 32 MiB combined input. The owner's mesh sizes and element types need representative acceptance |
| Add mesh from custom scripts | The original [Controller](../PrePoMax/Controller.cs) has `AddGeneratedModelMesh`, backed by `FeMesh.AddMesh`. The Linux import path constructs a fresh `FeModel` | No exposed append-to-current-model workflow was found. Adding script-generated parts must preserve existing definitions and handle colliding node/element IDs, part IDs and names, and set/surface references. The exact custom generators and their outputs still need to be inventoried; running scripts externally is not evidence that their original integration has been ported |
| Node sets | [ProjectNodeSets](../portable/PrePoMax.Application/ProjectNodeSets.cs) lists and creates explicit-ID sets and selects nodes by coordinate box; GTK provides marker preview | No set membership editing, rename, duplicate or removal service here, and no graphical node picking. The graphical face-selection tools do not provide node-set authoring parity |
| Element sets | Imported sets persist in [StaticModelMetadata](../portable/PrePoMax.Persistence/StaticModelMetadata.cs), and [ProjectSections](../portable/PrePoMax.Application/ProjectSections.cs) can assign solid sections to named element sets | No general Linux element-set authoring service or GTK editor was found. Imported/generated backing sets do not replace user-created, editable element sets |
| Surfaces | [ProjectSurfaces](../portable/PrePoMax.Application/ProjectSurfaces.cs) lists and creates named solid element surfaces. GTK supports validated face picking, rectangles, angle growth and preview | Creation is limited to 100 faces. No surface membership editing, rename, duplicate or removal workflow here; broader surface/topology support and selections at representative mesh sizes remain open |
| Sections | Solid-section manager supports named part/element-set targets, ordering and coverage | Cannot complete the create-element-set-to-section sequence in GTK; other original section types remain missing |
| Constraints and contacts | Supported prepared ties, rigid bodies, surface springs, surface interactions and contact pairs persist and export; see [contact project checks](../portable/tests/MeshIO.Characterization/ContactProjectChecks.cs) and [constraint export evidence](CONSTRAINT_EXPORT_EVIDENCE.md) | GTK creation/edit/removal and representative authored solver cases remain missing. Rigid-body authoring also needs reference-point authoring. Prepared-definition preservation is not interactive authoring |
| Navigate and edit the model | [GTK shell](../scripts/start-gtk-viewer.py) builds top button rows and separate dialogs; its existing `Gtk.TreeView` widgets are dialog tables | No persistent main model tree. Object selection, region highlighting, context actions and editing within the current project remain parity gaps |

The older combined “sets/surfaces and selection” status obscures three different
levels of support: basic node-set creation, imported element-set consumption,
and bounded graphical surface creation. They must be tracked separately.

## Model tree is a functional requirement

Use the original [ModelTree](../UserControls/ModelTree.cs), especially `Clear`,
`RegenerateTree`, `AddTreeNode` and context-action eligibility, as the behavior
reference. Its model hierarchy includes:

```text
Model
  Mesh
    Parts
    Node Sets
    Element Sets
    Surfaces
  Features
    Reference Points
    Cylinders
    Coordinate Systems
  Materials
  Sections
  Constraints
  Contacts
    Surface Interactions
    Contact Pairs
  Distributions
  Amplitudes
  Initial Conditions
  Steps
    <step>
      History Outputs
      Field Outputs
      BCs
      Loads
      Defined Fields
Analyses
```

Geometry and Results retain their own contexts as in the reference application.
The tree must reflect actual model objects, including supported imported
definitions for which an editor is still missing. Unsupported actions must be
identified explicitly; empty branches and visible objects must not imply that
their authoring workflows are complete.

Required interactions are selecting a region to inspect/highlight its mesh,
creating from the relevant collection, editing the selected object by
double-click/context menu, and type-appropriate rename, duplicate, delete,
visibility and activation actions. Keep step definitions under their owning
step. Preserve selection and expanded branches after edits. Existing editors
should open on the selected object instead of requiring the user to choose it
again in an unrelated manager. Top-level buttons can remain shortcuts.

Editing and saving must support a continuing session. Requiring a new PMA
filename after every set or assignment is an interim service boundary, not the
original application's interaction model. Undo/redo belongs to this authoring
workstream. Reuse the production model and portable services; do not construct a
second independent model in GTK or in the renderer.

## Implementation order and completion evidence

1. **Model-tree foundation and current-project editing.** Expose a UI-neutral
   inventory of real model objects, mount the native tree beside the viewport,
   connect the existing supported editors and region previews, and establish
   session save/undo behavior. Acceptance: import/open a model and navigate its
   parts, sets, surfaces, sections, constraints, contacts and per-step objects;
   edit a supported object in context and retain tree state. A tree populated
   only with labels does not close this milestone.
2. **Complete region authoring through the tree.** Add general element-set
   creation first, then complete membership editing, rename, duplicate and
   removal for node sets, element sets and surfaces. Include explicit IDs,
   useful graphical selection, membership preview and reference handling.
   Preserve element-face labels and distinguish generated backing regions from
   user-owned regions. Acceptance: create each region type, change it, use it
   in a definition, save/reopen and verify membership and exported references;
   rename must not leave broken references and deletion must handle dependents.
3. **Additional mesh in the same model.** Port the applicable original import
   and custom-generation paths after identifying the owner's script entry
   points, mesh formats and element types. Define collision/remapping behavior
   before publishing an append operation. Acceptance: append script-generated
   mesh with deliberately overlapping IDs/names; preserve the original mesh,
   connectivity, sets, surfaces, assignments and contacts, and validate all
   remapped references after save/reopen. Failed append must preserve the
   current session. Do not silently weld coincident nodes.
4. **Assignments on authored regions.** Connect section, constraint and contact
   creation/edit/removal to the tree and compatible named regions. Include
   surface interactions and reference points needed by the selected workflows.
   Acceptance: author a section on a newly created element set, a support/load
   on a newly created node set/surface, a tie or rigid body, and a contact pair
   with its interaction. Reopen, inspect production CalculiX output and solve
   representative authored fixtures. Complete broader section/constraint types
   according to the actual model requirements.

These are implementation slices of one required workflow, not independent
claims of full parity. The overall gate is a representative imported model plus
additional custom-generated mesh, authored regions and assignments, exercised
through the tree, saved, reopened, exported and solved with checked results.
Use a real workflow fixture as well as small diagnostic fixtures. A small cube
alone cannot qualify import/selection scale or the custom mesh integration.

Existing CAD, packaging, renderer, results and compatibility work is retained.
New feature expansion in those areas follows this authoring gate unless it is
needed to unblock it. CAD repair remains outside scope. Material and analysis
breadth follow the region/assignment needs of the representative workflow.

# Linux functional parity priorities

Scope updated: 2026-10-29, following the project owner's workflow clarification.

Superseding product direction (2026-11-06): delivery is now through the browser,
with **the current Prepomac checkout as the ultimate functional reference**.
See the [Prepomac web parity inventory](PREPOMAC_WEB_PARITY.md) and
[first-release scope](ONLINE_PRE_POST.md). This document records the earlier
Linux/GTK baseline. Its original-PrePoMax scope and CAD-repair exclusion do not
define the ceiling of the Prepomac parity target.

The target is functional parity with the original PrePoMax engineering workflows
through the GTK application and portable services. **CAD repair is outside the
current project scope and is not a completion gate.** New geometry capabilities
should follow a demonstrated original-workflow need. The completed CAD/meshing
slices remain available. The immediate gate is mesh import, additional mesh
from custom scripts, node-set/element-set/surface authoring and assignment of
sections, constraints, contacts, supports and loads through the original model
tree. See the [source review and acceptance criteria](LINUX_MESH_AUTHORING_REVIEW.md).
The tree and current-project editing are foundational workflow requirements.
The owner's [reference tree](LINUX_MODEL_TREE_TARGET.md) fixes the required
hierarchy and branch order; new editors continue to attach to that structure.

This is a source-based workflow inventory, not a completed audit of every
controller method or a numerical feature-completion percentage. The reference
application is the original [Controller](../PrePoMax/Controller.cs), its forms,
production model definitions and CalculiX keyword writers in this checkout.

| Original workflow | Linux application now | Remaining parity work |
|---|---|---|
| Static analysis step add/edit/rename/duplicate/activate/remove | GTK **Steps…** and CLI operations; copy-last supports/loads; default output requests; nonlinear and increment controls; shared production export | Non-static step types, load/support propagation, custom controls and positional user-keyword remapping |
| Materials | Constant elastic and density editing; create independently or assign to a section; remove unused materials; multiline plasticity add/edit/remove with Isotropic, Kinematic and Combined hardening | Temperature-dependent plasticity, thermal and other property editors, temperature tables and material-library workflows |
| Sections | GTK **Sections…** and CLI solid-section create/edit/rename/duplicate/activate/remove; named part/set targets, later-active precedence and coverage checks | Shell/membrane/mass/2D sections, graphical region picking and original selection-owned set cleanup |
| Supports | Fixed and prescribed translations, create/edit/remove, named-region targets | Broader boundary types, rotations and propagation workflows |
| Mechanical loads | Nodal force, total-force traction, constant pressure, gravity and centrifugal authoring | Moment, pretension, variable/hydrostatic pressure and broader load editors |
| Thermal analyses | Selected definitions exist in portable model/persistence | End-to-end thermal step/load/boundary/field authoring and solve acceptance |
| Mesh import and additional mesh | Bounded mesh-only INP/Medit/MSH 2.2 import creates a new PMA; imported named regions persist | Append mesh to the current model, original custom-script/generated-mesh integration, ID/name/reference remapping, representative formats/element types and scale |
| Node sets | Tree-based live Node/Element/Edge/Face/Edge angle/Face angle/Part/ID selection, full-mesh rectangles, node-level combinations and selection Undo/Clear/All/Invert; CLI IDs/ranges/boxes, rename with typed reference updates, duplicate/removal and model Undo/Redo | Representative mesh/platform acceptance, derived element-surface source membership rebuilding, original history persistence/replay, untracked keyword references and scale qualification |
| Element sets | Tree/CLI creation from IDs/ranges or whole parts, membership editing, rename with typed reference updates, duplicate/removal, node preview, reuse of validated face selection, direct section assignment and session undo/redo | General through-volume selection, surface-backed membership updates, untracked keyword references and broader scale qualification |
| Surfaces | Tree/CLI membership edit/rename, independent duplicates, reference-aware removal, face/node preview and rebuilt nodes/area; pressure/traction consumers follow renamed surfaces | Broader topology, replacement/selection scale beyond 100 faces, generated backing ownership/cleanup, selection history and untracked keyword references |
| Reference points | Supported prepared numeric reference points persist | Tree-based authoring and validation for rigid bodies and other dependent definitions |
| Contact and constraints | Selected contacts/ties/rigid bodies/springs survive supported PMX conversion and PMA | GTK create/edit/remove workflows and representative solver cases |
| Output requests | GTK **Field outputs…** and **History outputs…** plus CLI add/edit/duplicate/propagate/activate/remove; nodal/element/contact fields and named-region nodal/element/contact/section histories; complete production variables/export | Selection-owned history regions and non-static compatibility |
| Amplitudes and coordinate systems | Supported prepared definitions and references persist | GTK creation/edit/removal, reference validation and solver acceptance |
| Solver jobs | Native CalculiX start/cancel, result archive publication, source preservation and recovery | Broader original analysis types, job controls and solver/support matrix |
| Results | Static field/frame/component contours, deformation, native FRD/DAT and indexed binary loading; history catalog, aligned table, GTK line plot and delimited export | Original-app visual/numeric comparison, history-set transforms, complex/filter/derived-output breadth and large-model qualification |
| Project compatibility | Native editable PMA and bounded PMX conversion | Wider fixtures, older/protected layouts, history/annotations/UI state and original material libraries |
| CAD/meshing | Retained solid import/remesh, linear/quadratic order, global/box/one-face sizing, topology inspection | Original geometry/meshing operations required by parity; stable selections and tool coverage. CAD repair is excluded |
| Model navigation and edit history | Persistent production model tree, bounded region previews, supported context actions and selected-object editors; Apply within the current session, Save/Save as and snapshot undo/redo | Complete region editors, Geometry/Results contexts, analysis-job actions, broader selection/visibility parity, automatic recovery and original command-history replay |
| Desktop integration | GTK dialogs/actions, clipboard/focus/recovery, private-runtime Debian package | Original user workflows, accessibility and mixed-monitor/distro/GPU qualification |

The [model-tree and edit-session foundation](LINUX_MODEL_TREE.md) now has bounded
GTK acceptance. Supported editors Apply without a per-change filename prompt;
snapshot Undo/Redo preserves model/result state and supported rename selection.

The next authoring priorities, in order, are:

1. Model-tree/current-project editing foundation: implemented for the bounded
   Model context and supported editors; broaden context/interaction parity as
   the following workflows are ported.
2. Bounded node-set, element-set and surface maintenance now pass through the
   reference tree. [Interactive mesh-based node-set selection](LINUX_NODE_SET_MESH_SELECTION.md)
   now passes its bounded modes/rectangle/history workflow. Broader imported
   mesh acceptance, original history replay, topology, generated-region ownership
   and reference/scale qualification remain open.
3. Additional mesh in the current model, including the applicable custom-script
   paths, preserving IDs or explicitly remapping every dependent reference.
4. Section, constraint and contact authoring on those regions, including the
   supporting interactions/reference points required by the workflow.

Each slice must expose the production definition through the tree, preserve
projects and contribute to the complete import/append/author/save/reopen/export/
solve acceptance case. Material-property/section breadth, amplitudes, coordinate
systems and additional analysis types follow the representative model's needs.
Existing renderer, compatibility and platform gates remain open; new feature
expansion follows the mesh-authoring gate unless needed to unblock it.

An earlier completed slice under the broader parity effort is
[static analysis step authoring](LINUX_STEP_EDITING.md). It follows the original
`Controller.AddStep`, `DuplicateSteps`, `ActivateDeactivateStep` and `RemoveSteps`,
[StepCollection's copy-last behavior](../CaeModel/Steps/StepCollection.Legacy.cs),
and [ViewStaticStep's incrementation controls](../PrePoMax/Forms/51_Step/ViewStaticStep.cs).

[Solid section authoring](LINUX_SECTION_EDITING.md) now follows the original
section add/replace/rename/duplicate/remove workflow and production assignment
order. Independent material creation and coverage checks also allow recovery
after removing every section. This closes the bounded 3D solid-section manager
slice; broader section types remain parity work.

[Field output request authoring](LINUX_FIELD_OUTPUT_EDITING.md) now follows the
original nodal, element and contact request workflow, including propagation to
later steps and advanced CalculiX file-output flags.

[History output request authoring](LINUX_HISTORY_OUTPUT_EDITING.md) covers the
four original static request types, compatible named regions, totals/global
settings and propagation. [History-result browsing and export](LINUX_HISTORY_RESULTS.md)
adds the production catalog, aligned table, GTK line plot and delimited export.

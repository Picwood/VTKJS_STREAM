# Prepomac functional parity target

Scope clarified by the project owner: 2026-11-06. **The ultimate target is
Prepomac's engineering functionality in the browser.** The current web pilot
and the first externally solved release are milestones toward that target.
Their static-solid and simple-field limits do not define the final product.

Implementation is governed by the [detailed roadmap](PREPOMAC_IMPLEMENTATION_ROADMAP.md)
and [matched acceptance catalogue](PREPOMAC_PARITY_TESTS.md). The
[registry](parity/plan.json) tracks dependencies, source test invocations and
planned cases without treating inventory coverage as a test pass.

The functional reference is the **current Prepomac working tree at the repository
root**, including its local additions, rather than only upstream PrePoMax or
the older source snapshot inside `VTKJS_STREAM`. Relevant evidence includes the
[Prepomac documentation](../../../../README.md),
[Python API and workflow contracts](../../../../Python_API_Overview.md),
[controller](../../../../PrePoMax/Controller.cs),
[custom controller operations](../../../../PrePoMax/Controller.CustomFeatures.cs),
and the source lists in [the desktop application project](../../../../macOS/PrePoMax/PrePoMax.csproj)
and [model project](../../../../macOS/CaeModel/CaeModel.csproj).
This baseline includes uncommitted/local work; a Git revision alone does not yet
identify all of it. Record the source identity and fixtures when qualifying a
feature. Documentation of a desktop feature is reference evidence, not proof
of its web implementation or of every desktop backend's numerical qualification.

## What matching functionality means

Users must be able to complete the corresponding engineering task: create or
import the same definitions, select the intended regions, change the same
properties, preserve references, save/reopen, export equivalent solver input
and obtain equivalent numerical results and reports. Browser-native controls
can replace desktop dialogs, file pickers and keyboard conventions while
preserving their engineering meaning and the required model-tree hierarchy.

A tree entry, visible button, backend class or successful compilation does not
close a parity item. Track these separately for each feature: domain operation,
application API, browser interaction, persistence, export/results and acceptance.
Snapshot Undo/Redo is useful in the pilot but does not establish Prepomac's
command regeneration or Python journal replay behavior.

External solving remains the chosen **first release** workflow. Prepomac's job
settings, input generation and result associations remain part of the long-term
inventory. Managed remote execution and the placement of Python/native tools
need their own implementation design; this clarification does not introduce
server-side solving into the first release.

## Initial source-based inventory

This is a capability-level backlog, not an exhaustive method audit or a
completion percentage. “Portable” below refers to the Linux trial's services;
portable/GTK support is distinct from browser support. Every row still needs
representative Prepomac comparison evidence before being marked equivalent.

| Capability | Browser pilot now | Prepomac parity work |
| --- | --- | --- |
| Projects, units and model state | Bounded portable PMA import/download, temporary workspace, revision checks and snapshot undo/redo | Qualify PMX read/write and older/protected files, units, custom definitions, embedded results, annotations, settings, command history and recovery; persist hosted projects |
| Model tree and selection | Reference Model hierarchy, inspection, bounded server-resolved region highlighting, explicit-ID node-set creation | Geometry/Model/Results/Fatigue contexts, visibility/isolation, viewport pick/rectangle/angle selection, selection algebra/history and context-specific editing |
| Geometry and meshing | Mesh import only in the browser; bounded CAD/remesh services exist in the trial | Prepomac CAD import/edit/partition and meshing workflows, supported meshers and controls, quality tools, transforms, generated mesh and append with reference remapping |
| Mesh regions and features | Node sets by ID; existing parts/sets/surfaces are inspectable | Full node-set/element-set/surface creation, membership edit, rename/duplicate/remove, reference points, coordinate systems and cylinders |
| Materials and libraries | Constant elastic material creation/editing | Remaining elastic/plastic/thermal properties and temperature tables, equations, material libraries and reference-preserving replacement |
| Sections and composites | Reassign material on an existing solid section | Section creation/editing across supported types; shells, composites, layups, plies, orientations, mapping and related failure criteria |
| Contacts and constraints | Inspectable definitions without browser editors | Surface interactions, contact pairs, ties, rigid bodies, springs and Prepomac's pressure-dependent spring definition/export |
| Steps and model definitions | Create a default static step, fixed support and nodal force; edit supported forces | Step types/settings and lifecycle, broader supports/loads, propagation, amplitudes, distributions, initial conditions, defined fields and output requests |
| Solver handoff | Current CalculiX input download and matching-mesh FRD attachment | Equivalent supported keyword output, analysis/job settings, result/run provenance and supported additional solver handoffs, including the Prepomac Code_Aster path |
| Results and histories | Static real nodal field/component/frame, deformation, scalar coloring and legend | Independent result opening, multiple result sets, DAT histories, plots/exports, animation, complex fields, equations, filters, transforms, derived outputs, probes, annotations and extrema |
| Individual stress colors | Global scalar coloring only | Per-element-set/component limits and modes, shared-node/material treatment, physical-value preservation, legend behavior and persistence |
| Fatigue | No browser workspace | Loads/model/SN-curve setup, load histories and repetitions, package export, execution handoff, result fields, cancellation and setup persistence; retain each backend's documented qualification limits |
| SRSuntour and mesh recovery | No browser editors/actions | Cylinder lifecycle, extrusion/sweep/chamfer/contact operations, file tools, result tools and command-backed element deletion with dependent-reference maintenance |
| Python, automation and journals | No browser scripting or journal workflow | Preserve public API semantics, command recording, scripts, regeneration, journal export/replay and automation operations through an appropriate hosted execution boundary |
| Presentation and scale | VTK.js progressive geometry, camera controls and bounded highlighting | Required engineering symbols, clipping, explode/transform, queries/captures, numerical and visual comparisons and representative large-model qualification |

The previous Linux roadmap excluded CAD repair from its immediate scope. That
restriction must not silently remove Prepomac geometry capabilities from this
ultimate inventory. Audit the actual baseline operations and qualify the ones
required for parity; do not interpret the broader target as a request to invent
additional CAD functionality.

## Source reuse and project preservation

The trial's [portable model project](../portable/CaeModel/CaeModel.csproj) links
selected sources **inside `VTKJS_STREAM`**. In contrast, Prepomac's .NET desktop
projects link the root model/results/API sources and explicitly include custom
features. A web editor over the trial's current bounded archive schema therefore
cannot be assumed to support or preserve all Prepomac state.

Move toward shared UI-independent Prepomac domain/application operations by
extracting or linking reviewed source groups and testing each transition.
Keep browser HTTP/UI code separate from the domain model. Do not build a second
engineering model in TypeScript or bulk-replace the trial's sources without
checking platform adaptations, persistence and existing tests.

Before exposing each broader definition, extend the project representation to
preserve it and its references across import, unrelated edits, save/reopen,
undo/redo and export. Unsupported project content must be preserved or the
operation explicitly refused; it must not be silently dropped. Pressure-spring
source FRDs, distributions, material libraries and other referenced files need
managed project artifacts rather than desktop filesystem paths.

## Delivery sequence and evidence

The detailed roadmap supersedes the earlier broad sequence. Establish the
reference/harness first, then common command/persistence operations, durable
ownership, tree/selection, mesh/region lifecycle and static authoring. The first
hosted release adds qualified external handoff and core post-processing.
Geometry/meshing, broader definitions, advanced results, composites, custom
tools and fatigue follow their actual prerequisites. Python/journal semantics
start with the command foundation and are completed alongside those features.
Whole-project, GPU/performance and production checks close the final parity gate.

Reuse [API test scripts](../../../../APIs/Python/Test_Scripts) and
[desktop custom-feature verification](../../../../macOS/verify-custom-features.py)
as references. The registry preserves every API invocation, including expected
nonzero exits and generated-result dependencies. Online ownership/recovery/
isolation checks supplement desktop comparisons rather than substituting for them.

For each accepted feature, compare a Prepomac and browser run using the same
fixture: object types/properties and IDs, region membership/references, reopened
state, normalized solver keywords and numerical outputs with stated tolerances.
Check failed/cancelled operations and unrelated edits for source preservation.
Add visual comparisons for selection, annotations, scalar/individual coloring
and deformation. Keep fixture-specific performance limits explicit.

The existing desktop/API and web test counts are regression evidence for their
covered behavior. They do not establish a percentage of Prepomac parity, and
the current browser pilot is not claimed to have achieved this target.

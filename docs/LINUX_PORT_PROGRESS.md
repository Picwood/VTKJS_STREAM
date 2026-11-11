# Native Linux migration

Updated: 2026-10-30.

Product priority update (2026-11-06): the owner has pivoted the delivery target
to [browser pre- and post-processing](ONLINE_PRE_POST.md), with external solving.
The first local web slice is implemented; hosted identity, durable storage and
online deployment remain open. The GTK parity priority below records the native
baseline and no longer takes precedence over the browser delivery roadmap.
Its engineering workflow and model-tree requirements still apply.

Related assessment (2026-10-15): [ccxview comparison and improvement options](CCXVIEW_EVALUATION.md).
The recommendation is accepted. [Result sessions](LINUX_RESULT_SESSIONS.md) and
[indexed binary field access](LINUX_INDEXED_RESULTS.md) are implemented for the
documented scope. Large-model measurements and existing platform/visual gates remain open.

## Current scope and implementation priority

The current priority is **functional parity with the original PrePoMax**, using
the GTK shell and production engineering core. The owner's immediate workflow
is **import mesh → add mesh from custom scripts → create/edit node sets,
element sets and surfaces → assign sections, constraints, contacts, supports
and loads**, with the **original model tree as the primary workspace**.
The owner's screenshot is recorded as the
[reference hierarchy](LINUX_MODEL_TREE_TARGET.md); branch order and parent/child
relationships are acceptance requirements for subsequent authoring work.

The [mesh-authoring and model-tree review](LINUX_MESH_AUTHORING_REVIEW.md)
records the source evidence, missing capabilities and next acceptance gate.
The [model tree and edit-session foundation](LINUX_MODEL_TREE.md) is now
implemented, including context actions, selected-object editors and snapshot
undo/redo.
[Element-set authoring](LINUX_ELEMENT_SET_EDITING.md) now supplies
creation, membership editing, rename, duplicate/removal and direct section
assignment from the tree. [Node-set/surface maintenance](LINUX_REGION_MAINTENANCE.md)
now adds membership edit/rename, independent duplicates, guarded removal and
typed reference updates. Append-to-current-model mesh integration and GTK
constraint/contact authoring remain missing. Supported prepared
definitions and passing core checks do not establish this workflow's parity.

The original **mesh-based node-set selection** workflow now has a bounded
[interactive implementation](LINUX_NODE_SET_MESH_SELECTION.md): Node, Element,
Edge, Face, Edge angle, Face angle, Part and ID, with live viewport selection
while the tree-based editor is open. Node-level combinations, selection Undo
and native full-mesh rectangles are included. The
[original-process review](LINUX_NODE_SET_SELECTION_REVIEW.md) records the source
behavior and acceptance criteria. Qualify representative imported meshes while
continuing toward additional mesh integration and constraint/contact
assignments. Surface topology, persisted selection-history replay and
backing-set ownership/cleanup remain region parity work.
This supersedes earlier next-work lists that start with material-property
breadth or further CAD/results expansion; see the updated
[source-based parity inventory](LINUX_FUNCTION_PARITY.md). **CAD repair is
outside the project scope and is not a Linux-completion gate.**

## Status toward a full Linux build

**The reusable engineering core builds on Linux; the complete PrePoMax desktop
application does not yet build or run natively.** The legacy application project
still targets .NET Framework 4.8 and references WinForms. The portable projects
are separate .NET 10 projects. A command-line host now runs the bounded editable
project workflow; it does not replace the desktop application.

The latest completed Linux checks total **5,901**. Native CalculiX and
Gmsh acceptance also passes for the documented fixtures. This is evidence for
covered workflows, not a percentage of application feature parity.

| Area | Current evidence | Remaining gate |
|---|---|---|
| STL and shared utilities | Production sources build; numeric, geometry, selection and equation checks | Remaining UI/platform helpers and full compatibility audit |
| Mesh engine | FeMesh aggregate, topology, parts, transforms and selected mesh I/O build; GTK/CLI STEP, IGES, BREP and GEO solids run through distribution Gmsh into validated tetrahedral PMA projects; retained CAD can be remeshed with project-persistent global size, algorithm, optimization, linear/quadratic order and one coordinate-defined local refinement box plus a source/topology-bound CAD face refinement; PMA v6 retains the parsed source-bound CAD entity graph | Editable CAD/B-rep geometry, stable topology naming, geometry partitioning where required by original workflows, edge sizing, multiple face refinements and broader setup entities, curved quadratic elements and format coverage |
| Model definition | 230 CaeModel sources: FeModel aggregate, selected mesh I/O, model properties, materials, sections, concentrated, gravity, moment, centrifugal and thermal loads, displacement/fixed/submodel boundaries, static steps, constraint definitions, output requests and production CalculiX input generation for prepared models | General model cloning, legacy import methods and broader native workflow coverage |
| Results engine | FeResults, FRD/DAT import, invariants, deformation, interpolation, derived histories, catalog/aligned-table browsing, GTK plotting and delimited export | Two legacy copy methods; broader complex/filter/derived-output validation |
| Solver execution | Native CalculiX cube/two-step workflow plus parameterized bar acceptance through CaeJob; Ubuntu 24.04 package recommends and validates distribution CalculiX 2.21 with an analytical solve | Broader solver cases, versions and supported-distribution matrix |
| Persistence | Bounded editable tetrahedral/hexahedral multipart static-model save/reopen, solver-input equivalence, native FRD/DAT, PMA-v4 binary results, checksummed source geometry and PMA-v6 source-bound CAD topology, and fail-closed conversion of the supplied PMX model/results | Editable B-rep topology/UI state, protected/older layouts and broader fixtures |
| Rendering | The native Linux package uses VTK.js/WebGL and has no ActiViz binary dependency; portable project scene export is accepted by the production upload reader | Original Windows compatibility-backend retirement, visual acceptance and large-model physical-GPU benchmark |
| Desktop application | GTK 3/WebKitGTK selected as the production Linux host; stable application identity, packaged launch, project/import/edit/solve/result workflows, recovery and X11/Wayland DRM-backed WebGL acceptance pass | Broader PrePoMax user workflows, screen-reader/mixed-monitor qualification and full desktop parity |
| Distribution | Portable CI, relocatable private-runtime archive and reproducible Debian/Ubuntu amd64 package; clean Ubuntu 24.04 validates distribution CalculiX plus Gmsh CAD import/remesh, version upgrade with user-state preservation, private-runtime/integration revalidation and purge | Other native tools, signing/update repository and supported distro/GPU matrix |

## Full-build gap assessment

The current branch is a usable bounded native Linux application for opening or
importing supported projects, editing a growing static-analysis subset, solving
with CalculiX and viewing results. It is not yet a full Linux replacement for the
Windows PrePoMax desktop application. The remaining gates, in release-impact
order, are:

1. **Mesh authoring through the original model tree:** the persistent model
   hierarchy, supported context actions, current-project editing and snapshot
   undo/redo now have bounded GTK acceptance. General element-set authoring and
   its direct section-assignment action also pass. Node-set/surface maintenance
   now passes its bounded reference/duplicate/edit/remove workflow. Original
   mesh-based node-set selection now also passes bounded interactive
   node/element/edge/face/angle/part modes, node-level selection operations and
   full-mesh rectangles. Qualify representative imported meshes and add
   additional custom-generated mesh in the same model with
   validated ID/name/reference remapping. Expose section, constraint and
   contact assignments on the authored regions and verify the complete
   import/append/author/save/reopen/export/solve workflow. See the
   [review and acceptance criteria](LINUX_MESH_AUTHORING_REVIEW.md).
   Existing static-step add/edit/rename,
   duplicate, activation and removal are now exposed in GTK and CLI, including
   copy-last supports/loads and production default outputs. A solid-section
   manager now covers named part/set assignments, ordering and coverage, with
   independent elastic material creation. Bounded multiline plasticity now
   covers all three production hardening rules. Broader section types,
   temperature/thermal and other material properties, load/boundary properties, contact/constraint workflows,
   amplitudes, coordinate systems and additional step types
   remain. These existing managers and prepared-definition capabilities do not
   close the mesh-authoring gate. That gate takes priority over broader
   material/analysis features and extending CAD features.
2. **Geometry and meshing parity:** a bounded CAD-solid-to-linear or
   straight-sided-quadratic tetrahedral-PMA path now runs through distribution
   Gmsh in GTK and CLI.
   Generated CAD-face regions, explicit GEO physical names and exact source
   bytes now survive in PMA. A PMA can create a fresh remeshed project from those
   retained bytes with bounded min/max size, algorithm and optimization controls.
   Global controls and one local coordinate-defined refinement box persist as
   native mesh setup objects, appear in project inspection, prefill GTK and can
   drive a later remesh without repeated values. Retained STEP/IGES/BREP projects
   now also refine one selected CAD face with a bounded transition distance; its
   saved source/topology binding rejects changed snapshots before publication.
   A source-bound CAD entity/boundary graph now persists in PMA v6 and is
   inspectable in GTK and CLI. Editable CAD/B-rep geometry, stable naming, geometry
   partitioning, edge sizing, multiple face refinements and broader setup
   entities, curved quadratic elements and MMG/Netgen/OpenFOAM remain outside the workflow.
3. **Project compatibility:** supported PMX fixtures convert their analysis data,
   but command history, annotations, UI state, protected/older layouts and wider
   real-project coverage remain open. `.pma` is still the native editable format.
4. **Renderer parity and scale:** retire the remaining hidden ActiViz compatibility
   dependencies from the original Windows desktop path and shared interfaces,
   complete graphical selection/result parity, compare against the original
   application and run large-model physical-GPU and Wayland acceptance. The
   packaged native Linux process already runs without ActiViz binaries.
5. **Distribution and support matrix:** Ubuntu 24.04 now supplies the recommended
   CalculiX 2.21 solver and the package harness proves an analytical solve.
   Integrate the remaining native tools, add signing/update delivery,
   then qualify headed Ubuntu 24.04 operation, additional supported
   distributions, GPU drivers and Windows regression. Clean Ubuntu 24.04
   install/preflight/purge is automated, including an in-place higher-version
   upgrade, solver revalidation and XDG-state preservation.

The production shell remains GTK 3/WebKitGTK. The existing
[CAD-to-Gmsh workflow](LINUX_GMSH_WORKFLOW.md) supplies the bounded geometry-to-mesh
path. The model-tree/edit-session foundation and bounded region maintenance
are implemented, as is bounded interactive mesh-based node-set creation/editing.
Representative selection qualification, additional script-generated mesh
integration and region-based constraint/contact authoring with solver/result
evidence follow. Broader material, section and analysis
types follow the demonstrated workflow need. CAD repair is excluded from this
scope.

**Project-format boundary:** `.pma` remains the portable archive format. The
standalone PMX tool can now convert the supported analysis model from the supplied
unprotected PrePoMax v2.5.1 dev fixture into `.pma`; the GTK open dialog invokes
the same fail-closed converter after requesting a new `.pma` destination.
Validated legacy results are stored in PMA v4 and load through the
portable result/scene path; command history, annotations and UI state are not converted.
Renaming files is not a conversion. See
[PMX conversion](LINUX_PMX_CONVERSION.md) and
[GTK PMX import](LINUX_PMX_GTK_IMPORT.md).

## Latest application milestone

Picking correction (2026-10-30): the previous build reproduces
`pick-invalid: The world hit lies outside the selected chunk bounds` on a
large-coordinate diagnostic mesh. The renderer now reconstructs the hit from
the GPU-selected triangle instead of quantized depth, and validates it against
the uploaded Float32 geometry. Node-set picking bypasses the 350 ms surface
double-click wait, sends selection messages directly to GTK and reuses one
validated mesh/topology snapshot per editor session. Unit and large-coordinate
GTK workflows pass; targeted browser picking and renderer validation tests
also pass. See the [fix evidence](evidence/linux-node-set-picking-fix-2026-10-30.txt).

[Interactive mesh-based node-set creation/editing](LINUX_NODE_SET_MESH_SELECTION.md)
now runs from the reference tree in a nonmodal editor. It includes the original
Node, Element, Edge, Face, Edge angle, Face angle, Part and ID modes,
node-level combinations, live highlights without camera resets, selection
Undo/Clear/All/Invert and full native mesh rectangles. Complete existing sets
initialize editing; Cancel preserves the model and Apply joins model Undo/Redo.
The latest portable checks total **5,901**. GTK exercises actual clicks and
rectangles, support assignment, rename and Save/reopen; native CalculiX
confirms U1=0.001 on the unit-cube loaded face. Final-ID persistence is supported;
original selection-history replay, broader meshes and platform/scale acceptance
remain open. See the [acceptance record](evidence/linux-node-set-mesh-selection-2026-10-29.txt).

## Previous application milestone: node-set and surface maintenance

[Node-set and surface maintenance](LINUX_REGION_MAINTENANCE.md) now exposes
membership editing, rename, duplicate and guarded removal through the reference
tree and CLI. Typed references follow renamed regions across supported model
definitions. Surface membership changes rebuild independent face backing sets,
exact node membership and area; shared/user-owned old sets are retained. The
portable suite passes **5,805 checks**. GTK passes the complete two-part
maintenance/Undo/Redo/save/reopen workflow and existing creation regressions.
Native CalculiX 2.21 confirms load redistribution after expanding a surface to
both parts: loaded-face U1=0.0005/0.00075. Mesh append and constraint/contact
authoring remain next; broader selection/topology and automatic backing-set
cleanup remain region parity work.

## Previous application milestone: element-set authoring

[Element-set authoring through the reference tree](LINUX_ELEMENT_SET_EDITING.md)
now covers explicit IDs/ranges, whole parts, membership edit/rename,
duplicate/removal, preview and direct solid-section assignment. Typed references
follow renames, dependent removals are refused, and surface-backed membership
is protected. The GTK tree reuses original icon assets and connector lines;
the owner's hierarchy is recorded and checked. The portable suite passes
**5,719 checks**. GTK passes full set/section authoring and session Undo/Redo
on one- and two-part fixtures. Native CalculiX 2.21 solves the authored two-part
project with U1=0.001 at all eight loaded-face nodes after expanding the set's
section to both parts. Node-set/surface maintenance, mesh append and
constraint/contact authoring remain next.

## Previous application milestone: model tree and edit sessions

[Model-tree navigation and edit sessions](LINUX_MODEL_TREE.md) now put the
production model hierarchy beside the viewport. Supported definitions open
directly in their editors; collections expose creation and supported objects
offer context actions and region previews. Normal edits Apply in the current
session, with Save/Save as and snapshot Undo/Redo instead of a destination
prompt for every change. Tree selection survives refresh and supported renames,
including Undo/Redo. Prepared contacts and constraints remain visible with
their missing authoring actions identified. The portable suite passes **5,627
checks**; Python session tests and GTK tree/session, section-regression and
empty-start acceptance pass. A saved session project solves natively with
maximum U1=0.001/0.002. General element-set creation, broader region maintenance,
additional mesh integration and contact/constraint authoring remain next.

## Previous application milestone: plastic material properties

[Plastic material property authoring](LINUX_PLASTICITY_EDITING.md) now follows
the original constant-temperature workflow through the production model,
persistence and CalculiX writer. GTK **Plasticity…** and CLI operations list,
add, replace and remove multiline yield-stress/plastic-strain data with
Isotropic, Kinematic or Combined hardening. Edits validate bounded monotonic
tables, publish a new PMA and invalidate stale results. The portable suite
passes **5,583 checks**. Native CalculiX 2.23 solves the authored Kinematic
two-step fixture at maximum U1=0.001/0.002; GTK passes set/edit/remove and
save/reopen. Clean Ubuntu 24.04 passes installed CLI authoring, production
export and a distribution-CalculiX solve with the property retained through
later field/history edits. Temperature-dependent plasticity, thermal/other
properties and material libraries remain open. CAD repair stays outside scope.

## Previous application milestone: history-result browsing and export

[History-result browsing and export](LINUX_HISTORY_RESULTS.md) now follows the
original result-side workflow through production DAT parsing, `FeResults` table
alignment and `ResultHistoryOutputWriter`. GTK **History results…** lists series,
opens an aligned numeric table and bounded native line plot, and exports one set
or all sets with comma, semicolon or tab delimiters. CLI and companion operations
cover the same catalog/table/export boundary. The portable suite passes **5,543
checks**. Native CalculiX proves the workflow against authored two-step DAT;
GTK passes table/plot/selected-set export, and clean Ubuntu 24.04 passes installed
solve/catalog/table/export plus package upgrade and purge. Selection-owned
temporary regions, non-static request compatibility and broader complex/derived
result workflows remain open. CAD repair stays outside scope.

## Previous application milestone: history output requests

[History output request authoring](LINUX_HISTORY_OUTPUT_EDITING.md) now follows
the original static workflow in GTK and CLI for nodal, element, contact and
section requests. Add, edit/rename, duplicate, propagate, activate/deactivate
and remove operate on type-compatible named regions and expose every production
variable plus totals/global settings. Persistence now round-trips all four
concrete request types. New-file edits preserve shared definitions, meshing and
source-bound CAD topology while invalidating stale results. The portable suite
passes **5,521 checks**. Native CalculiX verifies authored two-step DAT output
and retains U1=0.001/0.002. GTK passes all seven operations and clean Ubuntu
24.04 passes installed operations/export plus upgrade/purge preservation.
History table/plot/export is now complete; selection-owned temporary regions and
non-static compatibility remain open. CAD repair stays outside scope.

## Previous application milestone: field outputs

[Field output request authoring](LINUX_FIELD_OUTPUT_EDITING.md) now follows the
original PrePoMax workflow in GTK and CLI: nodal, element and contact add,
edit/rename, duplicate, propagate, activate/deactivate and remove. The editor
exposes every production field variable, last-iteration/contact-element/global
flags and element 2D/3D mode. Propagation replaces same-named requests in place
or appends independent copies to later steps. New-file edits preserve analysis,
meshing controls and source-bound CAD topology and invalidate old results. The
portable suite passes **5,481 checks**. Native CalculiX verifies the authored
two-step requests and retains maximum U1=0.001/0.002 at times 1/2. GTK X11
software-WebGL passes all operations and save/reopen. Clean Ubuntu 24.04 passes
installed request operations and export, then preserves the authored project
across package upgrade and purge. Region-based history request editing is now
complete; history result workflows remain open. CAD repair stays outside scope.

## Previous application milestone: solid sections

[Solid section authoring](LINUX_SECTION_EDITING.md) now follows the original
PrePoMax workflow in GTK and CLI: add, edit/rename, duplicate, activate/deactivate
and remove, with named part/element-set targets and later-active assignment
precedence. Coverage and effective assignment counts are visible; incomplete
coverage blocks Solve while allowing further edits. Elastic materials can be
created independently, including after all sections/materials are removed.
New-file edits preserve shared definitions, meshing controls and source-bound
CAD topology and invalidate old results. The portable suite passes **5,427
checks**. Native CalculiX verifies the two-block assignment/override workflow
with maximum U1=0.00025 for uniform E=4000 and U1=0.0005 for a local E=2000
override. GTK X11 software-WebGL passes all six operations, empty-section
recovery, material creation and solve/result reopening. Clean Ubuntu 24.04
passes installed section operations, coverage refusal/recovery and export,
then preserves the authored project across package upgrade and purge. Shell,
membrane, mass and 2D sections, graphical region picking and in-session
undo/redo remain open. CAD repair stays outside scope.

## Previous application milestone: static analysis steps

[Static analysis step authoring](LINUX_STEP_EDITING.md) now follows the original
PrePoMax workflow in GTK and CLI: add with copy-last supports/loads, edit/rename,
full duplication, activate/deactivate and remove. The native editor exposes
NLGEOM and increment/time controls, retains unrelated definitions and writes a
new PMA while invalidating old results. New-step output defaults match the
production constructor; duplicated steps retain their output requests. Empty
sequences stay editable and cannot start a solve. The portable suite passes
**5,367 checks**. Native CalculiX verifies independently edited step loads with
U1=0.001/0.002 at times 1/2. GTK X11 software-WebGL passes all six operations,
empty-sequence recovery and actual solve/result reopening. The solver host now
accepts field-output-only results without requiring an unrequested DAT file.
Clean Ubuntu 24.04 passes installed step operations and Direct-mode export,
then preserves/reopens the authored two-step project across upgrade and purge.
This work follows the updated
[functional parity priority](LINUX_FUNCTION_PARITY.md); CAD repair is out of scope.

## Previous application milestone: source-bound CAD face refinement

One [CAD face refinement](LINUX_CAD_FACE_SIZING.md) now runs through GTK and CLI
retained STEP/IGES/BREP remeshing, with local size, transition distance and saved
source/topology identity. It combines with the existing coordinate box and
persists as a shared `CadFaceMeshSize` item in metadata v33/v34. A changed native
topology snapshot fails before publication. The portable suite passes **5,301
checks**; native Gmsh validates spatial refinement and stored controls, and
CalculiX 2.23 matches the analytical pressure-solve displacement at all **1,291
nodes** of the combined box/face quadratic cube. GTK X11 software-WebGL selection,
remesh, saved-control prefill and renderer shutdown pass. Clean Ubuntu 24.04
repeats combined face/box/quadratic remeshing with distribution Gmsh 4.12.1,
exports C3D10, preserves/reopens the exact user PMA across package upgrade,
and passes runtime/tool revalidation plus purge. General stable naming,
editable CAD, repair/partition, edge sizing and multiple/graphical CAD-face
selections remain open. See [CAD face sizing](LINUX_CAD_FACE_SIZING.md).

## Previous application milestone: retained CAD topology

A [source-bound CAD boundary graph](LINUX_CAD_TOPOLOGY.md) now survives CAD import,
PMA save/reopen, model editing, solving and remeshing. The existing Gmsh process
captures geometric points/curves/faces/volumes, bounds, physical groups and signed
boundary references before meshing; PMA v6 stores a checksummed topology payload
bound to the exact retained source hash. CLI `cad-topology` and GTK **Inspect
CAD…** expose the inventory. The portable suite passes **5,251 checks**. Native
Gmsh validates cube remesh repeatability, spherical seams and a shared face between
two volumes. The analytical quadratic pressure solve retains the identical graph
in its result archive, and GTK X11 software-WebGL import/remesh/inspection pass.
Clean Ubuntu 24.04 repeats topology capture/inspection and retained-source
remeshing with distribution Gmsh 4.12.1; the package upgrade preserves/reopens
the exact user PMA, and runtime/tool revalidation plus purge pass.
This is a parsed read-only boundary graph. Editable B-rep geometry, stable naming
across geometry/kernel changes, repair/partition and topology-based setup remain
open. See [CAD topology](LINUX_CAD_TOPOLOGY.md) for the bounds and evidence.

## Previous application milestone: local refinement boxes

One [local refinement box](LINUX_LOCAL_SIZING.md) now runs through GTK and CLI
CAD import/remesh, persists as a shared `BoxMeshSize` item in PMA metadata v31/v32,
and survives model editing and stored-control remeshing. Native Gmsh verifies
smaller tetrahedra near the box, including with conflicting GEO source sizing.
The portable suite passes **5,193 checks**. A locally refined quadratic cube
matches the analytical pressure-solve displacement at all **1,301 nodes** using
CalculiX 2.23 and reopens its result archive. GTK X11 software-WebGL import,
saved-control prefill and remesh acceptance pass. Clean Ubuntu 24.04 also passes
installed local quadratic remeshing and C3D10 export with distribution Gmsh 4.12.1,
an in-place package upgrade preserving/reopening the exact XDG-stored PMA,
private-runtime/tool revalidation and purge. Editable CAD topology, stable
naming, repair/partition, face/edge-based sizing and broader setup entities remain
open. See [local sizing](LINUX_LOCAL_SIZING.md) for bounds and validation.

## Previous application milestone: quadratic tetrahedra

Straight-sided quadratic CAD meshing now runs through GTK and CLI using native
Gmsh, producing editable C3D10 projects with six-node CAD-face regions. The
`--second-order` control survives save/reopen and stored-control remeshing;
GTK exposes and prefills the same control. Quadratic setup uses metadata v29/v30,
while first-order projects retain v27/v28. Import converts Gmsh midside ordering
and rejects non-midpoint nodes, inverted tetrahedra and mismatched surface nodes
before publication. The portable suite passes **5,159 checks**. Native Gmsh
4.14.0 acceptance passes, and CalculiX 2.23 matches the analytical uniaxial
solution at all **2,091 nodes** of a CAD-derived quadratic cube after pressure
creation, solve and result-archive reopening. GTK X11 import and remesh both
render 1,082 surface points and 2,160 triangles under software WebGL; this is
functional acceptance, not physical-GPU qualification. The clean Ubuntu package
harness was subsequently rerun successfully with quadratic and local-box
remeshing; see the latest milestone above.
Curved quadratic CAD projection/quality checks remain open alongside editable
B-rep topology, stable naming, repair/partition and local sizing. See
[CAD-to-Gmsh workflow](LINUX_GMSH_WORKFLOW.md).

## Previous application milestone: persistent global controls

Global CAD meshing controls now survive PMA save/reopen as one bounded native
`MeshingParameters` plus `TetrahedralGmsh` pair. Metadata v27 is the inline
schema and v28 is its external-mesh archive form. Project inspection reports
maximum/minimum size, Delaunay/Frontal/HXT algorithm and optimization; GTK
prefills the remesh dialog, and `remesh-cad PROJECT NEW_PROJECT` reuses the
stored values. Unsupported setup shapes fail closed. The portable suite passes
5,141 checks, native Gmsh performs the no-value second remesh, GTK X11 verifies
the reopened controls, and clean Ubuntu 24.04 repeats the workflow through the
installed package.

## Previous application milestone

Retained PMA-v5 CAD can now be remeshed directly from GTK or the portable CLI.
The operation snapshots the archived bytes into the private Gmsh job directory,
accepts finite positive minimum/maximum sizes, a bounded automatic/Delaunay/
Frontal/HXT algorithm choice and optional tetrahedral optimization, then uses the
existing validated MSH import and atomic PMA publication boundary. The new PMA
retains the exact geometry source and the one constant elastic material assigned
to all solid sections. Because topology identities can change, supports, loads,
results and other model edits are intentionally reset. Native Gmsh, CLI and GTK
X11 acceptance pass, and the clean Ubuntu package harness now repeats remeshing
with distribution Gmsh. See [CAD-to-Gmsh workflow](LINUX_GMSH_WORKFLOW.md).

## Previous authoring milestone

The GTK host now creates named element surfaces from faces selected directly in
the VTK.js view. In addition to one-face clicks, Alt-drag performs bounded
rendered-triangle rectangle selection; left-to-right requires containment and
right-to-left uses crossing selection. Both paths support modifier-based
replace/add/subtract/intersect operations. The renderer server validates every
source triangle, and the portable service resolves a batch to distinct solid
element faces with one project open. The editor previews face nodes, groups faces
into internal element sets, saves to a new PMA and removes stale results. A created surface
is immediately offered by both pressure and total-force traction authoring. GTK
X11 acceptance selected and persisted five cube faces in one crossing rectangle,
followed by pressure creation on that new surface and a successful CalculiX 2.23
solve with result-project reopening. Each selection is now republished as one
translucent orange, non-pickable face actor; the five-face GTK path verifies that
highlight before the dialog adds its pink node markers. Double-click now grows a
picked boundary face across locally connected faces using the native 0–90°
**Grow ≤** threshold. Growth uses mesh adjacency rather than a whole-model scan
and retains the 100-face publication bound. GTK acceptance at 90° shaded and
persisted all six cube faces, then created pressure and completed a CalculiX 2.23
solve with result reopening. See [surface editing](LINUX_SURFACE_EDITING.md),
[pressure editing](LINUX_PRESSURE_EDITING.md) and
[force-load editing](LINUX_LOAD_EDITING.md).

## Latest result-loading milestone

The Linux viewer now uses [indexed binary results](LINUX_INDEXED_RESULTS.md) for
supported static PMA-v4 payloads. A warm catalog uses metadata; selection decodes
one field record, plus matching displacement when required. Archive-content
checks, production numerical decoding and lifecycle cleanup remain in place.
FRD and unsupported binary layouts use the eager session path. All fixture
components and deformed scenes match independent reads, and both real renderer
acceptance cases pass. Cold indexing still validates mesh/history and scans every
field; selections rebuild the mesh. Measuring those remaining costs on the brake
project and larger multi-frame results is the next gate.

## Latest import milestone

INP mesh import now resolves nested `*INCLUDE, INPUT=...` files, named
node/element-set references and element-based surface definitions. Forward
references are supported for sets. Missing files, cycles, invalid faces and
unsupported include or surface options fail before publishing an archive.
See [mesh import](LINUX_MESH_IMPORT.md) for bounds and verification.

The supplied PrePoMax v2.5.1 dev PMX now passes a read-only NRBF record
inventory: 11,674 records across 250 class types. The new standalone
`PrePoMax.PmxInspect` tool also passes 22 record/metadata cases.
Current model collections are now separated from command history: 6 materials
(4 with plasticity), 3 nonlinear static steps, 12 contact pairs and 15
constraints. The current portable archive contract can represent these inspected
definition types, and the converter now constructs them explicitly from legacy records.
Optional `--mesh` inspection now validates 4,592,568 nodes, 960,847 quadratic
elements and 292 node/element sets in the supplied PMX. Its 20 binary-mesh
cases pass alongside the 22 record cases. This mesh size still exceeds the
bounded INP transfer path; direct PMX conversion now bypasses that limit.
The latest inspector resolves current part settings to C3D10, C3D20R and
C3D15 and checks 171 named links across 46 surfaces plus section/load/boundary
definitions, with no missing targets in this scope. Validation now passes
22 record/metadata cases and 16 mesh cases. The expanded checks pass 231
named links, including all contact and recognized constraint endpoints; all
960,847 elements reference existing parts with compatible quadratic settings.
The latest mesh pass confirms exact label membership for all 14 parts and
validates 60,303 topology-specific surface face assignments. Validation now
passes 22 record/metadata and 20 mesh cases. Solver applicability, element
quality and comparison with the original application's rendered results remain pending. See
[legacy PMX investigation](LINUX_PMX_COMPATIBILITY.md).

The inspector now emits an explicit conversion capability report aligned with
the current PMA persistence guards. PMA metadata v17 now round-trips C3D10,
C3D10T, C3D15, C3D20 and C3D20R elements, including explicit topology and
solver formulations; earlier versions remain readable. PMA metadata v7 added
ordered Elastic, Density, Plastic, ThermalExpansion,
ThermalConductivity and SpecificHeat definitions, including temperature tables,
plastic hardening and expansion reference temperature. Metadata v9 adds numeric
reference points and finite tabular amplitudes, retaining step load/boundary
links to them. Archive version 3 and metadata v10 move mesh arrays into a
checksummed `model.mesh` payload. Metadata v11 preserves element sets whose
labels are part IDs, and v13 preserves finite, properly defined auxiliary
cylinders. Metadata v15 adds surface interactions and contact pairs; v17 adds
ties, rigid bodies and surface springs. Metadata v19 adds contact field-output
requests. Metadata v21 adds ordered, bounded CalculiX user keywords. Metadata
v23 adds bounded, finite, coordinate-defined 3D coordinate systems. Metadata
v25 adds finite constant pressure on named element surfaces. Metadata v27 adds
the bounded global Gmsh setup pair; v29/v30 adds quadratic Gmsh setup and v31/v32
adds a local coordinate box. Current archive metadata v28/v30/v32 retains the
32 MiB JSON bound; archive v4 raises the combined payload bound to 1.5 GiB for
versioned binary results. The supplied project now
has **zero capability blocker groups** and reports
`eligible-for-converter-implementation` after complete mesh inspection. The new
fail-closed converter then created and reopened a 702 MiB archive-v4,
metadata-v18 PMA from the external fixture; current conversions write metadata
v28. Production export equivalence passes for the converted model:
4,592,568 nodes, 960,847 elements, 14 parts, 6 materials, 4 sections, 3 nonlinear
static steps, 12 contact pairs and 15 constraints. The conversion omits 78 empty
legacy element sets and normalizes duplicate memberships; it retains 83 node sets,
131 nonempty element sets and 46 surfaces. The source PMX remains unchanged.
The new `--results` pass consumes the complete 544,409,218-byte tail with bounded,
version-aware decoding. Serialized metadata and the one result-set name match.
It finds 16 fields: DISP, STRESS, TOSTRAIN and FORC at static times 0.2, 0.4,
0.7 and 1.0. Their 72 component arrays contain 127,825,128 finite values and no
non-finite samples; the history container has zero sets. The pass peaks near
707 MiB and takes about 11 seconds. The portable payload and loading gate is now
implemented: the converted PMA contains a 544,409,317-byte `results.bin`,
`PrePoMax.Cli fields` returns 124 raw
and derived selections, and a real MISES frame exports to a 44 MiB renderer scene.
GTK now converts a selected PMX to a new PMA and opens the resulting scene; its
success and fail-closed paths pass native X11 acceptance. The real 702 MiB PMA
also reaches an acknowledged GTK MISES contour frame with 714,210 visible points
and 1,428,456 triangles. `result-fingerprint` records exact node/value hashes,
statistics and samples for external comparison. The remaining result-parity gate
is comparison with the original application.

The new [Linux result-reference capture](LINUX_RESULT_REFERENCE_CAPTURE.md)
turns that comparison input into one fail-closed command. It selects the exact
field/frame through the production viewer companion, waits for stable browser
paint, captures the isometric contour and records diagnostics, scene topology,
runtime versions, project/artifact hashes and the deterministic result
fingerprint. The supplied BRAKE MISES frame now has a reproducible Linux bundle;
review against an equivalent original-application capture is still required.

PMA-v4 result operations now validate all archive entries as streams and copy
only `model.json` plus `results.bin`; they no longer materialize the 4.6-million-
node editable model. Geometry publication likewise retains only `model.json` and
`model.mesh`, with compact external-mesh records. The real fingerprint peak fell
from 9,060,720 to 3,129,820 KiB, direct geometry-scene peak fell from 8,604,768
to 2,491,308 KiB, and GTK model-to-MISES peak fell from 12,477,460 to 3,632,488
KiB. GTK elapsed time fell from 58.9 to 33.55 seconds. See
[result streaming](LINUX_RESULT_STREAMING.md).

Broader read-only coverage now includes seven distinct real PMX saves: six from
v2.5.1 dev and one from v2.4.0. All seven complete bounded record and mesh
inspection with unchanged hashes. Adding contact field-output persistence and
conversion removes the sole blocker from five fixtures spanning 3.1–4.6 million
nodes. The 3,069,296-node MAG fixture now completes real metadata-v20 conversion,
independent CLI reopen, canonical 259 MB CalculiX export equivalence, scene
export and authenticated renderer publication. The resulting renderer manifest
contains 14 actors, 513,064 points and 1,026,148 triangles; the source hash is
unchanged. Metadata v21/v22 now preserves bounded active, unsuppressed leaf
CalculiX user keywords and their ordered insertion paths. NEXTREMOIN now
completes a 4,623,915-node metadata-v22 conversion and independent CLI reopen;
its root-index-16 `*Preprint` line appears in the production deck and the source
hash remains unchanged. Four additional v2.5.1 fixtures remain
converter-eligible. The v2.4.0 NEON fixture now completes metadata-v24
conversion with its rectangular coordinate system, two ordered user keywords
and one binary result set. Independent reopen exposes 62 result selections;
canonical 245,843,628-byte deck export, scene export and authenticated renderer
publication pass. The reader now reconstructs and consumes unnamed pre-v2.4.4
result parts before reading their node lookup. See the
[broader fixture matrix](LINUX_PMX_FIXTURE_MATRIX.md).

## Latest authoring milestone

The GTK host now edits constant elastic materials and existing concentrated
forces, surface tractions, surface pressures and prescribed translations in supported `.pma` projects. Force selection uses
both step and load name; the assigned region stays unchanged. Each edit saves
a new project, removes stale FRD/DAT results and reopens the mesh. Native
CalculiX acceptance verifies the edited load's displacement and the unchanged
second step. See [material editing](LINUX_MATERIAL_EDITING.md) and
[force editing](LINUX_LOAD_EDITING.md) and
[boundary editing](LINUX_BOUNDARY_EDITING.md) for scope and verification.
**Add material…** now creates a constant elastic material and assigns it to an
existing solid section atomically. Native GTK save/reopen and CalculiX 2.23
acceptance pass; doubling the assigned Young's modulus halves the cube fixture's
maximum U1 in both steps. See [material editing](LINUX_MATERIAL_EDITING.md).
**Assign material…** switches a solid section to a different existing supported
material without duplicating it. GTK save/reopen passes, and assigning the
original E=1000 material restores the fixture's prior solver response.
The material editor now removes unused materials while disabling removal for
materials referenced by any section. Native save/reopen and solver continuity
pass after removing the unreferenced E=2000 fixture material.
Constant material density can now be added, changed or removed. **Gravity…**
offers only density-backed solid-section regions and creates a production
`GravityLoad` in a selected step. GTK save/reopen and CalculiX 2.23 acceptance
pass; unit self-weight adds the analytical 0.0005 axial displacement while the
step without gravity remains unchanged. See
[density and gravity authoring](LINUX_GRAVITY_EDITING.md).
The same **Gravity…** dialog now edits acceleration and removes existing gravity
definitions. Doubling acceleration produces the analytical 0.002 response;
removal restores the original 0.001 fingerprint.
**Centrifugal…** now creates, edits and removes density-backed 3D centrifugal
loads. GTK save/reopen passes for all three operations. CalculiX normalizes the
axis and squares speed: speed 2 adds the analytical 0.00133333 axial displacement,
speed 3 raises the combined response to 0.004, and removal restores the original
0.001 fingerprint. See
[centrifugal-load authoring](LINUX_CENTRIFUGAL_EDITING.md).
Boundary edits now constrain or release translations in existing displacement
boundaries, while refusing an empty boundary definition. Native acceptance
checks prescribed movement and the response after adding a zero constraint.
New displacement boundaries can now be added to existing node sets, with
step-specific native solver acceptance. **Add fixed support…** now creates a
production `FixedBC` on a selected step and nonempty node set. GTK save/reopen
passes, and CalculiX acceptance reports step-1 DISP/U1 exactly zero on all cube
nodes while the unchanged second step retains maximum U1 0.002. See
[boundary editing](LINUX_BOUNDARY_EDITING.md). [Node-set creation](LINUX_NODE_SET_EDITING.md)
now accepts validated mesh node IDs or an inclusive coordinate box and makes
the new region available to
boundary creation. Selected nodes can now be previewed as markers on the
undeformed mesh before saving. New concentrated forces can also be added to node sets,
with explicit per-node force units and native solver acceptance. Existing
force loads and displacement boundaries can be removed from a selected step
when saving a new project; see [definition removal](LINUX_DEFINITION_REMOVAL.md).
The [mesh importer](LINUX_MESH_IMPORT.md) now defaults to mesh-only `.inp`
(C3D4/C3D8/C3D8R, named node/element sets); ASCII `.mesh` remains available.
It creates a new editable project through the CLI or GTK import dialog,
using archive version 3 with metadata v28 and a binary mesh payload.
Imported tetrahedra render, accept model edits, solve and display result
contours. GTK import requests explicit material values and a new output path;
failed imports preserve the current scene. The GTK launcher also starts
[without a project](LINUX_EMPTY_START.md), offering Open and Import while
model actions remain disabled. CAD/meshing integration, broader formats and
graphical selection remain pending. Previously generated viewer preview archives
must be rebuilt to include these editors.

## Next integration milestones

1. **Model-to-solver core:** expand CaeModel around the minimal static analysis
   model and production CalculiX input writer. Acceptance: construct the cube
   using model objects, export its input, solve natively, and load results.
   The cube/bar acceptance now constructs FeModel objects, writes complete input
   through CalculixFileWriter.WriteActiveModel, solves and imports results.
   Constant surface-traction conversion now runs through shared production code,
   including the native cube/bar acceptance; see [traction evidence](SURFACE_TRACTION_EVIDENCE.md).
   Constraint definitions and tie/rigid-body/equation writers now compile; native
   acceptance includes axial equation constraints. See [constraint evidence](CONSTRAINT_EXPORT_EVIDENCE.md).
   The real FeModel aggregate now builds and owns the acceptance model graph.
   [Aggregate evidence](FEMODEL_AGGREGATE_EVIDENCE.md) records the remaining legacy
   methods. The [production exporter evidence](CALCULIX_FULL_WRITER_EVIDENCE.md)
   records the prepared-model API. [Active export preparation](ACTIVE_EXPORT_EVIDENCE.md)
   now removes inactive parts on an independent mesh snapshot. General project
   cloning remains separate.
   The writer now uses explicit temporary mesh copies with transactional setup
   and restoration; see [export-state evidence](CALCULIX_EXPORT_STATE_EVIDENCE.md).
   User-keyword and gravity/centrifugal export copies also no longer use legacy
   serialization; see [export-copy evidence](CALCULIX_EXPORT_COPY_EVIDENCE.md). See the
   [integrated static workflow](STATIC_MODEL_WORKFLOW.md) for the exact boundary.
2. **Project reopen:** choose and implement portable metadata persistence and
   legacy import boundaries. The first [versioned archive container](PORTABLE_ANALYSIS_ARCHIVE.md)
   now also carries [bounded editable static-model metadata](STATIC_PROJECT_PERSISTENCE.md).
   Native cube/bar acceptance saves, reopens, solves and archives results.
   [Metadata version 2](MECHANICAL_PROJECT_PERSISTENCE.md) adds fixed boundaries,
   concentrated/gravity/centrifugal loads and material density.
   [Metadata version 3](MULTIPART_PROJECT_PERSISTENCE.md) preserves multiple mesh
   parts and their material assignments, with a native two-material solve. Plastic
   and thermal properties now round-trip as well; broader regions,
   derived-result definitions and legacy import remain pending.
3. **Linux application host:** a [command-line host](LINUX_CLI_HOST.md) now opens
   editable projects, exports input and runs isolated native solver jobs with
   archived results. [Portable application services](LINUX_APPLICATION_SERVICES.md)
   now own these operations and expose pollable, cancellable project jobs.
   The [portable scene bridge](LINUX_RENDER_SCENE_BRIDGE.md) exports undeformed
   project mesh geometry in the renderer host-upload format. The
   [portable renderer host](LINUX_RENDERER_HOST.md) now starts a private server,
   publishes geometry, verifies an authenticated session and owns shutdown.
   The [browser bootstrap boundary](LINUX_BROWSER_BOOTSTRAP.md) now validates
   trusted readiness and supplies the current session through an adapter callback.
   [Real-browser acceptance](LINUX_BROWSER_RENDER_EVIDENCE.md) now verifies the
   complete project-to-browser path, stable frame acknowledgement, resize and reload
   under software WebGL. A [runnable Chromium viewer reference](LINUX_VIEWER_SPIKE.md)
   opens projects in a Linux window and owns renderer shutdown; headed and
   headless acceptance pass. The [GTK/WebKitGTK host](LINUX_GTK_VIEWER_SPIKE.md)
   passes scene, reload and shutdown checks in a native embedded window. The
   [shell decision record](LINUX_SHELL_CANDIDATES.md) now selects GTK 3/WebKitGTK
   for production Linux use. It runs as `Gtk.Application` with the stable desktop
   application ID. GTK uses asynchronous companion communication and a native
   file-open dialog, with tested project switching and invalid-file recovery.
   [GTK solve/cancel controls](LINUX_GTK_SOLVER_WORKFLOW.md) now run real native
   jobs, report status, cancel and reopen saved result projects.
   [Archived-result contours](LINUX_RESULT_CONTOURS.md) now connect FRD
   field/component/frame selection to scalar display on the undeformed mesh.
   [Displacement scaling](LINUX_DEFORMED_RESULTS.md) now deforms static frames
   while preserving contour values and supports exact zero-scale restoration.
   [GTK view recovery](LINUX_GTK_RECOVERY.md) now restores the retained scene
   after WebKit process termination through a native Retry view action.
   [Native clipboard copying](LINUX_GTK_CLIPBOARD.md) now exports a bounded
   scene summary through GTK, with private-selection X11 acceptance.
   [Native keyboard/accessibility groundwork](LINUX_GTK_ACCESSIBILITY.md) adds
   named controls, shortcuts and mnemonics, with focused X11 smoke at 1× and 2×.
   F6/Shift+F6 now switch to the embedded camera control and restore native
   focus. [Wayland scheduling investigation](LINUX_WAYLAND_FRAME_SCHEDULING.md)
   identified the reproduced stall: GTK 3 reports a fully obscured native window
   while WebKitGTK 2.52.6 still reports a visible page. Native visibility now
   pauses both frame and startup deadlines. Real cover/uncover acceptance
   completes the same scene without retry or reload; exposed failures still
   enable Retry view. The bounded bridge also preserves lifecycle messages
   through bursts of ignored notifications. Current client validation passes
   92 tests plus the production build; the portable numerical baseline is unchanged.
   DRM-backed WebGL 2 acceptance passes on X11 and Wayland with the integrated
   Intel `i915` device; forced software rendering is rejected.
   Cancelled native file choosers now restore the initiating native or embedded
   control, covered by the extended focus smoke. Full Tab-order, screen-reader
   and Wayland/DPI qualification remain next.
   Native force authoring now offers both nonempty node sets and valid element
   surfaces; the latter creates total-force surface tractions and passes GTK
   save/reopen plus a real CalculiX solve. Native pressure authoring now creates,
   edits and removes constant `DLoad` definitions on those element surfaces;
   GTK save/reopen and the real cancellation response pass as well. Native
   element-surface creation now consumes validated renderer click or rectangle
   selections, retains explicit solid element-face entry, previews the selected
   nodes and feeds both load workflows. The crossing-rectangle smoke persists five
   faces and verifies shaded multi-face feedback. Native double-click growth now
   applies a configurable 0–90° local normal threshold; its six-face smoke also
   reaches pressure authoring and a real solver run.
   Native fixed-support creation now selects a step/node set, persists a
   production `FixedBC`, invalidates old results and passes GTK plus CalculiX
   solve/result-reopen acceptance.
   Native material creation now selects an existing solid section, adds a
   constant elastic material, changes its assignment and passes GTK plus
   CalculiX solve/result-reopen acceptance. Existing supported materials can
   also be reassigned without creating a duplicate, and unused materials can be
   removed without affecting active section assignments.
   Constant density editing plus density-gated gravity and three-dimensional
   centrifugal creation, editing and removal now pass native GTK and analytical
   CalculiX acceptance.
   PEND-006 is complete. The [CAD-to-Gmsh workflow](LINUX_GMSH_WORKFLOW.md) now
   creates a validated tetrahedral PMA directly from a selected CAD solid.
   Generated CAD-face regions, explicit GEO physical names and exact source bytes
   now persist, and retained CAD can be remeshed into a new project with bounded
   global Gmsh controls. Remaining application work parses editable CAD/B-rep
   topology, adds stable naming, repair/partition and local setup entities, and
   expands the broader controller operations. The packaged
   host already completes the documented import/model/solve/view workflows
   without hidden Windows dependencies.
4. **Packaging and parity:** [Linux viewer build/launch entry points](LINUX_VIEWER_BUILD.md)
   now build the portable solution, renderer and locked client, and launch GTK
   from one project path. Runtime preflight now executes both managed entry
   points without starting services and detects incompatible roll-forward before
   opening the viewer. Local end-to-end launch and negative preflight checks pass.
   A [relocatable preview archive](LINUX_VIEWER_BUNDLE.md) now publishes both
   managed applications and includes the client, launcher, private .NET 10 and
   ASP.NET Core runtimes, runtime notices and checksum manifest. The smaller
   framework-dependent mode remains available. Extraction under a spaced path,
   private-runtime probes with external .NET hidden, real render/reload/shutdown,
   overwrite refusal and missing-bundle-artifact rejection pass locally. The
   updated locked client passes 92 tests, production build and a zero-vulnerability
   npm audit. A [reproducible Debian/Ubuntu amd64 package](LINUX_DEBIAN_PACKAGE.md)
   installs that private-runtime payload under `/usr/lib`, declares the host
   Python/GTK/WebKit stack, and supplies a command, desktop entry, AppStream data
   and PMA/PMX MIME registration. Metadata validation, extraction, private-runtime
   preflight and real X11 rendering pass. A fresh Ubuntu 24.04 container also
   resolves the declared dependencies, validates installed desktop/MIME caches
   and private runtimes, installs the recommended Ubuntu CalculiX 2.21 package,
   and solves an analytical C3D8 cube. It upgrades to a strictly higher version
   while preserving XDG user state, revalidates solver discovery, and verifies
   complete application cleanup after purge. The CI workflow now rebuilds and
   repeats that acceptance. The package now recommends distribution Gmsh and the
   clean harness creates a PMA from STEP, remeshes its retained CAD and inspects
   both archives. Remaining native tools,
   signing/update delivery and broad GPU/distro qualification remain open.

Prioritize these integration gates over extending isolated result edge-case tests.
Do not treat the earlier evaluation's person-month estimate as an updated forecast;
there is not enough measured application-integration work to give a reliable
completion date or percentage.

## Build and test the current Linux core

From the repository root, with .NET 10 available on PATH:

```bash
dotnet build portable/PrePoMax.Core.slnx -c Release
bash scripts/test-portable.sh
```

On this workstation the SDK is also available at
`~/.local/share/prepomax/dotnet/dotnet`. The solution builds the eight portable
libraries, the command-line executable and the viewer companion together; it does not produce a Linux
desktop executable. Native
acceptance remains opt-in through `scripts/test-native-calculix.py` and
`scripts/test-native-gmsh.py` with their documented runtime prerequisites.

StaticStep and its keyword writers now compile; see the
[recorded build boundary](STATIC_STEP_BUILD_BOUNDARY.md). Native acceptance inputs
now generate static headers, output requests and step termination through production code.

## Completed core evidence

- [Mesh aggregate](FEMESH_AGGREGATE_EVIDENCE.md), [mesh I/O](MESH_IO_EVIDENCE.md),
  [Gmsh export](GMSH_EXPORT_EVIDENCE.md) and [native Gmsh](NATIVE_GMSH_EVIDENCE.md).
- [Native process execution](NATIVE_PROCESS_EVIDENCE.md) and
  [CalculiX integration](CALCULIX_INTEGRATION.md).
- [Result aggregate](FERESULTS_AGGREGATE_EVIDENCE.md), [FRD import](FRD_IMPORT_EVIDENCE.md),
  [history export](HISTORY_EXPORT_EVIDENCE.md), [cloud interpolation](CLOUD_INTERPOLATION_EVIDENCE.md)
  and [surface interpolation](MESH_INTERPOLATION_EVIDENCE.md).

The chronological slices below retain earlier boundaries. The status table above
supersedes statements that the full FeResults aggregate is still pending.

## Direction and existing plans

Follow the phased core/platform/persistence/UI migration in
[LINUX_PORT_EVALUATION.md](../LINUX_PORT_EVALUATION.md), with the renderer work
already implemented in this branch. That August evaluation predates the VTK.js
migration: its proposal to choose a new native ActiViz renderer is historical,
not a reason to discard the existing portable server/client.
`PLAN.md` is the **BaramMesh integration plan**, not the Linux migration plan.

Use [VTKJS_PENDING_WORK.md](VTKJS_PENDING_WORK.md) for renderer acceptance.
PEND-004 (remaining ActiViz dependencies) and PEND-001 (large-model physical
Linux GPU benchmark) remain open. PEND-006 is complete with GTK/WebKitGTK selected;
new Linux workflows should target that host rather than begin wholesale form
conversion.

## Checkout baseline

- Development host inspected: Ubuntu 26.04.1 LTS, x64. This does not establish
  a supported release distribution; CI initially uses Ubuntu 24.04 and Windows.
- `PrePoMax.sln` and its libraries still use .NET Framework 4.8 projects.
  WinForms, ActiViz, native Windows libraries, and process/path assumptions
  prevent building the complete application natively here.
- VTK.js server/client already have recorded Linux functional evidence.
  The prototype uses SDK 9.0.3xx/latest feature band and targets .NET 8;
  keep that separate from the .NET 10 core migration until deliberately upgraded.
- No .NET, Node, npm, or curl was available on PATH at initial inspection.
  The original temporary SDK was removed between sessions. SDK 10.0.401 is now
  installed persistently at `~/.local/share/prepomax/dotnet`. The test script
  finds it automatically when `dotnet` is absent from PATH; shell profiles were
  not modified.

## First portable library

`portable/STL/STL.csproj` builds the **existing production STL sources** for
`net10.0`, with their existing assembly identity. The original Windows project
and solution remain available. Separate project/output directories avoid mixing
legacy and portable intermediate files. No copied implementation or compatibility
stubs are used, and this slice has no NuGet package dependencies.

Run from the repository root:

```bash
bash scripts/test-portable.sh
# To select an SDK explicitly:
PREPOMAX_DOTNET="$HOME/.local/share/prepomax/dotnet/dotnet" bash scripts/test-portable.sh
```

Install a .NET 10 SDK for permanent development using Microsoft's
[installation instructions](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script).
`portable/global.json` scopes SDK selection to this migration; it does not
override the renderer's SDK pin.

The dependency-free characterization executable checks independently specified
ASCII triangle coordinates and ordering, LF/CRLF input, English/Italian locales,
text and binary disk round trips, Unicode/spaced paths, binary record layout and
attributes, and caller-owned stream lifetime. It exits nonzero on failure.
The `Portable core` CI workflow runs the same executable on Linux and Windows.
This is a small valid-file baseline, not malformed-file or full STL conformance
coverage. It does not exercise the application's separate mesh import layer.

Validation on 2026-09-10: Ubuntu 26.04.1 x64, SDK 10.0.401, **60 checks passed**.
`bash -n scripts/test-portable.sh` and `git diff --check` passed. The Windows CI
job is configured but has not been run locally. Existing-source warnings remain:
two CS0108 warnings for `Normal.Read` hiding `Vertex.Read`, and CA2022 for the
single unchecked stream read in `STLDocument.IsText`. Characterize short-reading
streams before changing that behavior in a follow-up.

## Next implementation gates

The CaeGlobals extraction now includes 153 shared source files compiling on
.NET 10, with 766 passing checks alongside 60 STL checks and 178 legacy evaluator
oracle checks. See the
[dependency boundary, fixes, and evidence](CAEGLOBALS_PORTABILITY.md). Selection,
region metadata, initial numerical helpers, units and equations are included.
[Evaluator compatibility evidence](EQUATION_PORT_COMPATIBILITY.md) records the
tested API adaptation and its limits. Desktop/platform helpers remain outside
this subset. Next: bring mesh sources into the portable graph and extract their
remaining collection, persistence and mesher dependencies incrementally.

The first [CaeMesh slice](CAEMESH_PORTABILITY.md) is now implemented: 30 active
node/element/geometry source files build with 1,048 mesh checks, bringing the
combined suite to 2,052 passing assertions. `FeMesh` and parts remain outside the
build. Next: primitive binary I/O and typed copy behavior for parts, followed by
mesh collections and meshing configuration boundaries.

The [copy/I/O follow-up](MESH_COPY_IO_EVIDENCE.md) is also complete: primitive
binary I/O, explicit selection/mass-property copies, and mesh groups/sets are now
included. The current suite passes 2,106 assertions across 154 CaeGlobals and
35 CaeMesh source files plus STL. Next: resolve bounding-box tag copying and
extract visualization topology/graph dependencies to bring parts into the graph.

The [parts/visualization slice](MESH_PARTS_VISUALIZATION_EVIDENCE.md) now adds
base/mesh/result parts, bounding boxes, visualization and graph/topology helpers.
CaeMesh contains 53 source files and the current suite passes 2,172 assertions.
Object-valued bounding-box tags still need an explicit application copy policy;
default null/integer-tag copies are tested. Next: geometry/CAD parts and meshing
settings, then the remaining `FeMesh` aggregate boundaries.

The [geometry/configuration slice](GEOMETRY_CONFIGURATION_EVIDENCE.md) now brings
CaeMesh to 62 production source files, with 1,166 mesh checks and 2,198 total
assertions passing on Linux. Geometry and compound copies preserve their types,
flags and independent settings; mesher configuration numbers are culture invariant.
Next: remaining `FeMesh` aggregate dependencies and native tool boundaries.

The [mesh region slice](MESH_REGION_EVIDENCE.md) brings CaeMesh to 66 shared
source files and 1,208 mesh checks (2,240 assertions total). Surfaces, reference
points and coordinate systems are included. Next: isolate Gmsh setup/data
contracts, then resolve the aggregate copy and desktop notification boundaries.

The [Gmsh setup slice](GMSH_SETUP_EVIDENCE.md) brings CaeMesh to 81 shared
source files and 1,242 mesh checks (2,274 total). Configuration no longer imports
the native bridge, and extrusion/revolution/sweep copies preserve independent
geometry arrays. Next: Gmsh data persistence and aggregate service boundaries.

The [Gmsh data/topology slice](GMSH_TOPOLOGY_EVIDENCE.md) brings CaeMesh to 85
shared source files and 1,258 mesh checks (2,290 total). The in-memory contract
is separate from the Windows serialization writer; a portable process handoff
remains pending. Next: remaining FeMesh aggregate services and persistence.

The [mesh helper extraction](MESH_HELPER_EVIDENCE.md) brings CaeMesh to 91
shared source files and 1,532 mesh checks (2,564 total). FeMesh delegates geometry
ID encoding and point transforms to portable helpers. Its aggregate still needs
framework cleanup, collection resolution, notifications and copy ownership.

1. **Portable core extraction:** inventory `CaeGlobals` by dependency, separate
   UI editors/dialogs/Win32 services from data and algorithms, and characterize
   units/equations/collections. Then bring `CaeMesh`, `CaeModel`, and `CaeResults`
   across incrementally, removing concrete renderer dependencies. Compile actual
   shared sources and retain Windows regression checks at each step.
2. **Native tools:** the validated Linux Gmsh handoff is connected to GTK and CLI,
   and its distribution dependency is packaged and tested. Portable tool
   discovery, process cancellation and Ubuntu CalculiX 2.21 package acceptance pass.
   Resolve Linux 3MF assets and test real archive output. MMG, Netgen, and
   OpenFOAM need independent integration evidence.
3. **Persistence:** define new versioned writes and a bounded legacy import
   boundary before claiming `.pmx` compatibility on modern .NET. Include old
   files, command history, material libraries, and protected projects.
4. **Linux desktop host:** GTK 3/WebKitGTK is selected under PEND-006 and now has
   a stable application identity, packaged launch, real VTK.js rendering, native
   dialogs, input/accessibility groundwork and owned shutdown/recovery. Extend
   screen-reader and mixed-monitor qualification without reopening the framework
   decision.
5. **End-to-end MVP:** bounded CAD import → Gmsh → editable model now passes;
   generated/physical face regions persist and reach production CalculiX pressure
   output. Complete broader CAD cases, full solve/result scenarios and physical
   GPU acceptance separately.
6. **Packaging:** the reproducible Debian package, XDG job storage, private
   runtime and clean Ubuntu install/upgrade/purge gate now pass. Signing, update
   delivery, remaining native-tool integration and broader distro/GPU qualification remain.

The current bounded application is runnable on Linux and has native solver and
legacy-import evidence. Full desktop parity, editable CAD/advanced meshing and
the broad physical-GPU qualification matrix are not yet established.

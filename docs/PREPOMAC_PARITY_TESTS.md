# Prepomac parity acceptance catalogue

Generated from [parity/plan.json](parity/plan.json), updated 2026-11-09. Edit the registry and regenerate with `python3 scripts/check-web-parity-plan.py --write-catalogue` from the trial root.

**52 acceptance cases** (3 in_progress, 1 passed, 48 planned) map 74 desktop scripts (77 declared runs), 3 offline checks and 15 custom verifiers. These are mappings, not passing test results. The registry separately tracks desktop run outcomes and web-case evidence.

Implementation order, release gates, comparison rules, numerical thresholds and evidence requirements are in the [roadmap](PREPOMAC_IMPLEMENTATION_ROADMAP.md). Cases with `web_only` reference mode cover online behavior absent from desktop Prepomac. `mixed` cases retain desktop semantics and add an explicitly adapted hosted/native boundary.

## Common execution contract

Use identical immutable fixture copies, units and recorded toolchains. Run the reference operation and web/application operation, then exercise the actual browser task. Compare every listed output with its declared policy, save/reopen, and check rejection/cancellation behavior. Retain both outputs and a machine-readable diff. Where no original oracle/fixture exists, create and review one before claiming a pass. Do not delete assertions to fit a different host.

## M00 — Reference baseline and comparison harness

Milestone prerequisites: none. Status: **in_progress**.

### M00-T01 — Freeze and inventory the current reference working tree

Status: **in_progress**. Policy: `exact-domain`. Reference mode: `desktop`.

References: [Run_Tests.py](../../../../APIs/Python/Test_Scripts/Run_Tests.py), [Audit_API_Coverage.py](../../../../APIs/Python/Test_Scripts/Audit_API_Coverage.py), [Audit_API_Documentation.py](../../../../APIs/Python/Test_Scripts/Audit_API_Documentation.py), [test_runner_offline.py](../../../../APIs/Python/Test_Scripts/test_runner_offline.py).

Fixtures: Current root working tree, including untracked custom sources; all collected script headers.

1. Record Git HEAD, dirty/untracked state and SHA-256 of relevant source/test/fixture/build inputs; record SDK, tools, browser and hardware.
2. Inventory every declared run, expected exit/report condition and generated-fixture dependency; never count a script name as one run when its header declares several.
3. A missing, newly added or changed reference test is surfaced for review rather than silently omitted.

The original 2026-11-06 baseline was completed and its immutable evidence is retained. The current reference inventory added virtual-friction and dialog-layout verifiers; M00 is reopened for that extension. Current sources/runtime are captured for the material slice, but three new reference verifiers remain not_run.

Implementation: [capture-prepomac-baseline.py](../scripts/capture-prepomac-baseline.py), [run-prepomac-custom-baseline.py](../scripts/run-prepomac-custom-baseline.py), [record-parity-evidence.py](../scripts/record-parity-evidence.py).

Evidence: [summary.json](evidence/M00-2026-11-06/summary.json), [reference-outcomes.json](evidence/M00-2026-11-06/reference-outcomes.json), [REPORT.md](evidence/M01-identity-2026-11-09/REPORT.md), [summary.json](evidence/M01-identity-2026-11-09/summary.json).

### M00-T02 — Establish the dual-adapter comparison protocol

Status: **passed**. Policy: `exact-domain`. Reference mode: `desktop`.

References: [Test_Model_Document.py](../../../../APIs/Python/Test_Scripts/Test_Model_Document.py), [Test_Selection_Correctness.py](../../../../APIs/Python/Test_Scripts/Test_Selection_Correctness.py), [Test_Result_Field_Outputs.py](../../../../APIs/Python/Test_Scripts/Test_Result_Field_Outputs.py), [Test_Model_Materials_Sections.py](../../../../APIs/Python/Test_Scripts/Test_Model_Materials_Sections.py).

Fixtures: One document/property case, one sparse-ID selection case and one fixed FRD field case.

1. Reference and web adapters emit the same versioned semantic snapshots keyed by source IDs and explicit units.
2. Deliberately change one selected ID, one material value and one result value; each comparator must fail and identify the differing path.
3. Separate passed, failed, not-run, blocked and explicitly excluded outcomes; no blanket skip-to-pass.

Implementation: [run-web-parity.py](../scripts/run-web-parity.py), [compare.py](../tests/parity/compare.py), [reference_snapshot.py](../tests/parity/reference_snapshot.py), [ProjectParitySnapshot.cs](../portable/PrePoMax.Application/ProjectParitySnapshot.cs), [ParityEndpoints.cs](../portable/PrePoMax.Web/ParityEndpoints.cs), [Program.cs](../portable/tests/Parity.Probe/Program.cs).

Evidence: [harness-summary.json](evidence/M00-2026-11-06/harness-summary.json), [model-comparison.json](evidence/M00-2026-11-06/model-comparison.json), [field-comparison.json](evidence/M00-2026-11-06/field-comparison.json), [harness-wrong-property.json](evidence/M00-2026-11-06/harness-wrong-property.json), [harness-wrong-material-value.json](evidence/M00-2026-11-06/harness-wrong-material-value.json), [harness-wrong-source-id.json](evidence/M00-2026-11-06/harness-wrong-source-id.json), [harness-wrong-field-value.json](evidence/M00-2026-11-06/harness-wrong-field-value.json).

## M01 — Shared operations, commands and lossless project contracts

Milestone prerequisites: M00. Status: **in_progress**.

### M01-T01 — Shared model operations and replacement semantics

Status: **in_progress**. Policy: `exact-domain`. Reference mode: `desktop`.

References: [Test_Api_Session_Rules.py](../../../../APIs/Python/Test_Scripts/Test_Api_Session_Rules.py), [Test_Model_Definition_Coverage.py](../../../../APIs/Python/Test_Scripts/Test_Model_Definition_Coverage.py), [Test_Model_Materials_Sections.py](../../../../APIs/Python/Test_Scripts/Test_Model_Materials_Sections.py), [Test_Model_Readback_Equations.py](../../../../APIs/Python/Test_Scripts/Test_Model_Readback_Equations.py).

Fixtures: Prepared static model plus one unsupported/custom definition.

1. A replace keeps every unspecified property and expression as Prepomac does.
2. The shared gateway validates a complete operation before publication; rejected operations preserve current model, revision and references.
3. An unrelated edit preserves unsupported fields or is refused before changing the project; no silent narrowing of the model schema.

Material patch/rename plus typed constant-elastic creation, solid-section assignment and unused removal/recreation qualified. Material IDs persist; source rename-section discrepancy remains explicit. Other entity IDs, broader definitions/equations, PMX and journal/script gates remain open.

Implementation: [ProjectCommandSession.cs](../portable/PrePoMax.Application/ProjectCommandSession.cs), [ProjectMaterials.cs](../portable/PrePoMax.Application/ProjectMaterials.cs), [Workspace.cs](../portable/PrePoMax.Web/Workspace.cs), [Program.cs](../portable/PrePoMax.Web/Program.cs), [ProjectCommandChecks.cs](../portable/tests/MeshIO.Characterization/ProjectCommandChecks.cs), [run-material-command-parity.py](../scripts/run-material-command-parity.py), [M01_MATERIAL_COMMANDS.md](M01_MATERIAL_COMMANDS.md), [ProjectMaterialIdentities.cs](../portable/PrePoMax.Persistence/ProjectMaterialIdentities.cs), [StaticProjectArchive.cs](../portable/PrePoMax.Persistence/StaticProjectArchive.cs), [AnalysisArchive.cs](../portable/PrePoMax.Persistence/AnalysisArchive.cs), [ProjectMaterialIdentityChecks.cs](../portable/tests/MeshIO.Characterization/ProjectMaterialIdentityChecks.cs), [run-material-identity-parity.py](../scripts/run-material-identity-parity.py), [ProjectMaterialLifecycleChecks.cs](../portable/tests/MeshIO.Characterization/ProjectMaterialLifecycleChecks.cs), [run-material-lifecycle-parity.py](../scripts/run-material-lifecycle-parity.py), [reference_material_lifecycle.py](../tests/parity/reference_material_lifecycle.py).

Evidence: [REPORT.md](evidence/M01-material-2026-11-06/REPORT.md), [summary.json](evidence/M01-material-2026-11-06/summary.json), [REPORT.md](evidence/M01-identity-2026-11-09/REPORT.md), [summary.json](evidence/M01-identity-2026-11-09/summary.json), [REPORT.md](evidence/M01-lifecycle-2026-11-09/REPORT.md), [summary.json](evidence/M01-lifecycle-2026-11-09/summary.json).

### M01-T02 — PMX/PMA compatibility, protection and atomic reopen

Status: **planned**. Policy: `exact-domain`. Reference mode: `desktop`.

References: [verify-pmx-compatibility.py](../../../../macOS/verify-pmx-compatibility.py), [verify-custom-features.py](../../../../macOS/verify-custom-features.py), [StaticProjectChecks.cs](../portable/tests/MeshIO.Characterization/StaticProjectChecks.cs).

Fixtures: Current PMX, older Windows command fixture, truncated/protected files, custom definitions and embedded results.

1. Preserve model IDs, connectivity, typed definitions, history, cylinders, fatigue and results where present.
2. Verify cross-open against Prepomac for each claimed PMX direction; PMA semantic equality alone is not PMX interoperability.
3. Wrong key, modified protection wrapper, partial stream and unsupported command/layout leave the open project intact.
4. Keep keys out of URLs, logs, saved state and journals.

Evidence: none recorded.

### M01-T03 — Command identity, transactions and replay foundation

Status: **in_progress**. Policy: `exact-domain`. Reference mode: `desktop`.

References: [Test_Rpc_Live_Session.py](../../../../APIs/Python/Test_Scripts/Test_Rpc_Live_Session.py), [Test_Record_And_Run_History_Output.py](../../../../APIs/Python/Test_Scripts/Test_Record_And_Run_History_Output.py).

Fixtures: Create/replace/rename/remove commands, undo branches and a deliberately failing multi-operation transaction.

1. A UI/API mutation records one domain command with source revision and stable object identity.
2. Undo/redo restores object state, references and result validity; discarded redo does not reappear.
3. Define Prepomac partial-script-failure semantics separately from atomic single-operation rejection; do not silently roll back or replay a different history.
4. Snapshot history is not treated as journal/regeneration parity.

Material patch/rename plus typed constant-elastic creation, solid-section assignment and unused removal/recreation qualified. Material IDs persist; source rename-section discrepancy remains explicit. Other entity IDs, broader definitions/equations, PMX and journal/script gates remain open.

Implementation: [ProjectCommandSession.cs](../portable/PrePoMax.Application/ProjectCommandSession.cs), [ProjectMaterials.cs](../portable/PrePoMax.Application/ProjectMaterials.cs), [Workspace.cs](../portable/PrePoMax.Web/Workspace.cs), [Program.cs](../portable/PrePoMax.Web/Program.cs), [ProjectCommandChecks.cs](../portable/tests/MeshIO.Characterization/ProjectCommandChecks.cs), [run-material-command-parity.py](../scripts/run-material-command-parity.py), [M01_MATERIAL_COMMANDS.md](M01_MATERIAL_COMMANDS.md), [ProjectMaterialIdentities.cs](../portable/PrePoMax.Persistence/ProjectMaterialIdentities.cs), [StaticProjectArchive.cs](../portable/PrePoMax.Persistence/StaticProjectArchive.cs), [AnalysisArchive.cs](../portable/PrePoMax.Persistence/AnalysisArchive.cs), [ProjectMaterialIdentityChecks.cs](../portable/tests/MeshIO.Characterization/ProjectMaterialIdentityChecks.cs), [run-material-identity-parity.py](../scripts/run-material-identity-parity.py), [ProjectMaterialLifecycleChecks.cs](../portable/tests/MeshIO.Characterization/ProjectMaterialLifecycleChecks.cs), [run-material-lifecycle-parity.py](../scripts/run-material-lifecycle-parity.py), [reference_material_lifecycle.py](../tests/parity/reference_material_lifecycle.py).

Evidence: [REPORT.md](evidence/M01-material-2026-11-06/REPORT.md), [summary.json](evidence/M01-material-2026-11-06/summary.json), [REPORT.md](evidence/M01-identity-2026-11-09/REPORT.md), [summary.json](evidence/M01-identity-2026-11-09/summary.json), [REPORT.md](evidence/M01-lifecycle-2026-11-09/REPORT.md), [summary.json](evidence/M01-lifecycle-2026-11-09/summary.json).

## M02 — Durable owned workspaces and artifact lifecycle

Milestone prerequisites: M01. Status: **planned**.

### M02-T01 — Durable revisions, restart and artifact recovery

Status: **planned**. Policy: `exact-domain`. Reference mode: `web_only`.

References: [test-web-workspace.py](../scripts/test-web-workspace.py), [ProjectServiceChecks.cs](../portable/tests/MeshIO.Characterization/ProjectServiceChecks.cs).

Fixtures: Two saved projects with revisions and referenced source/result files; interrupted publication.

1. Restart and reconnect restore the last committed revision, tree selection and valid artifact references.
2. Crash before/after atomic revision publication exposes either the old complete revision or the new complete revision, never a mixture.
3. Download/reopen produces the same semantic model and result association; garbage collection never deletes a referenced revision/artifact.

Durable multi-user service behavior has no direct desktop-equivalent test; desktop project preservation remains the semantic reference.

Evidence: none recorded.

### M02-T02 — Ownership and every artifact/resource access boundary

Status: **planned**. Policy: `exact-domain`. Reference mode: `web_only`.

References: [test-web-workspace.py](../scripts/test-web-workspace.py).

Fixtures: Two authenticated owners, two projects per owner, anonymous and expired sessions.

1. Cross-owner enumeration/read/write/download/history/scene/resource/job/diagnostic requests fail without revealing protected project content.
2. Stale revisions, CSRF, wrong-origin requests and expired credentials are rejected without mutation.
3. Renderer host credentials and private filesystem paths are absent from browser payloads, URLs and error bodies.

This is an online release requirement, not a claim that desktop Prepomac has multi-user ownership.

Evidence: none recorded.

### M02-T03 — Admission, cancellation and cleanup

Status: **planned**. Policy: `exact-domain`. Reference mode: `web_only`.

References: [test-web-workspace.py](../scripts/test-web-workspace.py), [ProjectServiceChecks.cs](../portable/tests/MeshIO.Characterization/ProjectServiceChecks.cs).

Fixtures: Oversize/expanded uploads, abandoned sessions, full workspace pool, cancelled parser/mesh/renderer tasks.

1. Configured compressed/expanded/model/process/disk quotas are enforced before committing a revision; retention limits cannot discard the current project.
2. Cancellation/timeout reports its actual outcome, terminates owned work and preserves the last committed project.
3. Reconnect and retry are idempotent or use explicit new operation IDs; leaked processes/files and cross-project result caches are detected.

Use small deterministic resource-limit fixtures; large-model qualification is a separate case.

Evidence: none recorded.

## M03 — Model tree, viewport selection and interaction

Milestone prerequisites: M01. Status: **planned**.

### M03-T01 — Model-tree identity and object actions

Status: **planned**. Policy: `exact-domain`. Reference mode: `desktop`.

References: [Test_Model_Keyword_Tree_Coverage.py](../../../../APIs/Python/Test_Scripts/Test_Model_Keyword_Tree_Coverage.py), [Test_Model_Parts.py](../../../../APIs/Python/Test_Scripts/Test_Model_Parts.py), [ProjectModelTreeChecks.cs](../portable/tests/MeshIO.Characterization/ProjectModelTreeChecks.cs).

Fixtures: Populated Model tree with empty collections, identical names in different steps and references.

1. Match required branch labels/order/parentage and stable object identity across refresh and rename.
2. Create/edit/duplicate/rename/remove/activate/show/hide actions attach to the correct object/collection; unavailable actions are explicit.
3. Selection and expansion survive edits, refresh, undo and reopen without selecting the wrong step-local object.

Evidence: none recorded.

### M03-T02 — Source-ID picking and selection algebra

Status: **planned**. Policy: `exact-domain`. Reference mode: `desktop`.

References: [Test_Selection_Correctness.py](../../../../APIs/Python/Test_Scripts/Test_Selection_Correctness.py), [Test_Selection_Pick.py](../../../../APIs/Python/Test_Scripts/Test_Selection_Pick.py), [Test_Api_Topology.py](../../../../APIs/Python/Test_Scripts/Test_Api_Topology.py).

Fixtures: Sparse/large source IDs, coincident faces, hidden parts, transformed/large-origin model and a known cube.

1. Node/element/edge/face/part/angle and rectangle selection resolves the same authoritative ID membership as Prepomac.
2. Add/subtract/intersect/invert/all/clear/undo and selection precision follow the reference semantics; hiding a part does not rewrite an already stored selection.
3. Reject stale topology or client-supplied invalid memberships; preview limits never silently truncate saved region membership.

Evidence: none recorded.

### M03-T03 — Navigation and graphics interaction

Status: **planned**. Policy: `geometry-visual`. Reference mode: `desktop`.

References: [verify-navigation.py](../../../../macOS/verify-navigation.py).

Fixtures: Known camera poses; pointer release outside viewport; repeated/mixed mouse chords and view reset.

1. Match the supported Prepomac navigation modes and release/capture behavior using actual browser pointer events.
2. Camera orientation, projection, recenter/fit and selection coexist; page scrolling or context menus do not leave a stuck drag.
3. Use geometric/camera assertions plus screenshots; report any browser/platform input adaptation explicitly.

Evidence: none recorded.

## M04 — Mesh import/append, regions and mesh maintenance

Milestone prerequisites: M03. Status: **planned**.

### M04-T01 — Mesh import and append with reference remapping

Status: **planned**. Policy: `exact-domain`. Reference mode: `desktop`.

References: [Test_Model_Document.py](../../../../APIs/Python/Test_Scripts/Test_Model_Document.py), [Test_Model_Parts.py](../../../../APIs/Python/Test_Scripts/Test_Model_Parts.py), [Test_Model_Mesh_Tools.py](../../../../APIs/Python/Test_Scripts/Test_Model_Mesh_Tools.py), [Test_Model_Export_Sources.py](../../../../APIs/Python/Test_Scripts/Test_Model_Export_Sources.py), [ProjectMeshImportChecks.cs](../portable/tests/MeshIO.Characterization/ProjectMeshImportChecks.cs).

Fixtures: Mesh fixtures with sparse IDs, same-name parts/sets, duplicate node/element IDs and linear/quadratic element types.

1. Import the same supported source definitions; full deck import must not be mislabeled as mesh-only import.
2. Append preserves existing model definitions and remaps every affected ID/name/reference explicitly; coincident nodes are not silently welded.
3. Failed append leaves the original project and original export unchanged; save/reopen preserves combined connectivity and assignments.

Evidence: none recorded.

### M04-T02 — Node/element set lifecycle

Status: **planned**. Policy: `exact-domain`. Reference mode: `desktop`.

References: [Test_Model_Node_Sets.py](../../../../APIs/Python/Test_Scripts/Test_Model_Node_Sets.py), [Test_Model_Regions.py](../../../../APIs/Python/Test_Scripts/Test_Model_Regions.py), [Test_Model_Region_Coverage.py](../../../../APIs/Python/Test_Scripts/Test_Model_Region_Coverage.py).

Fixtures: Overlapping sets, part-derived sets, picked/ID/range selections and dependent sections/loads.

1. Match exact membership and create/replace/rename/duplicate/remove outcomes including namespace collisions.
2. Renames update typed references; duplicates are independent; removal handles dependents as the reference does.
3. Unrelated edits, undo/redo, save/reopen and exported region references remain equivalent.

Evidence: none recorded.

### M04-T03 — Surfaces, reference points and coordinate systems

Status: **planned**. Policy: `exact-domain`. Reference mode: `desktop`.

References: [Test_Model_Regions.py](../../../../APIs/Python/Test_Scripts/Test_Model_Regions.py), [Test_Model_Region_Coverage.py](../../../../APIs/Python/Test_Scripts/Test_Model_Region_Coverage.py), [Test_Api_Topology.py](../../../../APIs/Python/Test_Scripts/Test_Api_Topology.py).

Fixtures: Element/node surfaces, internal backing regions, coordinates and reference-point-dependent definitions.

1. Preserve element-face labels, normals, area and node membership; distinguish node and element surfaces and validate compatible consumers.
2. Create/edit/rename/duplicate/remove maintains backing-set ownership, local axes and dependent definitions.
3. Picking plus serialization/export matches the reference; invalid or stale source faces cannot create partial regions.

Evidence: none recorded.

### M04-T04 — Mesh transforms, quality and command-backed recovery

Status: **planned**. Policy: `exact-domain`. Reference mode: `desktop`.

References: [Test_Model_Mesh_Tools.py](../../../../APIs/Python/Test_Scripts/Test_Model_Mesh_Tools.py), [Test_Model_Inverted_Elements.py](../../../../APIs/Python/Test_Scripts/Test_Model_Inverted_Elements.py), [Test_Custom_Feature_API.py](../../../../APIs/Python/Test_Scripts/Test_Custom_Feature_API.py), [verify-custom-features.py](../../../../macOS/verify-custom-features.py).

Fixtures: Inverted/degenerate elements; diagnostic sets; transformed copied parts and shared regions.

1. Identify the same bad elements and retain diagnostic identity.
2. Element deletion updates parts, sets, surfaces and dependent validity; undo/redo restores/reapplies exactly.
3. Transforms/copies retain intended connectivity and region relationships; source parts remain unchanged when copy is requested.

Evidence: none recorded.

## M05 — Complete static-solid model authoring

Milestone prerequisites: M04. Status: **planned**.

### M05-T01 — Static materials, solid sections and assignment precedence

Status: **planned**. Policy: `exact-domain`. Reference mode: `desktop`.

References: [Test_Model_Materials_Sections.py](../../../../APIs/Python/Test_Scripts/Test_Model_Materials_Sections.py), [Test_Model_Definition_Coverage.py](../../../../APIs/Python/Test_Scripts/Test_Model_Definition_Coverage.py), [Test_Model_Readback_Equations.py](../../../../APIs/Python/Test_Scripts/Test_Model_Readback_Equations.py).

Fixtures: Elastic/density/plastic models, material references, overlapping solid section regions and orphan elements.

1. Match material values/expressions, table ordering and solid-section properties.
2. Exercise create/replace/rename/duplicate/remove/activation and reference/coverage behavior; preserve unspecified values.
3. Compare equivalent active section coverage and CalculiX material/section keyword blocks; errors do not mutate the model.

Evidence: none recorded.

### M05-T02 — Static steps, mechanical supports/loads and propagation

Status: **planned**. Policy: `exact-domain`. Reference mode: `desktop`.

References: [Test_Model_Steps.py](../../../../APIs/Python/Test_Scripts/Test_Model_Steps.py), [Test_Model_BCs.py](../../../../APIs/Python/Test_Scripts/Test_Model_BCs.py), [Test_Model_Loads.py](../../../../APIs/Python/Test_Scripts/Test_Model_Loads.py), [Test_Model_Readback_Equations.py](../../../../APIs/Python/Test_Scripts/Test_Model_Readback_Equations.py).

Fixtures: Static subset of the reference scripts; fixed/translational supports, forces/traction/pressure/gravity/centrifugal and two steps.

1. Match static controls, equation readback, step order, copied/propagated definitions and independent later edits.
2. Verify per-node force versus total surface force, signs, axes, frames and units; export the same physical loading.
3. Exercise invalid type/region/value and removal/activation cases; do not count non-static script branches as covered here.

Evidence: none recorded.

### M05-T03 — Reference-dependent constraints and contact authoring

Status: **planned**. Policy: `exact-domain`. Reference mode: `desktop`.

References: [Test_Model_Constraints.py](../../../../APIs/Python/Test_Scripts/Test_Model_Constraints.py), [Test_Model_Interactions.py](../../../../APIs/Python/Test_Scripts/Test_Model_Interactions.py).

Fixtures: Ties, rigid bodies, reference points, ordinary springs, master/slave surfaces and contact interactions.

1. Create/edit/duplicate/rename/remove supported static constraints/contact types using the same compatible region/reference-point forms.
2. Master/slave identity, tolerance, adjustment, orientation, interaction options and dependent history references survive reopen/export.
3. Invalid/overlapping/missing references are refused with no partial commands; pressure-dependent springs remain M11.

Evidence: none recorded.

### M05-T04 — Outputs, parameters, keywords and equations

Status: **planned**. Policy: `exact-domain`. Reference mode: `desktop`.

References: [Test_Model_Outputs.py](../../../../APIs/Python/Test_Scripts/Test_Model_Outputs.py), [Test_Model_Document.py](../../../../APIs/Python/Test_Scripts/Test_Model_Document.py), [Test_Model_Readback_Equations.py](../../../../APIs/Python/Test_Scripts/Test_Model_Readback_Equations.py), [Test_Model_Keyword_Tree_Coverage.py](../../../../APIs/Python/Test_Scripts/Test_Model_Keyword_Tree_Coverage.py).

Fixtures: Nodal/element/contact fields; history output regions; parameters and positional user keywords.

1. Match static output variables/flags, region scoping and propagation order.
2. Preserve original expressions instead of substituting evaluated values on edit/reopen.
3. Retain keyword insertion order and references; unsupported remapping is explicitly refused, never silently dropped.

Evidence: none recorded.

## M06 — External-solver handoff and core post-processing

Milestone prerequisites: M05. Status: **planned**.

### M06-T01 — Export handoff and external analytic solve

Status: **planned**. Policy: `native-numerical`. Reference mode: `desktop`.

References: [Test_Analysis.py](../../../../APIs/Python/Test_Scripts/Test_Analysis.py), [Test_Job_Correctness.py](../../../../APIs/Python/Test_Scripts/Test_Job_Correctness.py), [test-native-calculix.py](../scripts/test-native-calculix.py).

Fixtures: Same authored desktop/web cube plus analytic cube E=1000, nu=0 and total x force=1.

1. Compare normalized solver input while preserving physical keyword order and values.
2. Run both inputs externally with the same recorded solver/build/settings; retain inputs, stdout/stderr, FRD/DAT and exit conditions.
3. For the analytic cube U1=0.001 on the loaded face; preserve source tolerance rel=1e-5, abs=1e-10 and near-zero transverse components.
4. Compare the Test_Analysis model against its own reference outputs; do not substitute the different analytic-cube setup.

Evidence: none recorded.

### M06-T02 — FRD/DAT import, revision association and standalone result opening

Status: **planned**. Policy: `exact-domain`. Reference mode: `desktop`.

References: [Test_Result_Coverage.py](../../../../APIs/Python/Test_Scripts/Test_Result_Coverage.py), [Test_Analysis.py](../../../../APIs/Python/Test_Scripts/Test_Analysis.py), [Test_Model_Export_Sources.py](../../../../APIs/Python/Test_Scripts/Test_Model_Export_Sources.py), [test-web-workspace.py](../scripts/test-web-workspace.py).

Fixtures: Matching/mismatching FRD; DAT from a known run; changed-load same-mesh project; standalone result and embedded PMX results.

1. Match supported result names, fields/components, frames, times, units, IDs and standalone-result behavior.
2. Reject mismatching topology/units where known; keep claimed export-revision association separate from mesh compatibility.
3. A raw FRD with identical mesh does not prove material/load provenance: show that limitation unless the external handoff receipt is verified.
4. Result replacement and unrelated edits follow explicit invalidation rules; failed import preserves current model/result state.

Evidence: none recorded.

### M06-T03 — Field values, deformation and rendering baseline

Status: **planned**. Policy: `field-numerical-visual`. Reference mode: `desktop`.

References: [Test_Api_View_Results.py](../../../../APIs/Python/Test_Scripts/Test_Api_View_Results.py), [Test_Result_Coverage.py](../../../../APIs/Python/Test_Scripts/Test_Result_Coverage.py), [ProjectResultChecks.cs](../portable/tests/MeshIO.Characterization/ProjectResultChecks.cs).

Fixtures: Fixed immutable cube/two-step FRD, undeformed and positive/negative/zero displacement scales.

1. Compare all field values by node/element and component IDs, units, frame identity, extrema and missing-value mask.
2. Same artifact decoding uses exact equality; analytic/derived expectations retain their source-specific tolerances.
3. Verify deformed coordinates x+scale*U, scalar values independent of scale, zero-scale restoration and visible scalar legend/range.

Evidence: none recorded.

### M06-T04 — Raw histories, plots and first hosted release workflow

Status: **planned**. Policy: `field-numerical-visual`. Reference mode: `desktop`.

References: [Test_Result_History_Outputs.py](../../../../APIs/Python/Test_Scripts/Test_Result_History_Outputs.py), [ProjectHistoryResultChecks.cs](../portable/tests/MeshIO.Characterization/ProjectHistoryResultChecks.cs), [test-web-workspace.py](../scripts/test-web-workspace.py).

Fixtures: Raw nodal/contact histories and aligned columns; a full authored/imported mesh model and external solve.

Additional completion prerequisites: M02.

1. Match the raw-history subset: set/field/component names, timestamps, values, units, alignment, plotting data and delimited export.
2. Complete login/open/import/select/author/export/external solve/upload/inspect/save/restart/reopen without desktop-only steps.
3. Derived history expressions/transforms are tracked under M09; pass M02 ownership/recovery before calling this an online release.

Evidence: none recorded.

## M07 — Geometry and meshing parity

Milestone prerequisites: M04. Status: **planned**.

### M07-T01 — Geometry import, metadata, transforms and assembly exports

Status: **planned**. Policy: `geometry-visual`. Reference mode: `desktop`.

References: [Test_Model_Geometry_Metadata.py](../../../../APIs/Python/Test_Scripts/Test_Model_Geometry_Metadata.py), [Test_Model_Geometry_Transform.py](../../../../APIs/Python/Test_Scripts/Test_Model_Geometry_Transform.py), [Test_Model_Export_Geometry_Assembly.py](../../../../APIs/Python/Test_Scripts/Test_Model_Export_Geometry_Assembly.py), [Test_Model_Export_Sources.py](../../../../APIs/Python/Test_Scripts/Test_Model_Export_Sources.py).

Fixtures: Actual CAD/STL fixtures declared in reference headers; multi-part assemblies and units.

1. Match part identity, dimensions/topology/measurements and supplemental metadata across supported import/export formats.
2. Move/rotate/mirror/scale preserves expected IDs/names and copied/moved source semantics.
3. Check joined/per-part assembly exports by semantic geometry, not raw file byte order.

Evidence: none recorded.

### M07-T02 — Geometry editing, splitting and repair

Status: **planned**. Policy: `geometry-visual`. Reference mode: `desktop`.

References: [Test_Model_Split_Geometry.py](../../../../APIs/Python/Test_Scripts/Test_Model_Split_Geometry.py), [Test_Model_Split_Periodic_Faces.py](../../../../APIs/Python/Test_Scripts/Test_Model_Split_Periodic_Faces.py), [Test_Model_Split_Cells_Compound.py](../../../../APIs/Python/Test_Scripts/Test_Model_Split_Cells_Compound.py), [Test_Model_Split_Cells_Fuse.py](../../../../APIs/Python/Test_Scripts/Test_Model_Split_Cells_Fuse.py), [Test_Model_Split_Cells_Group.py](../../../../APIs/Python/Test_Scripts/Test_Model_Split_Cells_Group.py), [Test_Model_Group_Cells_Plain_Compound.py](../../../../APIs/Python/Test_Scripts/Test_Model_Group_Cells_Plain_Compound.py), [Test_Model_Offset_Face.py](../../../../APIs/Python/Test_Scripts/Test_Model_Offset_Face.py), [Test_Model_Delete_Cad_Faces.py](../../../../APIs/Python/Test_Scripts/Test_Model_Delete_Cad_Faces.py), [Test_Model_Defeature.py](../../../../APIs/Python/Test_Scripts/Test_Model_Defeature.py), [Test_Model_Fuse_Faces_Repair.py](../../../../APIs/Python/Test_Scripts/Test_Model_Fuse_Faces_Repair.py).

Fixtures: Exact BREP split/periodic/compound/hole/offset fixtures declared in the scripts.

1. Match geometry operations, selected topology, result part/solid/shell classifications, measurements and metadata.
2. Preserve semantic selection binding or fail stale topology; do not assert CAD repair succeeded when the reference only reports a warning.
3. Cancel/failure preserves source geometry; changed native kernel versions require explicit baseline review.

Evidence: none recorded.

### M07-T03 — Mesher settings, shells and native job lifecycle

Status: **planned**. Policy: `meshing-semantic`. Reference mode: `desktop`.

References: [Test_Model_Meshing.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing.py), [Test_Model_Meshing_Coverage.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing_Coverage.py), [Test_Model_Meshing_Validity.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing_Validity.py), [Test_Model_Meshing_Mmg.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing_Mmg.py), [Test_Model_Meshing_Shell_Types.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing_Shell_Types.py).

Fixtures: Reference geometry/STL fixtures and each supported mesher with recorded versions.

1. Match create/replace validation and retained mesh settings; preserve unspecified region/options and selected element families.
2. Compare documented topology/quality/coverage invariants, boundary mapping and exported engineering response; use exact connectivity only when deterministic.
3. Cancel/timeout/failure cleans owned native processes and preserves previous mesh; unavailable tools are blocked, not passed.

Evidence: none recorded.

### M07-T04 — Sweep, revolve, transfinite and mixed-cell mapping

Status: **planned**. Policy: `meshing-semantic`. Reference mode: `desktop`.

References: [Test_Model_Meshing_Revolve.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing_Revolve.py), [Test_Model_Meshing_Split_Cells.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing_Split_Cells.py), [Test_Model_Meshing_Sweep_Compound.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing_Sweep_Compound.py), [Test_Model_Meshing_Sweep_Transfinite.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing_Sweep_Transfinite.py), [Test_Model_Meshing_Three_Block_Mapping.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing_Three_Block_Mapping.py), [Test_Model_Meshing_Transfinite_Frame.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing_Transfinite_Frame.py), [Test_Model_Meshing_Transfinite_Prism.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing_Transfinite_Prism.py), [Test_Model_Meshing_Netgen_Transfinite.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing_Netgen_Transfinite.py), [Test_Model_Meshing_QuadWild_Transfinite.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing_QuadWild_Transfinite.py), [Test_Workflow_Geometry_To_Result.py](../../../../APIs/Python/Test_Scripts/Test_Workflow_Geometry_To_Result.py).

Fixtures: Sweep/revolve/prism/frame/compound BREP fixtures; reference cube.stl workflow.

1. Retain the exact test invariants for shared boundaries, structured sides/divisions, cell groups, element types and no orphan/inverted elements.
2. Compare per-mesher expected semantic outputs and numerical response; do not demand identical numbering across different mesher builds.
3. Complete geometry-to-model/export/external solve/results/save/reopen and preserve geometry metadata.

Evidence: none recorded.

## M08 — Broader definitions, analysis types and solver settings

Milestone prerequisites: M05, M06, M02. Status: **planned**.

### M08-T01 — Remaining material/section types and model definitions

Status: **planned**. Policy: `exact-domain`. Reference mode: `desktop`.

References: [Test_Model_Materials_Sections.py](../../../../APIs/Python/Test_Scripts/Test_Model_Materials_Sections.py), [Test_Model_Fields.py](../../../../APIs/Python/Test_Scripts/Test_Model_Fields.py), [Test_Model_Definition_Coverage.py](../../../../APIs/Python/Test_Scripts/Test_Model_Definition_Coverage.py), [Test_Model_Readback_Equations.py](../../../../APIs/Python/Test_Scripts/Test_Model_Readback_Equations.py).

Fixtures: All non-composite source branches: temperature tables, shells/membranes/masses, fields, amplitudes/distributions and initial conditions.

1. Complete each source-supported subtype/property rather than treating the static-solid subset as full script parity.
2. Preserve units, table order, equations, coordinate systems and reference-dependent semantics.
3. Compare model snapshots and keyword output before/after replace/rename/reopen; run reference numerical fixtures where defined.

Evidence: none recorded.

### M08-T02 — Non-static steps, thermal and remaining loads/supports

Status: **planned**. Policy: `native-numerical`. Reference mode: `desktop`.

References: [Test_Model_Steps.py](../../../../APIs/Python/Test_Scripts/Test_Model_Steps.py), [Test_Model_BCs.py](../../../../APIs/Python/Test_Scripts/Test_Model_BCs.py), [Test_Model_Loads.py](../../../../APIs/Python/Test_Scripts/Test_Model_Loads.py), [Test_Model_Outputs.py](../../../../APIs/Python/Test_Scripts/Test_Model_Outputs.py), [Test_Journal_Mesh_Setup.py](../../../../APIs/Python/Test_Scripts/Test_Journal_Mesh_Setup.py).

Fixtures: Each source-defined step/control/load/support/defined-field branch and thermal examples.

1. Cover the step and definition types enumerated by the current scripts, including incompatible-type refusals and propagation.
2. Match physical/mechanical/thermal units and expressions, activation/order and request compatibility.
3. Require a numerical fixture for each claimed analysis family before marking family parity; missing reference fixtures are tracked gaps.

Evidence: none recorded.

### M08-T03 — Analysis settings and additional solver handoffs

Status: **planned**. Policy: `native-numerical`. Reference mode: `desktop`.

References: [Test_Job_Correctness.py](../../../../APIs/Python/Test_Scripts/Test_Job_Correctness.py), [Test_Analysis.py](../../../../APIs/Python/Test_Scripts/Test_Analysis.py), [verify-calculix.py](../../../../macOS/verify-calculix.py), [verify-calculix.sh](../../../../macOS/verify-calculix.sh), [Dockerfile](../../../../macOS/Native/CodeAster/Dockerfile), [build.sh](../../../../macOS/Native/CodeAster/build.sh).

Fixtures: Recorded CalculiX variants and source-supported Code_Aster solid/plain-shell cases; composite extension in M10..

1. Match job settings, replacement/inherit rules, solver selection and external input/result handoff without adding server solving to R1.
2. For any later execution adapter, verify start/status/cancel/timeout/log/artifact behavior independently.
3. Code_Aster numerical comparison needs explicit fixtures; the Dockerfile is source evidence, not an existing parity test.
4. An unavailable solver/backend remains unqualified; preserve the documented supported subset.

The Code_Aster test gap must be closed with a new differential fixture before claiming parity for that handoff.

Evidence: none recorded.

### M08-T04 — Later controlled solver-job execution

Status: **planned**. Policy: `native-numerical`. Reference mode: `mixed`.

References: [Test_Job_Correctness.py](../../../../APIs/Python/Test_Scripts/Test_Job_Correctness.py), [Test_Analysis.py](../../../../APIs/Python/Test_Scripts/Test_Analysis.py), [verify-calculix.sh](../../../../macOS/verify-calculix.sh).

Fixtures: Two owners and isolated job directories; successful, failed, timed-out and cancelled native solves.

Additional completion prerequisites: M02.

1. Preserve Prepomac job settings, start/wait/status/progress/output/open-results and cancellation semantics through an owned service operation.
2. No browser-provided arbitrary executable/path is run; configured solver capabilities, inputs and resource limits govern execution.
3. A failed/zero-exit-missing-output/cancelled job cannot publish valid results or mutate the source revision; retain diagnostic/artifact associations.
4. External solving remains the R1 path; hosted solving is accepted separately after ownership, quotas and durable job recovery.

This is post-R1 work. A reference backend without qualified hardware/fixtures remains unqualified, not passed.

Evidence: none recorded.

## M09 — Advanced results, histories and presentation

Milestone prerequisites: M06. Status: **planned**.

### M09-T01 — Derived fields, complex results, filters and transforms

Status: **planned**. Policy: `field-numerical-visual`. Reference mode: `desktop`.

References: [Test_Result_Field_Outputs.py](../../../../APIs/Python/Test_Scripts/Test_Result_Field_Outputs.py), [Test_Result_Regions_Coverage.py](../../../../APIs/Python/Test_Scripts/Test_Result_Regions_Coverage.py), [Test_Result_Coverage.py](../../../../APIs/Python/Test_Scripts/Test_Result_Coverage.py).

Fixtures: Fixed real/complex result artifacts; equations, limits, envelopes, filters and coordinate transforms.

1. Match formula/filter options, outputs, valid/invalid states, all values and units; retain missing/NaN masks.
2. Compare transforms against identical local axes/reference points and preserve physical tensor/vector meaning.
3. Verify clone/rename/delete/export/persistence and source result immutability.

Evidence: none recorded.

### M09-T02 — Derived histories and history command recording

Status: **planned**. Policy: `field-numerical-visual`. Reference mode: `desktop`.

References: [Test_Result_History_Outputs.py](../../../../APIs/Python/Test_Scripts/Test_Result_History_Outputs.py), [Test_Record_And_Run_History_Output.py](../../../../APIs/Python/Test_Scripts/Test_Record_And_Run_History_Output.py).

Fixtures: From-field/equation/integration/merged/extracted/XY/table histories with filters and mismatched time axes.

1. Match each source algorithm, time alignment, integration location, units, ordering and CSV output.
2. Generated plot data and derived-history dependencies survive edit/reopen; graph display does not replace numeric assertions.
3. Recorded history operations replay with the same input/result associations.

Evidence: none recorded.

### M09-T03 — Result lifecycle, view modes, annotations and animation

Status: **planned**. Policy: `geometry-visual`. Reference mode: `desktop`.

References: [Test_Api_View_Results.py](../../../../APIs/Python/Test_Scripts/Test_Api_View_Results.py), [Test_Api_View_Exploded.py](../../../../APIs/Python/Test_Scripts/Test_Api_View_Exploded.py), [Test_Result_Coverage.py](../../../../APIs/Python/Test_Scripts/Test_Result_Coverage.py), [Test_Api_Topology.py](../../../../APIs/Python/Test_Scripts/Test_Api_Topology.py).

Fixtures: Multiple results/frames, exploded/section views, known queries and annotations.

1. Switch result/current frame/view without corrupting model context or selection IDs; compare supported presentation settings.
2. Probes/extrema/annotations report physical values and units at the correct source IDs, independently of visual scale.
3. Verify animation frame sequence/timing state, clipping/explode/transforms, capture and reconnect/context-loss recovery.

Evidence: none recorded.

## M10 — Composites and material orientation workflows

Milestone prerequisites: M08, M09. Status: **planned**.

### M10-T01 — Composite sections, parameters and orientations

Status: **planned**. Policy: `exact-domain`. Reference mode: `desktop`.

References: [Test_Model_Composite_Sections.py](../../../../APIs/Python/Test_Scripts/Test_Model_Composite_Sections.py), [Test_Model_Composite_Parameters.py](../../../../APIs/Python/Test_Scripts/Test_Model_Composite_Parameters.py), [Test_Model_Material_Orientations.py](../../../../APIs/Python/Test_Scripts/Test_Model_Material_Orientations.py).

Fixtures: composite_shells.inp: flat/inclined/cylindrical parts, symmetric and parameter-driven laminates.

1. Match layer material/thickness/angle/order/symmetry, orientation axes and parameter regeneration.
2. Compare exported ply/orientation keywords and element grouping against the reference; preserve definitions across unrelated edits and reopen.
3. Unsupported values and invalid orientation/region combinations fail before recording.

Evidence: none recorded.

### M10-T02 — Ply stresses, shell transforms and failure criteria

Status: **planned**. Policy: `composite-source-tolerances`. Reference mode: `desktop`.

References: [Test_Result_Composite.py](../../../../APIs/Python/Test_Scripts/Test_Result_Composite.py).

Fixtures: The exact [0/90]s laminate and strength constants from Test_Result_Composite.py.

1. Keep layer-specific result sets, component units and material-axis values.
2. Reuse source stress bounds: S11 error <=1% of expected+0.05 and S22 error <=3%+0.05; keep the source principal-stress checks.
3. Reuse source MaxStress/TsaiWu/Hashin formulas and index tolerance 1e-4, TsaiWu reserve-factor relative bound 1e-3; preserve mode, NaN and missing-direction cases.
4. Compare identical saved result artifacts first, then independently solved matched inputs.

Evidence: none recorded.

### M10-T03 — GUI-only composite mapping/modeler operations

Status: **planned**. Policy: `geometry-visual`. Reference mode: `desktop`.

References: [CaeModel.csproj](../../../../macOS/CaeModel/CaeModel.csproj), [CompositeLayup.cs](../../../../CaeModel/Composite/CompositeLayup.cs), [DiscreteOrientationsXmlReader.cs](../../../../CaeModel/Composite/DiscreteOrientationsXmlReader.cs), [Audit_API_Coverage.py](../../../../APIs/Python/Test_Scripts/Audit_API_Coverage.py).

Fixtures: Minimal layer/mapping/rosette/drape/discrete-orientation fixtures created during baseline capture.

Additional completion prerequisites: M07.

1. Enumerate actual GUI-only commands omitted/refused by the Python surface and capture reference snapshots through a read-only desktop extractor.
2. Match the implemented CAD/mesh orientation/mapping operations and persisted layer/material assignments.
3. Do not count Python API coverage as coverage of the whole composite modeler; missing fixtures/extractors remain open.

These are planned differential tests where a complete public API oracle is absent; source-file existence is not passing evidence.

Evidence: none recorded.

### M10-T04 — Composite Code_Aster handoff and numerical comparison

Status: **planned**. Policy: `native-numerical`. Reference mode: `mixed`.

References: [Dockerfile](../../../../macOS/Native/CodeAster/Dockerfile), [build.sh](../../../../macOS/Native/CodeAster/build.sh), [Test_Result_Composite.py](../../../../APIs/Python/Test_Scripts/Test_Result_Composite.py).

Fixtures: Source-supported true layered-shell model and qualified Code_Aster environment; matching CalculiX ply model for a physically comparable case.

1. Compare supported layered-shell definitions, material axes, solver handoff and FRD displacement/stress/failure-index/reserve-factor/critical-ply fields with desktop Code_Aster output.
2. Use same-backend same-input comparison first; define an analytic/laminate expected value and its tolerance before comparing different element formulations.
3. Create the missing Code_Aster oracle fixture; a Docker image build or a successful FRD conversion is not numerical evidence.

This is post-R1 work. A reference backend without qualified hardware/fixtures remains unqualified, not passed.

Evidence: none recorded.

## M11 — Prepomac custom mechanical and result tools

Milestone prerequisites: M04, M05, M06. Status: **planned**.

### M11-T01 — Cylinder lifecycle and SRSuntour mesh generation

Status: **planned**. Policy: `native-numerical`. Reference mode: `desktop`.

References: [verify-cylinders.py](../../../../macOS/verify-cylinders.py), [verify-srsuntour.py](../../../../macOS/verify-srsuntour.py), [verify-chamfer-model.py](../../../../macOS/verify-chamfer-model.py), [Test_Custom_Feature_API.py](../../../../APIs/Python/Test_Scripts/Test_Custom_Feature_API.py).

Fixtures: Generated linear/quadratic hex/wedge meshes, rotated/reversed axes, radial layers and a separately consented real-model copy.

Additional completion prerequisites: M07.

1. Match cylinder JSON/properties and lifecycle; extrusion/sweep/chamfer topology, dimensions, part IDs and source-bound mappings.
2. Repeat/update preserves customized ties/contacts/sections, creates no duplicate mappings/orphans and leaves unrelated cylinders unchanged.
3. Retain source geometry tolerances and native solve checks; sweep/CAD branches also require M07.

Real private models are supplemental fixtures; no checked-in private file or workstation-specific solver path is required for the small deterministic suite.

Evidence: none recorded.

### M11-T02 — Pressure-dependent springs

Status: **planned**. Policy: `native-numerical`. Reference mode: `desktop`.

References: [verify-pressure-springs.py](../../../../macOS/verify-pressure-springs.py).

Fixtures: Source CPRESS cases including first-step/last-increment, master/slave side, zero/negative/large pressures and dense/sparse pairing.

1. Match projected node pairing and the four pressure laws; generated SPRING2 degrees/connectivity and sum of stiffnesses match.
2. Retain source stiffness weight tolerance rel=1e-8, abs=1e-8; the native fixture gives U3=0.5 and 1/6 for K=200 and 600.
3. Missing source FRD/invalid edit/export preserves permanent sets/model; referenced artifact, PMX, undo and journal survive relocation.

Evidence: none recorded.

### M11-T03 — Individual stress colors and local extrema

Status: **planned**. Policy: `field-numerical-visual`. Reference mode: `desktop`.

References: [verify-individual-colors.py](../../../../macOS/verify-individual-colors.py), [verify-custom-features.py](../../../../macOS/verify-custom-features.py).

Fixtures: Shared-node/shared-part multi-material stress result, per-component ranges, plateaus and hidden parts.

1. Match individual range assignment/conflict rejection, unassigned gray and 0–100% legend while physical values remain unchanged.
2. Persist modes/ranges across components/increments/reopen; verify actual pixels on a recorded physical GPU before visual qualification.
3. Local extrema match connected-threshold regions, lowest-ID plateau tie-break, tolerance, count limit and visible-part filtering.

Evidence: none recorded.

### M11-T04 — Standalone spring/result transformations and FRD export

Status: **planned**. Policy: `field-numerical-visual`. Reference mode: `desktop`.

References: [verify-file-tools.py](../../../../macOS/verify-file-tools.py), [verify-result-springs.py](../../../../macOS/verify-result-springs.py).

Fixtures: Temporary INP spring fixtures and FRD continuation/missing-component/multiple-step cases from the verification sources.

1. Match generated spring pairing/stiffness and result transformations; preserve unrelated keywords/records and units.
2. Validate missing/truncated/incompatible inputs and export source/step/component options without corrupting originals.
3. Exported FRD reopens in both hosts with matching physical values, connectivity and frame names.

Evidence: none recorded.

### M11-T05 — Contact virtual friction and automatic pressure weighting

Status: **planned**. Policy: `native-numerical`. Reference mode: `desktop`.

References: [verify-virtual-friction.py](../../../../macOS/verify-virtual-friction.py), [verify-automatic-virtual-friction.py](../../../../macOS/verify-automatic-virtual-friction.py), [VirtualFrictionSettings.cs](../../../../CaeModel/Contact/VirtualFrictionSettings.cs), [VirtualFrictionExportContext.cs](../../../../CaeModel/Contact/VirtualFrictionExportContext.cs), [VirtualFrictionPairing.cs](../../../../CaeModel/Contact/VirtualFrictionPairing.cs), [CalculixFileWriter.VirtualFriction.cs](../../../../CaeModel/FileInOut/Output/Calculix/CalculixFileWriter.VirtualFriction.cs), [CaeModel.csproj](../../../../macOS/CaeModel/CaeModel.csproj).

Fixtures: Temporary contact/mesh fixtures generated by verify-virtual-friction.py; explicitly supplied interference PMX and qualified solver for the automatic two-pass case.

1. Preserve all virtual-friction settings across save/reopen and rename/reference operations or explicitly refuse unsupported content before publication.
2. Match the deterministic pairing, stiffness conventions, direction handling and solver results checked by the original scripts.
3. Automatic pressure weighting and asynchronous two-pass execution match the original validator; no private PMX is read implicitly.

Added to the current inventory on 2026-11-09 after these reference features appeared since M00. The dated M00 baseline and current material-identity slice do not qualify them.

Evidence: none recorded.

## M12 — Fatigue workspace and calculation handoff

Milestone prerequisites: M09. Status: **planned**.

### M12-T01 — Fatigue setup, load histories and portable package

Status: **planned**. Policy: `exact-domain`. Reference mode: `desktop`.

References: [Test_Custom_Feature_API.py](../../../../APIs/Python/Test_Scripts/Test_Custom_Feature_API.py), [verify-custom-features.py](../../../../macOS/verify-custom-features.py).

Fixtures: Source load-file formats and generated histories, result-step associations, SN settings and moved package.

1. Match copy-on-edit/cancel behavior, load generation/import/repetitions and persisted setup.
2. Reject wrong-mesh/missing-stress inputs; keep original stress-result association across repeated fatigue runs.
3. Package handoff JSON/manifest/INP/FRDs/loads/runner survives relocation and includes all required managed artifacts.

Evidence: none recorded.

### M12-T02 — Fatigue numerical CPU behavior

Status: **planned**. Policy: `fatigue-source-tolerances`. Reference mode: `desktop`.

References: [verify-fatigue-backend.py](../../../../macOS/verify-fatigue-backend.py).

Fixtures: Exact NumPy fatigue fixtures: independent load cases, fast/general path, critical planes, batch-size and input refusals.

1. Reuse all source unittest expectations, including accumulated damage 15e-6, cycle count 15 and life 1e6 in the repeated-case fixture.
2. Keep fast/general comparison rtol=1e-9, atol=1e-10 and source per-test tighter/default bounds; compare every result field, not damage alone.
3. Match physical-plane damage accumulation before choosing the governing plane and validate scaling/batch independence.

Evidence: none recorded.

### M12-T03 — Fatigue execution handoff and conditional backends

Status: **planned**. Policy: `native-numerical`. Reference mode: `desktop`.

References: [verify-custom-features.py](../../../../macOS/verify-custom-features.py), [verify-fatigue-backend.py](../../../../macOS/verify-fatigue-backend.py).

Fixtures: Portable CPU package, CUDA-unavailable environment, controlled cancellation and separately available CUDA/MATLAB/Runtime backends.

1. CPU completion/progress/cancel/result import matches reference semantics; CUDA absence reports the same CPU fallback explicitly.
2. For a backend without reference numerical qualification, retain unqualified status until both desktop and web-handoff numerical fixtures run on the required environment.
3. Never equate an installed launcher or successful package export with CUDA/MATLAB/FATLAB numerical parity.

First release external solving is unchanged; remote fatigue execution, if chosen, requires the M02 controlled operation boundary.

Evidence: none recorded.

## M13 — Python/API, journal and automation parity

Milestone prerequisites: M01. Status: **planned**.

### M13-T01 — Public Python/API validation and readback

Status: **planned**. Policy: `exact-domain`. Reference mode: `desktop`.

References: [Test_Api_Enum_Vocabulary.py](../../../../APIs/Python/Test_Scripts/Test_Api_Enum_Vocabulary.py), [Test_Api_Keyword_Arguments.py](../../../../APIs/Python/Test_Scripts/Test_Api_Keyword_Arguments.py), [Test_Api_Session_Rules.py](../../../../APIs/Python/Test_Scripts/Test_Api_Session_Rules.py), [Audit_API_Documentation.py](../../../../APIs/Python/Test_Scripts/Audit_API_Documentation.py), [Audit_API_Coverage.py](../../../../APIs/Python/Test_Scripts/Audit_API_Coverage.py).

Fixtures: Every API signature/enum case in the sources; invalid names/types/numbers/options and replacement defaults.

Additional completion prerequisites: M08, M09, M10, M11, M12.

1. Match vocabulary, expressions, return-shape/unit conventions and refusal behavior; preserve unspecified properties.
2. Unknown keywords/types/invalid numeric values cannot mutate or record commands.
3. Run the source API unchanged when the hosted Python adapter is available; any temporary semantic HTTP adapter is explicitly recorded rather than called unchanged-script parity.

Evidence: none recorded.

### M13-T02 — Recorded scripts, command journals and regeneration

Status: **planned**. Policy: `exact-domain`. Reference mode: `desktop`.

References: [Test_Rpc_Live_Session.py](../../../../APIs/Python/Test_Scripts/Test_Rpc_Live_Session.py), [Test_Journal_Mesh_Setup.py](../../../../APIs/Python/Test_Scripts/Test_Journal_Mesh_Setup.py), [Test_Record_And_Run_History_Output.py](../../../../APIs/Python/Test_Scripts/Test_Record_And_Run_History_Output.py), [Test_Batch_Run.py](../../../../APIs/Python/Test_Scripts/Test_Batch_Run.py), [Test_Prompt_Refused.py](../../../../APIs/Python/Test_Scripts/Test_Prompt_Refused.py).

Fixtures: Successful and partial-failure scripts, import/generated-output dependencies, undo branches, meshing and derived histories.

Additional completion prerequisites: M07, M09.

1. Match command suppression/record-and-run, current-history selection and independent replay; compare resulting model/export and result definitions.
2. Preserve each declared expected exit/report condition, including batch errors 3/4/7 and strict-prompt refusal 5.
3. Replay paths use managed artifact/work directories; existing unrelated files are not overwritten, and recorded scripts keep their documented scope/import rules.

Evidence: none recorded.

### M13-T03 — Automation transports and hosted execution permissions

Status: **planned**. Policy: `exact-domain`. Reference mode: `mixed`.

References: [Test_Rpc_Server.py](../../../../APIs/Python/Test_Scripts/Test_Rpc_Server.py), [Test_Rpc_Live_Session.py](../../../../APIs/Python/Test_Scripts/Test_Rpc_Live_Session.py), [Test_Mcp_Http.py](../../../../APIs/Python/Test_Scripts/Test_Mcp_Http.py), [Test_Mcp_Workflow.py](../../../../APIs/Python/Test_Scripts/Test_Mcp_Workflow.py).

Fixtures: Source RPC/MCP client workflows and read-only/refused/expired access, plus two hosted project owners.

Additional completion prerequisites: M06, M02.

1. Keep operation capability/read-only/discard/conflict semantics even when desktop pipes are replaced by hosted transport.
2. Agent/UI operations use the same domain commands and journal identity; audit endpoint/protocol error and handshake behavior.
3. Python/native execution runs only inside the defined owned artifact/resource boundary; an arbitrary project upload cannot execute server code.
4. Mark native job branches planned until an explicit later execution adapter is available; the R1 external-solver handoff remains valid.

Transport-specific desktop assertions need explicit adapters; preserve operation semantics rather than reproducing a named pipe in a browser.

Evidence: none recorded.

## M14 — Integrated Prepomac parity and production qualification

Milestone prerequisites: M02, M07, M08, M09, M10, M11, M12, M13. Status: **planned**.

### M14-T01 — Whole-project cross-host regression and restore

Status: **planned**. Policy: `native-numerical`. Reference mode: `desktop`.

References: [Test_Workflow_Geometry_To_Result.py](../../../../APIs/Python/Test_Scripts/Test_Workflow_Geometry_To_Result.py), [Test_Analysis.py](../../../../APIs/Python/Test_Scripts/Test_Analysis.py), [verify-pmx-compatibility.py](../../../../macOS/verify-pmx-compatibility.py), [verify-custom-features.py](../../../../macOS/verify-custom-features.py).

Fixtures: Versioned golden projects spanning solids, shells, composites, contact/springs, custom tools and fatigue; a representative consented user model.

1. Run full matched desktop/web author-save-reopen-export-external-solve-post-process cycles; compare model snapshots, artifacts, normalized input and numerical outputs.
2. Restore backups/revisions across service upgrade/restart and recheck old/project-protection fixtures.
3. No milestone is complete merely because isolated unit tests passed; close its cross-host and browser evidence.

Evidence: none recorded.

### M14-T02 — Browser/GPU/large-model qualification

Status: **planned**. Policy: `hardware-qualified`. Reference mode: `desktop`.

References: [VTKJS_PENDING_WORK.md](VTKJS_PENDING_WORK.md), [VTKJS_STREAMING_RENDERER_SPEC.md](../VTKJS_STREAMING_RENDERER_SPEC.md), [benchmark-report.md](../prototypes/VtkJsStreaming/docs/benchmark-report.md), [verify-navigation.py](../../../../macOS/verify-navigation.py), [verify-individual-colors.py](../../../../macOS/verify-individual-colors.py), [verify-dialog-layout.py](../../../../macOS/verify-dialog-layout.py).

Fixtures: Existing 3M renderer benchmark, representative full workflow projects, physical-GPU/browser/platform matrix and software WebGL functional run.

1. Retain existing renderer thresholds, protocol limits and three-cold-run physical-GPU requirements; report viewport/DPR, hardware, toolchain and raw samples.
2. Measure browser, server and native-process memory separately, plus load/pick/update times and cleanup; do not add unlike metrics or infer GPU performance from software rendering.
3. Keyboard/focus/pointer/touch/narrow-screen and context-loss/reconnect checks preserve supported task completion; qualify each promised browser/platform explicitly.

 Reference inventory extension 2026-11-09: verify-dialog-layout.py adds editor resizing and control-reachability intent. Browser adaptation compares usable form content and responsive layout; native WinForms anchors are reference implementation details. This validator has not been run in the browser.

Evidence: none recorded.

### M14-T03 — Final capability accounting and release decision

Status: **planned**. Policy: `exact-domain`. Reference mode: `desktop`.

References: [PREPOMAC_WEB_PARITY.md](PREPOMAC_WEB_PARITY.md), [Run_Tests.py](../../../../APIs/Python/Test_Scripts/Run_Tests.py).

Fixtures: Complete source/test/feature inventory, all evidence manifests, known reference failures and remaining hardware/backend gaps.

1. Every baseline capability/declared test run maps to an implemented tested workflow or an explicit unresolved gap.
2. No required planned, failed, blocked or unqualified case is counted as passed or hidden by aggregate test counts.
3. Freeze release/source/fixture identities and supported scope; changes to Prepomac or tests trigger targeted requalification.

Evidence: none recorded.

## Declared Prepomac desktop runs

Collected using the existing runner's header parser. Keep each invocation's expected exit and report checks from the registry; successful handling of an expected error is not a failing test. Generated `PyTest_Job.frd` cases depend on `Test_Analysis.py`. No run below was executed as part of writing this roadmap.

| Declared run | Source fixture | Expected exit | Reference outcome | Matched web coverage |
| --- | --- | ---: | --- | --- |
| [Test_Analysis.py](../../../../APIs/Python/Test_Scripts/Test_Analysis.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 0 | passed | uncovered |
| [Test_Api_Enum_Vocabulary.py](../../../../APIs/Python/Test_Scripts/Test_Api_Enum_Vocabulary.py) | No initial model | 0 | failed | uncovered |
| [Test_Api_Keyword_Arguments.py](../../../../APIs/Python/Test_Scripts/Test_Api_Keyword_Arguments.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 0 | passed | uncovered |
| [Test_Api_Session_Rules.py](../../../../APIs/Python/Test_Scripts/Test_Api_Session_Rules.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 0 | passed | uncovered |
| [Test_Api_Topology.py](../../../../APIs/Python/Test_Scripts/Test_Api_Topology.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 0 | passed | uncovered |
| [Test_Api_View_Exploded.py](../../../../APIs/Python/Test_Scripts/Test_Api_View_Exploded.py) | [cube.stl](../../../../APIs/Python/Test_Scripts/Models/cube.stl) | 0 | passed | uncovered |
| [Test_Batch_Run.py [1]](../../../../APIs/Python/Test_Scripts/Test_Batch_Run.py) | No initial model | 0 | failed | uncovered |
| [Test_Batch_Run.py [2]](../../../../APIs/Python/Test_Scripts/Test_Batch_Run.py) | No initial model | 3 | passed | uncovered |
| [Test_Batch_Run.py [3]](../../../../APIs/Python/Test_Scripts/Test_Batch_Run.py) | No initial model | 4 | passed | uncovered |
| [Test_Batch_Run.py [4]](../../../../APIs/Python/Test_Scripts/Test_Batch_Run.py) | No initial model | 7 | passed | uncovered |
| [Test_Custom_Feature_API.py](../../../../APIs/Python/Test_Scripts/Test_Custom_Feature_API.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 0 | passed | uncovered |
| [Test_Job_Correctness.py](../../../../APIs/Python/Test_Scripts/Test_Job_Correctness.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 0 | failed | uncovered |
| [Test_Journal_Mesh_Setup.py](../../../../APIs/Python/Test_Scripts/Test_Journal_Mesh_Setup.py) | No initial model | 0 | passed | uncovered |
| [Test_Mcp_Http.py](../../../../APIs/Python/Test_Scripts/Test_Mcp_Http.py) | No initial model | 0 | failed | uncovered |
| [Test_Mcp_Workflow.py](../../../../APIs/Python/Test_Scripts/Test_Mcp_Workflow.py) | No initial model | 0 | failed | uncovered |
| [Test_Model_BCs.py](../../../../APIs/Python/Test_Scripts/Test_Model_BCs.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 0 | passed | uncovered |
| [Test_Model_Composite_Parameters.py](../../../../APIs/Python/Test_Scripts/Test_Model_Composite_Parameters.py) | No initial model | 0 | passed | uncovered |
| [Test_Model_Composite_Sections.py](../../../../APIs/Python/Test_Scripts/Test_Model_Composite_Sections.py) | [composite_shells.inp](../../../../APIs/Python/Test_Scripts/Models/composite_shells.inp) | 0 | passed | uncovered |
| [Test_Model_Constraints.py](../../../../APIs/Python/Test_Scripts/Test_Model_Constraints.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 0 | passed | uncovered |
| [Test_Model_Defeature.py](../../../../APIs/Python/Test_Scripts/Test_Model_Defeature.py) | [blind_holes.brep](../../../../APIs/Python/Test_Scripts/Models/blind_holes.brep) | 0 | passed | uncovered |
| [Test_Model_Definition_Coverage.py](../../../../APIs/Python/Test_Scripts/Test_Model_Definition_Coverage.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 0 | passed | uncovered |
| [Test_Model_Delete_Cad_Faces.py](../../../../APIs/Python/Test_Scripts/Test_Model_Delete_Cad_Faces.py) | [blind_holes.brep](../../../../APIs/Python/Test_Scripts/Models/blind_holes.brep) | 0 | passed | uncovered |
| [Test_Model_Document.py](../../../../APIs/Python/Test_Scripts/Test_Model_Document.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 0 | passed | uncovered |
| [Test_Model_Export_Geometry_Assembly.py](../../../../APIs/Python/Test_Scripts/Test_Model_Export_Geometry_Assembly.py) | [box_and_cylinder.brep](../../../../APIs/Python/Test_Scripts/Models/box_and_cylinder.brep) | 0 | passed | uncovered |
| [Test_Model_Export_Sources.py](../../../../APIs/Python/Test_Scripts/Test_Model_Export_Sources.py) | [cube.stl](../../../../APIs/Python/Test_Scripts/Models/cube.stl) | 0 | passed | uncovered |
| [Test_Model_Fields.py](../../../../APIs/Python/Test_Scripts/Test_Model_Fields.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 0 | passed | uncovered |
| [Test_Model_Fuse_Faces_Repair.py](../../../../APIs/Python/Test_Scripts/Test_Model_Fuse_Faces_Repair.py) | [blind_holes.brep](../../../../APIs/Python/Test_Scripts/Models/blind_holes.brep) | 0 | passed | uncovered |
| [Test_Model_Geometry_Metadata.py](../../../../APIs/Python/Test_Scripts/Test_Model_Geometry_Metadata.py) | [L_bracket_r.step](../../../../APIs/Python/Test_Scripts/Models/L_bracket_r.step) | 0 | passed | uncovered |
| [Test_Model_Geometry_Transform.py](../../../../APIs/Python/Test_Scripts/Test_Model_Geometry_Transform.py) | [box_and_cylinder.brep](../../../../APIs/Python/Test_Scripts/Models/box_and_cylinder.brep) | 0 | passed | uncovered |
| [Test_Model_Group_Cells_Plain_Compound.py](../../../../APIs/Python/Test_Scripts/Test_Model_Group_Cells_Plain_Compound.py) | [sweep_side2.brep](../../../../APIs/Python/Test_Scripts/Models/sweep_side2.brep) | 0 | passed | uncovered |
| [Test_Model_Interactions.py](../../../../APIs/Python/Test_Scripts/Test_Model_Interactions.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 0 | passed | uncovered |
| [Test_Model_Inverted_Elements.py](../../../../APIs/Python/Test_Scripts/Test_Model_Inverted_Elements.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 0 | passed | uncovered |
| [Test_Model_Keyword_Tree_Coverage.py](../../../../APIs/Python/Test_Scripts/Test_Model_Keyword_Tree_Coverage.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 0 | passed | uncovered |
| [Test_Model_Loads.py](../../../../APIs/Python/Test_Scripts/Test_Model_Loads.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 0 | passed | uncovered |
| [Test_Model_Material_Orientations.py](../../../../APIs/Python/Test_Scripts/Test_Model_Material_Orientations.py) | [composite_shells.inp](../../../../APIs/Python/Test_Scripts/Models/composite_shells.inp) | 0 | passed | uncovered |
| [Test_Model_Materials_Sections.py](../../../../APIs/Python/Test_Scripts/Test_Model_Materials_Sections.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 0 | passed | uncovered |
| [Test_Model_Mesh_Tools.py](../../../../APIs/Python/Test_Scripts/Test_Model_Mesh_Tools.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 0 | passed | uncovered |
| [Test_Model_Meshing.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing.py) | [cube.stl](../../../../APIs/Python/Test_Scripts/Models/cube.stl) | 0 | failed | uncovered |
| [Test_Model_Meshing_Coverage.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing_Coverage.py) | [box_and_cylinder.brep](../../../../APIs/Python/Test_Scripts/Models/box_and_cylinder.brep) | 0 | passed | uncovered |
| [Test_Model_Meshing_Mmg.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing_Mmg.py) | [open_shell.stl](../../../../APIs/Python/Test_Scripts/Models/open_shell.stl) | 0 | passed | uncovered |
| [Test_Model_Meshing_Netgen_Transfinite.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing_Netgen_Transfinite.py) | [box_and_cylinder.brep](../../../../APIs/Python/Test_Scripts/Models/box_and_cylinder.brep) | 0 | failed | uncovered |
| [Test_Model_Meshing_QuadWild_Transfinite.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing_QuadWild_Transfinite.py) | [box_and_cylinder.brep](../../../../APIs/Python/Test_Scripts/Models/box_and_cylinder.brep) | 0 | passed | uncovered |
| [Test_Model_Meshing_Revolve.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing_Revolve.py) | [box_and_cylinder.brep](../../../../APIs/Python/Test_Scripts/Models/box_and_cylinder.brep) | 0 | failed | uncovered |
| [Test_Model_Meshing_Shell_Types.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing_Shell_Types.py) | [box_and_cylinder.brep](../../../../APIs/Python/Test_Scripts/Models/box_and_cylinder.brep) | 0 | failed | uncovered |
| [Test_Model_Meshing_Split_Cells.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing_Split_Cells.py) | [sweep_side2.brep](../../../../APIs/Python/Test_Scripts/Models/sweep_side2.brep) | 0 | failed | uncovered |
| [Test_Model_Meshing_Sweep_Compound.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing_Sweep_Compound.py) | [cube.stl](../../../../APIs/Python/Test_Scripts/Models/cube.stl) | 0 | failed | uncovered |
| [Test_Model_Meshing_Sweep_Transfinite.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing_Sweep_Transfinite.py) | [box_and_cylinder.brep](../../../../APIs/Python/Test_Scripts/Models/box_and_cylinder.brep) | 0 | failed | uncovered |
| [Test_Model_Meshing_Three_Block_Mapping.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing_Three_Block_Mapping.py) | No initial model | 0 | passed | uncovered |
| [Test_Model_Meshing_Transfinite_Frame.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing_Transfinite_Frame.py) | [transfinite_frame.brep](../../../../APIs/Python/Test_Scripts/Models/transfinite_frame.brep) | 0 | failed | uncovered |
| [Test_Model_Meshing_Transfinite_Prism.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing_Transfinite_Prism.py) | [box_and_cylinder.brep](../../../../APIs/Python/Test_Scripts/Models/box_and_cylinder.brep) | 0 | failed | uncovered |
| [Test_Model_Meshing_Validity.py](../../../../APIs/Python/Test_Scripts/Test_Model_Meshing_Validity.py) | [open_shell.stl](../../../../APIs/Python/Test_Scripts/Models/open_shell.stl) | 0 | passed | uncovered |
| [Test_Model_Node_Sets.py](../../../../APIs/Python/Test_Scripts/Test_Model_Node_Sets.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 0 | passed | uncovered |
| [Test_Model_Offset_Face.py](../../../../APIs/Python/Test_Scripts/Test_Model_Offset_Face.py) | [blind_holes.brep](../../../../APIs/Python/Test_Scripts/Models/blind_holes.brep) | 0 | failed | uncovered |
| [Test_Model_Outputs.py](../../../../APIs/Python/Test_Scripts/Test_Model_Outputs.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 0 | passed | uncovered |
| [Test_Model_Parts.py](../../../../APIs/Python/Test_Scripts/Test_Model_Parts.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 0 | passed | uncovered |
| [Test_Model_Readback_Equations.py](../../../../APIs/Python/Test_Scripts/Test_Model_Readback_Equations.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 0 | passed | uncovered |
| [Test_Model_Region_Coverage.py](../../../../APIs/Python/Test_Scripts/Test_Model_Region_Coverage.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 0 | passed | uncovered |
| [Test_Model_Regions.py](../../../../APIs/Python/Test_Scripts/Test_Model_Regions.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 0 | passed | uncovered |
| [Test_Model_Split_Cells_Compound.py](../../../../APIs/Python/Test_Scripts/Test_Model_Split_Cells_Compound.py) | [box_and_cylinder.brep](../../../../APIs/Python/Test_Scripts/Models/box_and_cylinder.brep) | 0 | passed | uncovered |
| [Test_Model_Split_Cells_Fuse.py](../../../../APIs/Python/Test_Scripts/Test_Model_Split_Cells_Fuse.py) | [sweep_side2.brep](../../../../APIs/Python/Test_Scripts/Models/sweep_side2.brep) | 0 | failed | uncovered |
| [Test_Model_Split_Cells_Group.py](../../../../APIs/Python/Test_Scripts/Test_Model_Split_Cells_Group.py) | [sweep_side2.brep](../../../../APIs/Python/Test_Scripts/Models/sweep_side2.brep) | 0 | passed | uncovered |
| [Test_Model_Split_Geometry.py](../../../../APIs/Python/Test_Scripts/Test_Model_Split_Geometry.py) | [blind_holes.brep](../../../../APIs/Python/Test_Scripts/Models/blind_holes.brep) | 0 | passed | uncovered |
| [Test_Model_Split_Periodic_Faces.py](../../../../APIs/Python/Test_Scripts/Test_Model_Split_Periodic_Faces.py) | [blind_holes.brep](../../../../APIs/Python/Test_Scripts/Models/blind_holes.brep) | 0 | passed | uncovered |
| [Test_Model_Steps.py](../../../../APIs/Python/Test_Scripts/Test_Model_Steps.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 0 | passed | uncovered |
| [Test_Prompt_Refused.py](../../../../APIs/Python/Test_Scripts/Test_Prompt_Refused.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 5 | passed | uncovered |
| [Test_Result_Composite.py](../../../../APIs/Python/Test_Scripts/Test_Result_Composite.py) | [composite_shells.inp](../../../../APIs/Python/Test_Scripts/Models/composite_shells.inp) | 0 | passed | uncovered |
| [Test_Rpc_Live_Session.py](../../../../APIs/Python/Test_Scripts/Test_Rpc_Live_Session.py) | No initial model | 0 | failed | uncovered |
| [Test_Rpc_Server.py](../../../../APIs/Python/Test_Scripts/Test_Rpc_Server.py) | No initial model | 0 | failed | uncovered |
| [Test_Selection_Correctness.py](../../../../APIs/Python/Test_Scripts/Test_Selection_Correctness.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 0 | passed | uncovered |
| [Test_Selection_Pick.py](../../../../APIs/Python/Test_Scripts/Test_Selection_Pick.py) | [cube.inp](../../../../APIs/Python/Test_Scripts/Models/cube.inp) | 0 | passed | uncovered |
| [Test_Workflow_Geometry_To_Result.py](../../../../APIs/Python/Test_Scripts/Test_Workflow_Geometry_To_Result.py) | [cube.stl](../../../../APIs/Python/Test_Scripts/Models/cube.stl) | 0 | failed | uncovered |
| [Test_Api_View_Results.py](../../../../APIs/Python/Test_Scripts/Test_Api_View_Results.py) | Generated by Test_Analysis.py | 0 | failed | uncovered |
| [Test_Record_And_Run_History_Output.py](../../../../APIs/Python/Test_Scripts/Test_Record_And_Run_History_Output.py) | Generated by Test_Analysis.py | 0 | passed | uncovered |
| [Test_Result_Coverage.py](../../../../APIs/Python/Test_Scripts/Test_Result_Coverage.py) | Generated by Test_Analysis.py | 0 | passed | uncovered |
| [Test_Result_Field_Outputs.py](../../../../APIs/Python/Test_Scripts/Test_Result_Field_Outputs.py) | Generated by Test_Analysis.py | 0 | passed | uncovered |
| [Test_Result_History_Outputs.py](../../../../APIs/Python/Test_Scripts/Test_Result_History_Outputs.py) | Generated by Test_Analysis.py | 0 | passed | uncovered |
| [Test_Result_Regions_Coverage.py](../../../../APIs/Python/Test_Scripts/Test_Result_Regions_Coverage.py) | Generated by Test_Analysis.py | 0 | passed | uncovered |

## Offline and custom reference checks

Reference execution is tracked separately from matched web-case evidence. An offline numerical check and its application/GPU integration path are distinct qualifications.

| Check | Kind | Outcome | Invocation notes |
| --- | --- | --- | --- |
| [Audit_API_Coverage.py](../../../../APIs/Python/Test_Scripts/Audit_API_Coverage.py) | offline | failed | Use Run_Tests.py --filter Audit_API_Coverage; preserve its expected outcome. |
| [Audit_API_Documentation.py](../../../../APIs/Python/Test_Scripts/Audit_API_Documentation.py) | offline | passed | Use Run_Tests.py --filter Audit_API_Documentation; preserve its expected outcome. |
| [test_runner_offline.py](../../../../APIs/Python/Test_Scripts/test_runner_offline.py) | offline | passed | Use Run_Tests.py --filter test_runner_offline; preserve its expected outcome. |
| [verify-calculix.py](../../../../macOS/verify-calculix.py) | custom | passed | This is an embedded-Prepomac child script. Invoke through bash macOS/verify-calculix.sh <configured-ccx> Release, not standalone Python. |
| [verify-chamfer-model.py](../../../../macOS/verify-chamfer-model.py) | custom | blocked | Use the source-defined generated checks and a consented copy for the real-model regression; never overwrite the source PMX. |
| [verify-custom-features.py](../../../../macOS/verify-custom-features.py) | custom | passed | Use the invocation and environment setup in this verifier; preserve numerical and application assertions. |
| [verify-cylinders.py](../../../../macOS/verify-cylinders.py) | custom | passed | Use the invocation and environment setup in this verifier; preserve numerical and application assertions. |
| [verify-fatigue-backend.py](../../../../macOS/verify-fatigue-backend.py) | custom | passed | Use the invocation and environment setup in this verifier; preserve numerical and application assertions. |
| [verify-file-tools.py](../../../../macOS/verify-file-tools.py) | custom | passed | Run the offline unittest part and the --integration application path; one does not establish the other. |
| [verify-individual-colors.py](../../../../macOS/verify-individual-colors.py) | custom | passed | Run application checks plus the --gpu path on recorded supported physical hardware; software WebGL is only functional evidence. |
| [verify-navigation.py](../../../../macOS/verify-navigation.py) | custom | passed | Use the invocation and environment setup in this verifier; preserve numerical and application assertions. |
| [verify-pmx-compatibility.py](../../../../macOS/verify-pmx-compatibility.py) | custom | failed | Use the invocation and environment setup in this verifier; preserve numerical and application assertions. |
| [verify-pressure-springs.py](../../../../macOS/verify-pressure-springs.py) | custom | passed | Use the invocation and environment setup in this verifier; preserve numerical and application assertions. |
| [verify-result-springs.py](../../../../macOS/verify-result-springs.py) | custom | passed | Use the invocation and environment setup in this verifier; preserve numerical and application assertions. |
| [verify-srsuntour.py](../../../../macOS/verify-srsuntour.py) | custom | passed | Use the invocation and environment setup in this verifier; preserve numerical and application assertions. |
| [verify-virtual-friction.py](../../../../macOS/verify-virtual-friction.py) | custom | not_run | Pending baseline extension: run the original validator with a qualified ccx in a disposable directory; this material slice did not execute it. |
| [verify-automatic-virtual-friction.py](../../../../macOS/verify-automatic-virtual-friction.py) | custom | not_run | Pending baseline extension: requires an explicitly supplied interference PMX fixture and qualified solver. No default/private PMX was accessed for this slice. |
| [verify-dialog-layout.py](../../../../macOS/verify-dialog-layout.py) | custom | not_run | Pending baseline extension: run the original resize validator; adapt control reachability and resize intent for browser qualification. |

The JSON registry also records every declared switch/report condition and source SHA-256. A source hash identifies a reference input, not a production build, numerical qualification or pass.

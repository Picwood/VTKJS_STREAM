# Legacy PMX compatibility investigation

Inspected: 2026-10-08.

The user-supplied `BRAKE_537037-V2-complete-FRANKNRX.pmx` was inspected
read-only on its external volume. It has not been copied into the repository,
converted or opened through legacy object deserialization.

| Observation | Value |
|---|---|
| File size | 659,606,221 bytes |
| 32-byte ASCII header | `PrePoMax v2.5.1 dev`, NUL padded |
| Following little-endian model-block length | 115,196,967 bytes |
| Bytes after the model block | 544,409,218 bytes |

A bounded raw-DEFLATE prefix inspection (512 output bytes) finds assembly
`PrePoMax, Version=2.5.1.0` and `PrePoMax.Controller`, with model, results,
annotations, fatigue, Baram mesh and command fields. These are observations
of the prefix, not a validation of the complete model or results payload.
The remaining bytes occupy the results area according to the legacy reader. The
new bounded `--results` mode consumes and validates that complete area as described
below.

`PrePoMax/Controller.cs`, `TryReadCompressedPmxAfter_2_0_6`, reads the
compressed model block, restores an object array through
`Tools.LoadDumpFromFile`, obtains the model and results collection from the
controller, then reads custom model and results binary sections. The existing
portable `.pma` archive does not implement this contract.

## Read-only inventory tool

```sh
dotnet build portable/PrePoMax.PmxInspect -c Release
dotnet portable/PrePoMax.PmxInspect/bin/Release/net10.0/PrePoMax.PmxInspect.dll \
  /path/to/original.pmx > /path/to/inventory.json
dotnet portable/PrePoMax.PmxInspect/bin/Release/net10.0/PrePoMax.PmxInspect.dll \
  /path/to/original.pmx --convert /path/to/new-project.pma
dotnet portable/PrePoMax.PmxInspect/bin/Release/net10.0/PrePoMax.PmxInspect.dll \
  /path/to/original.pmx --results > /path/to/results-inventory.json
python3 scripts/test-pmx-inspect.py
```

The standalone tool uses Microsoft's
[System.Formats.Nrbf decoder](https://learn.microsoft.com/en-us/dotnet/api/system.formats.nrbf.nrbfdecoder)
to decode records without instantiating the serialized application types.
It reports type names, instance counts and member names, plus current model
collection membership, active flags and nonlinear-geometry flags. Optional modes
inspect the binary mesh and results or convert the supported analysis-model slice.
It has no dependency on the original desktop application assembly and does not
enable BinaryFormatter.

It accepts the unprotected compressed layout from version 2.0.6 onward,
limits compressed model input to 256 MiB and the serialized section to
128 MiB, and reads only that section from the decompressor. Those are input
bounds, not a fixed process memory limit. It rejects invalid lengths,
truncated records and trailing bytes in the serialized section. By default the custom mesh binary section is not read. Optional `--mesh`
inspection consumes it through decompressed EOF. Optional `--results` matches
tail names to the serialized `ResultsCollection`, bounds lengths and counts,
validates every compressed block, decodes each `FieldData`, `Field` and history
entry through the production version-aware readers, and requires exact tail EOF.
The result mesh block is decompressed and integrity-checked without materializing
a second large mesh.
Protected and older layouts require separate work.

The supplied fixture successfully decodes **27,116,757 serialized bytes**
into **11,674 records across 250 class types**, rooted in an object array.
Relevant record counts include:

| Serialized type | Instances |
|---|---:|
| StaticStep | 9 |
| Material / Elastic / Plastic | 12 / 12 / 8 |
| ContactPair / SurfaceInteraction | 42 / 6 |
| RigidBody / Tie / SurfaceSpring | 5 / 23 / 17 |
| DisplacementRotation / CLoad | 53 / 15 |
| SolidSection | 18 |

These are counts across the serialized graph, which can include historical
command objects and copies. They are **not counts of active model definitions**.
They establish that a lossless conversion cannot be assumed from the current
bounded elastic/static portable format.

Validation: the tool builds without warnings and passes 15 synthetic cases,
covering successful inventory, unchanged input, header/version rejection,
compressed/serialized bounds, truncation, invalid NRBF and trailing bytes.
The external fixture also completes successfully. These initial 15 cases were
kept as a separate inspector suite and were later expanded as described below.

## Current model inventory

The inspector now follows only the root array's first controller and its
`_model` reference. It reads selected ordered dictionaries through their
serialized key/value pairs, and step/material lists through their logical
size rather than spare array capacity. It never follows `_commands`.
Collections include inactive definitions, with `_active` reported separately;
"current" is collection membership, not a claim that every item is enabled.
Unknown collection layouts fail instead of producing guessed counts. Arrays
and logical collection sizes are capped at 100,000 entries.

The supplied fixture's current collections contain:

| Collection | Current definitions |
|---|---|
| Materials | 6; all have elastic properties, 4 also have plasticity |
| Other material properties | 5 density; 1 each thermal expansion, conductivity and specific heat |
| Sections | 4 solid sections |
| Constraints | 7 ties, 6 surface springs, 2 rigid bodies |
| Contact | 12 contact pairs, 2 surface interactions |
| Amplitudes | 1 tabular amplitude |
| Initial conditions | 0 |
| Steps | 3 static steps, all active and with nonlinear geometry enabled |
| Step loads / boundaries | In saved order: 0/3, 2/3, 2/3 |

These counts supersede whole-graph counts for the listed current collections.
They do not establish solver applicability, region validity or conversion
eligibility. Mesh topology, results, additional model collections, step
controls, outputs and detailed numerical parameters still require inspection.

The synthetic current-model fixture deliberately includes an extra material
under command history, an inactive current material and unused list capacity.
Checks prove those distinctions survive inspection, and reject negative or
oversized list lengths. All 15 inspector cases pass, and the real PMX also
completes. The original file is opened read-only.

## Mesh binary inspection

Add `--mesh` after the project filename to inspect the two binary mesh
sections after the serialized records. The reader follows the local legacy
`FeModel`, `FeMesh` and `VisualizationData` layouts, including part names
from version 2.4.4 onward. It validates finite coordinates, unique positive
node/element IDs, supported VTK connectivity widths, connectivity references
and available current node/element-set labels (including part-based sets).
Visualization arrays are consumed structurally, without reconstructing them.
Unexpected trailing decompressed model data is rejected.

The supplied project passes with:

| Mesh observation | Count |
|---|---:|
| Nodes | 4,592,568 |
| Elements | 960,847 |
| Quadratic tetrahedra (VTK 24) | 760,356 |
| Quadratic hexahedra (VTK 25) | 178,176 |
| Quadratic wedges (VTK 26) | 22,315 |
| Visualization parts / distinct element part IDs | 14 / 14 |
| Node and element sets checked | 292 |

The geometry binary section is present but empty. VTK topology does not
identify every solver formulation; part metadata still needs mapping. This
project exceeds both the linear-element coverage and the 100,000-record
limit of the current INP transfer path.

Each binary count is limited to 10 million entries; aggregate declared
counts are limited to 100 million. Part-name byte lengths are capped at
4096. These are parsing bounds, not a fixed memory limit. Optional mesh
inspection stores node/element IDs but does not instantiate legacy objects
or retain coordinates/connectivity. Surface links, analysis-region links,
part membership consistency, element quality and results remain unchecked.

Validation: zero-warning build, 15 record cases and 13 binary-mesh cases
pass. Mesh cases cover both version branches, node/element duplicates,
nonfinite coordinates, bad connectivity, count bounds, truncation, trailing
data and visualization consumption. The external project also passes all
implemented mesh and set-reference checks without modification.

## Solver settings and named-region links

The default inventory now includes `regionMetadata`. It resolves current
collection dictionary keys, without exporting project names, and checks:

- Surface node-set and element-face element-set names.
- Section part/element-set references and material names.
- Step load and boundary-condition named regions: parts, node sets,
  element sets, surfaces and reference points.

Other region kinds are reported as unchecked, rather than assumed valid.
Missing targets produce explicit diagnostics in the JSON report. These are
inspection findings, so they do not change the process exit status; malformed
serialization still fails the command. Membership is checked with ordinal
name comparison and includes inactive definitions.

The supplied project has **46 current surfaces** and passes **171 named-link
checks** without missing-target or unchecked-region diagnostics in this scope.
Its 14 part settings resolve to:

| Solver formulation | Part IDs |
|---|---|
| C3D10 | 1, 2 |
| C3D20R | 5, 6, 7, 8, 9 |
| C3D15 | 3, 4, 10, 11, 12, 13, 14 |

The quadratic solid enum mapping follows `CaeMesh/Elements/FeElementType.cs`;
unknown enum values remain explicitly `Unknown:<value>`. These are settings
from current part metadata, not a check of each element's part assignment.
Other element families are outside this mapping. Name existence does not
validate surface face numbers, region contents, reference-point construction,
solver applicability or contact/constraint links.

Validation: **19 record/metadata cases and 13 mesh cases pass**, with a
zero-warning build. New synthetic metadata cases cover valid references,
missing targets, unchecked selection regions and unknown formulations.
The external fixture also completes without modification.

## Contact, constraint and element-part checks

Named-link inspection now includes contact master/slave regions and surface
interaction names; tie and rigid-body master/slave regions; and surface-spring
master regions. Surface springs have no required slave endpoint. Unknown
constraint types are explicitly reported as unchecked. The supplied fixture
passes **231 named-link checks**, including its **12 contact pairs and 15
constraints**, without missing-target or unchecked-type diagnostics.

With `--mesh`, each element's part ID must exist in the current part metadata.
For quadratic tetrahedra, hexahedra and wedges, the corresponding part setting
must select a known nonzero formulation. All **960,847 elements** in the
fixture pass this check, with zero unchecked formulations. Other topology
families are counted as unchecked rather than inferred to be compatible.
This verifies formulation availability for the element's topology, not full
part label membership, region suitability or numerical model validity.

Validation: a zero-warning build and **22 record/metadata plus 16 mesh cases**
pass. Added cases cover contact and constraint references, missing endpoints,
unknown constraint types, missing part metadata and topology/formulation
mismatch. The full external project also completes successfully, read-only.

## Conversion capability gate

The explicit conversion capability report is now implemented. It checks the
available current-model type inventory and complete mesh structure against the
PMA persistence boundary. Numeric object construction, element quality and
solver validity remain converter-time checks. Results need a separate
version-aware reader and validation against the original application before
full migration.

## Surface faces and exact part membership

Mesh inspection now compares every current part's serialized element labels
with the elements carrying that part ID in the binary mesh. It rejects missing,
extra and duplicate labels, undeclared binary part IDs, and metadata-only
parts. All **14 parts** in the supplied project have exact membership.

For each surface element-face entry, the inspector expands the referenced
element set, including sets created from part IDs, and checks the face number
against every referenced element topology. The supplied project passes
**60,303 element-face assignments**. The accepted solid ranges are S1–S4 for
tetrahedra, S1–S5 for pyramids and wedges, and S1–S6 for hexahedra. Shell and
beam ranges are also bounded for files containing those topologies.

This establishes structural consistency between the serialized metadata and
binary mesh. It does not validate geometric face orientation, surface area,
element Jacobians or whether a given surface is physically suitable for an
analysis definition.

Validation: the warning-free build now passes **22 record/metadata and 20
mesh cases**. Added mesh cases cover exact and mismatched part labels, valid
and invalid topology-specific surface faces, and conversion capability. The
full external PMX passes these checks and remains unchanged.

## Conversion implementation

The first fail-closed conversion slice is implemented. It constructs production
model objects from decoded legacy records and binary mesh data, saves and reopens
the PMA, and checks its exported input. Element quality and result persistence
remain separate work.

## Conversion capability report

Every JSON inventory now contains `conversionCapability`. Run with `--mesh`
for a complete report; without it, status is
`partial-mesh-inspection-required`. A complete report has one of two states:

- `eligible-for-converter-implementation`: inspected definitions fit the
  current PMA persistence contract, but no conversion has happened.
- `blocked-by-current-pma-contract`: one or more definitions would be lost.

The report compares current model collections, material-property types, step
definition types and binary topology with the exact guards in
`StaticModelMetadata`. It aggregates blockers by code and never treats a
clean inspection as proof of a successful round trip.

After quadratic-solid, binary-mesh, material, reference-point, amplitude,
part-derived-set, cylinder, contact and constraint persistence, the supplied
PMX has **zero capability blocker groups**. With `--mesh`, the report is complete
and its status is `eligible-for-converter-implementation`.

All six materials now fit the PMA contract. It preserves ordered Elastic,
Density, Plastic, ThermalExpansion, ThermalConductivity and SpecificHeat
properties, their temperature tables, plastic hardening and the thermal
expansion reference temperature. The three nonlinear static steps are also
supported and are not blockers. Both numeric reference points and the tabular
amplitude also fit the current contract, including step definition links.
The four part-derived element sets preserve their part IDs, and all four
auxiliary cylinders preserve their validated geometry and meshing parameters.
The current contract also preserves ordered surface behavior, friction and gap
conductance properties; contact-pair settings and surface links; and tie,
rigid-body and surface-spring definitions with their named endpoints.

Current round-trip tests cover C3D10T, C3D15 and C3D20R, explicit topology,
formulations, all six supported material-property types, malformed tables and
legacy compatibility, numeric reference points, tabular amplitudes,
part-derived sets, cylinders, contacts, constraints, contact field outputs and
bounded ordered CalculiX user keywords. Metadata v23 adds finite,
coordinate-defined 3D rectangular and cylindrical coordinate systems. Metadata
v25 adds finite constant pressure on named element surfaces. Archive version 3
uses metadata v28 plus a checksummed binary mesh payload; v27 adds the bounded
global Gmsh setup pair. The reader rejects missing,
truncated, trailing or wrongly signed mesh data. Metadata versions 1–25 remain
readable, as do v26 projects from the preceding external-mesh schema. The full
portable suite passes **5,141 checks**. Capability inspection passes **22 record/metadata,
20 mesh, 3 result and 5 conversion cases**. The complete report on the external file finishes
read-only with all 4,592,568 nodes and 960,847 elements checked, and the build
has zero warnings.

## Next compatibility gate

Compare field values and rendered frames with the original application. PMX
conversion is connected to the Linux Open workflow with explicit scope and
failure handling; the bounded PMA-v4 result payload, application field catalog,
independent result-mesh loading and renderer-scene export are implemented. The
CLI now emits deterministic node/value fingerprints to make that comparison
repeatable.

The Linux half of this comparison is now captured by
[`capture-linux-result-reference.mjs`](../scripts/capture-linux-result-reference.mjs).
For the supplied step-1/increment-4 MISES frame it records the exact
1,775,349-value fingerprint, 7 renderer actors, 714,210 visible points,
1,428,456 triangles, an isometric PNG and renderer diagnostics in a checksummed
bundle. The command verifies that the source PMA is unchanged before publishing
the bundle. An equivalent original-application capture remains necessary for a
parity disposition.

Broader read-only compatibility now covers seven distinct external saves across
PrePoMax v2.5.1 dev and v2.4.0. All pass full mesh inspection unchanged. PMA
metadata v19/v20 and the converter now preserve `ContactFieldOutput`, which
makes five additional 3.1–4.6-million-node fixtures eligible for conversion.
The 3,069,296-node MAG fixture now also completes metadata-v20 conversion,
independent reopen, canonical solver-input equivalence, scene export and
authenticated renderer publication with its source hash unchanged. The
metadata-v21/v22 contract now preserves the real fixtures' bounded leaf user
keywords. The 4,623,915-node NEXTREMOIN fixture completes conversion and
independent reopen with its `*Preprint` line at the requested insertion path.
The metadata-v23/v24 contract preserves NEON's coordinate-defined rectangular
frame. Its 2,281,565-node model, older unnamed result parts and 62 result-field
selections now convert and reopen; canonical deck export, scene export and
authenticated renderer publication pass. See the
[PMX fixture matrix](LINUX_PMX_FIXTURE_MATRIX.md).

This fixture is external and is not part of automated tests. Synthetic tests
cover source/result preservation, PMA creation/reopen and overwrite refusal.
Full `.pmx` migration remains incomplete because UI/controller state is not
converted; mesh-only `.inp` remains the simpler transfer path for meshes and sets.

## Analysis-model conversion result

The supplied PMX now converts read-only into a 702 MiB PMA. The conversion
materializes 4,592,568 nodes and 960,847 elements, validates and embeds results,
peaks near 11.4 GiB resident memory on this workstation and completes in about
98 seconds. The resulting PMA
reopens through both `StaticProjectArchive` and `PrePoMax.Cli inspect`; production
save/reopen/export equivalence passes.

Converted definitions include 14 parts, 83 node sets, 131 nonempty element sets,
46 surfaces, 2 reference points, 6 materials, 4 solid sections, one tabular
amplitude, 4 cylinders, 2 surface interactions, 12 contact pairs, 15 constraints
and 3 nonlinear static steps with their supported loads, boundaries and outputs.
Seventy-eight empty legacy element sets are omitted and duplicate memberships are
normalized in saved order. Unsupported types, formulas, dangling links, malformed
arrays, unknown enums, nonempty geometry, an existing destination and source/output
path aliasing fail before publishing the destination.

The PMA has no FRD or DAT payload. Archive v4 stores the original
544,409,218-byte results area in a 544,409,317-byte signed/versioned
`results.bin` envelope after read-only inspection consumes it exactly.
It contains one metadata-matched result set with 16 fields: DISP, STRESS,
TOSTRAIN and FORC at four static frames (times 0.2, 0.4, 0.7 and 1.0). Across
72 component arrays, all 127,825,128 values are finite. Its history object has
zero sets. The 62,838,276-byte result mesh block inflates to 135,602,849 bytes;
the 16 field blocks total 481,570,754 compressed and 511,302,320 inflated bytes.
The bounded scan completes in about 11 seconds at roughly 707 MiB peak resident
memory. Command history, annotations and UI state remain outside this conversion
slice. The portable application restores 124 selectable raw/derived components,
and the real step-1/increment-4 MISES frame exports as a 44 MiB renderer scene.
See [conversion details](LINUX_PMX_CONVERSION.md).

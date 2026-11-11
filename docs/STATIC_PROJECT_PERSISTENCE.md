# Editable static project persistence

Validated 2026-10-21. StaticProjectArchive.Save/Open now writes and reconstructs
an editable FeModel, checks its exported input against the archived deck, and
retains optional FRD/DAT bytes, a versioned binary result stream and a bounded
checksummed source-geometry payload in the same
`.pma` container. This is a bounded
analysis-definition format, not legacy `.pmx` compatibility or complete UI state.

## Supported boundary

Metadata schema 1 supports one 3D analysis part with linear hexahedral elements,
explicit node/element sets and surfaces, elastic materials, solid sections,
static steps, displacement/rotation boundaries, constant surface traction,
nodal/element field requests and nodal history requests. Numeric values and the
listed analysis settings are stored as JSON. Material/section/load names,
assignments, step order, units and output requests are restored into production
types. Surface source-node-set metadata is distinct from its internal node set.

In schema 1, unsupported types, expressions, additional parts, geometry, mesh
setup, custom step controls, contacts, constraints, distributions, amplitudes,
initial conditions and coordinate systems are
rejected when saving. Later extensions below add bounded support for several of
these definition groups.
UI appearance, selection provenance, command history, derived result definitions,
protected projects and legacy files remain outside this schema. Save also checks
that reconstructing metadata reproduces solver input, refusing an input-affecting
setting it cannot represent. This is not a general object-graph serializer.

The current codec also preserves nodal, element, contact and section history
request subtypes with their named regions, variables, totals and global setting.
This extension uses the existing typed entity shape and requires no new metadata
version; see [history output editing](LINUX_HISTORY_OUTPUT_EDITING.md).

Archive version 2 adds `model.json` to the checksummed manifest. Version 1 analysis
snapshots remain readable through AnalysisArchive; StaticProjectArchive rejects
archives without editable metadata. Reopen rejects unsupported schema versions,
unknown entity fields/kinds, invalid enum values, dangling mesh-region references
and discrepancies between model metadata and archived solver input. Equivalence
ignores only model hashes, heading dates and CRLF/LF differences. The date is
volatile export metadata: saving yesterday must not prevent reopening today.
JSON metadata remains bounded to 32 MiB. Archive version 3 stores mesh numeric
arrays separately. Archive v4 raises the combined uncompressed payload limit to
1.5 GiB so a bounded binary result stream can accompany the largest validated
model fixture.

## Integration and evidence

The native generator now saves and reopens the model before writing its input.
After solving, it saves the actual FRD/DAT files into that model archive and checks
byte-for-byte reopening. Native cases remain the single/two-step cubes, long bar,
compression and zero load. Cube fields/history are checked by production readers;
parameterized cases retain the analytic displacement checks. Result bytes are
preserved, but the archive API does not assert that arbitrary supplied results
correspond to the current editable model; edited models can have stale results.

The complete portable suite passes 5,141 checks. Persistence checks cover materials/assignments, mesh regions,
steps/output requests, independent editable loads, edited save/reopen, retained
result bytes, rejection without overwriting existing files, future metadata,
dangling regions, invalid enums, metadata/input mismatch, unknown fields and
cross-date/cross-newline reopening under English and Italian locales.

The initial implementation's missing load-magnitude updates were fixed by firing
equation change callbacks during restoration. Debug writes to fixed `/tmp` paths
were removed. Native artifacts are retained in
`/tmp/prepomax calculix job wgli9pqz`. Windows execution was not tested locally.

Later milestones expanded the metadata through version 28, connected project
services to the Linux host and added the archive-v4 binary result payload. The
remaining persistence work is broader legacy/layout coverage and reducing the
memory cost of very large model and result payloads.


## Metadata version 2 extension

[Mechanical persistence](MECHANICAL_PROJECT_PERSISTENCE.md) adds concentrated
forces, gravity, centrifugal loads, fixed boundaries and density. Version 1
remains readable. Earlier type exclusions above describe version 1.

## Metadata version 3 extension

[Multipart persistence](MULTIPART_PROJECT_PERSISTENCE.md) adds multiple mesh parts,
explicit membership and element formulation, including part-based solid sections.
Versions 1 and 2 remain readable; other model scope restrictions still apply.

## Metadata versions 4 and 5

Version 4 adds linear tetrahedra and their C3D4 part formulation. Version 5
records element topology explicitly and adds quadratic tetrahedra, wedges and
hexahedra with C3D10/C3D10T, C3D15 and C3D20/C3D20R formulations. Versions 1–4
remain readable. Version 5 rejects unknown topology names, mismatched
connectivity widths and parts without a formulation for their assigned element
topology. Older portable builds cannot read version 5.

## Metadata versions 6–26 and archive versions 3–5

Metadata version 6 introduced external binary mesh storage for the version-5
definition schema. Version 7 adds ordered Plastic, ThermalExpansion,
ThermalConductivity and SpecificHeat properties alongside Elastic and Density.
It preserves temperature tables, plastic hardening and thermal expansion's
reference temperature. Each supported property type can occur once, Elastic is
required, and expressions or malformed/nonfinite tables are rejected.

Metadata version 9 adds numeric reference-point coordinates and finite,
nonempty tabular amplitudes. Reference-point and amplitude links already stored
on supported loads and boundaries therefore survive reopening. Creation
selection provenance and non-tabular amplitude types remain outside the schema.

Metadata version 11 distinguishes explicit element-ID sets from element sets
created from part IDs, validating both against the corresponding mesh
collection. Version 13 adds finite, properly defined auxiliary cylinders,
including their geometry and meshing parameters.

Metadata version 15 adds ordered surface-interaction properties for surface
behavior, friction and gap conductance, plus contact pairs with their method,
sliding, adjustment, interaction and surface links. Version 17 adds ties,
rigid bodies and surface springs with validated surface, reference-point and
node-set links. Numeric values are required where the portable format cannot
retain equations.

Metadata version 19 adds contact field-output requests, retaining their variable
flags plus active, last-iteration, contact-element and global settings.

Metadata version 21 adds up to 1,000 bounded active, unsuppressed leaf CalculiX
user keywords with ordered insertion paths and exact text. Version 23 adds up to
1,000 finite, coordinate-defined 3D rectangular or cylindrical coordinate
systems, retaining name, active state, type and three defining points. It
rejects 2D and selection-derived frames until their additional semantics are
represented.

Metadata version 25 adds finite, constant `DLoad` pressure on named element
surfaces. It retains the surface assignment and signed magnitude and rejects
element-ID/face overrides, nondefault distributions, expressions and other
pressure variants outside this bounded contract.

Metadata version 27 adds one bounded global Gmsh sizing and tetrahedral setup
pair. Current `.pma` files use metadata version 28: the version-27 definition schema
with nodes, element topology/connectivity, part membership, node/element-set
labels and surface face IDs in a checksummed `model.mesh` entry. The binary payload is
versioned with the `PMAMESH1` signature, limited to 768 MiB, and validated for
counts, UTF-8 identifiers, topology widths, exact consumption and agreement
with JSON identities. Archive versions 1–2, inline metadata versions 1–5, 7, 9,
11, 13, 15, 17, 19, 21, 23 and 25, and external-mesh metadata versions 6, 8, 10,
12, 14, 16, 18, 20, 22, 24, 26 and 28
remain readable.

Archive version 4 requires a `results.bin` entry containing a bounded
`PMXRSLT1` envelope around the versioned CaeResults stream. Reopen checks the
manifest checksum, envelope signature/version, result-name count and uniqueness,
stream version and exact length before application decoding. The portable result
service reconstructs result parts when legacy metadata is absent and uses the
independent result mesh for contours and deformation.

Archive version 5 adds the exact source geometry bytes and original base
filename for CAD/GEO-created projects. The source is limited to 128 MiB and is
covered by the archive manifest checksum. Model edits and result publication
preserve it while dropping only stale results. Inspection reports its name,
length and SHA-256, and extraction refuses to overwrite an existing destination.

Missing, truncated, trailing and wrongly signed mesh payloads are rejected.
The archive's combined uncompressed payload limit is now 1.5 GiB. Payloads are
still materialized as byte arrays, so this is bounded large-model support rather
than a streaming persistence implementation.

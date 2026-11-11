# Portable analysis snapshot archive, versions 1–7

Current editable saves use version 7 with a checksummed material identity map;
versions 1–6 remain readable. See the [M01 material persistence contract](M01_MATERIAL_COMMANDS.md#material-rename-and-project-persistence).
The original container design and earlier format additions are documented below.

Implemented 2026-09-16 in the dependency-free .NET 10 library
`portable/PrePoMax.Persistence`. This is the first persistence container slice,
not editable project metadata and not legacy `.pmx` compatibility.

`AnalysisArchive.Save(path, AnalysisSnapshot)` and `AnalysisArchive.Open(path)`
round-trip an analysis name, exact exported input bytes and optional FRD/DAT
result bytes. The suggested extension is `.pma`. No path is extracted from the
archive and no serialized .NET type names or executable object graphs are read.

The ZIP layout is fixed:

| Entry | Contents |
|---|---|
| `manifest.json` | Format `prepomax-analysis-snapshot`, Version 1, Name, Payloads |
| `analysis.inp` | Required, nonempty solver input |
| `results.frd` | Optional, nonempty result payload |
| `history.dat` | Optional, nonempty history payload |
| `model.json` | Required editable definitions in versions 2–5 |
| `model.mesh` | Required binary mesh payload in versions 3–4; optional in version 5 |
| `results.bin` | Required bounded versioned CaeResults stream in version 4 |
| `geometry.source` | Required retained source-geometry bytes in version 5 |
| `geometry.topology.json` | Source-bound CAD topology, required in version 6 and optional in version 7 |
| `material-identities.json` | Required versioned material ID/name map in version 7; editable metadata is also required |

Manifest property names are case sensitive. Each Payloads item contains Name,
Length and an uppercase SHA-256 hex checksum. Unknown manifest members, unknown
entries, duplicate ZIP entry names, unsupported format/version, missing or
undeclared payloads, inconsistent lengths and checksum mismatches are rejected.
The manifest is limited to 16 KiB and the combined uncompressed payloads to
1.5 GiB. This API loads payloads into memory; larger or streaming payloads need a
separate implementation. Checksums detect corruption, not authenticity.

Save validates and snapshots input bytes, writes a uniquely named temporary file
beside the destination, closes the ZIP, flushes the file and replaces the target.
Failed writes/replacements clean the temporary file. This does not promise
power-loss durability of the directory entry on every filesystem.

Validation: the portable checks generate input from the actual static-model
exporter, round-trip FRD fixture bytes and a small DAT fixture, then reopen those
payloads through production result readers. They also check unsolved snapshots,
invalid saves preserving existing bytes, failed replacement cleanup, corruption,
future versions, missing input, duplicate/unknown entries and malformed JSON.
The complete portable suite now totals 5,141 checks. The native Gmsh workflow,
GTK X11 CAD smoke and clean Ubuntu package solver/mesher lifecycle also pass.

The initial next step was editable model metadata with explicit type/version
rules. That work is now represented by archive versions 2–4 below, including
save/reopen/export equivalence for the supported model scope. UI state, nonempty
legacy geometry, command history and protected PMX files remain unsupported.

## Editable model extension (2026-09-17)

Archive version 2 adds optional `model.json` and requires that entry for version 2;
version 1 must not carry it. AnalysisArchive reads both versions, with the same
combined payload limit. The metadata implementation now depends on CaeModel.
See [static project persistence](STATIC_PROJECT_PERSISTENCE.md) for the bounded
editable model schema and native save/reopen/solve evidence. Earlier statements
above that editable metadata is wholly pending describe the initial container slice.

## Binary mesh extension (2026-10-07)

Archive version 3 requires both `model.json` and `model.mesh`. The mesh entry
removes large numeric arrays from JSON while retaining manifest length and
SHA-256 checks. Versions 1–2 remain readable. Saving a mesh without model
metadata, or opening a version whose declared entries do not match, is rejected.
Current model-only saves use archive version 3 with model metadata v28.

## Binary result extension (2026-10-08)

Archive version 4 adds required `results.bin` alongside `model.json` and
`model.mesh`. The `PMXRSLT1` envelope records its own format version, the CaeResults
binary version, 1–1024 bounded UTF-8 result names and an exact binary-stream
length. The manifest independently checks its length and SHA-256. The entry is
limited to 768 MiB and stored without redundant ZIP compression; combined
uncompressed archive payloads are limited to 1.5 GiB. Versions 1–3 remain readable.

`Describe` validates archive layout without materializing payloads.
`CopyPayloads` reads and hashes every declared entry while copying only requested
payloads to caller-owned streams. PMA-v4 result loading uses this path so large
solver-input and mesh entries are verified but not retained in memory.
Geometry-scene opening uses the same path for `model.json` and `model.mesh`.
Full editable opening remains the stronger semantic path that also regenerates
the CalculiX input and compares it with `analysis.inp`.

## Retained geometry extension (2026-10-23)

Archive version 5 adds `geometry.source` and a bounded original base filename in
the manifest. The payload is nonempty, limited to 128 MiB and protected by the
same manifest length and SHA-256 validation as every other entry. Directory
components, separators and control characters are rejected from the filename.
Version 5 requires editable model metadata and permits the existing external mesh
and binary-result entries, so edits and solved result archives can retain the
source without weakening their current validation.

`StaticProjectArchive` exposes the retained bytes as `ProjectGeometrySource`.
The application copies that source through model edits and solve publication;
the CLI and GTK host can extract it only to a new destination. This stores the
original STEP, IGES, BREP or GEO input exactly. It does not yet deserialize an
editable B-rep topology.

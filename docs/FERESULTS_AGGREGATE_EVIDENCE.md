# Portable FeResults aggregate

The .NET 10 CaeResults project now links 47 production sources, including the
FeResults aggregate, result-history definitions and remaining result enums.
The existing `FrdFileReader.Read` aggregate adapter is shared by both builds
and renamed from Legacy to Aggregate. `ReadData` remains available for raw fields.

Three existing numeric helpers (complex real value from magnitude/phase, median,
and average) moved into the shared Tools.Results partial without algorithm changes.
Obsolete unused platform imports were removed, and ordered dictionaries explicitly
use CaeGlobals types to avoid framework ambiguity.

Two methods remain in `FeResults.Legacy.cs`, included only by the legacy project:
CopyMeshItemsFromMesh and CopyFeatureItemsFromMesh. They depend on legacy cloning.
The rest of the aggregate compiles against the actual portable dependencies;
no placeholder result engine or serialization shim is supplied.

Eight checks per locale load the two-step cube through the production aggregate:

- Mesh and step discovery.
- Automatic von Mises invariant preprocessing.
- Derived `=[STRESS.MISES] / 2` evaluation with explicit parent names and initialized
  equation parameters, matching application setup requirements.
- Second-step deformation at scale 10, unchanged original coordinates, reset to
  undeformed geometry and switching to first-step deformation without accumulation.

Run `bash scripts/test-portable.sh` (3,201 checks, including 354 result checks)
and `python3 scripts/test-native-calculix.py`. The latter also executes these
aggregate checks against freshly generated two-step FRD output in both locales.

Compilation is broader than runtime coverage. Full result persistence, complex
and modal workflows, arbitrary derived equations, history derivation, filtering,
legacy mesh-item copying and desktop/renderer integration remain unvalidated.
The aggregate exposes additional existing compiler/analyzer warnings, including
short reads in result persistence; these are not suppressed. Windows build
validation remains pending.

## Binary payload round trip

Compressed mesh and field blocks now use the shared exact-read helper instead
of assuming FileStream.Read fills its buffer. The regression failed in mesh
block decompression when reads were limited to three bytes.

Five checks per locale save a deformed two-step aggregate, restore its payload
through short reads using format version 2.4.4, and verify complete consumption, undeformed coordinates,
step IDs, regenerated stress invariants and subsequent first-step deformation.
Saving preserves the source's displayed deformation on the successful path.
The restored aggregate starts with separately supplied mesh metadata, matching
the existing payload contract; this is not a complete project-file round trip.

Existing persistence short-read warnings for these two blocks are resolved.
Serialization of surrounding project metadata, compatibility across file versions,
complex results and restoration after failed writes remain unvalidated.

## Failed-save state restoration

The writer now uses finally blocks to restore temporarily swapped mesh nodes,
recompute stripped field invariants, and restore complex-result preparation and
selection. Previously an I/O failure during a compressed field write left the
live field without its invariant components.

Six checks across both locales inject an IOException at the first field payload
write and verify error propagation, restored displacement magnitudes and preserved
displayed deformation. The invariant assertion failed before the fix. This tests
real-valued fields at that failure point; complex-state restoration and failures
inside cleanup itself remain unvalidated. A failed output file may be partial;
this change does not implement atomic file replacement.

## Corrupt field payloads

Malformed decoded field records now propagate an InvalidDataException identifying
the one-based field record number and retaining the original exception. Previously
a catch-all silently skipped the field and continued loading. Common I/O, argument
and overflow parse errors receive context; other exceptions also propagate.

A regression in each locale replaces the first field payload with a valid compressed
block containing an incomplete integer marker. Framing and subsequent records stay
intact. The test confirmed silent omission before the fix and explicit rejection
afterward. Loading is not transactional: callers must discard a target aggregate
when loading fails. No file-format change was made.

## Histories derived from FRD fields

Fourteen checks across both locales exercise production ResultHistoryOutputFromField
through FeResults.AddResultHistoryOutput. A selected four-node region yields U1
samples of 0.001 and 0.002 at total times 1 and 2, plus zero U3 samples. A separate
single-node MISES history restricted to step 2/increment 1 yields one sample near 2.
These checks run on the retained fixture and fresh native two-step solver output.

This establishes selected-node static field histories and step filtering without
requiring DAT history output. Other region types, harmonic histories, coordinate
outputs and history filtering still need validation. No production algorithm change
was required for these cases.

## Named regions and coordinate histories

Twenty-four additional checks across both locales cover named node sets and
surfaces referencing a node set, each with undeformed and deformed coordinate
output. They verify region membership and times, physical X coordinates of 1
or 1.001/1.002, and preservation of a displayed mesh deformed at scale 10.
This confirms that history coordinates do not inherit display exaggeration.

These cases run on the retained and freshly solved two-step cube. Surface
metadata is explicitly supplied in the test, as it is not present in this FRD
fixture. Geometry-based surface creation, missing displacement fields and
harmonic coordinate histories remain outside this coverage. No production
algorithm change was required.

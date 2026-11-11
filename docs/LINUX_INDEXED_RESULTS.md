# Indexed binary result access

Implemented and validated: 2026-10-15.

The second step of the accepted [ccxview recommendation](CCXVIEW_EVALUATION.md)
is implemented in the Linux viewer's result session. Supported PMA-v4 projects
retain a verified binary payload and a field index. Field selection decodes the
selected record, plus its matching displacement record when deformation needs
one. The archive format and production numerical algorithms are unchanged.

## Scope and behavior

The indexed path supports a single binary result set written with version
`2_005_001`, containing static, real fields in `DataState.OK`, positive step and
increment IDs, matching field/metadata names and equal component lengths within
each record. Complex-family names are excluded because the production reader can
synthesize additional fields from real/imaginary pairs. Unsupported versions,
states or layouts fall back to the existing eager reader. FRD remains on the
decoded-session path. Standalone `ProjectResults` operations remain eager and
serve as a differential oracle. `useBinaryIndex: false` explicitly selects the
previous session strategy for controlled comparisons; zero retention budget
uses uncached eager loading.

Cold access hashes and snapshots the source as before, then:

1. Streams every archive entry through its checksum, extracting `model.json`
   and the binary payload. The original archive snapshot is removed afterward.
2. Validates the envelope and scans each independently compressed field. Bounded
   inflation reads metadata and consumes all value bytes without allocating
   nodal arrays. A one-sample metadata projection uses production invariant code
   solely to determine component names and ordering; it supplies no engineering
   values to the viewer.
3. Validates the mesh and history with the production collection reader, using
   the original compressed mesh and history with an empty field list. Those
   transient objects are discarded. Cold catalog access is **not mesh-free**.
4. Retains the field offsets, selections and private payload if metadata fits
   the admission budget. Public catalog arrays remain independent copies.

Warm catalog access hashes the source and returns metadata without field decode.
A selection copies the original mesh prefix and requested compressed records
into a temporary collection, then calls the production reader. A scalar requires
one record; deforming stress requires stress and the same frame's `DISP`; a
deformed `DISP` selection still requires only one. The temporary collection is
removed after decoding. Units, node lookup, invariants, finite-value checks and
scene construction continue through existing code.

The session remains serial and retains at most one entry. Clear, replacement,
geometry reset/reopen, solver start and disposal remove its disk payload. Failed
index builds and non-admitted accesses also remove private data. Cancellation is
checked during source hashing/copying, archive streaming, field scanning and
slice copying. Production mesh/history and selected-field decoding still have
synchronous sections without internal cancellation checks.

## Bounds and tradeoffs

The default 512 MiB admission budget applies to the conservative **metadata size
estimate** for indexed entries, with a separate 128 MiB index cap. It applies to
loading allocations for eager entries. An archive larger than the budget can
therefore retain an index; a test exercises that distinction. This is not a
process-memory limit, and transient mesh decoding can exceed the budget.

Existing source (2 GiB), binary payload (768 MiB plus envelope), compressed block
(512 MiB), inflated block (1 GiB), field-count (100,000) and component-value-count
(100 million) bounds still apply. The metadata projection also limits strings to
4,096 encoded bytes and components to 1,024 per field. It rejects malformed
lengths, truncated metadata and duplicate components/fields. Archive checksums
cover unused entries; cold indexing also reads unselected field records and
validates mesh/history before admitting anything.

Disk use includes a source snapshot during opening, one extracted payload, and
one temporary selected collection. The selected collection is at most the size
of the raw binary payload. A memory-backed temporary directory consumes system
memory too; lower managed retention does not establish lower total memory use.

This first index deliberately retains neither mesh nor selected field arrays.
Every selection rebuilds the mesh, and repeated scale changes decode the same
one or two records again. Every hit also reads the entire source for its content
hash. Unsupported binary layouts can incur an index attempt before eager loading.
These costs are visible in the diagnostics and remain optimization candidates.

## Validation and evidence

The latest portable checks total **5,141**, including **2,126 mesh I/O checks**
across English and Italian cultures. Indexed checks cover every raw and derived
component in the two-step fixture, exact fingerprints/statistics/units, scene
bytes at zero/positive/negative scale, selected-record counts, admission bounds,
content identity across paths, eviction and cleanup. Rechecksummed corrupt mesh,
unselected-field and history payloads fail; unknown versions, non-static and
complex fields exercise eager fallback.

Both FRD and binary fixtures pass the real renderer transport/lifecycle test.
Native CalculiX lifecycle acceptance from the binary project passes, including
solver-start session eviction, cancellation, failure/retry and source preservation.
Commands and results: [validation log](evidence/result-index-validation-2026-10-15.txt).

The [profiling record](evidence/result-index-binary-2026-10-15.json) contains an
eight-node, two-step archive with **8 indexed records** and **13,720 estimated
metadata bytes**. Warm catalog access decodes zero field records; each MISES
fingerprint decodes one. All session fingerprints equal independent eager reads.
Warm fingerprint allocations were about 614 KB versus 1,098 KB for independent
eager reads. These tiny-fixture, process-allocation figures include transient
objects; timings include JIT, cache/order and workstation scheduling effects.
They do not predict a large-model speedup or measure browser paint/GPU work.

Reproduce with the existing characterization executable:

```bash
dotnet portable/tests/MeshIO.Characterization/bin/Release/net10.0/MeshIO.Characterization.dll \
  --write-binary-result-project /tmp/index-binary.pma
dotnet portable/tests/MeshIO.Characterization/bin/Release/net10.0/MeshIO.Characterization.dll \
  --profile-result-session /tmp/index-binary.pma
```

Replies add `Indexed`, `IndexedFieldRecords`, `IndexMetadataBytes`,
`DecodedFieldRecords` and `SelectedDecodeMilliseconds` to `resultAccess`.
`Load.DecodeMilliseconds` measures cold index scanning/mesh/history validation
or eager loading; it is zero on hits. `SelectedDecodeMilliseconds` separately
includes selected mesh/field decoding and value extraction. Thus a cache hit
does not imply that an indexed selection performed no decoding.

## Next gate

Measure the representative brake PMA and a larger multi-frame project, separating
archive hashing, index opening, mesh reconstruction, selected fields and scene
publication. The previously reported 702 MiB brake measurements remain the eager
streaming baseline; this change has not remeasured that project. Use that evidence
to size any retained mesh/selected-frame cache before pursuing renderer scalar
updates and retained displacement. Broader quadratic, shell and sparse-field
fixtures remain necessary before expanding result semantics.

Implementation: [index](../portable/PrePoMax.Application/BinaryResultIndex.cs),
[session](../portable/PrePoMax.Application/ProjectResultSession.cs),
[differential tests](../portable/tests/MeshIO.Characterization/IndexedResultChecks.cs).

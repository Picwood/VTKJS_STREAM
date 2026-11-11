# Retained result sessions and loading measurements

Implemented and validated: 2026-10-15.

The first implementation step from the accepted [ccxview evaluation](CCXVIEW_EVALUATION.md)
is complete. The Linux viewer shares one result session between field listing,
field selection and displacement-scale changes. Eligible eager loads decode once;
subsequent accesses verify archive content and reuse the decoded result.

The follow-up [indexed binary milestone](LINUX_INDEXED_RESULTS.md) is also complete
for the documented static-real layout. It retains metadata and a private disk
payload, then decodes selected records. The decoded-collection behavior and
original measurements below describe the eager strategy still used for FRD and
unsupported binary layouts.

## Eager behavior and shared ownership

`RendererHost` owns a serial-use `ProjectResultSession`. The viewer's `fields`
command and result-scene exporter use that same session. There is no global cache.
Standalone `ProjectResults` calls continue to perform independent loads.

For a cache-eligible archive:

1. Open the source and hash its complete bytes with SHA-256. The cache key is
   content, not the pathname, length or modification timestamp.
2. On a hit, reuse the validated, privately held `FeResults` collection and catalog.
   Public catalog arrays are copied; callers cannot mutate the retained array.
3. On a miss, release the previous entry, then copy the source through the same
   open handle to a private temporary snapshot. Hash that copy and reject changes
   between the two passes.
4. Load the snapshot through the existing production readers and validators.
   PMA-v4 checks every declared payload hash, including unused entries. The FRD
   path retains editable-model/result mesh consistency checks.
5. Admit the decoded collection only within the retention policy. Remove the
   private snapshot on success, failure or cancellation.

At most one entry (decoded collection or binary index) is retained. A successful geometry publication—including
project reopen or return to mesh geometry—clears it. Starting a solver job clears
it, and host disposal releases it. An invalid project-open request leaves the
existing view/session available. Failed replacement decoding never admits a
partial entry. Invalid field selections retain the existing browser bootstrap.

Content validation is repeated on each hit, so replacing a file at the same path,
even with identical size and timestamp, cannot return stale cached results.
Results represent the source content read for that operation, not a live view of
subsequent filesystem edits.

## Bounds and fallback

The viewer uses a **512 MiB load-allocation admission budget**. A load is eligible
for retention only if the process-wide managed allocation delta for loading and
catalog construction fits that budget. This deliberately counts transient
allocations, and unrelated concurrent allocations can conservatively prevent
admission. It is not a measurement of retained bytes or a process-RSS limit.

With indexing disabled, an archive larger than the budget bypasses hashing/snapshot
caching and uses the existing uncached loader. With indexing enabled, a source
snapshot permits an index attempt before any eager fallback. A smaller compressed archive whose decoded loading
allocations exceed the budget also returns correct results without retaining
them. A zero budget disables retention. The session constructor exposes the
budget for controlled measurements; the viewer keeps the conservative default.

Session source archives are capped at 2 GiB before copying; existing archive
payload/decompression and scene limits still apply. Loading itself can allocate
more than the admission budget. In particular, the previously measured **702 MiB
brake PMA was uncached in the first milestone** at the viewer default. Indexed
metadata can now be retained independently of source size and transient mesh
allocations, subject to the supported layout. The brake case has not been
remeasured; neither milestone claims a large-model speedup or raises the budget.

Cancellation is checked during hashing, snapshot copying, PMA-v4 payload streaming,
between load/decode stages, and during scene writing before publication. The
legacy synchronous FRD/model and binary collection decoders are not interruptible
inside every parsing loop. The companion still processes result requests serially;
this change does not add a new UI cancellation command for field loading.

## Diagnostics

Successful `fields`/`field` companion replies include non-secret `resultAccess`
data: cache hit/admission, loading allocations, identity-hash time, snapshot-copy
time, archive-validation/loading time and result-decode time. Hits report zero
archive/load work. Indexed selections report their additional work separately
through `DecodedFieldRecords` and `SelectedDecodeMilliseconds`; see the
[index diagnostics](LINUX_INDEXED_RESULTS.md). The loading allocation figure on a hit describes the
retained entry's original admission, not new allocation for that hit.

Successful field publication also returns `publication`: export time, metadata
and scene upload time, authenticated renderer-session setup time, scene bytes and
total publication time. Export time includes result access and scene construction;
these are nested measurements and must not be summed twice. Total publication
includes explicit heap compaction; it ends before temporary-directory cleanup.
It is not browser first-paint time. Existing stable-frame capture remains the
mechanism for measuring browser completion.

The FRD archive stage includes editable-model restoration/validation. The binary
archive stage includes streaming all entry hashes, copying requested payloads
and reading model units. The decode stage uses production CaeResults code.
These are stage boundaries for investigation, not isolated parser microbenchmarks.

## First-milestone validation and measurements

- `bash scripts/test-portable.sh`: **4,469 passing checks**, including **1,458
  mesh I/O checks**. The latter execute in English and Italian cultures.
- New checks cover both FRD and PMA-v4 catalogs/fingerprints, byte-identical scenes
  at positive/negative/zero deformation, repeated reuse, catalog isolation,
  valid replacement/eviction, same-size/same-timestamp corruption, unselected-entry
  checksum corruption, budget fallback, cancellation, disposal and temporary cleanup.
- `scripts/test-viewer-fields.py` passes against the real renderer for both FRD
  and binary two-step archives. It checks source-node scalars, contours, displacement,
  timing replies, cache reuse, invalid-field recovery, reset/reopen eviction and
  failed-open recovery. Source archive bytes remain unchanged.
- Native viewer solve/archive-reopen, cancellation, failure/retry and EOF cleanup
  also pass with the existing documented CalculiX runtime configured. An initial
  invocation without that library path failed in the solver loader; no runtime
  dependency or solver implementation was changed.
- Release build: zero warnings/errors. No new physical-GPU or graphical visual
  acceptance is claimed.

Small-fixture profiling on .NET 10.0.12 yielded:

| Archive | Result loads / subsequent hits | Median retained fingerprint, 3 accesses | Median independent fingerprint, 3 reads |
|---|---:|---:|---:|
| FRD PMA, 3,802 bytes | 1 / 3 | 0.072 ms | 11.416 ms |
| Binary PMA, 3,982 bytes | 1 / 3 | 0.070 ms | 0.819 ms |

These eight-node fixtures demonstrate avoided decoding and identical numerical
results. They are too small to predict engineering-model speedups; JIT and file
cache/order effects are included. The first session catalog took about 320 ms
and 234 ms respectively in fresh processes. Recorded peak working set is cumulative
for each profiling process, not a before/after memory comparison.

Raw measurements: [FRD](evidence/result-session-frd-2026-10-15.json),
[binary](evidence/result-session-binary-2026-10-15.json).
Check summaries: [validation evidence](evidence/result-session-validation-2026-10-15.txt).

Reproduce after building the portable suite, using the installed .NET executable:

```bash
dotnet portable/tests/MeshIO.Characterization/bin/Release/net10.0/MeshIO.Characterization.dll \
  --write-result-project /tmp/session-frd.pma
dotnet portable/tests/MeshIO.Characterization/bin/Release/net10.0/MeshIO.Characterization.dll \
  --write-binary-result-project /tmp/session-binary.pma
dotnet portable/tests/MeshIO.Characterization/bin/Release/net10.0/MeshIO.Characterization.dll \
  --profile-result-session /tmp/session-binary.pma
```

The profiler accepts an optional final retention budget in MiB (`0` disables
retention). It reports a selected MISES field, or the first available field,
three session fingerprints and three independent reads, failing if fingerprints
differ. The updated profiler also records a warm catalog and fingerprint allocations;
the binary default now exercises the index, so it will not reproduce the historical
decoded-cache timings above. Run the renderer integration check as documented in
[result contour evidence](LINUX_RESULT_CONTOURS.md), substituting each archive.

## Accepted implementation sequence

| Step | Status | Next acceptance |
|---|---|---|
| Content-checked result session, bounded retention and stage timings | Complete for the documented fixtures | Broader project measurements remain useful; no large-model claim |
| Indexed catalog/selected-field access | Complete for the [documented binary layout](LINUX_INDEXED_RESULTS.md) | **Next:** measure brake/larger multi-frame cases; decide whether to retain mesh/selected-frame data |
| Scalar-only updates and retained displacement basis | Pending measured need | Reuse existing renderer seams; unchanged IDs, picks, bounds, sections and exports |
| Sparse/no-data fields and Linux post-processing tools | Pending | Explicit missing-value semantics and selected user workflows |
| Shared ccxview numerical corpus and specialized tools | Pending | Pinned fixtures and independent numerical references |

Indexed access now uses the existing separately compressed binary records without
an archive-version change and preserves the integrity/lifecycle contract. The
512 MiB admission budget remains unchanged; it should not be raised to hide eager
loading. Large-model evidence is required before choosing further retention policy.

Implementation: [session](../portable/PrePoMax.Application/ProjectResultSession.cs),
[result access](../portable/PrePoMax.Application/ProjectResults.cs),
[renderer host](../portable/PrePoMax.Application/RendererHost.cs),
[regressions](../portable/tests/MeshIO.Characterization/ResultSessionChecks.cs).

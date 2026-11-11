# Bounded streaming of archived binary results

Validated: 2026-10-12.

Follow-up (2026-10-15): the viewer now uses [bounded result sessions](LINUX_RESULT_SESSIONS.md)
and [indexed binary field access](LINUX_INDEXED_RESULTS.md) after verifying archive
content. The large-fixture measurements below remain the eager streaming baseline;
the indexed path has not yet been measured on that project.

PMA-v4 field listing, fingerprints and contour/deformation export no longer
materialize the editable solver input, external model mesh and complete
`results.bin` envelope together. `AnalysisArchive.CopyPayloads` streams every
declared entry through its manifest SHA-256 check and copies only requested
entries. The result envelope is written to an isolated temporary file;
`ResultPayload.ReadHeader` validates its signature, version, names and exact
binary length before the production CaeResults reader continues from the same
file stream. Temporary data is removed after decoding.

Binary-result rendering needs the independent result mesh and the model's unit
system, but not the full editable mesh. The application therefore reads the
bounded `model.json` identity and bypasses `StaticProjectArchive.Open` for PMA-v4
result operations. Geometry scenes and FRD results retain their existing full
model validation path; FRD still compares nodes and connectivity with the
editable mesh.

All archive entries are still read and hashed, including entries that are not
copied. Unknown, duplicate, missing, truncated, trailing, oversized or
checksum-mismatched entries fail before result publication. Tests cover the
descriptor, selective copy, corruption parity with ordinary archive opening,
streaming envelope headers and byte-identical FRD/binary fingerprints.

## Real-fixture measurements

For the 702 MiB converted brake project on this workstation:

| Operation | Before | After |
|---|---:|---:|
| MISES fingerprint elapsed | 19.45 s | 8.52 s |
| MISES fingerprint peak RSS | 9,060,720 KiB | 3,129,820 KiB |
| Field catalog elapsed | about 47 s | 7.93 s |
| Field catalog peak RSS | about 6.6 GiB | 2,851,868 KiB |
| Model scene elapsed | 14.13 s | 10.34 s |
| Model scene peak RSS | 8,604,768 KiB | 2,491,308 KiB |
| GTK model-to-MISES elapsed | 58.9 s | 33.55 s |
| GTK model-to-MISES peak RSS | 12,477,460 KiB | 3,632,488 KiB |

The field catalog JSON and final MISES fingerprint are byte-identical before
and after this change. GTK reports the same 14 model actors and the same 7 result
actors, 714,210 result points and 1,428,456 triangles.

Geometry publication now selectively retains `model.json` and `model.mesh`, uses
compact struct-backed external-mesh records and discards other archive payloads.
The 46 MiB scene is byte-identical to the full-open result. The long-lived viewer
also compacts its large-object heap between completed large-scene stages so the
model, field-catalog and result allocations do not accumulate.

Fast geometry publication validates every manifest hash and fully restores the
model metadata and external mesh, but it does not regenerate and compare the
392 MiB solver input. Normal editable-project opening still performs that deck
equivalence check. A future archive format can record a direct semantic model/deck
link without rebuilding the full input during display.

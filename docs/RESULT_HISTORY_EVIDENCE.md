# Portable CalculiX history results

2026-09-12: portable/CaeResults links 12 shared production sources: DatFileReader,
DatDataSet, result name metadata, history data/set/field/component/entry classes,
filter metadata, and DataTypeEnum. This is a partial result assembly, not FeResults
or the full field-result/renderer stack.

Removed an unused vtkControl import and qualified CaeGlobals.OrderedDictionary
for .NET 10. DataTypeEnum moved out of Field.cs into its own shared file, retaining
its namespace and legacy compile entry. The required numerical, compression and
primitive stream helpers moved from Tools.cs into Tools.Results.cs unchanged,
with their constants and legacy compile entry retained.

DAT time and value parsing now use invariant culture and disallow thousands
separators in the numeric value parser. Existing repair of exponent notation
without E remains supported. The displacement fixture failed before the change;
it now passes in en-US and it-IT.

## Validation

The default suite now totals **2,829 assertions**, including 16 new history
checks. An independently specified two-node displacement DAT fixture verifies
entry counts, decimal/scientific values, signed E-less exponent values, time,
missing-file behavior and a binary history round trip with complete stream
consumption. The fixture path contains spaces and Unicode. CaeResults changes
now trigger the portable Windows/Linux CI matrix. Windows was not run locally.

Compilation exposes existing CA2022 warnings for single-read legacy FileStream
helpers and history entry readers. They are not suppressed or qualified as safe
for short reads. No legacy compressed stream, modal/stress invariant, complex
result, malformed DAT, full-project or FRD compatibility claim is made. Those
methods compile but need dedicated fixtures before relying on them on Linux.

The executable is still absent from both the configured CalculiX build target
and the inspected user-local install path. No actual CalculiX solve was run.
The prepared solver smoke remains available once that build completes.

Next: connect production history reading to the pending solver acceptance test,
then expand field-result/FRD ingestion and job result publication.

## Short reads and truncated compressed histories

Shared primitive result reads (boolean, integer and UTF-8 string payloads) and
compressed history payload reads now loop until their complete byte buffers
are filled. A premature zero-byte read throws `EndOfStreamException` instead
of treating unread bytes as zero or passing an incomplete compressed block to
the decompressor. This uses APIs available to both net48 and net10 and changes
no serialized bytes.

The CaeResults harness adds six checks per locale: a compressed history round
trip through a FileStream subclass limited to one byte per read, complete
payload consumption, and four truncation points covering the existence marker,
length prefix and compressed payload. The short-read round trip failed before
the fix. The tests preserve names, units, local status, times, values and counts.
These checks do not establish compatibility for all legacy result-file versions
or validation of malicious length prefixes.

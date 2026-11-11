# CalculiX temporary export state

Implemented 2026-09-16. The actual CalculixFileWriter.GetModelKeywords now uses
CalculixExportState for temporary surfaces/element sets, replaced elements and
mass-section export metadata. The helper compiles in both project variants;
the complete writer still remains outside the portable build.

Surface and element-set values use their existing explicit copy constructors.
OrderedDictionary.CopyValues preserves the dictionary name, explicit key order
and original key comparer while allowing each caller to define value copying.
No general object serialization is used for these two export collections.

Setup finishes all copies before replacing either model collection. Previously,
failure while cloning element sets could leave already-replaced surfaces on the
model, outside the writer's try/finally. Disposal restores original collection
identities and replaced elements. It also restores the original ElementSetName
of every mass section, instead of clearing point-mass metadata to null while
leaving distributed-mass metadata changed. Cleanup is idempotent. Removing the
writer's catch-and-rethrow preserves original exception stack traces.

Validation: `bash scripts/test-portable.sh` passes 3,555 checks, including 22 new
assertions across English/Italian locales. Checks mutate copied face dictionaries
and element labels, replace an element, add a temporary set and change both mass
section kinds. They verify isolation and restoration after a simulated keyword
failure and successful completion, repeated disposal, copy-construction failure,
and explicit dictionary ordering/comparer/value independence.

`git diff --check` passes. Native solver acceptance was not rerun: the scoped
acceptance exporter does not use the complete writer, so it would not validate
this wiring. Windows compilation and actual full-writer integration remain
unverified. This is temporary-state ownership, not whole-model cloning, general
transaction rollback or concurrent export support.

User-keyword/load copying is now implemented; see
[copy evidence](CALCULIX_EXPORT_COPY_EVIDENCE.md).

Remaining gate: replace whole-model cloning, reconnect variable
pressure/imported-pressure and submodel dependencies, then compile the complete
writer and exercise its success/failure paths and native solver output directly.


Later integration: the production writer now builds and native acceptance uses
its prepared-model file API. See [current evidence](CALCULIX_FULL_WRITER_EVIDENCE.md);
exporter-exclusion statements above describe the earlier slice.

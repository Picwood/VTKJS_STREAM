# Portable keyword and load export copies

Implemented 2026-09-16. CalculixFileWriter now has no DeepClone calls; its entry
point still calls FeModel.DeepCopy, which remains outside the portable build.

CalculixUserKeyword.CopyForExport owns a new child tree, preserves user text,
active/suppressed flags and deactivated markers, and freezes rendered output for
other child keyword types. It deliberately discards editor Parent references;
those references are not consumed by the exporter. Cyclic child trees are
rejected. This is an export snapshot, not a general editable model clone.
The writer uses it for both keyword insertion and suppression-state evaluation.

CalGravityLoad.CreateForExport and CalCentrifLoad.CreateForExport capture the
values consumed by their writers in independent load instances. They receive
an explicitly resolved region, retain amplitude, complex phase and centrifugal
axis settings, and leave the original mass-section region unchanged. Equations
are evaluated when the export copy is made. This replaces the previous clone-
then-change-region path; existing keyword constructors remain available.

Validation: `bash scripts/test-portable.sh` passes 3,581 checks, with 26 new
assertions across en-US/it-IT for nested keyword ownership, flags, deactivated
markers, rendered equation data, parent isolation, cycle rejection, resolved
load regions, amplitude/phase and independence from later source edits.
Centrifugal checks cover both ordinary and axisymmetric loads.
`git diff --check` passes.

The full writer still does not compile in the portable project, so these checks
exercise the production copy APIs directly, not full-exporter wiring. Windows
compilation and native solver runs were not performed in this slice. The scoped
native acceptance exporter does not exercise these paths.

Remaining integration: establish whole-model copy/inactive-part ownership,
reconnect variable/imported pressure and submodel dependencies, compile the actual
writer, then run native acceptance through its entry point and validate caller
state on both success and failure.


Later integration: the production writer now builds and native acceptance uses
its prepared-model file API. See [current evidence](CALCULIX_FULL_WRITER_EVIDENCE.md);
exporter-exclusion statements above describe the earlier slice.

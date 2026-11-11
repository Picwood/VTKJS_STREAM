# Production CalculiX exporter on Linux

Validated 2026-09-16. Portable CaeModel compiles 230 shared production files,
including CalculixFileWriter, model-dependent keyword constructors, variable
pressure conversion and the submodel keyword. OpenFoamFileReader also compiles
in portable CaeResults; compilation alone is not OpenFOAM workflow acceptance.

The acceptance generator now calls the production writer instead of assembling
individual keywords. FeModel owns the mesh, material, section, load and step graph.

## Portable API boundary

- GetInputText generates a complete deck for a prepared model.
- WritePreparedModel generates that deck before opening the destination, then
  writes UTF-8 without a byte-order mark. Model-generation failures preserve an
  existing destination; filesystem failures are not an atomic-write guarantee.
- Models with inactive mesh parts are explicitly rejected. The caller must
  prepare the export model; these APIs do not silently delete parts or claim a
  portable whole-model clone. Export is synchronous and requires exclusive use
  of the model.
- The existing Write method remains in CalculixFileWriter.Legacy.cs, retaining
  Windows whole-model cloning and inactive-part removal. Automatic preparation
  is still pending on Linux.

## Validation

The portable suite passes 3,603 checks. New complete-writer checks exercise
user-keyword insertion, repeatability, original collection/element identity after
success and invalid-region failure, rejection of inactive parts without removal,
file output equivalence, UTF-8 without BOM, and destination preservation on
model rejection. They run under en-US and it-IT.

`python3 scripts/test-native-calculix.py` now routes file generation through
WritePreparedModel. It exercises single/two-step cubes, the long bar, compression
and zero load with native CalculiX. Production FRD/DAT readers validate cube
results; independent analytic DAT checks validate the parameterized bars.
Locale comparisons exclude only the random model hash in the heading, because
separately constructed FeModels have different identities.

The earlier scoped-exporter equation constraints are not added by the current
acceptance model. Constraint keyword checks remain, but these native cases do
not establish native constraint/contact/thermal/dynamic parity.

The .NET 10 solution build and git diff --check pass. Legacy CaeModel project
reference metadata was repaired: each newly added source is included once at
project level. Full Windows compilation was not run. Native artifacts are
retained in `/tmp/prepomax calculix job uw017toi` for this validation run.

## Remaining gates

Automatic export-model copying and inactive-part handling, broader model/solver
coverage, portable project save/reopen, Linux host/controller integration,
remaining renderer retirement and distribution packaging remain open. Full
application build/run support is not claimed by this exporter milestone.


Later integration: [active export preparation](ACTIVE_EXPORT_EVIDENCE.md) adds
GetActiveInputText/WriteActiveModel with mesh snapshot ownership and inactive-part
removal. The prepared-model API and legacy Write method retain their contracts.

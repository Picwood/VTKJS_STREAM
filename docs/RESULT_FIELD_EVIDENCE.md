# Portable result fields

Updated: 2026-09-14.

The portable CaeResults project now links 15 production source files, adding
`Field`, `FieldComponent` and `FieldData` to the existing DAT/history slice.
The existing principal-value helper was moved into the shared `Tools.Results.cs`
partial, used by both legacy and portable builds. The numerical algorithm is
unchanged. Field dictionaries explicitly use the application's ordered dictionary
to avoid ambiguity with the newer framework type.

The CaeResults characterization executable runs 46 checks across `en-US` and
`it-IT`, including 30 new field checks:

- Displacement magnitudes and extrema for known vectors.
- Independent component arrays after copying a field.
- Von Mises and Tresca stress for uniaxial, hydrostatic and pure shear states.
- Repeated invariant computation.
- Binary field round trips, invariant flags and complete payload consumption
  using format version 1.4.0.
- Copied step, increment, time and component metadata.

Run `bash scripts/test-portable.sh` for the complete portable suite.

This is a dependency slice for FRD ingestion. The production FRD reader still
depends on the full `FeResults` aggregate, which is not yet included. These tests
do not establish FRD import, complex field compatibility, exhaustive numerical
accuracy, legacy file compatibility or desktop result visualization. The native
CalculiX cube solve and DAT ingestion are recorded separately in
[solver integration evidence](CALCULIX_INTEGRATION.md).

## Constant assignments and cached extrema

`FieldComponent.SetValuesTo` now refreshes its cached extrema after replacing
values. `SetValuesToZero` delegates to that method. Previously field-level
constant assignments left old minimum/maximum values and indices in place.
Four checks per locale cover zeroing a nonconstant field, assigning a nonzero
constant, assigning missing values, and returning a missing field to zero.
The zeroing assertion failed before the fix. This is shared production behavior
for the portable and legacy builds; it does not change direct array mutation,
which still requires an explicit `UpdateMaxMin` call.

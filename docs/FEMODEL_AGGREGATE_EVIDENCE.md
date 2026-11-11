# FeModel aggregate on Linux

Implemented 2026-09-16. Portable CaeModel now compiles 217 shared production
source files, including the real FeModel aggregate and its supporting parameters,
views, cylinders, contacts, distributions, initial conditions, remaining step
classes and additional keyword writers. Build inclusion is not workflow parity
for all these types.

FeModel is now partial. Its Windows project includes FeModel.Legacy.cs, which
retains existing implementations of whole-model cloning/copy construction,
boundary-displacement-model preparation, user-keyword state updates, unsupported
mesh/model import paths, and variable-pressure conversion methods. Those methods
are absent from the portable API; they are not replaced with stubs. Existing
STL/MMG and in-memory mesh entry points remain in the shared aggregate.

Shared code retains model-owned collections, material/section assignment,
region validation, equation parameters, geometry/mesh state, constant and
supported distribution-based traction operations. CaeGlobals.OrderedDictionary
is qualified to avoid ambiguity with the .NET 10 collection. Five previously
excluded .Legacy.cs files now compile because their FeModel-dependent contracts
or previews are available; those filenames reflect the earlier build boundary.

Desktop geometry warnings and parameter-error dialogs remain in partial hooks.
The portable build does not display the advisory geometry warning. Failed model
parameter publication throws CaeException on Linux instead of invoking a UI
error dialog; the Windows hook retains its dialog behavior. There is no new
persistence format or claim of legacy .pmx compatibility.

## Integration and validation

StaticModelWorkflow now owns an actual FeModel. Mesh, materials, sections and
steps live in its collections; export invokes FeModel's surface-traction dispatch.
The keyword assembly is still the scoped acceptance writer, not CalculixFileWriter.

- `bash scripts/test-portable.sh`: 3,533 checks pass. Twenty new assertions across
  en-US/it-IT check aggregate ownership, case-insensitive lookup, section/material
  assignments, missing/restored regions, traction dispatch and model parameters.
- `python3 scripts/test-native-calculix.py`: single/two-step cubes, long bar,
  compression and unloaded cases pass. English and Italian inputs match;
  production FRD/DAT readers validate cubes and independent analytic displacement
  checks validate the parameterized bars.
- `git diff --check`: passes. Existing-source compiler warnings remain.

Native artifacts: `/tmp/prepomax calculix job fe66tgm2` (temporary local files).
Windows compilation was not run; the moved legacy methods need Windows regression
coverage. Contacts, additional steps, distribution/imported-load previews and
other newly compiled types do not yet have full native workflow acceptance.

## Next exporter gate

A compile probe of the actual CalculixFileWriter identified these remaining seams:

1. Write clones the entire model and removes inactive parts. Establish portable
   copy ownership without BinaryFormatter or mutating the caller's model.
2. Temporary surface/element-set copying and restoration are now extracted and
   tested; see [export-state evidence](CALCULIX_EXPORT_STATE_EVIDENCE.md).
   User keyword trees and gravity/centrifugal loads now have explicit export
   copies; see [copy evidence](CALCULIX_EXPORT_COPY_EVIDENCE.md).
3. Reconnect variable-pressure conversion and imported-pressure/OpenFOAM
   boundaries, plus the submodel filename encoding helper.
4. Switch native acceptance to the complete exporter and test inactive entities,
   caller-state preservation, constraints, contacts and user keywords.

The full exporter is deliberately excluded until these implementations work.
Linux UI hosting, project reopen and packaging remain separate application gates.


Later integration: the production writer now builds and native acceptance uses
its prepared-model file API. See [current evidence](CALCULIX_FULL_WRITER_EVIDENCE.md);
exporter-exclusion statements above describe the earlier slice.

# Shared surface traction integration

Implemented on 2026-09-16 as the next model-to-solver integration slice.

`CaeModel/SurfaceTractionConverter.cs` extracts the production FeModel constant
surface-traction and equivalent nodal surface-weight algorithms. Both the legacy
Windows project and portable CaeModel compile this same file. FeModel delegates
to it, retaining its public methods. Shell edge integration obtains thickness
through a caller-supplied element-ID resolver; solid faces need only the mesh.
FeModel lazily resolves section assignments once per conversion and excludes mass
sections from the thickness index, matching GetSectionAssignments numbering.

The converter preserves total force components, equivalent element face weights,
shared-node accumulation, amplitude and complex phase. Nonzero force on an empty
or nonpositive/nonfinite-area surface now raises CaeException instead of yielding
empty or invalid loads. Shell edges require finite positive thickness. Zero total
force retains the empty-load result. These validation changes also affect the
legacy caller.

The acceptance cube/bar now stores STLoad objects on a real element-face surface
and converts them during export. It no longer computes quarter forces in test
assembly code. This remains a scoped acceptance exporter, not the complete
FeModel/CalculixFileWriter pipeline.

Validation:

- `bash scripts/test-portable.sh`: 3,483 checks pass, including 10 new checks for
  loaded nodes, vector conservation, phase/amplitude, actual face area, shell
  thickness and shared-node weighting, missing/zero thickness, empty surfaces,
  and zero force. Existing export checks cover independent step forces and edits.
- `python3 scripts/test-native-calculix.py`: native single/two-step cubes, long
  bar, compression and unloaded cases; locale-identical inputs; production FRD/DAT
  import for cubes and independent analytic displacement checks for bars.
- `git diff --check`: passes.

Native run artifacts: `/tmp/prepomax calculix job fy9_llqc` (temporary local
files, not repository fixtures). Windows compilation was not run here. Full
FeModel remains outside the portable build, so its section-resolution adapter
still needs Windows/full-aggregate regression coverage. Shell integration has
portable checks but no native shell acceptance in this slice.

Next: continue the real aggregate/export boundary, including variable and
imported traction/pressure conversion, constraints, contacts and user keywords.
Project reopen, Linux host, physical GPU acceptance and packaging remain open.

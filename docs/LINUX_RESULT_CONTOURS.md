# Portable archived-result contours

Validated 2026-10-08. Editable result archives can supply static nodal fields
from FRD or a PMA-v4 binary result payload to the portable renderer. The GTK field selector lists field/component,
step, increment and time, and includes a Mesh geometry entry to clear contours.
Selection republishes the scene and bootstraps the client to its current scene ID.
An optional displacement scale exports deformed geometry from the matching DISP
frame; scale zero retains the reference geometry.

`ProjectResults` reads archived FRD or PMA-v4 `results.bin` with production
CaeResults code. For FRD it checks
node IDs/coordinates and element types/connectivity against the editable mesh,
rejects unknown fields/frames and non-finite/missing values, and maps scalar data
by source node ID. Unit labels come from the editable model's unit system. The
reader uses an isolated temporary directory and removes it after parsing.
PMA-v4 binary results use the bounded streaming path documented in
[result streaming](LINUX_RESULT_STREAMING.md), avoiding construction of the
editable model for field catalog, fingerprint and result-scene operations.

The host sends field metadata before replacing the scene because the renderer
applies metadata during replacement. Returning to geometry resets the field
label instead of retaining a stale result name. A rejected field export leaves
the existing browser bootstrap available. These checks do not prove arbitrary
archived results correspond to the current loads/materials: a structurally
matching model can still contain stale results after edits.

## Evidence

- `bash scripts/test-portable.sh`: 5,141 passing checks, including 2,126 mesh I/O
  assertions. Result checks cover FRD and binary-result archive reopening, two
  frames, analytical U1 values mapped by node ID, unavailable fields, source
  preservation, mismatched meshes, contour export and deformation under English
  and Italian cultures.
- `scripts/test-viewer-fields.py DOTNET TWO_STEP.pma SERVER.dll`: actual server
  transport preserves loaded-face U1=0.001 and U1=0.002 in successive steps,
  field/component labels and scalar ranges. The test downloads and decodes the
  authenticated binary resources, tests invalid-field recovery, restores geometry
  metadata and verifies unchanged archive bytes.
- GTK `--smoke --smoke-field DISP/U1`: the native selector loads the archived
  field and the browser acknowledges the resulting scene before renderer shutdown.
- `PrePoMax.Cli result-fingerprint` reports the exact SHA-256 of sorted node-ID
  and IEEE-754 value pairs, units, extrema, mean and five deterministic samples.
  Tests prove FRD and PMA-v4 binary payloads produce the same fingerprint.
  Each hashed pair is the little-endian signed 32-bit node ID followed by the
  little-endian raw bits of its single-precision value.
- Viewer companion builds with zero warnings/errors. Tests use the existing
  net8.0 renderer with explicit .NET 10 runtime roll-forward on this workstation.

Generate the deterministic two-step acceptance archive after building the
characterization executable:

```bash
dotnet portable/tests/MeshIO.Characterization/bin/Release/net10.0/MeshIO.Characterization.dll \
  --write-result-project /tmp/two-step-result.pma
dotnet portable/PrePoMax.Cli/bin/Release/net10.0/PrePoMax.Cli.dll \
  result-fingerprint /tmp/two-step-result.pma DISP U1 1 1 1 --json
```

Use that archive with the GTK launch command in [the GTK spike](LINUX_GTK_VIEWER_SPIKE.md),
adding `--smoke --smoke-field DISP/U1` for automation. The transport test and GTK
probe are opt-in integration checks, separate from the portable assertion total.

This milestone covers static real-valued nodal contours on the supported linear
hexahedral mesh. [Displacement scaling](LINUX_DEFORMED_RESULTS.md) now adds deformed geometry
with numerical and zero-scale restoration checks. Animation, complex/harmonic
selection and manual visual/GPU acceptance remain open.

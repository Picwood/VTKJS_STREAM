# Linux result-reference capture

Validated: 2026-10-12.

`scripts/capture-linux-result-reference.mjs` creates a durable comparison bundle
for one exact result field and frame. It drives the production viewer companion,
waits for the browser's stable-frame acknowledgement, selects an isometric
camera, and records:

- a PNG containing the rendered frame, legend, and viewer controls, cropped to
  exclude the diagnostics panel's random scene/session identifiers;
- browser/renderer diagnostics;
- the scene actor, point, and triangle counts;
- the deterministic node/value fingerprint from `PrePoMax.Cli`;
- project and artifact SHA-256 hashes plus runtime versions.

The capture uses Playwright Chromium with SwiftShader so that repeated reference
captures do not depend on the workstation's physical GPU. It is evidence for
data transport and a reproducible Linux rendering reference. Human comparison
with the original application and physical-GPU qualification remain separate.

The output directory must not exist. Capture is fail-closed through a temporary
directory, and the command hashes the source project before and after the run.
It publishes the completed directory only when the selected field exists, the
replacement scene is acknowledged, the fingerprint succeeds, and the source is
unchanged.

The supplied BRAKE PMA was captured locally as
`BRAKE_537037-V2-linux-result-reference-v2`. The accepted frame contains 7
actors, 714,210 visible points and 1,428,456 triangles. Its 1,775,349-value
fingerprint is
`A99283AB16B32876FBD17BA0293CCD9672DCEABDFEA999B4CB259B97F89AC4A2`;
the scalar range is 0.000012014192–644.1169 MPa. End-to-end capture took 46.42
seconds and peaked at 3,535,688 KiB on this workstation. The external project
and generated bundle are not repository fixtures.

From the repository root, after building the portable solution, renderer server,
and client:

```bash
node scripts/capture-linux-result-reference.mjs \
  "$HOME/.local/share/prepomax/dotnet/dotnet" \
  /path/to/project.pma \
  "$PWD/prototypes/VtkJsStreaming/src/PrePoMax.Render.Server/bin/Release/net8.0/PrePoMax.Render.Server.dll" \
  "$PWD/prototypes/VtkJsStreaming/src/client/dist" \
  /path/to/new-reference-directory \
  STRESS MISES 1 4 1 0
```

`reference.json` is the comparison entry point. Compare `ResultFingerprint`
first; matching screenshots cannot compensate for different node/value data.
Then compare scene topology and the PNG against an original-application capture
made with the same field, frame, undeformed scale, camera orientation, viewport,
part visibility, and scalar range.

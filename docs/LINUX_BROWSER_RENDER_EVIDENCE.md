# Portable project rendered in a Linux browser

Validated 2026-09-22. The new `scripts/test-browser-host.mjs` runs a real Chromium
client against `RendererHost` and an editable multipart project. A test-only .NET
helper owns the private renderer process; anonymous redirected pipes carry
bootstrap messages between it and the browser adapter. Credential-bearing pipe
responses are not written to logs or files. The helper refuses terminal I/O.

The adapter gets navigation URL and frame identity from Playwright's native
binding callback. It calls the production `TryBootstrapBrowser` boundary and
rechecks the same main frame and URL before delivering the message through the
client's existing embedded-WebView interface. This adapter is acceptance tooling,
not a selected Linux desktop implementation.

Observed acceptance:

- Production renderer starts and receives a two-part editable project's geometry.
- Wrong-origin readiness receives no credentials.
- Client adopts the session and downloads the authenticated scene/resources.
- Browser reports two pipelines, 16 points and 24 triangles, and acknowledges a
  stable frame for the published scene with two actors.
- Resize retains the pipelines; page reload bootstraps and acknowledges the same
  published scene again.
- Closing the test closes the browser and disposes the renderer host.

Reproduce from the repository root after `npm ci` in the client directory and
`npx playwright install chromium` there:

```bash
npm run build --prefix prototypes/VtkJsStreaming/src/client
dotnet build portable/tests/BrowserHost.Characterization -c Release
dotnet build portable/tests/MeshIO.Characterization -c Release
dotnet build prototypes/VtkJsStreaming/src/PrePoMax.Render.Server -c Release
# Generate the deterministic multipart project in an existing temporary directory:
dotnet portable/tests/MeshIO.Characterization/bin/Release/net10.0/MeshIO.Characterization.dll \
  --write-multipart-cube /tmp/cube.inp 1 en-US
node scripts/test-browser-host.mjs /absolute/path/to/dotnet /tmp/cube.inp.pma \
  "$PWD/prototypes/VtkJsStreaming/src/PrePoMax.Render.Server/bin/Release/net8.0/PrePoMax.Render.Server.dll" \
  "$PWD/prototypes/VtkJsStreaming/src/client/dist"
```

Optional `PREPOMAX_BROWSER_SCREENSHOT` selects an output PNG. Local run used
Node 22.16.0, the lockfile's Playwright 1.62.1 Chromium headless shell 151.0.7922.34,
SwiftShader, a 1200×800 viewport resized to 900×600, and .NET 10 with explicit
`DOTNET_ROLL_FORWARD=Major` for the net8.0 server. The screenshot was saved at
`/tmp/prepomax-browser-frame.png`; temporary files are not durable evidence.

This is functional Chromium reference evidence. The later GTK host provides
result contours, packaging, a production WebView/navigation adapter and recorded
DRM-backed WebGL acceptance. PEND-006 is complete; PEND-001 and PEND-005 remain
open for large-model performance and human visual acceptance.

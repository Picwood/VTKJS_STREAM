# VTK.js streaming renderer prototype

This prototype implements the vertical slice described in the repository-level `VTKJS_STREAMING_RENDERER_SPEC.md`. The protocol projects remain isolated from ActiViz; the copied PrePoMax application now contains an opt-in integration adapter that publishes real part surfaces to the renderer.

## Current status

WP0–WP5 are complete. The canonical renderer streams 3,000,000 unique source nodes and 5,993,002 triangles through bounded generated resources, renders progressively, performs scalar-only updates, and maps spatially culled GPU picks to server-validated source IDs. All initial quantitative gates passed on the documented Windows/Intel integrated-GPU reference run. Ubuntu 22.04 Linux x64 also passes the self-contained server and Chromium/WebGL2 functional gate; a physical Linux GPU performance run remains pending and is not inferred from software WebGL.

The WinForms MVP additionally transfers a real 15-part PMX surface snapshot (828,619 points and 1,623,416 triangles) through the credentialed host bridge. See [docs/progress.md](docs/progress.md) for evidence and known gaps.

Outstanding production, platform, hardware, and optional-parity work is prioritized with explicit prerequisites and completion evidence in the [pending-work register](../../docs/VTKJS_PENDING_WORK.md).

To launch the built WinForms application with the VTK.js visible viewport from the repository root, run `.\scripts\start-prepomax-vtkjs.ps1`, optionally with `-ModelPath`. ActiViz assemblies are still required at application startup; see [the dependency status and removal gate](../../docs/VTKJS_ACTIVIZ_STATUS.md).

## Prerequisites

- .NET 9 SDK 9.0.3xx or a later 9.0 feature band capable of targeting `net8.0`
- Node.js 22+
- npm 10+

The prototype targets .NET 8 because it is a supported cross-platform runtime baseline. `global.json` selects the reproducible .NET 9 SDK feature band available to the project while preventing accidental prerelease or major-version roll-forward.

## Build and test

PowerShell:

```powershell
dotnet build .\VtkJsStreaming.sln -c Release
dotnet test .\VtkJsStreaming.sln -c Release --no-build
Push-Location .\src\client
npm ci
npm test
npm run build
npm run test:e2e
Pop-Location
..\..\scripts\audit-vtkjs-prototype-isolation.ps1
./scripts/benchmark.ps1
# Optional Windows legacy differential check (requires ActiViz 5.8):
./scripts/compare-frustum-corpus-activiz.ps1
./scripts/compare-frustum-randomized-activiz.ps1
```

Bash:

```bash
dotnet build ./VtkJsStreaming.sln -c Release
dotnet test ./VtkJsStreaming.sln -c Release --no-build
(cd ./src/client && npm ci && npm test && npm run build && npm run test:e2e)
./scripts/benchmark.sh
```

## Development startup

From this directory, run `./scripts/run-dev.ps1` on PowerShell or `./scripts/run-dev.sh` on Bash. In this explicit development mode, the backend listens on `127.0.0.1:5174` and permits the Vite origin at `127.0.0.1:5173`. Without `PREPOMAX_RENDER_PORT`, the backend selects an ephemeral IPv4 loopback port.

The development session endpoint returns its token for manual protocol testing. This is an explicit development convenience and must not be the production bootstrap mechanism. Vite development mode receives a style-only CSP allowance for its injected stylesheet; production builds retain the stricter external-style policy.

Lifecycle limits default to an eight-hour session lifetime, 64 concurrent sessions, and 30 commands per session per one-second fixed window. They can be tightened with `PREPOMAX_RENDER_SESSION_MINUTES` (1–1440), `PREPOMAX_RENDER_MAX_SESSIONS` (1–1024), and `PREPOMAX_RENDER_COMMANDS_PER_SECOND` (1–1000). Capacity exhaustion returns retryable HTTP 503; command throttling returns retryable HTTP 429 with `Retry-After`.

## Protocol smoke test

After reading the backend URL from its startup output:

```powershell
$session = Invoke-RestMethod -Method Post http://127.0.0.1:<port>/api/v1/sessions -SessionVariable renderSession
Invoke-RestMethod http://127.0.0.1:<port>/api/v1/scenes/$($session.sceneId)/manifest -WebSession $renderSession
```

Bulk resources are raw little-endian arrays. Do not deserialize them as JSON.

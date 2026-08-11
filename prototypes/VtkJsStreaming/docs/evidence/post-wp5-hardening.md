# Post-WP5 hardening evidence

Date: 2026-08-06  
Host: Windows x64

## Implemented acceptance items

- Expiring random render sessions with a configurable hard capacity.
- Per-session command fixed-window throttling with bounded limiter state.
- Stable `session-capacity-reached`, `command-rate-limited`, and `command-unsupported` errors.
- Terminal `server.shutting-down` delivery followed by WebSocket close status 1013.
- Request-abort propagation remains connected to generated-resource delay and streaming paths.

## Verification

| Command | Result |
|---|---|
| `dotnet format VtkJsStreaming.sln --verify-no-changes --no-restore` | Pass; no formatting changes required |
| `dotnet build VtkJsStreaming.sln -c Release --no-restore` | Pass; 0 warnings, 0 errors |
| `dotnet test VtkJsStreaming.sln -c Release --no-restore` | Pass; 23/23 tests across two assemblies |
| `npm test -- --run` | Pass; 11/11 tests |
| `npm run build` | Pass; strict TypeScript and production Vite build |
| `npm run test:e2e` | Pass; 2/2 Playwright scenarios |

The server tests include direct time-controlled expiry/capacity tests, independent session rate windows, real HTTP 429/`Retry-After` verification, direct event-hub completion, and a real in-process WebSocket assertion that the terminal envelope arrives before close status 1013.

## Remaining acceptance gaps

- Linux x64 verification cannot be inferred from this Windows host.
- A process-level shutdown smoke test remains for the eventual packaged controller/host integration; current coverage exercises the registered lifecycle service's event-hub behavior in-process.
- The production client still emits the documented approximately 720 KB single-chunk warning; bundle splitting is the next local optimization item.

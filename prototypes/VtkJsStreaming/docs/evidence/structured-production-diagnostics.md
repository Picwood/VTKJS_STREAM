# Structured production diagnostics evidence

Date: 2026-09-02
Status: PEND-003 complete

## Implemented boundary

The server now emits source-generated structured application events with fixed numeric IDs and named fields. Values are limited to fixed operation names, server-generated request correlations, canonical UUIDs, numeric metrics, status values, and fixed safe reason codes. Raw paths, query strings, request/response bodies, credentials, model names, and exception text are not application-log fields. Production suppresses the remaining framework categories that emit raw request URLs, endpoint templates, or physical content roots.

| Event ID | Event | Correlation and bounded fields |
|---:|---|---|
| 6000 | server started | protocol and server versions |
| 6001 | server stopping | protocol and server versions |
| 6010 | Production session rejected | fixed safe reason |
| 6011 | session created | canonical session/scene UUIDs |
| 6012 | session reused | canonical session/scene UUIDs |
| 6013 | browser session adopted | canonical session/scene UUIDs |
| 6020 | selected request completed | fixed operation, safe request ID, session/scene/command/correlation UUIDs, status, elapsed milliseconds, response bytes, cancellation |
| 6021 | resource completed | safe request ID, session/scene/resource UUIDs, status, elapsed milliseconds, response bytes, cancellation |

The selected request set covers session creation/adoption, manifest and diagnostics reads, section resolution, event-stream lifecycle, commands, host scene replacement, and host shutdown. Resource traffic has its own event. Animation-frame and other high-frequency host mutations are deliberately excluded, so the implementation does not log per node, triangle, glyph, or animation frame.

`GET /api/v1/diagnostics` now includes protocol/server versions, request/resource counts and bytes, cancellation count, and the last safe HTTP error code. Every API response includes `X-PrePoMax-Server-Version`, exposed through Development CORS without exposing credentials.

The downloadable client JSON now includes protocol/client/server versions; session and scene IDs; viewport, DPR, WebGL vendor/renderer and limits; actor/chunk/resource and source/render tuple counts; planned/transferred/decoded bytes; load/scalar/pick/frame timings; memory/GPU statistics; resource retry and cancellation counters; context-loss count; and the last bounded safe error code. Human-readable error text remains UI-only and is not copied into diagnostics.

## Automated security and correlation evidence

`RenderTelemetryTests.ProductionTelemetry_IsStructuredCorrelatedAndContainsNoSecretsOrPhysicalPaths` captures the real Production logger pipeline. It proves lifecycle/session/request/resource event IDs, resource/session IDs, command message/correlation IDs, version headers, counters, and last-error state. It also proves captured messages exclude the host credential, session credential, current physical root, and raw API paths.

Client unit coverage proves bounded version capture and retry/cancellation accounting. Playwright proves the complete diagnostic schema on a WebGL scene, exact count/byte values, no credential/path leakage, and `resource-hash-invalid` after retry exhaustion followed by clean recovery.

## Canonical 3M overhead measurement

Two sequential Windows headless Chrome 152 runs used the unchanged 2000x1500 canonical scene, 30 chunks, concurrency 3, and 8 MiB resource cap. The first used normal Information-level structured events; the second suppressed `PrePoMax.Render.Server` below Warning. These are software/headless observability measurements, not replacements for the committed physical-GPU qualification.

| Metric | Structured events | Category suppressed | Observed delta |
|---|---:|---:|---:|
| complete initial load | 3306.7 ms | 3189.3 ms | +117.4 ms (+3.68%) |
| manifest to first visible chunk | 78.4 ms | 73.6 ms | +4.8 ms |
| longest load task | 58 ms | 51 ms | +7 ms |
| backend managed peak above baseline | 11,875,528 bytes | 11,174,400 bytes | +701,128 bytes (+0.67 MiB) |
| initial resource events | 150 | counters retained: 150 | exactly one per resource |
| initial resource bytes | 157,048,032 | counters retained: 157,048,032 | exact descriptor total |
| initial correlated requests | 152 | counters retained: 152 | session + manifest + 150 resources |

Both runs passed all functional, progressive-loading, memory, selection, and lifecycle assertions. Single paired timings include normal run-order and headless variance, so the 3.68% figure is an observed envelope rather than a universal physical-GPU estimate. The bounded event cardinality and sub-1 MiB managed-peak difference show no size-dependent or per-primitive amplification.

Raw results:

- [structured logging enabled](structured-diagnostics-3m-headless-2026-09-02.json)
- [structured logging category suppressed](structured-diagnostics-3m-headless-logging-disabled-2026-09-02.json)

## Windows verification

- Server Release tests: 69/69.
- Client tests: 57/57.
- TypeScript/Vite production build: passed.
- Playwright/WebGL correctness: 2/2.
- Canonical 3M headless observability runs: 2/2.
- Full PrePoMax Release/x64 build and renderer staging: passed with the repository's existing warnings.
- Production host smoke: health/client/session/shutdown passed; child exited; `SensitiveLogDataAbsent=true`.
- Model-backed packaged WinForms smoke: 1 actor, 12 points, 4 triangles, 4 locator points, 1 locator cell, browser camera forwarded, scalar range 0..1, graceful close, child exited.

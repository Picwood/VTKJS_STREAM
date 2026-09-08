# Production session bootstrap decision and evidence

Date: 2026-09-01  
Status: accepted and implemented for the Windows MVP

## Decision

Production session creation is desktop-mediated. PrePoMax generates a 256-bit base64url host-control credential in memory, passes it only to its owned renderer child through the child environment, and supplies it in `X-PrePoMax-Host-Token` when creating a browser session. Anonymous or incorrectly credentialed `POST /api/v1/sessions` requests receive 401 in Production. Development retains anonymous creation for deliberate browser/test convenience.

The embedded client no longer autoloads. After registering its message handlers it sends `prepomax.client.ready-v1`. The WinForms host accepts that message only from the renderer's exact scheme/host/port, creates or reuses its in-memory browser session, and sends `prepomax.session.bootstrap-v1` with the session ID, scene ID, and 256-bit session credential through `CoreWebView2.PostWebMessageAsJson`. The credential is never placed in a URL, command line, trace, or persistent file. The client strictly validates the envelope, calls authenticated `POST /api/v1/sessions/adopt`, receives the same-origin HttpOnly/SameSite=Strict cookie, and only then requests the manifest and opens the WebSocket.

Repeated ready messages are idempotent while the credential remains valid: the desktop supplies its cached session credential and the server returns that session rather than consuming another capacity slot. Normal session expiry remains authoritative; expired bearer credentials are rejected and removed. A stolen live bearer credential remains usable until expiry, as with every session bearer, so protection rests on cryptographic entropy, exact-origin WebView delivery, loopback-only binding, no secret logging/persistence, and OS protection of the trusted desktop/child processes.

Production emits the path-neutral `PREPOMAX_RENDER_READY=http://127.0.0.1:<port>` readiness line. This replaced reliance on framework lifetime logging, which disclosed physical content roots. It contains no credential or filesystem path.

## Threat model

Protected against: an unrelated loopback client minting a render session; guessed host/session credentials; cross-origin WebView messages; URL/history/referrer leakage; accidental credential logging; session-capacity exhaustion through repeated WebView readiness; and use of expired sessions.

Out of scope: a process already able to read or modify PrePoMax/renderer process memory, inject code into WebView2, or act with equivalent user/debug privileges. Such a process already crosses the desktop trust boundary.

## Verification

- Release server tests: 68/68. The production integration test proves anonymous and incorrect-host-token rejection, authorized creation, idempotent reuse, authenticated adoption, and HttpOnly/SameSite=Strict cookie attributes. Existing lifecycle coverage proves capacity and expiry.
- Client tests: 54/54. Bootstrap-envelope tests reject wrong type, malformed UUIDs, wrong token length, and non-base64url tokens.
- TypeScript/Vite production build: passed.
- Full `PrePoMax.sln` Release/x64 build and renderer staging: passed; only pre-existing project warnings were emitted.
- `scripts/smoke-vtkjs-host.ps1`: health 200, client 200, authorized session creation, graceful host shutdown 202, child exited, and `SensitiveLogDataAbsent=true` for the host token and physical roots.
- `scripts/smoke-vtkjs-winforms.ps1`: PrePoMax, renderer child, and WebView2 started; graceful close removed the child.
- Model-backed packaged smoke with `sphere-symbol-smoke.inp`: 1 actor, 12 render points, 4 triangles, 4 locator points, 1 locator cell, host camera forwarded, scalar range 0..1, graceful close and child exit. This proves the bootstrap advanced through cookie adoption, manifest/resources, WebSocket, and browser scene application.

The Linux lifecycle/browser scripts were updated to use the same Production host authorization and WebView bootstrap protocol. Native Linux execution remains a separate platform run, not claimed by this Windows evidence.

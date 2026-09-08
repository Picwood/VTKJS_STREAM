# Architecture, performance, security, and domain review audit

Date: 2026-09-01

This document supports specification section 23. A checked review item means the MVP evidence is sufficient for that review question; it does not imply that every production-roadmap item is complete. Open production work is tracked separately in the repository-level [pending-work register](../../../../docs/VTKJS_PENDING_WORK.md).

## Architecture

- C# owns scene preparation, full-cell topology, field/extrema metadata, section derivation, and final semantic selection. Browser picks and gestures remain candidates until server and controller validation.
- Protocol records and binary envelopes contain immutable values, IDs, arrays, and renderer-neutral material/camera state. They expose neither mutable FE objects nor VTK.js objects.
- Scene revisions, field versions, resource IDs, immutable hashes, and stale-revision rejection are explicit and covered by contract/server/browser tests.
- The specification ownership table and phased roadmap assign surface preparation, semantic queries, derived geometry, presentation, and animation. The current implementation advances several later phases without changing those boundaries.

## Performance

- The generator and browser independently assert 3,000,000 unique source nodes, 2,996,501 logical elements, and 5,993,002 render triangles.
- Raw resource bytes, browser heap/backing store, VTK graphics memory, server working set/managed heap, per-frame allocations, and five-cycle retention are recorded in committed JSON and reports.
- Manifest, first-visible full-resolution chunk, complete load, and first stable frame are distinct measurements.
- Orbit uses 60 frame samples per run at a documented 1920×1080 viewport/DPR 1; picks use 30 samples and p50/p95 reporting; medians use three fresh physical runs.
- Scalar activation asserts exactly one Float32 scalar per resident point tuple and unchanged topology/resource identities.

## Security

- Kestrel binds loopback only; production uses an ephemeral port when no explicit port is supplied. Sessions use 256 bits of cryptographic randomness.
- The same session authorization middleware covers manifest, resource, diagnostics, command, and WebSocket routes. Host mutation/shutdown routes require a separate random host credential.
- Origin/CORS/WebSocket-origin and CSP policies are retained as defence in depth; credential checks are independent.
- Resource descriptors, request paths, body lengths, counts, UUIDs, ranges, hashes, and host binary envelopes are bounded and validated.
- Production framework categories that reveal physical content roots are suppressed. Linux production smokes fail if captured logs contain the host credential, publish root, or client root. Playwright fails if downloadable browser diagnostics contain the test host credential or Windows/URI/WSL filesystem paths.
- Production session creation requires the separate desktop host credential. The exact-origin WebView readiness/bootstrap handoff uses no URL, log, command-line, or persistent-file secret; authenticated adoption establishes the HttpOnly/SameSite=Strict WebSocket cookie. Negative, reuse, expiry, and packaged model-loading evidence completes PEND-002.
- Source-generated event IDs provide bounded lifecycle/session/request/resource records with session, scene, request, and command correlations. Capture tests exclude credentials, physical roots, and raw URLs; client/server diagnostics now cover required versions, counts, bytes, retries, cancellations, context loss, and safe error state. Paired canonical 3M headless evidence bounds observed logging overhead and completes PEND-003.

## PrePoMax domain mapping

- Actor parent/layer/material/name/source-point/source-element semantics are explicit in the manifest and host envelopes and exercised with real PMX/FRD scenes.
- Surface tessellation preserves legacy triangle/quad/quadratic rules; source element IDs intentionally repeat once per generated triangle. Live ActiViz differentials cover full-cell frustum and section behavior across supported cell types.
- Pick results carry actor/chunk/triangle, source element, candidate node, three authoritative triangle nodes, world/display coordinates, camera direction, and selection precision for controller interpretation.
- `PMXSCN2` already carries separate server-only full-cell locator topology and builds/refits the managed BVH; the browser receives only render topology.
- C# owns scalar metadata and extrema callouts. Full-position animation frames and exploded transforms have implemented owners; displacement-basis transport remains a conditional optimization rather than an ownership gap.

## Fresh checks

- .NET: 78/78 tests passed.
- Playwright: 2/2 tests passed, including diagnostics secrecy and loopback-only traffic.
- Ubuntu 22.04 production lifecycle/browser smoke: passed with `SensitiveLogDataAbsent=true` for both server phases.

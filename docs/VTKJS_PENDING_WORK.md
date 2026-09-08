# VTK.js renderer pending-work register

Last reviewed: 2026-09-03

This is the durable handoff for work intentionally not claimed by the current MVP. Completed functional and reviewer gates are documented in the specification and evidence ledgers; items below must remain open until their stated completion evidence exists. Do not weaken thresholds or infer physical results from headless/software runs.

## Priority order

| ID | Priority | Status | Owner boundary | Work item |
|---|---|---|---|---|
| PEND-001 | P0 | Blocked on suitable hardware | Benchmark/client/server | Native Linux physical-GPU 3M benchmark |
| PEND-002 | P0 | Complete (2026-09-01) | Desktop host/server/security | Trusted production session bootstrap |
| PEND-003 | P0 | Complete (2026-09-02) | Server/client observability | Structured correlated production diagnostics |
| PEND-012 | P0 | Complete (2026-09-02) | Desktop bootstrap/client | Resynchronize WebView after first late non-empty topology |
| PEND-004 | P1 | Ongoing migration | PrePoMax adapter/client | Retire remaining hidden ActiViz compatibility backend |
| PEND-005 | P1 | Requires interactive reviewer | Desktop integration | Manual visual acceptance on representative models |
| PEND-006 | P1 | Decision deferred | Desktop/platform | Linux desktop shell and embedded-WebView selection |
| PEND-007 | P1 | Follows PEND-006 | Packaging/platform | Installers, redistribution, and GPU support matrix |
| PEND-008 | P2 | Ready in supported browsers | Client recovery | Golden-image and WebGL context-loss/OOM automation |
| PEND-009 | P2 | Ready incrementally | Tests/contracts/server | Residual test-matrix hardening |
| PEND-010 | P3 | Conditional | Animation protocol | Retained displacement-basis transport |
| PEND-011 | P3 | Optional visual parity | Client rendering | Camera-dependent engineering-symbol silhouettes |

## PEND-001 — Native Linux physical-GPU 3M benchmark

Why open: WSL2 Chromium software WebGL proves functional portability but cannot qualify physical GPU performance or memory.

Prerequisites: native supported Linux x64; Chrome/Chromium using a documented physical GPU (not SwiftShader/llvmpipe); .NET and Node versions recorded; 1920×1080 viewport and DPR 1; DevTools closed.

Reproduction:

```bash
cd prototypes/VtkJsStreaming
PREPOMAX_BENCHMARK_HEADLESS=0 ./scripts/benchmark.sh
```

Completion evidence: three cold runs; raw JSON plus summary committed; GPU/driver/browser/kernel recorded; every section 15.2 metric marked pass or fail without threshold changes; bottleneck and recommendation recorded for any miss. Update `benchmark-report.md`, `test-matrix.md`, and this register.

## PEND-002 — Trusted production session bootstrap

Completed: Production session creation now requires the desktop host-control credential; Development remains explicitly convenient. The exact-origin WebView readiness/bootstrap handshake transfers the session only through WebView messaging, adopts it into an HttpOnly/SameSite=Strict cookie, and never places secrets in URLs, traces, command lines, or persistent files. Repeated readiness reuses the current session, and existing expiry remains authoritative.

Evidence: [production session bootstrap decision and evidence](../prototypes/VtkJsStreaming/docs/evidence/production-session-bootstrap.md). It records the threat boundary, protocol, negative/reuse/expiry tests, Release/x64 build, lifecycle smoke, and model-backed packaged Windows process smoke.

## PEND-003 — Structured correlated production diagnostics

Completed: source-generated lifecycle/session/request/resource events now use stable IDs and bounded session/scene/request/command correlations. The server and downloadable client diagnostics expose the required version, count, byte, timing, retry, cancellation, context-loss, and safe-error fields. Production framework path/URL logging is suppressed; application events contain no credentials, arbitrary paths, bodies, or high-frequency frame/primitive spam.

Evidence: [structured production diagnostics](../prototypes/VtkJsStreaming/docs/evidence/structured-production-diagnostics.md), including capture-based secrecy/correlation tests, browser schema/recovery checks, packaged Windows smoke, and paired canonical 3M headless logging-enabled/suppressed measurements with committed JSON.

## PEND-004 — Remaining ActiViz compatibility retirement

Why open: many visible capabilities are browser-owned, but the copied application deliberately retains a hidden `vtkControl` for unported queries/mutations and preserves the default legacy renderer.

Completion method: maintain a call-site inventory grouped by capability. For each group, add a renderer-neutral host seam, browser/server ownership, default-path regression, VTK.js compatibility-bypass trace, and real process acceptance before removing the hidden call.

Current inventory: [grouped ActiViz compatibility call sites](../prototypes/VtkJsStreaming/docs/activiz-compatibility-inventory.md). Authoritative selection clearing and browser scene reset are complete. Ordinary geometry/model parts, results/animation/export, cached selection previews, viewport transformations, classified generic surfaces/primitives, non-pickable base/overlay points and lines, and managed-boundary OpenFOAM display now use retained/no-handle VTK.js seams. Native-handle replay is blocked from mutating the hidden renderer. Unsupported generic render cells reject the whole actor instead of partially rendering it; types 5/6/8/9/22/23 are supported surfaces and 3/4/21 are supported line primitives. The audit found no production producer of pickable `Add3DNodes`, so hypothetical pickable points remain deliberately rejected. Volume-cell rendering/OpenFOAM parity, exact overlay depth, transformed cap/export projection, 3MF packaging/parity, and some widgets/tools can still require compatibility state, so `Clear`/`ClearButKeepParts` remain deliberately mirrored. Evidence includes [cell-type classification](../prototypes/VtkJsStreaming/docs/evidence/cell-type-classification-retirement.md), [generic/native-handle construction](../prototypes/VtkJsStreaming/docs/evidence/generic-native-handle-construction-retirement.md), [auxiliary/OpenFOAM construction](../prototypes/VtkJsStreaming/docs/evidence/auxiliary-openfoam-construction-retirement.md), and the linked lifecycle, result, and export records. PEND-004 remains open.

Generic construction was broadened on 2026-09-04. Supported non-selection surfaces preserve pickability and locator input; non-pickable overlay points use persistent primitives. VTK.js selection caches already retain source actor data, and the legacy `AddActor(vtkMaxActor)` replay seam now refuses unexpected handles instead of forwarding them to hidden ActiViz. Exact overlay depth ordering, pickable point behavior, and unsupported cells remain explicit boundaries. Evidence: [generic/native-handle construction](../prototypes/VtkJsStreaming/docs/evidence/generic-native-handle-construction-retirement.md).

Generic cell classification was hardened on 2026-09-07. VTK triangle strips and pixels are deterministically triangulated; VTK polylines expand to adjacent retained line segments. Any other cell type rejects the complete generic surface actor, preventing silent partial display. Pickable points are also rejected because no production caller supplies them and the persistent primitive path is intentionally non-pickable. Volume cells remain locator/selection data rather than a claimed generic render path. Evidence: [cell-type classification](../prototypes/VtkJsStreaming/docs/evidence/cell-type-classification-retirement.md).

Coordinate-system orientation visibility was retired on 2026-09-07. The setting now controls a retained vtk.js orientation marker without touching hidden ActiViz. It remains camera-linked across scene updates, is non-interactive in the lower-left corner, uses the legacy RGB palette quantized to the actor's 8-bit contract, and is capped at 200 pixels. The stock vtk.js actor uses arrow shafts and does not render the legacy X/Y/Z captions; that exact appearance is explicitly assigned to PEND-005 rather than claimed. Evidence: [orientation marker](../prototypes/VtkJsStreaming/docs/evidence/orientation-marker-retirement.md).

Scale-widget visibility and unit were retired on 2026-09-07. A retained browser overlay reproduces the legacy 400-pixel nice-length calculation, five intervals/six labels, camera and resize updates, and bounded printable unit. Packaged acceptance proved hidden/visible lifecycle with a positive finite model length. Evidence: [scale widget](../prototypes/VtkJsStreaming/docs/evidence/scale-widget-retirement.md).

Scalar-legend ownership was retired on 2026-09-08. Retained state carries the effective scalar range, managed sampled palette, result/complex/unit text, number format, visibility, background, and border. The browser applies the same palette/range to rendered actors and its safe DOM legend; packaged acceptance proved all public seams and hidden/visible lifecycle without ActiViz mutation. Evidence: [scalar legend](../prototypes/VtkJsStreaming/docs/evidence/scalar-legend-retirement.md).

Categorical color-bar ownership was retired on 2026-09-08. Bounded retained state preserves ordered replace/append pairs, RGB, legacy label normalization, appearance, position reset, view reconstruction, and hide/clear lifecycle. Packaged acceptance exercised every public seam without hidden ActiViz mutation. Evidence: [categorical color bar](../prototypes/VtkJsStreaming/docs/evidence/categorical-color-bar-retirement.md).

Status-block and shared result-widget positioning ownership was retired on 2026-09-08. Bounded retained state preserves the legacy current-culture three/four-line content for all field types, animation scale/harmonic metadata, visibility and appearance. The shared layout resets scalar/color/status overlays to their legacy 20-pixel top-left/top-right anchors and survives navigation/reconstruction. Packaged acceptance exercised all public seams without hidden ActiViz mutation. Evidence: [status block and shared layout](../prototypes/VtkJsStreaming/docs/evidence/status-block-and-widget-layout-retirement.md).

Smoothing and highlight-setting ownership was retired on 2026-09-08. Retained normalized state drives primary, secondary, and mouse browser affordances and updates existing retained selection construction. Point/line preferences no longer recreate hidden ActiViz; WebGL's context antialiasing is the documented rasterization semantic because its API has no VTK 5.8 toggle equivalent. User-pick activation/deactivation and hover behavior are next. Evidence: [graphics interaction settings](../prototypes/VtkJsStreaming/docs/evidence/graphics-interaction-settings-retirement.md).

Operational status and the distinction between VTK.js visible rendering and a physically ActiViz-free executable are documented in [VTK.js launch and remaining ActiViz dependency](VTKJS_ACTIVIZ_STATUS.md). ActiViz assemblies are still required at startup today.

Non-pickable base auxiliary construction was retired on 2026-09-04. Surfaces use retained scene resources; points and lines use a separate persistent browser-primitive ownership set that survives selection clear and supports visibility, rename, removal, navigation replay, and full-scene cleanup. The VTK.js OpenFOAM seam bypasses `vtkOpenFOAMReader` and lets the existing managed `polyMesh` boundary reader submit faces and unique edges through those routes. This does not claim native-reader volumetric/internal-cell parity or 3MF/transformation projection for persistent primitives. Evidence: [auxiliary/OpenFOAM construction](../prototypes/VtkJsStreaming/docs/evidence/auxiliary-openfoam-construction-retirement.md).

Interactive animation-frame activation was retired on 2026-09-03. On 2026-09-04 eager hidden animation construction, metadata mirroring, scale lookup, acceleration mutation, PNG sequence export, and AVI export were retired. Both export paths wait for stable browser paint and capture the composited WebView. AVI uses a bounded built-in Motion JPEG AVI 1.0 writer; the obsolete native animation export adapter was removed. Configurable modern video formats and user-visible cancellation remain product enhancements, while player/visual review belongs to PEND-005. Evidence: [animation frame activation](../prototypes/VtkJsStreaming/docs/evidence/animation-frame-activation-retirement.md), [animated-result construction](../prototypes/VtkJsStreaming/docs/evidence/animated-result-construction-retirement.md), [browser PNG export](../prototypes/VtkJsStreaming/docs/evidence/browser-animation-png-export.md), and [browser AVI export](../prototypes/VtkJsStreaming/docs/evidence/browser-animation-avi-export.md).

Captured part geometry readback for 3MF was retired on 2026-09-03. The retained projection supplies browser-authoritative triangles, coordinates, scalars, presentation, visibility, and model edges. Browser-only scenes skip the hidden actor registry entirely; uncaptured auxiliary actors are merged from legacy state only after a legacy-producing seam has been used. Transformed/generated-edge parity and native writer packaging remain open. A real writer probe found that `lib3mf.dll` is absent from the repository and Release output, so archive production is not claimed. Evidence: [retained 3MF actor input](../prototypes/VtkJsStreaming/docs/evidence/retained-3mf-actor-input.md).

Renderer-neutral 3MF palette/range input was completed on 2026-09-03. VTK.js mode retains all color-spectrum settings, current/all-animation-frame range policy, and samples every configured palette in managed code; `GetLookupTable` no longer queries ActiViz. Packaged acceptance exercises all 14 spectra plus manual over/under colors. Evidence: [retained 3MF lookup table](../prototypes/VtkJsStreaming/docs/evidence/retained-3mf-lookup-table.md).

Completion evidence: when `PREPOMAX_RENDERER=vtkjs`, no render-state mutation or query depends on a hidden ActiViz instance; all required capabilities have process coverage; default ActiViz mode remains unchanged until its separately approved removal; domain assemblies no longer require ActiViz types at the renderer boundary.

## PEND-005 — Manual visual acceptance

Why open: automated counts, bounds, interactions, and process smokes do not replace a human review of engineering readability.

Models: small deterministic fixture, `fatigue_test.frd`, large tube PMX/FRD, `bitsolo-nodal`, derived rotor harmonic response, C3D13 mesh, and one symbol-dense load/constraint model.

Review: camera views and fit; contours/legend/extrema; transparency and overlap; picks and area overlays; sections/caps; exploded/follower/animation motion; annotations; all glyph families; DPI/resize; error/recovery messages.

Completion evidence: reviewer/date, OS/GPU/browser/DPI, screenshots or short capture, model/field/frame identifiers, discrepancy list with severity, and explicit accept/reject disposition linked from the MVP acceptance table.

## PEND-006 — Linux desktop shell and WebView decision

Why open: the renderer server/client are cross-platform, but the copied PrePoMax shell is WinForms/WebView2 and remains Windows-specific.

Completion evidence: time-boxed spikes for at least the leading shell/WebView candidates; input, clipboard, dialogs, file access, accessibility, GPU/WebGL, process ownership, redistribution, and update behavior measured; ADR selects or rejects a path; representative renderer and domain workflow runs on native Linux.

## PEND-007 — Packaging and GPU support matrix

Starts after PEND-006. Produce Windows/Linux installers, self-contained server/client packaging, WebView/runtime redistribution policy, signed update/uninstall behavior, and a tested GPU/browser/driver matrix including integrated, discrete, software fallback, and actionable unsupported-device behavior.

The Windows package must also source and stage the correctly licensed/architected native `lib3mf.dll`; the managed wrapper currently exists without that runtime dependency. Acceptance must create and inspect a nonempty 3MF archive from retained VTK.js export input.

## PEND-008 — Golden image and context-loss/OOM recovery

Add tolerant golden-image baselines per supported browser/GPU class. Where automation permits, inject WebGL context loss/restoration and verify pipeline reconstruction; otherwise document a repeatable manual procedure. Simulate unrecoverable allocation/context failure and require an actionable reduced-model/chunk/quality message rather than a blank canvas.

Completion evidence: stable tolerance rationale, committed reference images, automated or documented physical runs, no leaked pipelines/resources after recovery, and diagnostics increments for loss/retry/error state.

## PEND-009 — Residual test-matrix hardening

Work from `prototypes/VtkJsStreaming/docs/test-matrix.md`. Current named gaps include malformed descriptor integration cases, all scalar data types/range variants, golden manifest snapshot, local connectivity bounds, disconnected-response stress, failed-download atomicity injection, multi-actor visibility/removal stress, additional physical camera gestures/views, and additional supplied solver/model families.

Complete each row independently: add the regression, update the matrix row, record any fixture provenance, and keep bounds deterministic. Later VTK-version or fully curved-contour parity is required only if the compatibility target intentionally changes from VTK 5.8.

## PEND-010 — Retained displacement-basis transport

Conditional trigger: implement only if measured read-side frame storage or the current 20.3 MB host transaction becomes a demonstrated constraint. Compare complete origin-relative positions against retained Float32 displacement bases plus scale. Require unchanged source IDs/topology, bounded memory, exact origin reconstruction tolerance, scale/time/harmonic coverage, and a measured net benefit before changing the protocol.

## PEND-011 — Camera-dependent symbol silhouettes

Optional trigger: a visual reviewer determines that solid glyph geometry is insufficient. The bundled vtk.js has no direct polydata-silhouette filter. Evaluate a small maintained implementation, shader/edge alternative, or deliberate omission. Require camera-update correctness, bounded cost for 100,000 instances, lifecycle disposal, and visual evidence before claiming parity.

## PEND-012 — Late first-topology WebView resynchronization

Completed: explicit session headers now take precedence over the persistent HttpOnly cookie, preventing a stale cookie from another ephemeral loopback port from rejecting the current trusted bootstrap. Cookie-only browser requests remain supported. After the first non-empty topology publication, the desktop sends one fresh authenticated bootstrap. The browser atomically reloads that scene and acknowledges its exact scene ID, revision, actor count, and pipeline-chunk count only after stable paint; replacement-scene acknowledgements remain deferred until follower/camera application.

Reproduction:

```powershell
.\scripts\build-vtkjs-mvp.ps1
.\scripts\smoke-vtkjs-winforms.ps1 `
  -ModelPath .\prototypes\VtkJsStreaming\tests\fixtures\sphere-symbol-smoke.inp `
  -ExerciseActorPresentation
```

Evidence: [late first-topology resynchronization](../prototypes/VtkJsStreaming/docs/evidence/late-first-topology-resynchronization.md). The packaged smoke now requires and observes one browser actor/one pipeline chunk before exercising rename/color/contours; actor presentation and the adjacent visibility/camera smoke both pass. Session endpoint coverage includes an intentionally stale cookie plus a valid explicit credential.

## Register maintenance

When an item completes, do not delete it. Change its status to Complete, add evidence links and completion date, update affected acceptance/test documents, and move the next actionable item upward. New pending work must include an owner boundary, reason, prerequisites or trigger, exact acceptance evidence, and any command needed to reproduce it.

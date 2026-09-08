# Implementation progress

Last updated: 2026-09-03

## 2026-09-02 — Temporary actor-highlight retirement

- Added bounded retained `prepomax.actor-highlights.updated-v1` replacement state, separate from authoritative selection state.
- VTK.js now owns accumulated non-pickable wireframe highlight overlays and their clear, rename, removal, visibility, clipping, exploded-position, navigation, and disposal lifecycle.
- `FrmMain.HighlightActor` and `Clear3DSelection` bypass hidden ActiViz mutations only in VTK.js mode; the default backend is unchanged.
- Added strict client normalization tests and a packaged `-ExerciseActorHighlight` add/clear acceptance path.
- Verification passed: client 63/63, .NET 80/80, Playwright 2/2, strict client build, full Release/x64 build/staging, and 15-actor PMX process acceptance with `ActorHighlightApplied=True`.
- Explicitly deferred exact legacy model-edge/silhouette appearance to PEND-011 and manual visual review to PEND-005.

## WP0 — baseline and reproducibility

Status: implemented; Windows and Linux x64 functional verification passed.

- Created the isolated solution, contract/server/synthetic-data projects, client, tests, docs, and scripts.
- Enabled nullable reference types and warnings-as-errors for all prototype C# projects.
- Pinned VTK.js and all direct JavaScript tooling exactly and generated the npm lockfile.
- Recorded the local toolchain in `benchmark-report.md`.
- Added transport and coordinate-precision ADRs.
- No production renderer files exist in this repository and none were changed.
- Pinned the build to the installed .NET SDK 9.0.3xx feature band while retaining the supported `net8.0` runtime target.

## WP1 — protocol and deterministic small fixture

Status: implemented; automated contract and server integration verification passed.

- Added immutable renderer-neutral manifest, actor, chunk, resource, scalar-field, message, and error records.
- Added checked descriptor byte-length calculation and validation for identifiers, versions, finite values, resource schemas, hashes, layers, and representations.
- Added a deterministic two-actor fixture with one triangle per actor, large world origin, depth overlap, stable source IDs, and ten separately hashed binary resources.
- Added explicit little-endian packing independent of host endianness.
- Added loopback HTTP endpoints for sessions, scenes, manifest, full/range resources, WebSocket events, and command fallback.
- Added random 256-bit session credentials, cookie/header/bearer validation, protocol headers, ETags, immutable cache headers, and stable error codes for implemented failure modes.
- Added C# contract/fixture tests and TypeScript decoder/connectivity tests.
- Added in-process integration tests for authentication, manifest invariants, protocol rejection, byte ranges, stable ETags, conditional requests, and authenticated WebSocket control messages.
- Added an explicit development-only loopback origin policy and fixed port, while retaining an ephemeral default server port.

## WP2 — VTK.js viewer

Status: implemented; Windows automated browser verification passed.

- Added runtime manifest validation and resource-reference checking before loading.
- Added SHA-256 verification before typed-array construction.
- Added a request-level concurrency limiter and `AbortSignal` cancellation.
- Added deterministic unit coverage proving the request concurrency cap, cancellation, and geometry-before-attributes ordering.
- Geometry becomes drawable after positions/connectivity; source-ID/scalar attributes follow without rebuilding topology.
- Added VTK.js PolyData/Mapper/Actor pipelines with explicit cleanup of VTK objects and application typed-array references.
- Added actor visibility, reset camera, solid fallback, continuous scalar contours, Cool-to-Warm and Rainbow transfer functions, legend, progress, error state, diagnostics, and WebGL context-loss handling.
- Added a production headless-Chrome correctness capture using software WebGL. It is evidence of rendering correctness, not a performance result: [small fixture screenshot](evidence/wp2-small-fixture.png).
- Diagnosed and fixed VTK.js 36.6.0 container initialization: `GenericRenderWindow.setContainer()` must be called explicitly for its canvas to attach.
- Added orthographic/perspective control and front/back/left/right/top/bottom/isometric camera actions.
- Added contour toggling and copy/download diagnostics actions.
- Added a development-only delayed-chunk mode, capped at 10 seconds and disabled outside Development.
- Added Playwright coverage proving first-chunk rendering during a 1.5-second second-chunk delay, exactly ten fixture resource requests, zero visibility refetches, post-load camera/projection controls, contour toggling, and final renderer counts.

Follow-up coverage that does not block the WP2 exit criteria:

- tolerant golden-image comparison across supported browser/GPU combinations;
- automated context-loss/restoration injection where the browser backend permits it;
- production chunking now separates the application and bounded VTK dependency chunks, with strict execution ordering for VTK registration side effects and a build-failing 500,000-byte JavaScript chunk budget. The current entry is 92,598 bytes and the largest chunk is 215,278 bytes.

## WP3 — scalar-only update

Status: implemented; browser network/topology invariants passed.

- Added deterministic base and inverted scalar fields with versions 1 and 2 while keeping position, connectivity, and source-ID resource IDs identical.
- Added monotonic scene revisions and a retained immutable resource catalog so previously issued resource URLs remain valid.
- Added a bounded per-client event hub and authenticated `field.updated` WebSocket delivery.
- Added `field.toggle` command handling with stale-revision rejection.
- Added transactional client updates: both scalar resources are downloaded and SHA-verified before either existing VTK scalar array is mutated.
- Reused existing PolyData, connectivity, Mapper, and Actor objects; stable topology instrumentation tokens are exposed in diagnostics.
- Added the scalar switch UI and legend/field metadata refresh.
- C# tests prove field versions/descriptors, unchanged topology IDs, scalar-only event payloads, and `revision-conflict` handling.
- Playwright proves exactly two additional 12-byte scalar requests, no topology request, 24 scalar bytes transferred, revision advancement, and unchanged topology tokens.

## WP4 — picking vertical slice

Status: implemented; deterministic Windows browser and server verification passed.

- Added cell-associated VTK.js hardware selection using the concrete selector owned by the active OpenGL render-window view, with Z capture for relative world hits.
- Added constant-time actor-to-chunk lookup and direct primitive-to-element lookup; nearest-node resolution inspects only the selected triangle's three vertices.
- Reconstructed engineering coordinates as float64 JavaScript numbers by adding the manifest world origin to the rendered relative hit.
- Added `pick.resolve` contracts and command handling. C# revalidates actor/chunk state, triangle index, source element, candidate-node membership, finite vectors, and world bounds against authoritative binary topology.
- Added stable `pick-invalid` responses for malformed or forged candidates without changing scene revision.
- Added a visible hit marker, selection inspector, Pick controls for deterministic non-pickable coverage, and last/p50/p95 latency diagnostics.
- Added lifecycle cancellation/generation checks so a pick cannot commit into a replacement scene.
- Added unit tests for all three nearest-vertex outcomes and invalid primitives, a server integration test for valid large-coordinate and forged hits, and headless software-WebGL tests for known triangle IDs, overlapping depth, hidden fallback, non-pickable miss, camera/projection change, scalar-update survival, and large-origin accuracy.

## WP5 — scale and benchmark

Status: implemented and benchmarked on Windows x64; Linux x64 functional verification passed, physical-GPU benchmarking pending.

- Added the deterministic 2000×1500 generator with exactly 3,000,000 unique source-node IDs, 2,996,501 logical element IDs, and 5,993,002 rendered triangles.
- Added cancellation-aware generated resources that hash and write through a 256 KiB pooled buffer. Large bodies are never retained as managed byte arrays or copied through a MemoryStream.
- Made chunk cell rows, client request concurrency, and response cap configurable. The measured default is 50 cell rows, concurrency 3, and an 8 MiB cap; the largest response is 2,398,800 bytes.
- Added 30 independent PolyData chunks. Their 3,058,000 resident point tuples include 29 intentionally duplicated boundary rows while stable source IDs preserve exactly 3,000,000 unique nodes.
- Added client timing, long-task, VTK graphics-memory, browser heap/backing-store, orbit, pick, scalar activation, and unload instrumentation plus authenticated backend process/GC diagnostics.
- Added full view recreation on snapshot replacement/unload and explicit selector-state release. CDP forced-collection evidence shows 1.193% of one loaded scene retained after five cycles, with zero retained actors/chunks.
- Added ray/AABB chunk culling before hardware selection. A 100-row candidate stripe measured 126.9 ms p95; the 50-row default reduced the three-run physical median to 92.8 ms p95.
- Added integrity retry-once behavior, visible terminal resource errors, clean-reload recovery, and clean snapshot reload after WebSocket interruption.
- Replaced placeholder benchmark scripts with headed physical-GPU and explicitly non-qualifying headless modes. Raw and summarized JSON evidence is committed under `docs/evidence`.
- Three fresh Windows/Chrome/Intel-UHD runs passed every initial quantitative gate. See `benchmark-report.md` for exact results and methodology.

## Immediate next steps

1. Continue capability-by-capability retirement of the hidden ActiViz compatibility backend (PEND-004).
2. Run the native Linux physical-GPU benchmark when suitable hardware is available (PEND-001).
3. Conduct named-reviewer manual visual acceptance on representative models (PEND-005).

The native Linux physical-GPU benchmark remains PEND-001 and starts as soon as suitable hardware is available. All deferred and conditional work has prerequisites and completion evidence in the [pending-work register](../../../docs/VTKJS_PENDING_WORK.md).

## Post-WP5 acceptance hardening

Status: implemented and verified in-process on Windows.

- Added configurable session expiry and a hard concurrent-session bound. Expired credentials are removed on authorization or creation; capacity exhaustion is a retryable stable 503 failure.
- Added bounded per-session command fixed windows, retryable stable 429 responses, and `Retry-After`. The limiter's tracked-session state is capped with the session bound.
- Rejected unknown command types with `command-unsupported` instead of incorrectly returning `command.completed`.
- Added host lifecycle coordination that publishes `server.shutting-down`, completes bounded event queues, and closes WebSockets with endpoint-unavailable status. Existing request cancellation continues into generated-resource streaming.
- Added direct lifecycle tests and real HTTP/WebSocket integration tests for expiration, capacity, independent rate windows, stable throttling responses, terminal event ordering, and socket close status.

## PrePoMax shell MVP

Status: opt-in Windows integration with real static surfaces implemented and process-smoke verified on 2026-08-06.

- Embedded the built client in the copied PrePoMax `FrmMain` through a pinned WebView2 control.
- Added same-origin static hosting, ephemeral child-host discovery, exact-origin navigation restrictions, and credentialed graceful shutdown.
- Preserved the original ActiViz path as the default and as a hidden compatibility backend when `PREPOMAX_RENDERER=vtkjs` is enabled.
- Added reproducible build, host-smoke, and real-WinForms process-smoke scripts. Detailed scope and evidence are in the repository-level `docs/VTKJS_MVP_INTEGRATION.md` and `docs/evidence/vtkjs-mvp-windows.md`.
- Added deep `PartExchangeData` capture, legacy-equivalent surface triangulation, stable source IDs, material transfer, debounced publication, and atomic `scene.replaced` client reload.
- Added a credentialed, bounded `PMXSCN1` host upload with strict parsing and resource construction. A real PMX smoke transferred 15 actors, 828,619 points, and 1,623,416 triangles.
- Retained the hidden ActiViz backend for controller paths not yet fully ported; subsequent slices now cover annotation interaction/extrema, scale-factor frame activation, and derived views.
- Added exact-name part visibility state and incremental `actor.visibility.updated` delivery with no topology/resource refetch. State is bounded and survives geometry replacement.
- Added retained front/back/left/right/top/bottom/isometric/reset camera commands. Browser and real-process tests prove delivery; normal/vertical derived views remain on the compatibility backend.
- Added validated Float64 custom-camera forwarding for saved/pasted/user views with world-origin conversion in the client.
- Added incremental actor rename/RGBA updates and mirrored capture-cache mutation so later snapshots retain presentation changes. Browser coverage proves no topology/resource refetch.
- Connected real result drawing and scalar-field refresh calls to the capture bridge. `fatigue_test.frd` passed with deformed geometry and a finite non-default point-scalar range (1 actor, 2,850 points, 5,048 triangles).
- Added retained `PMXFLD1` result metadata and bounded, actor-batched `PMXSCL1` scalar updates. Component changes now create new immutable point-scalar resources, advance field/scene versions transactionally, and retain position/connectivity/source-ID resources.
- Browser host-path coverage proves one updated actor causes exactly one scalar fetch and no topology fetch. The real FRD smoke now reports `DISP / ALL / mm` with range `0` through `0.011121182702481747`.
- Extended the packaged WinForms smoke with a trigger-file-coordinated result selection. The real controller path switched `fatigue_test.frd` from `DISP / ALL` to `DISP / U1`, advanced scene revision 1 to 2, changed only scalar resource IDs, retained every topology/source-ID resource ID, and reported the U1 range `-0.008084929548203945` through `0.0010357999708503485` mm.
- Added the validated-pick WebView bridge. The client emits a versioned host message only after server `pick.resolve` validation; WebView2 accepts it only from the exact renderer origin and maps replace/Shift-add/Ctrl-subtract/Shift+Ctrl-intersect into the existing PrePoMax selection buffer. Node, element, and part selection modes are supported; default browsing selects the actor in the existing tree.
- The real FRD process smoke now generates a WebView click and proves source element `1142` reaches the PrePoMax element selection buffer before graceful shutdown.
- Added the reverse authoritative selection bridge from every `FrmMain.SelectionChanged` path. The client renders element triangle outlines, node point markers, and part wireframes as non-pickable orange overlays and retains selection across scene-view recovery.
- Bounded JSON selection messages to 100,000 IDs and 4,096 actor names. The later `PMXSEL1` slice below supersedes the original `unsupported` outcome for larger in-bound state; element overlays still fail visibly instead of truncating when the rendered-triangle safety bound is exceeded.
- Browser coverage exercises node, element, surface, part, and clear messages. The real FRD process smoke now also proves the selected source element `1142` creates at least one visible, non-overflowing overlay actor.
- Extended validated pick results with the three source-node IDs derived by the server from immutable connectivity. Surface-mode browser picks use those nodes to resolve a unique PrePoMax visualization face, including higher-order faces and shell front/back selection.
- Added exact face-node selection state and browser matching that requires all three rendered triangle nodes to belong to one selected face. Existing controller-driven `Surface` and `GeometrySurface` states use the same overlay, while `GeometryVertex` maps to the node overlay.
- The real FRD process smoke selected element `1142` as visualization face `11422` and proved the exact surface outline was visible without overlay overflow.
- Added strict geometry-edge resolution: only a side of the server-validated rendered triangle that is contained by one PrePoMax model edge can select it. Interior visualization-triangulation edges are rejected.
- Added bounded exact edge-cell state and non-pickable orange line overlays, including start-mid-end rendering for quadratic edges. Existing controller-driven `GeometryEdge` selection uses the same path.
- The real FRD acceptance probe rejected interior candidates, selected geometry edge `1820001` from a boundary triangle, and proved its overlay was visible without overflow.
- Added Alt+left-drag visible-surface area selection without taking over ordinary orbit. Left-to-right filters for fully enclosed rendered triangles; right-to-left uses hardware-selector crossing candidates. Existing Shift/Ctrl modifier operations are preserved.
- Added bounded `area.resolve` validation (4,096 chunks / 100,000 triangles). The server rejects unavailable actors, invalid primitive ranges, and malformed camera state, then derives authoritative actor, element, and triangle-node topology from immutable resources.
- Added the exact-origin `prepomax.area.validated-v1` WebView bridge and controller application for node, element, part, and surface selection. IDs are deduplicated through the existing selection history and returned through the existing overlay path.
- Added a server forgery/range test and a real Chromium rectangle-drag test that verifies a non-empty validated host envelope. At this checkpoint full hidden-volume frustum work remained pending; the later `PMXSCN2` BVH slice below supersedes that limitation.
- Added geometry-area interpretation using the existing PrePoMax `FeMesh.GetGeometryIds` precedence: surfaces consume their selected nodes first, followed by complete model edges and remaining geometry vertices. Dedicated vertex/edge/surface modes filter the encoded geometry IDs; mixed `Geometry` mode retains all types.
- Extended authoritative selection state with `item: geometry`. VTK.js composes exact face outlines, linear/quadratic edge lines, and vertex points in the same non-pickable selection state instead of degrading mixed selections to `unsupported`.
- Extended the packaged WinForms smoke harness with a synthetic WebView area gesture. On `fatigue_test.frd`, the controller resolved 16 geometry IDs from 5,048 validated rendered triangles and produced a visible, non-overflowing geometry overlay before graceful shutdown.
- Added the `PMXSCN2` full-cell selection foundation. Real actors now deep-copy their existing `vtkMaxActorData.CellLocator` node IDs, Float64 coordinates, source element IDs, VTK cell types, and local connectivity into the bounded host snapshot while leaving render triangles unchanged.
- Added strict server-only locator decoding and retention. Exact supported cell arities, finite coordinates, local references, declared connectivity totals, per-scene counts, and the existing 256 MiB upload cap are validated before scene replacement. Older `PMXSCN1` fixtures remain readable during migration.
- Locator data is not published in the browser manifest and consumes no browser/GPU resources. Visibility, scalar updates, and actor presentation preserve the retained topology; rename updates its actor identity atomically.
- Added positive tetra retention and invalid-connectivity tests. The real `fatigue_test.frd` packaged smoke transferred 6,503 locator nodes and 3,853 full cells alongside the unchanged 2,850 render points and 5,048 surface triangles.
- Added an immutable per-actor cell-bounds BVH built during `PMXSCN2` scene replacement. Six server-derived frustum planes prune BVH nodes, select exact inside locator points, and retain inside/border cells while respecting live actor visibility and pickability.
- Added bounded `frustum.resolve` with eight Float64 world corners, stable `frustum-invalid` / `frustum-unavailable` errors, and 100,000-node / 100,000-element result caps. Scenes without locator topology automatically retain the visible-triangle fallback.
- Extended the exact-origin area envelope with server-derived node/element IDs and actor names. PrePoMax now applies full-cell node, whole-element, part, surface, dedicated geometry, and mixed-geometry semantics through existing selection history; left-to-right elements require every element node inside.
- Deterministic tetra coverage proves exact node IDs plus an intersecting border cell. The packaged `fatigue_test.frd` smoke now requires `locator=True`, resolved 10 geometry IDs through the server frustum, produced a visible overlay, and shut down without leaving a renderer child.
- Replaced candidate-only cell vertex rejection with the `vtkExtractSelectedFrustum` 5.8 bounds-accept plus edge/face clipping model. This removes false positives for disjoint cells with overlapping bounds and reproduces legacy results when a cell encloses the entire frustum.
- Added a shared eight-case JSON parity corpus and an ActiViz oracle script. The installed ActiViz 5.8 runtime and the managed server agree on crossing, grazing, enclosing, quadratic-edge, quadratic-surface, boundary-point, and zero-length cases.
- Added seven-pixel world selection precision to the server-validated pick contract. The browser derives it at the hit depth; the server rejects non-finite, non-positive, or chunk-scale-abusive values before returning it in the exact-origin host envelope.
- Added unique validated-triangle to full-visualization-face reconstruction and single-click geometry classification. Mixed geometry preserves the legacy vertex-first, edge-second, surface-last precedence; dedicated geometry-vertex and geometry-surface clicks use the same validated face path.
- Extended the packaged smoke with `GeometryPoint`. A real `fatigue_test.frd` browser click selected geometry ID `30001`, produced a visible composed geometry overlay, and shut down with no surviving renderer child.
- Added bounded, debounced full-state synchronization for regular PrePoMax annotations through `prepomax.annotations.updated-v1`. The client validates and retains callouts, uses safe DOM text, subtracts the scene world origin, and camera-projects anchors above the VTK canvas.
- Mirrored `AddArrowWidget`, named removal, clear, and scene-reset lifecycle calls into the VTK.js bridge. A packaged `fatigue_test.frd` smoke created a measure annotation and reported one retained and one visible callout before graceful shutdown.
- Added authoritative result-extrema synthesis from the per-actor extreme node data already supplied to legacy VTK. Global Min/Max callouts follow actor visibility and color-contour state and refresh on scalar/component, unit, rename, and scene lifecycle changes.
- Replaced annotation smoke count assumptions with callout-identity assertions. A packaged `fatigue_test.frd` run switched `DISP/ALL` to `DISP/U1` using scalar-only resources, then verified visible Min and Max callouts carrying formatted values and node IDs.
- Added interactive callout offsets and inline text editing. Drag completion sends a bounded absolute pixel offset that is validated, retained, and echoed by the host; non-reserved double-click edits update the real annotation override and redraw authoritative state. Reserved Measure/Min/Max text remains protected.
- Browser coverage verifies a 30/20-pixel drag message and edited-text message. The packaged FRD smoke created a real result-node annotation, moved it 25/20 pixels, applied `VTK.js edited annotation` to PrePoMax, and verified the echoed browser callout before graceful shutdown.
- Added `PMXSEL1`, a bounded little-endian selection envelope delivered through a read-only WebView2 shared buffer whenever the retained authoritative state exceeds a JSON-path bound. Strict client decoding covers signature, counts, UTF-8, cell arity, declared references, exact length, and release-after-use.
- The new tube corpus contains 577,325 input nodes and 370,848 C3D10 elements. Its PMX process smoke published 171,686 render points / 321,960 triangles plus 577,324 locator nodes / 370,848 cells, transferred all 577,324 displayed node IDs in roughly 2.3 MiB, and produced a visible non-overflowing node overlay.
- Audited the complete `PMXSEL1` size equation and raised the premature ID policy limit from 2,000,000 to 8,000,000. All other maxima combined with eight million IDs yield 61,793,640 bytes (58.93 MiB), preserving the 64 MiB hard envelope without multipart state. Counts above eight million are rejected before value decoding.
- Added deterministic adaptive node overlays: all authoritative IDs are retained and overlays remain exact through one million rendered matches. Larger states use a stable stride capped at 500,000 overlay points and report `overflow=true`; the cap was tightened from one million after the physical 3M-match run showed the original representation below the integrated-GPU interaction target. Unit tests cover exact/sampled boundaries; Playwright decodes 2,000,001 IDs; a packaged `fatigue_test.frd` WebView2 smoke transmitted 2,000,001 unique IDs in 8,000,044 bytes and produced a visible exact overlay for the 6,503 matching locator nodes.
- Completed the physical multi-million-match acceptance on the canonical WP5 scene. Three fresh headed Chrome/Intel UHD runs retained 3,000,000 authoritative IDs, matched 3,058,000 resident point tuples, and rendered a stable 436,858-point/30-actor sampled overlay. Median activation was 474.6 ms and median selected-orbit performance was 27.78 FPS; all four dedicated gates passed in every run. Raw JSON is under `docs/evidence/wp5-multimillion-selection-2026-08-31*.json`.
- Retired `AddSphereActor` as the first engineering-glyph primitive. A bounded retained WebView2 replacement state carries unique names, absolute anchors, RGBA, and display size; vtk.js renders a shared 13×13 sphere source through non-pickable glyph mappers and recomputes parallel/perspective world scales on render. Client tests cover malformed states and aggregate bounds; Playwright proves two-point instancing, 2× zoom scaling, and disposal; the packaged four-node C3D4 smoke proves the real `FrmMain` path bypasses ActiViz, creates one positive-scale glyph, and shuts down cleanly.
- Forwarded PrePoMax's derived normal and vertical camera results through the existing bounded custom-camera command, preserving the legacy orientation rules while making the VTK.js viewport authoritative.
- Added bounded `PMXREM1` actor removal. Capture, current manifest/resources, visibility state, locator topology, client pipelines, and selection overlays now converge without a scene reload; parser/state and Playwright coverage includes duplicate rejection and resource disposal.
- Added the first functional deformation-animation bridge. PrePoMax's retained per-frame render coordinates, scalars, extrema, and locator coordinates now replace the visible scene when `SetAnimationFrame` runs; source IDs and topology are inherited from the authoritative base actor instead of reconstructed from VTK output.
- Added coalesced frame publication and a real-controller `-ExerciseAnimation` process smoke. On the supplied tube FRD, the half-scale frame retained 163,598 render points / 321,960 triangles and 577,324 locator points / 370,848 cells, changed position content hashes, and changed the `DISP/ALL` maximum from 3.4277818 mm to 1.7138909 mm.
- Added bounded, versioned position/scalar/locator-coordinate deltas. `PMXFRM2` uses one Float64 frame origin plus origin-relative Float32 coordinates, while the server retains `PMXFRM1` compatibility. Parser/state tests prove coordinate reconstruction and stable connectivity/source-ID references; Playwright proves two resource fetches per chunk, unchanged pipeline topology tokens, and atomic browser activation.
- The real tube FRD smoke now separates animation frame 0 publication from frame 1 activation, requires a same-scene revision advance through `published frame`, compares position content hashes, and asserts every connectivity/source-ID resource ID is unchanged.
- Forwarded PrePoMax animation type/time/step/increment/scale metadata into the VTK.js bridge, validated a common positive frame count across every participating actor, and made multi-actor frame capture atomic before cache mutation.
- Generalized the process smoke with `-AnimationMode ScaleFactor|TimeIncrements|Harmonic`. `fatigue_test.frd` passed a genuine two-frame time-increment delta with unchanged topology/source-ID resources. Harmonic smoke now rejects non-complex results explicitly; a complex-result FRD is still required for positive acceptance.
- Added sequential `-MeasureAnimationCadence` acceptance and order-independent, per-actor/chunk resource invariants. The 1.65 GB `bitsolo-nodal.frd` is modal rather than complex; PrePoMax exposes 4 actors, 422,797 render points, 836,744 triangles, 1,127,607 locator points, 657,394 cells, and 6 time-increment frames. The harness now gates each next request on a browser-reported stable paint and records download, VTK apply, and paint timing. All 5 post-baseline deltas passed with unchanged topology mappings.
- Profiled and optimized the large-frame path. `PMXFRM2` reduced the `bitsolo-nodal` host transaction from 38,900,992 to 20,296,168 bytes (47.8%). The optimized sequence transferred 6,764,752 browser bytes per frame and measured median browser download/apply/paint/total times of 32.8/62.3/1.8/96.9 ms, with 2.087 s median trigger-to-stable-browser acceptance under 250 ms harness polling. The VTK array application is now the largest measured browser component; host capture/upload/reconstruction remains the dominant end-to-end component.
- Added host/server phase telemetry and identified full reconstruction of the 657,394-cell locator BVH as the dominant server phase. Animation updates now preserve BVH leaf membership and refit all leaf/parent bounds from the deformed coordinates instead of re-sorting stable topology. On `bitsolo-nodal`, median server update fell from 1,277.9 to 295.7 ms (76.9%), median host transport from 1,357.8 to 382.1 ms (71.9%), and trigger-to-stable-browser acceptance from 1,874.9 to 936.7 ms. A deformed-tetra regression proves frustum queries follow the refitted coordinates.
- Removed duplicate V2 coordinate materialization. The reader retains the wire-format Float32 render/locator arrays and reconstructs doubles only for final locator state; scene state and the refitted BVH share that immutable array. For `bitsolo-nodal`, this removes 18,604,848 bytes of parser expansion and a 27,062,568-byte BVH locator copy per frame (45,667,416 bytes total). The follow-up run kept every invariant and measured 263.2 ms median server update; aggregate cadence was not used as a gain because host-write variance dominated that run.
- Added server read/update allocation counters plus legacy activation and VTK.js capture timing to the process harness. Retained BVH nodes now refit their existing bounds arrays in place after full actor/count validation, reducing median `bitsolo-nodal` server-update allocation from 78,153,584 to 61,715,864 bytes/frame (21.0%).
- Added a coordinate/scalar-only animation capture path. Post-baseline frames validate and copy only render coordinates/scalars and locator coordinates, reusing source IDs, triangulation, locator IDs/types/cells, and presentation from the published baseline. Median VTK.js capture fell from 196.3 to 22.3 ms (88.6%) and combined legacy-plus-mirror activation from 228.2 to 74.5 ms (67.3%). All five real-model deltas retained topology/resource mappings.
- Replaced the animation reader's full-request byte array with a bounded 64 KiB pooled streaming decoder. It handles fragmented reads, preserves V1/V2 validation and exact-length rejection, and returns its scratch buffer after every request. On `bitsolo-nodal`, median server-read allocation fell from 40,625,064 to 20,370,912 bytes/frame (49.9%); two complete six-frame process runs retained all topology and lifecycle invariants.
- Retained PMXFRM2 locator coordinates as origin-relative Float32 values in scene state and the spatial index instead of materializing a 27,062,568-byte Float64 array. A specialized BVH bounds loop reconstructs world coordinates without per-value interface dispatch. Median `bitsolo-nodal` server-update allocation fell from 61,712,664 to 34,651,248 bytes/frame (43.9%), with 174.6 ms median update time and unchanged frustum node/element results.
- Replaced staged animation `float[]` plus `byte[]` position/scalar resources with `GeneratedBinaryResource` instances backed by the retained frame arrays. Pooled hashing and HTTP writes generate exact little-endian bytes on demand, including unaligned ranges. Median `bitsolo-nodal` server-update allocation fell from 34,651,248 to 21,124,304 bytes/frame (39.0%; 73.0% below the original measured 78,153,584 bytes), with unchanged hashes, ranges, topology, and five-frame activation acceptance.
- Added validation/resource/manifest/refit timing and allocation telemetry through server response headers, the embedded host trace, and the process harness. It isolated roughly 21.04 MB/frame in BVH refit to boxed `IReadOnlyList<int>` enumerators across 657,394 cells. Indexed cell-point loops reduced median total update allocation from 21,124,304 to 221,176 bytes/frame (98.95%; 99.72% below the original measurement); median validation and refit allocation are now zero.
- Added nodal displacement output to the supplied Coriolis `rotor` input and a reproducible `prepare-rotor-harmonic-fixture.ps1` generator. Because non-cyclic complex-frequency output contains only real mode shapes, the generator derives a three-frequency steady-state response with real/quadrature loads, yielding paired `DISP`/`DISPI` fields.
- Fixed the FRD reader to recognize `1PMODE` metadata without requiring the legacy `MODAL` descriptor and removed a null dereference on the first method-3 complex-frequency block. The generated rotor FRD now loads with complex fields and steps 3/4.
- Positive harmonic process acceptance passed: 1 actor, 2,560 render points, 4,608 triangles, 2,656 locator points, 368 cells, and 4 phase frames. All 3 post-baseline deltas changed position hashes with stable topology mappings; median trigger-to-revised-manifest latency was 265 ms.
- Added renderer-neutral camera snapshots. VTK.js publishes world-coordinate position/focal-point/view-up/parallel-scale values after stable scene paint, host camera applications, and interaction end; the exact-origin WinForms bridge validates and retains them for saved-camera, view-normal, and closest-up queries.
- Retired standard and custom camera mutations from the VTK.js compatibility path. Front/back/top/bottom/left/right/isometric, zoom-to-fit, and custom commands now skip ActiViz when opted in. Normal/vertical views remain explicitly compatibility-backed after synchronizing the hidden camera from retained VTK.js state. The packaged `fatigue_test.frd -ExerciseCameraState` round-trip passed.
- Added bounded `PMXCAM2` operations while preserving `PMXCAM1`: `fit` applies retained orientation and fits current visible bounds; `adjust` reproduces the legacy 1.05-radius depth placement, one-unit focal separation, and clipping reset while preserving parallel scale. Server state retains the operation for correct replay after scene replacement.
- Retired normal, vertical, and camera-adjusting redraw mutations from ActiViz in VTK.js mode. Their orientation is derived from retained browser state, redundant adjustment is deferred while a camera command is pending, and pre-snapshot commands no longer fall back to the hidden renderer. Parser, Playwright, and packaged real-FRD sequential acceptance passed.
- Retired box zoom from ActiViz in VTK.js mode. The host arms a one-shot browser mode; rectangle apply pans and reduces orthographic parallel scale, while Escape/cancel completes without mutation and clears the toolbar toggle. Scene recreation now preserves camera telemetry and parallel projection. Playwright apply/cancel coverage and the packaged `fatigue_test.frd -ExerciseBoxZoom` process smoke passed.
- Retired the incidental hidden-camera adjustment from actor hide/show. ActiViz retains compatibility actor visibility without moving its camera; VTK.js adjusts depth/clipping from browser-visible bounds after visibility and removal events, preserves parallel scale, and publishes retained camera telemetry. Playwright and packaged `fatigue_test.frd -ExerciseVisibilityCamera` acceptance passed.
- Retired bounds-based hidden-camera adjustment from animation-frame activation while retaining legacy animation actor/scalar state. VTK.js now adjusts depth/clipping after atomically applying frame coordinates/scalars, preserves parallel scale, publishes camera telemetry, and marks stable activation with `cameraAdjusted`. Playwright and packaged `fatigue_test.frd -AnimationMode TimeIncrements` acceptance passed.
- Added browser-owned animation follower motion. The exact-origin host retains a bounded one/three-node definition, VTK.js resolves source IDs without topology refetch, and frame activation applies the legacy point or plane transform before its single bounds adjustment. Hidden animation follower mutation is suppressed in VTK.js mode; Playwright covers a translated three-node plane and `followerAdjusted` telemetry.
- Completed follower camera ownership for full scene replacements. The outgoing view hands off resolved world-coordinate points, so a replacement with a different floating origin can replay its retained camera and apply the old-to-new follower transform after loading. Stable `prepomax.follower-view.applied-v1` telemetry crosses the exact-origin bridge, and VTK.js mode now bypasses hidden follower apply/update/remove entirely. Playwright covers a two-actor, changed-origin replacement and retained parallel scale.
- Added authenticated transient server section geometry. `POST /api/v1/scenes/{sceneId}/section` derives cut polygons and lines from retained full cells, interpolates retained locator scalars, applies parent/lightened/single-color policy, and returns bounded origin-relative `PMXSEC1` data without changing scene revision or accumulating drag resources.
- Added client-side binary validation and atomic cap lifecycle. Plane changes cancel stale requests; prior non-pickable cap pipelines are disposed before replacement; disable and scene teardown release them; exploded actor offsets participate in both server intersection and browser presentation. Linear volume and shell cells plus midside-segmented supported quadratic edges are covered.
- Extended `PMXSCN2`'s reserved fourth behavior byte as backward-compatible inverted section eligibility (zero eligible, one opt-out). Scalar-only and frame updates remap render values to retained locator source-node IDs so later cuts use current contours.
- Added tetra face/scalar/winding, exploded-offset, shell-line, invalid-request, authenticated endpoint, and binary decoder tests. Packaged `fatigue_test.frd` and the 370,848-cell tube PMX each created/rotated/removed one server cap actor with zero skipped cells and no ActiViz section mutation.
- Completed section-aware rectangle selection. `frustum.resolve` treats the retained vtk.js half-space as a seventh BVH/geometry plane, while the synthetic/v1 fallback clips visible triangle polygons before inside projection and the server rejects forged fully hidden candidates from immutable positions. The section interaction guard is removed; unit, endpoint, and Playwright coverage proves crossing, fully clipped, boundary, malformed-plane, and active-section Alt-drag behavior.
- Replaced the quadratic-tetra cap fan with the exact VTK 5.8 type-24 contour decomposition: eight fixed linear tetrahedra and the legacy marching-tetra case table. The canonical C3D10 oracle now produces the same 10 cut points and 9 triangles as live ActiViz 5.8, including internal subdivision points and linearly interpolated result scalars. A boundary-crossing gate confines subdivision to actual cut cells; after catching and correcting the initial all-cell performance regression, the 370,848-cell tube again completed create/update/remove within the unchanged process harness, with one cap actor and zero skipped cells.
- Completed VTK 5.8 section parity for type-25 quadratic and type-29 triquadratic hexahedra. Both use the exact eight-linear-hexahedron maps and 256-case marching-cubes edge table; type 25 generates six face centers and one cell center with the legacy interpolation weights and stable shared-face identities. Canonical 3x3 cuts, adjacent-cell face deduplication, and a nondegenerate ambiguous case-90 corpus are covered against live ActiViz. The supplied rotor C3D20R model completed section create/update/remove with one cap actor, zero skipped cells, 2,560 render points / 4,608 triangles, and 2,656 locator points / 368 cells.
- Completed VTK 5.8 section parity for type-26/C3D15 quadratic wedges. The path generates the three rectangular-face centers with the exact fifteen-node weights, uses the fixed eight-wedge subdivision and legacy 64-case wedge table, shares generated face identities across adjacent cells, and removes only internal subdivision lines by triangle incidence. The live oracle covers a canonical 12-point / 12-triangle cut and a warped generated case 43 with 20 points / 18 triangles. The supplied `bitsolo-nodal` FRD, containing 29,912 C3D15 elements, loaded 657,394 locator cells and completed section creation with three cap actors, zero skipped cells, and clean shutdown.
- Completed VTK 5.8 section parity for type-27/C3D13 quadratic pyramids, closing the supported quadratic-volume family. The path generates the shared quadratic base center with the exact thirteen-node weights, contours six linear pyramids through the legacy 32-case table, then contours four internal linear tetrahedra. Canonical, adjacent-base, and generated case-21 corpora match live ActiViz topology and scalar interpolation. The supplied `mesh-C3D13.inp` adds a real importer/process qualification over 2,150 C3D13 and 112,311 C3D10 elements: 163,701 locator points / 116,611 cells, six cap actors, zero skipped cells, complete create/update/remove, and graceful shutdown.
- Added an executable cross-runtime randomized section differential. A shared JSON corpus drives both the production `SectionGeometryBuilder` through a dedicated friend-assembly CLI and live ActiViz 5.8. Triangle vertices are matched independently of point IDs and winding, including interpolated scalars and explicit Float32 tolerances. Each run covers eight canonical and eight independently warped cells/planes per type. Seeds 58027–58029 passed 240 cases and 3,159 triangles across types 24, 25, 26, 27, and 29 with no topology or scalar differences.
- Closed exact-plane multi-cell locator parity with a 12-case live ActiViz differential spanning VTK volume types 10-14, 24-27, and 29. Shared-face contributions are deduplicated actor-wide, linear volumes use the legacy marching tables instead of an angle-ordered polygon fan, and exact coincident coordinates merge even when input point IDs differ. Shared-face, shared-node, and duplicate-interface cases match ActiViz point counts and 53 canonical triangles; focused C# regressions retain source-element provenance.
- Added a seeded full-cell frustum differential that captures live ActiViz 5.8 point/cell results and drives the production managed selector through an overrideable xUnit corpus. Seeds 58103-58105 pass 612 valid transformed cases across all 17 supported VTK cell types. The first seed exposed the extractor's quadratic-volume parent-cell-edge face bug; a later case exposed its actor-level overall-bounds gate. Both observable legacy behaviors are now reproduced, and quadratic tetra/pyramid failures are retained in the ten-case committed regression corpus. The packaged `fatigue_test.frd` geometry-area smoke still resolved 10 IDs through `locator=True`, displayed the authoritative overlay, and shut down cleanly.

- Retired coordinate-axis and caption rendering from hidden ActiViz in VTK.js mode. One bounded retained replacement preserves absolute anchors, direction, color, constant-screen sizing, display-scaled caption vectors, and font scale. VTK.js reproduces the legacy line/cone source, safe text-only captions, floating-origin conversion, camera scaling, and deterministic disposal. Client 32/32, Playwright 2/2, .NET 78/78, Release/x64, and packaged C3D4 process acceptance passed.
- Retired the multi-anchor displacement-constraint cone family from hidden ActiViz in VTK.js mode. The extensible directional-glyph state was introduced with `displacement-cone`, requires paired anchors/nonzero directions, and reproduces the capped 31-sided source, 0.3 camera-relative scale, lighting, name mutation, floating origin, and lifecycle disposal. Client 35/35, Playwright 2/2, Release/x64, and packaged C3D4 process acceptance passed; exact camera-dependent silhouette edges remain explicitly unclaimed.
- Retired rotational-constraint symbols through a second directional kind. The browser reproduces the misleadingly named legacy `:cylinder` as its actual flat cube/line source: 24 face-local cube points, 12 triangles, and one two-point line, with exact dimensions/offset, paired direction orientation, 0.3 camera scale, and retained lifecycle. Client 36/36, Playwright 2/2, Release/x64, and packaged C3D4 process acceptance passed.
- Retired oriented load arrows through a third directional kind. Explicit inversion and relative-size state reproduces legacy source resolutions, inverse head/radius sizing, magnitude length scaling, anchor offsets, zero-normal removal, and naming. Headed bounds coverage exposed VTK.js's newer arrow recentering; production now cancels it to recover ActiViz 5.8 X=0..1 semantics before applying +0.05/-1.05. Client 37/37, Playwright 2/2, Release/x64, and packaged C3D4 process acceptance passed.
- Retired moment-load double arrows through a fourth directional kind. VTK.js appends the legacy 21-sided cone centered at X=0.65 to the standard 21/15-resolution arrow, cancels current VTK.js arrow recentering, applies the common +0.05 anchor gap and unmodified camera-relative symbol size, filters zero normals, and owns the composed source lifecycle. Client 38/38, Playwright 2/2, Release/x64, and packaged C3D4 process acceptance passed.
- Retired spring-constraint symbols through a fifth directional kind. The exact 93-point smoothed path, radius-0.03 ten-sided capped tube, fixed -1.05 translation, unmodified camera-relative size, `:spring` identity, and legacy inversion no-op are retained. Because vtk.js TriangleFilter ignores strips, production explicitly expands the tube's strips into the expected 1,856 triangles while preserving 950 shared points and normals. Client 39/39, Playwright 2/2, Release/x64, and packaged C3D4 process acceptance passed.
- Retired the smaller general cone used by line-with-arrow selection previews through a sixth directional kind. Its capped 11-sided radius-0.35 source, X=-1..0 placement, 13 points / 22 triangles, `:cones` mutation, 0.3 camera-relative scale, supplied RGBA, orientation, and lifecycle are retained without hidden actors. Client 40/40, Playwright 2/2, Release/x64, and packaged C3D4 process acceptance passed.
- Retired oriented thermometer symbols through a seventh directional kind. The radius-0.2 11x11 bulb, transformed radius-0.1/height-0.8 seven-sided stem, 129 shared points / 222 triangles, explicit normals, +0.05/-1.05 inversion translations, 0.5 scale, `:thermo` identity, and lifecycle match the legacy source. The real temperature seam and all browser/build gates passed.
- Retired oriented flux symbols through the eighth directional kind. The radius-0.2 9x11 sphere retains centered versus X=0.2 placement, +0.05/-1.05 inversion translations, 79 shared points / 154 triangles, 0.5 scale, `:flux` identity, and lifecycle. The real flux seam and all browser/build gates passed. The completed audit confirms every symbol-producing public `vtkControl` method used by `Controller.Draw*Symbols` now has a VTK.js branch; only optional camera-dependent silhouette edging remains unclaimed.
- Closed the physical follower-redraw coverage gap with a deterministic four-node FRD. The process harness applies a real three-node plane through `Controller.ApplyFollowerView`, waits for browser baseline capture, then activates scale-factor frame 1 through the normal animation path. Revision 2 retained topology and reported `followerAdjusted=True`, `cameraAdjusted=True`, all follower apply/update compatibility bypasses, 22.9 ms stable browser activation, graceful close, and renderer-child exit.
- Closed annotation-settings dialog automation. The browser action opens the real `FrmSettings`; the process asserts `Annotations` is the live property page and invokes the physical Cancel button through UI Automation. This exposed and fixed a host issue where a pre-handle `ListViewItem.Selected` assignment did not populate the property grid; `PrepareForm` now explicitly installs the requested page. The packaged run passed exact-origin action validation, host selection, Cancel, graceful close, and child exit.

## Trusted Production session bootstrap

Status: complete on 2026-09-01 (PEND-002).

- Production now rejects anonymous and incorrectly host-credentialed session creation; explicit Development mode preserves direct browser convenience.
- The embedded client announces readiness without autoloading. The exact-origin WinForms host creates/reuses the session and transfers it through WebView messaging; authenticated adoption establishes the HttpOnly/SameSite=Strict cookie before manifest or WebSocket traffic.
- Credentials remain in memory and headers/message payloads only. They are absent from URLs, command lines, traces, and persistent files. A dedicated path-neutral readiness line allows framework path-bearing lifetime logs to remain suppressed.
- Verification passed: server 68/68, client 54/54, Playwright 2/2, strict production build, full Release/x64 staging, safe-log lifecycle smoke, basic packaged WebView2 smoke, and a model-backed packaged scene load. See [decision and evidence](evidence/production-session-bootstrap.md).

## Structured Production diagnostics

Status: complete on 2026-09-02 (PEND-003).

- Added source-generated stable event IDs 6000-6021 for lifecycle, sessions, selected requests, and resources. Correlations are canonical UUIDs or bounded server request IDs; raw URLs, bodies, exception text, paths, and credentials are excluded.
- Added server protocol/version, request/resource byte/count, cancellation, and last-safe-error diagnostics plus an exposed bounded server-version response header.
- Completed the downloadable client schema with version/session identity, WebGL limits, source/render counts, planned/transferred/decoded bytes, retry/cancellation/context-loss counts, and the last safe error code.
- Capture-based Production tests, client metrics tests, and Playwright schema/retry/secrecy checks passed. A paired canonical 3M headless run observed a 117.4 ms (+3.68%) complete-load delta and +0.67 MiB backend managed peak with structured events enabled; exact event/byte cardinality remained bounded. See [evidence and raw JSON](evidence/structured-production-diagnostics.md).

## Verification evidence

## Viewport-appearance compatibility retirement — 2026-09-02

- Retired hidden `SetBackground` and `SetLighting` mutations in VTK.js mode while preserving the default ActiViz branches.
- Added bounded retained gradient/top/bottom RGB and ambient/diffuse state, WebView navigation and scene-view recreation replay, and material application to current/future streamed and section actors.
- Refreshed the remaining compatibility call-site inventory by capability and kept smoothing, widgets, transformations, actor readback/dual writes, and animation export explicitly open.
- Verification passed: client 59/59, Playwright 2/2, strict client build, complete Release/x64 staging, and packaged C3D4 process acceptance with exact browser state plus clean parent/child shutdown. See [evidence](evidence/viewport-appearance-retirement.md).

## Verification evidence

- `npm install`: passed; 109 packages audited, zero reported vulnerabilities.
- `npm test`: passed; 12 files and 47 tests.
- `npm run build`: passed under strict TypeScript; Vite production bundle created.
- `dotnet build -c Release`: passed with 0 warnings and 0 errors using SDK 9.0.316.
- `dotnet test`: passed; 78 tests across 2 assemblies, 0 failures, 0 skipped.
- Loopback runtime smoke test: passed for session creation, authenticated manifest retrieval, and an immutable binary resource. The manifest reported 2 actors and 10 descriptors; the resource returned the expected 36 bytes, protocol header, binary content type, range support, cache policy, and hash ETag.
- Headless Chrome production correctness run: passed using software WebGL/WebGL2. Two actors, two chunks, six points, two polygons, and all 152 fixture resource bytes were resident; first geometry callback was observed before full completion. No performance claim is made from this run.
- Production JavaScript bundle: 844,367 total minified bytes across 14 chunks; the application entry is 92,598 bytes and the largest VTK chunk is 215,278 bytes. Every JavaScript chunk is enforced below 500,000 bytes at build time. See [bundle code-splitting evidence](evidence/bundle-code-splitting.md).
- `npm run test:e2e`: passed; 2 Playwright/Chrome correctness tests cover WP2–WP4 behavior, clean event-stream snapshot recovery, retry exhaustion, visible integrity failure, subsequent reload recovery, and an all-request loopback assertion.
- Headed WP5 benchmark: passed functionally and passed all quantitative gates over three fresh Windows runs; median complete load 1.611 s, orbit 50.25 FPS, pick 92.8 ms p95, tracked browser peak 262.8 MiB, and five-unload retention 1.193%.

## Known deviations and limitations

- Configured ports bind IPv4 and IPv6 localhost; the ephemeral-port fallback remains IPv4-only because Kestrel does not support dynamic port allocation with `ListenLocalhost`.
- Session lifetime, session capacity, and command rate are process-local controls. Production session creation is now desktop-authorized and the exact-origin WebView adopts its session into an HttpOnly/Strict cookie; PEND-002 is complete. WebSocket subscriber queues are bounded to 32 messages and removed on disconnect.
- `PMXSCN2` rectangle selection now uses the full-cell server BVH; v1/synthetic scenes retain the visible-triangle fallback. The ten-case committed corpus and 612-case seeded randomized differential cover every supported cell type against ActiViz 5.8. Invalid/inverted cells, arbitrary non-box frusta in the standalone oracle, exhaustive input space, and later VTK versions are not claimed.
- Scale-factor, time-increment/modal, and complex harmonic real-process deltas are qualified. Host payload reduction, bounded pooled request streaming, compact V2 parsing, browser stable-paint qualification, subphase allocation telemetry, allocation-free median BVH refit, compact locator retention, invariant-topology capture reuse, and direct generated frame resources are complete. Retained displacement bases are optional unless the remaining read-side frame storage proves constraining.
- Browser annotation context actions and adaptive selections through the safe eight-million-ID `PMXSEL1` capacity are complete.
- Began capability-by-capability retirement of the hidden ActiViz backend. Annotation add, named removal, and remove-all now execute only in VTK.js when opted in, while default mode retains the original calls. Packaged interaction and settings-dialog smokes cover authoritative move/edit/reset/delete/settings behavior and compatibility-bypass traces.
- Camera ownership now resides in retained browser snapshots for VTK.js mode. Standard/custom/reset, normal/vertical visible-bounds fitting, camera-adjusting redraws, box zoom, actor visibility/removal, animation-frame bounds adjustment, and follower apply/update/remove across frame or full-scene changes bypass ActiViz. Exploded preview, section clipping/caps, and section-aware rectangle selection also bypass hidden rendering.
- VTK linear volume types 10-14 and quadratic types 24-27/29 now match the piecewise-linear subdivisions, legacy contour tables, exact-coordinate locator merging, and coincident-cell suppression used by `vtkCutter`. Evidence includes ambiguous generated cases, a 240-case three-seed canonical/warped differential, a 12-case shared-face/shared-node multi-cell differential, and a real 2,150-element C3D13 process smoke. Introducing fully isoparametric curved contours would intentionally differ from the legacy renderer; later VTK releases are not claimed.
- The small deterministic fixture intentionally uses byte arrays; the large generator uses bounded pooled direct streaming.
- Linux x64 functional qualification passed under Ubuntu 22.04 on WSL2: the self-contained server lifecycle, production client, Chromium/WebGL2 fixture rendering, exact counts, and credentialed shutdown all passed. This is software-WebGL correctness evidence, not a physical-GPU benchmark or Linux desktop-shell claim. See [Linux x64 functional evidence](evidence/linux-x64-functional.md).
## 2026-09-02 — Actor registry/presentation retirement

- Retired hidden ActiViz calls for `ContainsActor`, rename/color, per-actor contour visibility, and hide/show in VTK.js mode while leaving the default backend unchanged.
- Made captured actor identity authoritative across scalar/extrema, selection, follower, exploded-view, animation, and contour retained state. Browser chunk pipelines now adopt renamed actor names as well as RGBA.
- Added bounded retained `prepomax.actor-contours.updated-v1` state and atomic client validation; per-actor and global contour flags compose with finite-scalar availability.
- Passed 61/61 client tests, 80/80 renderer .NET tests, strict client build, full Release/x64 staging, 2/2 Playwright, and the packaged one-actor registry/server-presentation smoke.
- The packaged diagnostic exposed a separate initial-empty-scene event/session race. It was recorded as PEND-012 and is closed in the resynchronization slice below. The subsequent temporary actor-highlight slice is also complete; PEND-004 continues with scene lifecycle and authoritative selection clearing.

## 2026-09-02 — Late first-topology resynchronization

- Closed PEND-012: explicit current session credentials now override stale hostname-scoped WebView cookies, and the first non-empty topology triggers one trusted bootstrap refresh.
- Added exact stable scene acknowledgements with scene ID, revision, actor count, and pipeline-chunk count; full replacements still acknowledge after follower/camera application.
- Packaged actor-presentation acceptance now requires a non-empty browser pipeline and passed with exact rename/RGB/opacity/visibility/contour diagnostics. The adjacent visibility/camera smoke also passed.
- Verification: client 61/61, renderer .NET 80/80, Playwright 2/2, strict production build, and full Release/x64 staging.
## 2026-09-03 - authoritative selection clear and browser scene reset

- Retired hidden selection clearing in VTK.js mode. `Clear3DSelection` publishes a fresh empty selection snapshot and independently clears temporary actor highlights; `ClearButKeepParts` also clears both browser-owned states.
- Expanded full scene reset to clear captured topology, animation/extrema/contour/highlight state, selections, annotations, sphere/coordinate/caption/directional overlays, follower state, exploded offsets, section view, and box zoom.
- Replacement reloads now reuse the already-adopted authenticated browser session. This fixed the production-only HTTP 401 caused by attempting anonymous session creation after `scene.replaced`.
- Empty/current replacement snapshots are acknowledged after stable load, and overlapping replacement events coalesce around the authoritative fetched manifest.
- Hardened packaging: staging removes obsolete hashed client bundles after validating the exact output directory, and the server marks `index.html` `no-store` for persistent WebView profiles.
- Added deterministic scene-clear smoke synchronization and corrected locator evidence to select the last positive pre-clear topology sample.
- Packaged `BENDING_NEON36.pmx` acceptance passed with 15 actors, 828,619 points, 1,623,416 triangles, 2,140,668 locator points, 931,033 locator cells, and `BrowserSceneClearApplied=True`.

## Core part-construction slice - 2026-09-03

- Added explicit no-handle single/batch part submission. In VTK.js mode, successful renderer-neutral capture now bypasses duplicate hidden `vtkMaxActor` construction; capture rejection or failure uses a traced legacy fallback.
- Migrated ordinary geometry/model part drawing only. Result scalar/animation, selection cache, points/generic cells, OpenFOAM, transformations, and export/readback remain intentionally open.
- Added packaged acceptance gating through `-ExerciseCorePartConstruction`.
- `BENDING_NEON36.pmx` passed with 15 actors, 828,619 render points, 1,623,416 triangles, 2,140,668 locator points, 931,033 locator cells, `CorePartConstructionBrowserOnly=True`, and clean parent/child shutdown.
- Regression baseline remains green: client 63/63, .NET 80/80, Playwright 2/2, isolation audit pass, and full Release/x64 staging/build pass.

## Interactive animation-frame activation - 2026-09-03

- Removed the hidden `vtkControl.SetAnimationFrame` mutation from VTK.js interactive frame changes. Browser capture/transport/application is now the only runtime activation path; default ActiViz behavior is unchanged.
- Retained hidden animation construction and metadata specifically for the still-open AVI/PNG export path, avoiding a silent export regression.
- Extended packaged animation acceptance to require the result-animation bypass trace and `LegacyActivationMs=0`.
- The committed tetrahedral FRD passed with one actor, changed frame-1 positions, stable topology, `AnimationFrameActiVizBypassed=True`, and clean shutdown.
- Regression baseline: client 63/63, .NET 80/80, Playwright 2/2, isolation audit pass, and full Release/x64 staging/build pass.

## Retained 3MF actor input - 2026-09-03

- Added browser-authoritative export projection for captured part coordinates, rendered triangles, scalars, color/contour state, visibility, and model edges.
- VTK.js `GetVtkMaxActorData` now returns retained captured actors without querying the hidden VTK registry for browser-only scenes. When auxiliary legacy actors may exist, it merges only uncaptured names; default renderer behavior is unchanged.
- Added packaged `-ExerciseThreeMfExportInput` acceptance. The C3D4 case passed with one browser-only actor, 12 points, 4 triangles, and `ThreeMfExportRetainedInput=True`.
- A real archive-write probe exposed missing `lib3mf.dll` in the repository/staged output. Native archive production is therefore explicitly unqualified and added to packaging work.
- Regression baseline remains green: client 63/63, .NET 80/80, Playwright 2/2, isolation audit, Release/x64 build/staging, and diff checks.
- The remaining hidden `Clear` and `ClearButKeepParts` calls are not claimed retired: scalar-result, generic, point, selection-handle, OpenFOAM, transformed, and export-only auxiliary paths can still populate the hidden registry. This was the boundary entering the palette/range slice below.

## Retained 3MF lookup table - 2026-09-03

- Added managed sampling for all 14 PrePoMax color spectra, including brightness, reversal, RGB/HSV/Lab/diverging interpolation, and manual below/above colors.
- Retained current-frame scalar ranges from captured visible contour actors and animation-wide ranges from existing frame metadata.
- VTK.js `GetLookupTable` now emits `lookup-table-managed` and never queries `vtkControl.GetLookupTable`; the default renderer is unchanged.
- Packaged acceptance passed all palettes, a manually extended 11-color table, one actor / 12 points / four triangles, and clean shutdown.
- The next slice is duplicate hidden static scalar-result construction; generated edges, transforms/auxiliary export, native `lib3mf.dll`, and AVI/PNG remain documented boundaries.

## Static scalar-result construction - 2026-09-03

- Added a browser-only `AddScalarFieldOn3DCells` path with an explicit traced legacy fallback; the default renderer remains unchanged.
- Removed hidden actor-scalar and scalar-formatting mutations from VTK.js update/redraw seams while retaining browser camera adjustment.
- Completed retained actor round-trip data with node/element IDs, triangle types, and locator geometry.
- Added `(sceneId, revision)` browser acknowledgement and `-ExerciseStaticScalarConstruction` packaged coverage.
- Deterministic acceptance published construction range `-3..7` and scalar-only update range `-5..9`; committed-FRD `DISP/ALL` acceptance retained one actor / 12 points / four triangles / four locator points / one locator cell and then passed frame-1 activation with zero hidden work.
- At this milestone animated AVI/PNG storage, selection-handle construction, generic/point/OpenFOAM actors, transformations, and auxiliary export remained open; the selection-handle point/line/surface paths are retired in the subsequent sections.

## Cached selection-surface construction - 2026-09-03

- Extended `SelectionCache` with bounded renderer-neutral `vtkMaxActorData[]` entries while preserving the native actor cache for the default renderer.
- Element, surface, arbitrary-face, and surface-edge cache seams now re-submit renderer-neutral data in VTK.js mode instead of retaining/reinserting `vtkMaxActor` handles.
- Added browser-owned transient triangle/quad selection actors, collision protection against retained part names, authenticated discrete removal, public selection-clear integration, and export exclusion.
- Added `-ExerciseSelectionSurfaceConstruction`; packaged C3D4 acceptance proved cache retrieval, browser add/clear, no legacy fallback, retained base topology, and clean process shutdown.
- Point actors and line-only edge previews were subsequently retired by the transient-primitive slice below. Animated construction/export and other generic paths remain open.

## Selection point/line construction - 2026-09-03

- Added bounded retained point/line primitives with absolute coordinates, indexed line segments, RGBA, display point size, and line width. Browser pipelines are origin-relative, non-pickable, section-clipped, and recreated after navigation.
- Routed selection-layer `Add3DNodes` and the common single-cell `Add3DCells` seam browser-only after successful capture. VTK linear lines are retained directly; quadratic edges expand into start-mid and mid-end segments. Unsupported content keeps a traced visible fallback.
- Integrated primitive replacement with public selection clear, full scene clear, shutdown, and navigation recovery while excluding the transient state from model topology, locators, picking, and export.
- Packaged C3D4 acceptance reported 2 transient actors / 5 points / 2 lines and null native handles, then 0/0/0 after `Clear3DSelection`, with `SelectionPrimitiveConstructionBrowserOnly=True`. The separately executed cached-surface regression also remained green.
- Verification: client 66/66, .NET 80/80, Playwright 2/2, isolation audit pass, strict production client build, Release/x64 application build, and packaged staging pass.
- At this milestone PEND-004 next targeted animated-result construction plus the coupled AVI/PNG dependency; the following slice retires eager construction and leaves browser-owned capture/encoding open. Generic auxiliary actors, OpenFOAM, transformations, and final hidden-registry cleanup remain documented.

## Animated-result construction and export isolation - 2026-09-04

- Removed eager hidden `vtkMaxActor` and per-frame actor-array construction from VTK.js animation setup. Registration now succeeds only after retained frame-zero capture, and ordinary animation setup emits `construction-browser-only`.
- Moved animation metadata, scale-factor queries, and acceleration preference behind the retained adapter. Interactive frame activation remains the sole browser/server path with zero native activation work.
- Preserved AVI/PNG compatibility through explicit lazy materialization: the first export reconstructs retained sources and cloned metadata, applies acceleration and hidden-part visibility, and is reused only within that animation scene.
- Extended packaged animation acceptance with construction/export gates. The committed one-tetra FRD proved no pre-export native materialization, wrote two nonempty PNG frames through the public image-export seam, and then activated browser frame 1 with stable topology and `LegacyActivationMs=0`.
- Verification: client 66/66, .NET 80/80, Playwright 2/2, isolation audit pass, full Release/x64 build/staging, and clean process shutdown.
- PEND-004 next replaces export-only render-window capture, starting with browser PNG frames and deterministic acknowledgement; AVI encoding/codec behavior remains an explicit decision.

## Browser-owned animation PNG export - 2026-09-04

- Routed `SaveAnimationAsImages` through retained frame activation and WebView2 composited PNG capture in VTK.js mode; the default renderer remains unchanged and only AVI uses lazy native animation materialization.
- Added single-flight stable-paint acknowledgement for both incremental frame updates and first-frame full-scene replacement, with a 30-second timeout and lifecycle cancellation.
- Added bounded PNG signature/IHDR/dimension/size validation, same-directory staging, per-file atomic replacement, failure cleanup, awaited WinForms UI handling, and preserved forward/step/swing naming semantics.
- Packaged PNG-only acceptance wrote two valid, distinct frames and reported `AnimationPngCaptureBrowserOnly=True`, `AnimationExportAdapterMaterialized=False`, `AnimationConstructionBrowserOnly=True`, and `AnimationFrameActiVizBypassed=True`; no acceptance or staging files remained.
- Full regression verification is recorded in [browser animation PNG evidence](evidence/browser-animation-png-export.md). PEND-004 now targets the explicit AVI encoder/codec decision; generic/OpenFOAM actors, transformations, widgets, and final hidden-registry cleanup remain open.

## Browser-owned animation AVI export - 2026-09-04

- Selected a self-contained AVI 1.0 Motion JPEG path for VTK.js mode: no FFmpeg/runtime codec dependency, no system dialog, and one bounded composited browser JPEG held at a time.
- Added strict JPEG dimension/marker validation and a checked streaming RIFF writer with `avih`/`strh`/`strf`, `MJPG`, padded `00dc` keyframes, `idx1`, durable finalization, same-directory staging, and atomic destination replacement.
- Made the movie UI asynchronous and explicit: VTK.js reports its built-in MJPEG encoder and browser-managed frame storage; legacy ActiViz retains the original codec/RAM controls.
- Removed the now-unused native animation export adapter and its retained-source/metadata accessors. No VTK.js animation path constructs, activates, captures, or encodes through hidden ActiViz.
- Combined packaged acceptance validated PNG plus AVI, parsed exact RIFF/index structure, checked two distinct embedded JPEGs, reported both browser-only gates true and adapter materialization false, and removed output/staging files.
- Evidence: [browser animation AVI export](evidence/browser-animation-avi-export.md). PEND-004 next targets transformations and generic auxiliary/OpenFOAM actor construction; configurable modern video formats and cancellation are explicit future enhancements, not renderer blockers.

## Browser-owned transformation copies - 2026-09-04

- Routed symmetry, linear/circular pattern, apply, hide, and show through bounded retained host matrices without hidden ActiViz calls in VTK.js mode.
- Added resource-sharing non-pickable browser actor instances, legacy-compatible sequential composition, floating-origin matrix conversion, lifecycle replay/clear, presentation/visibility propagation, and post-scene acknowledgement.
- The first packaged run exposed and fixed an acknowledgement ordering race when retained matrices arrived before browser chunks. Real-model rerun passed with seven matrices, a positive transformed-actor count, zero pickable actors, and successful hide/show.
- Evidence: [browser transformation retirement](evidence/browser-transformations-retirement.md). PEND-004 next targets generic auxiliary/OpenFOAM construction; transformed section-cap/export projection remains explicitly pending.

## Browser-owned auxiliary and managed OpenFOAM construction - 2026-09-04

- Routed non-pickable base-layer auxiliary surfaces through retained scene capture and points/lines through a bounded persistent primitive set. Persistent primitives remain separate from selection primitives, survive selection clear, and support visibility, rename, removal, navigation replay, and scene cleanup.
- Preserved explicit fallback for unsupported, pickable, and overlay content. Array submission classifies each item independently so one rejected actor does not duplicate accepted browser actors in the hidden backend.
- VTK.js `AddOpenFoamMesh` now bypasses the native reader before file access. Existing Baram callers fall through to the managed `polyMesh` boundary reader, whose faces and unique edges use the renderer-neutral routes above.
- Packaged `BENDING_NEON36.pmx` acceptance reported 40 retained actors / 904,768 render points / 1,766,120 triangles with unchanged locator totals, then proved a synthetic auxiliary surface and two primitives remained after selection clear, honored hide/show, removed discretely, returned null native handles, and used no fallback.
- Verification: client 69/69, strict client build, and targeted Release/x64 packaged smoke passed at this checkpoint; the full regression/staging run is recorded with the evidence after completion.
- Evidence: [auxiliary/OpenFOAM construction retirement](evidence/auxiliary-openfoam-construction-retirement.md). PEND-004 next targets native-handle `AddActor` and remaining pickable/overlay generic construction. Volumetric OpenFOAM parity and primitive transformation/3MF projection remain explicit boundaries.

## Generic and native-handle construction retirement - 2026-09-04

- Broadened non-selection cell capture to supported pickable and non-pickable surfaces while preserving pickable flags and locator data. Non-pickable overlay points now use persistent browser primitives.
- Enforced the selection-cache architecture: active VTK.js caches replay renderer-neutral actor data, while unexpected `vtkMaxActor` handles are diagnosed and blocked before hidden-renderer mutation.
- Added named primitive and per-pipeline pickability diagnostics plus `-ExerciseGenericConstruction` packaged acceptance.
- The packaged fixture reported `GenericConstructionBrowserOnly=True`, browser `True|True` for pickable surface/named overlay point, null returned handles, authoritative removal, and clean shutdown. The independent auxiliary regression remained green.
- Verification: client 69/69, .NET 80/80, Playwright 2/2, isolation audit pass, strict client build, and Release/x64 application build/staging pass.
- Evidence: [generic/native-handle construction retirement](evidence/generic-native-handle-construction-retirement.md). At that milestone PEND-004 next targeted pickable points and unsupported-cell classification; the following slice records that outcome. Exact overlay depth ordering remains a visual boundary.

## Generic cell-type classification - 2026-09-07

- Extended retained surface conversion with VTK triangle strips (type 6) and pixels (type 8), preserving source-element provenance and deterministic winding/perimeter triangulation.
- Extended persistent primitive conversion with VTK polylines (type 4), expanded into adjacent line segments.
- Made generic surface classification atomic: if any cell type is outside 5/6/8/9/22/23, the complete actor is rejected with the sorted type list instead of rendering a misleading subset.
- Audited `Add3DNodes` callers and found no production pickable-point producer. Direct pickable primitive capture remains explicitly rejected because the persistent primitive pipeline is non-pickable.
- Added `-ExerciseCellClassification` process acceptance. The packaged fixture reported exact 4-triangle and 2-line expansion, mixed type-5/type-10 and pickable-point rejection, browser `true|true`, authoritative removal, `CellClassificationBrowserOnly=True`, and clean shutdown.
- Verification: client 69/69, .NET 80/80, Playwright 2/2, isolation audit 6 projects / 34 C# files / zero forbidden references, strict client build, and Release/x64 application build/staging pass.
- Evidence: [cell-type classification retirement](evidence/cell-type-classification-retirement.md). PEND-004 now advances to the remaining widget/tool seams and hidden-registry dependency audit; volume-cell rendering, exact overlay depth ordering, transformed/export projection, and final cleanup remain explicit.

## Browser-owned orientation marker - 2026-09-07

- Routed `SetCoorSysVisibility` through retained `prepomax.orientation-marker.updated-v1` state in VTK.js mode; default ActiViz behavior is unchanged.
- Added a non-interactive lower-left vtk.js orientation marker with dedicated renderer ownership, main-camera synchronization, resize handling, 50-pixel minimum / 200-pixel maximum, and legacy red/green/blue colors quantized to the actor's 8-bit contract.
- Retained visibility across scene-view reconstruction/navigation and deterministically disabled/deleted the marker renderer and axes actor at disposal.
- Added strict message normalization tests, diagnostic camera/palette/size/actor state, and `-ExerciseOrientationMarker` packaged acceptance.
- Browser testing exposed a vtk.js 36.6 `AxesActor` getter defect (`model.getXConfig`); production diagnostics avoid the broken getter and retain explicit immutable palette values. Both progressive-load and recovery browser tests passed after the correction.
- Packaged fixture acceptance reported hidden `false|0`, visible `true|1`, camera alignment, the 200-pixel bound, expected palette, `OrientationMarkerBrowserOnly=True`, and clean parent/child shutdown.
- Verification: client 71/71, .NET 80/80, Playwright 2/2, isolation audit 6 projects / 34 C# files / zero forbidden references, strict client build, Release/x64 application build/staging, PowerShell syntax, and diff checks passed.
- The stock vtk.js axes actor renders arrow shafts without legacy X/Y/Z captions. Functional orientation/visibility ownership is retired from hidden ActiViz, while exact shaft/caption appearance remains explicitly assigned to PEND-005.
- Evidence: [orientation marker retirement](evidence/orientation-marker-retirement.md). The scale visibility/unit slice subsequently completed; PEND-004 next targets scalar-legend state and the remaining result widgets.

## Browser-owned scale widget and ActiViz launch audit - 2026-09-07

- Routed scale-widget visibility and length-unit updates through retained `prepomax.scale-widget.updated-v1` state in VTK.js mode; default ActiViz behavior is unchanged.
- Added a non-interactive DOM scale overlay that reproduces the legacy 400-pixel target, nearest one-fifth-decade model-length rounding, adjusted width, alternating five-field bar, six `G4`-equivalent labels, and unit suffix.
- Camera animation, explicit rendering, resize, and view reconstruction recompute or restore the scale. Unit input is limited to 64 printable characters and DOM text assignment prevents markup interpretation.
- Packaged fixture acceptance reported hidden/visible states, positive `0.68 mm`, six labels, 402.021-pixel width, `ScaleWidgetBrowserOnly=True`, and clean shutdown.
- Audited physical ActiViz removal. VTK.js is the visible renderer when selected, but `vtkControl` is still constructed unconditionally and its project/assembly types remain startup dependencies. Added `scripts/start-prepomax-vtkjs.ps1` and `docs/VTKJS_ACTIVIZ_STATUS.md` so the launch mode and remaining removal gate are explicit.
- Verification: client 73/73, .NET 80/80, Playwright 2/2, isolation audit 6 projects / 34 C# files / zero forbidden references, strict client build, Release/x64 build/staging, packaged scale smoke, and launcher/PowerShell syntax passed.
- Evidence: [scale widget retirement](evidence/scale-widget-retirement.md). The scalar-legend, categorical color-bar, status-block, and shared-layout slices subsequently completed; PEND-004 now targets smoothing/highlight settings and user-pick mode.

## Browser-owned scalar legend - 2026-09-08

- Added retained `prepomax.scalar-legend.updated-v1` state for visibility, result and complex metadata, units, min/max policy, `G`/`E` format, background/border, effective range, and managed sampled palette.
- Routed scalar spectrum, text, number format, background, border, and initial-position seams away from hidden ActiViz when VTK.js mode is active; full scene clear hides the retained legend.
- Added a safe top-left DOM legend and applied its range/palette to current and future vtk.js mappers. State survives reconstruction and refreshes after scene/scalar/frame, contour, and visibility changes.
- Packaged fixture acceptance reported `ScalarLegendBrowserOnly=True`, hidden/visible lifecycle, `Displacements`/`Magnitude`/`mm`, `Complex: Real`, `Automatic`, `E3`, range `0..1`, nine colors, ten labels, background/border, and clean shutdown.
- Verification: client 75/75, .NET 80/80, Playwright 2/2, isolation audit 6 projects / 34 C# files / zero forbidden references, strict client build, Release/x64 build/staging, and packaged scalar-legend smoke passed.
- Evidence: [scalar legend retirement](evidence/scalar-legend-retirement.md). The categorical color-bar, status-block, and shared-layout slices subsequently completed; PEND-004 now targets smoothing/highlight settings and user-pick mode, while exact visual typography remains PEND-005.

## Browser-owned categorical color bar - 2026-09-08

- Added retained `prepomax.color-bar.updated-v1` state with bounded equal ordered RGB/label arrays, visibility, background, and border.
- Routed initial position, replacement, append, appearance, and hide/clear seams away from hidden ActiViz in VTK.js mode. Default ActiViz behavior is unchanged.
- Added a safe top-left DOM list, legacy underscore/hyphen label normalization, navigation/view-reconstruction replay, and full-scene clear integration.
- Packaged fixture acceptance reported `ColorBarBrowserOnly=True`, exact red/blue/green order, three normalized labels, visible-to-empty hidden lifecycle, appearance flags, and clean shutdown.
- Verification: client 77/77, .NET 80/80, Playwright 2/2, isolation audit 6 projects / 34 C# files / zero forbidden references, strict client build, Release/x64 build/staging, and packaged color-bar smoke passed.
- Evidence: [categorical color-bar retirement](evidence/categorical-color-bar-retirement.md). The status-block/shared-layout slice subsequently completed; PEND-004 now targets smoothing/highlight settings and user-pick mode, while exact typography and unusually large-list presentation remain PEND-005.

## Browser-owned status block and shared widget layout - 2026-09-08

- Added bounded retained `prepomax.status-block.updated-v1` and `prepomax.result-widget-layout.updated-v1` state, complete client validation, safe DOM lines, and explicit top-left/top-right pixel placement.
- Routed visibility, content, background, border, result-position reset, and color-position reset away from hidden ActiViz in VTK.js mode; default ActiViz behavior is unchanged.
- Preserved current-culture legacy text generation across all six result field types and added animation-frame step/increment/time plus scale-factor/harmonic-angle updates.
- Packaged acceptance reported `StatusBlockBrowserOnly=True`, exact 20-pixel placement for all three result overlays, frequency and harmonic content, hidden lifecycle, and clean shutdown.
- Verification: client 81/81, .NET 80/80, Playwright 2/2, isolation audit 6 projects / 34 C# files / zero forbidden references, strict client build, Release/x64 build/staging, packaged status/layout smoke, PowerShell syntax, and diff checks passed.
- Evidence: [status block and widget layout retirement](evidence/status-block-and-widget-layout-retirement.md). PEND-004 next targets smoothing/highlight settings and user-pick mode; exact typography and settings gestures remain review items.

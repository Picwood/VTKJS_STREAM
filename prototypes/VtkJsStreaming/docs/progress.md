# Implementation progress

Last updated: 2026-08-06

## WP0 — baseline and reproducibility

Status: implemented; Windows verification passed, Linux verification pending.

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
- bundle code splitting (currently approximately 709 KB minified / 195 KB gzip).

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

Status: implemented and benchmarked on Windows x64; Linux verification pending.

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

1. Run the clean-checkout functional and benchmark commands on a supported Linux x64 host and record pass/fail evidence.
2. Code-split the production client bundle.

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
- Added deterministic adaptive node overlays: all authoritative IDs are retained, while more than one million rendered matches use a stable stride capped at one million overlay points and report `overflow=true`. Unit tests cover exact/sampled boundaries; Playwright decodes 2,000,001 IDs; a packaged `fatigue_test.frd` WebView2 smoke transmitted 2,000,001 unique IDs in 8,000,044 bytes and produced a visible exact overlay for the 6,503 matching locator nodes.
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

## Verification evidence

- `npm install`: passed; 109 packages audited, zero reported vulnerabilities.
- `npm test`: passed; 8 files and 26 tests.
- `npm run build`: passed under strict TypeScript; Vite production bundle created.
- `dotnet build -c Release`: passed with 0 warnings and 0 errors using SDK 9.0.316.
- `dotnet test`: passed; 78 tests across 2 assemblies, 0 failures, 0 skipped.
- Loopback runtime smoke test: passed for session creation, authenticated manifest retrieval, and an immutable binary resource. The manifest reported 2 actors and 10 descriptors; the resource returned the expected 36 bytes, protocol header, binary content type, range support, cache policy, and hash ETag.
- Headless Chrome production correctness run: passed using software WebGL/WebGL2. Two actors, two chunks, six points, two polygons, and all 152 fixture resource bytes were resident; first geometry callback was observed before full completion. No performance claim is made from this run.
- Production JavaScript bundle: approximately 720 KB minified / 198 KB gzip. Vite reports the >500 KB chunk warning; code splitting remains an explicit optimization item.
- `npm run test:e2e`: passed; 2 Playwright/Chrome correctness tests cover WP2–WP4 behavior, clean event-stream snapshot recovery, retry exhaustion, visible integrity failure, and subsequent reload recovery.
- Headed WP5 benchmark: passed functionally and passed all quantitative gates over three fresh Windows runs; median complete load 1.611 s, orbit 50.25 FPS, pick 92.8 ms p95, tracked browser peak 262.8 MiB, and five-unload retention 1.193%.

## Known deviations and limitations

- Configured ports bind IPv4 and IPv6 localhost; the ephemeral-port fallback remains IPv4-only because Kestrel does not support dynamic port allocation with `ListenLocalhost`.
- Session lifetime, session capacity, and command rate are process-local prototype controls; production bootstrap/credential handoff remains controller integration work. WebSocket subscriber queues are bounded to 32 messages and removed on disconnect.
- `PMXSCN2` rectangle selection now uses the full-cell server BVH; v1/synthetic scenes retain the visible-triangle fallback. The ten-case committed corpus and 612-case seeded randomized differential cover every supported cell type against ActiViz 5.8. Invalid/inverted cells, arbitrary non-box frusta in the standalone oracle, exhaustive input space, and later VTK versions are not claimed.
- Scale-factor, time-increment/modal, and complex harmonic real-process deltas are qualified. Host payload reduction, bounded pooled request streaming, compact V2 parsing, browser stable-paint qualification, subphase allocation telemetry, allocation-free median BVH refit, compact locator retention, invariant-topology capture reuse, and direct generated frame resources are complete. Retained displacement bases are optional unless the remaining read-side frame storage proves constraining.
- Browser annotation context actions and adaptive selections through the safe eight-million-ID `PMXSEL1` capacity are complete.
- Began capability-by-capability retirement of the hidden ActiViz backend. Annotation add, named removal, and remove-all now execute only in VTK.js when opted in, while default mode retains the original calls. The packaged real-FRD interaction smoke requires compatibility-bypass traces in addition to authoritative move/edit/reset/delete behavior.
- Camera ownership now resides in retained browser snapshots for VTK.js mode. Standard/custom/reset, normal/vertical visible-bounds fitting, camera-adjusting redraws, box zoom, actor visibility/removal, animation-frame bounds adjustment, and follower apply/update/remove across frame or full-scene changes bypass ActiViz. Exploded preview, section clipping/caps, and section-aware rectangle selection also bypass hidden rendering.
- VTK linear volume types 10-14 and quadratic types 24-27/29 now match the piecewise-linear subdivisions, legacy contour tables, exact-coordinate locator merging, and coincident-cell suppression used by `vtkCutter`. Evidence includes ambiguous generated cases, a 240-case three-seed canonical/warped differential, a 12-case shared-face/shared-node multi-cell differential, and a real 2,150-element C3D13 process smoke. Introducing fully isoparametric curved contours would intentionally differ from the legacy renderer; later VTK releases are not claimed.
- The small deterministic fixture intentionally uses byte arrays; the large generator uses bounded pooled direct streaming.
- Linux x64 remains unverified on the current Windows-only host, so the cross-platform functional acceptance gate is failed/pending.

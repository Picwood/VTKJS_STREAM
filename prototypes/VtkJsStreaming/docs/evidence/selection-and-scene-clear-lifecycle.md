# Selection and scene-clear lifecycle evidence

Date: 2026-09-03

## Outcome

Authoritative selection clearing is retired from hidden ActiViz in VTK.js mode. Full scene clear is browser-authoritative for visible topology and retained renderer state, but the hidden registry cleanup calls are deliberately retained until core/generic actor construction stops creating hidden `vtkMaxActor` instances.

## Implementation

- `VtkJsRendererControl.ClearSelectionState` publishes a newly allocated empty selection snapshot.
- `FrmMain.Clear3DSelection` clears browser selection and temporary highlight state and emits compatibility-bypass evidence.
- `ClearCapturedScene` resets topology, animation/extrema/contours/highlights, selection, annotations, sphere symbols, coordinate axes/captions, directional glyphs, follower state, exploded offsets, section view, and box zoom.
- Replacement reloads reuse the adopted authenticated `RenderApi` session and announced scene ID. No anonymous production session is created.
- Empty snapshots acknowledge stable application with `prepomax.scene.applied-v1` (`actorCount=0`, `pipelineChunks=0`). Overlapping replacement events coalesce to the authoritative fetched snapshot.
- The build script validates and cleans only the exact staged client directory before copying, preventing obsolete hashed bundles; `index.html` is served with `no-store`.

## Process findings

The first iterations were intentionally recorded as failures rather than accepted on partial evidence:

1. A fixed-delay harness could clear before retaining the populated baseline.
2. Browser diagnostics reached zero, but no completion arrived because replacement recovery attempted anonymous production session creation and received HTTP 401.
3. Overlapping replacement events exposed an expected stale event ID versus newer fetched snapshot.
4. After the renderer passed, the harness incorrectly chose the final zero locator line instead of the positive pre-clear sample.

The final implementation uses authenticated reload, snapshot-authoritative event coalescing, a trigger plus stable actor/browser-count gate, and the last positive locator sample.

## Acceptance

- Client unit tests: 63/63.
- Strict TypeScript/Vite production build: pass.
- Targeted Playwright progressive/replacement test: pass, including empty `PMXSCN1` replacement, zero pipeline, and zero-actor acknowledgment.
- Full Release/x64 build and staging: pass with existing architecture warnings only.
- Packaged Windows smoke, `BENDING_NEON36.pmx`: 15 actors; 828,619 points; 1,623,416 triangles; 2,140,668 locator points; 931,033 locator cells; `BrowserSceneClearApplied=True`; graceful parent/child shutdown.

## Explicit remaining dependency

`_vtk.Clear()` and `_vtk.ClearButKeepParts()` still clean hidden actors created by core cells, scalar actors, and generic/non-part actor paths. Removing those calls now would leak/stale the hidden registry. The next bounded PEND-004 slice is renderer-neutral core actor construction and actor handles; only then can final scene lifecycle retirement be claimed.

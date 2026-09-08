# Actor registry and presentation retirement

Date: 2026-09-02

## Scope and outcome

The VTK.js branch no longer calls or queries the hidden ActiViz control for `ContainsActor`, actor rename/color, per-actor contour visibility, or hide/show. The default ActiViz branch is unchanged. Temporary highlight and selection clearing remain open as the next PEND-004 slice.

The authoritative registry is `VtkJsSceneCapture`. Rename propagates through retained scalar, extrema, selection, follower, exploded-view, animation, and contour keys before the existing authenticated server mutation is published. The browser now updates each chunk pipeline's actor name as well as RGBA, so name-based selection and retained state continue to address the actor after a rename.

Per-actor contour state uses the bounded full-replacement WebView message `prepomax.actor-contours.updated-v1` (4,096 unique actors, 1,024 characters per name). It composes with the global contour toggle and scalar availability. Invalid, duplicate, or oversized messages are rejected atomically.

## Verification

- Client unit tests: 61/61 passed, including valid cloning and malformed/duplicate/bound rejection.
- Strict TypeScript/Vite production build: passed.
- Renderer .NET tests: 80/80 passed (11 contracts, 69 server).
- Playwright: 2/2 passed. The progressive test proves live server rename/color updates pipeline identity and RGBA without topology requests, then suppresses that actor's scalar visibility through the host contour message.
- Complete MVP staging and `PrePoMax.sln` Release/x64: passed with the repository's existing warnings.
- Packaged WinForms process smoke using `sphere-symbol-smoke.inp`: passed with one actor, 12 render points, four triangles, full locator topology, registry true/old-removed/new-present assertions, server manifest rename/RGBA verification, compatibility-bypass traces, graceful close, and child-process exit.

Command:

```powershell
.\scripts\smoke-vtkjs-winforms.ps1 `
  -ModelPath .\prototypes\VtkJsStreaming\tests\fixtures\sphere-symbol-smoke.inp `
  -ExerciseActorPresentation
```

## Startup defect found and closed

The new process diagnostic exposed that a WebView bootstrapped while the host scene is empty could miss the first later `scene.replaced` event; a stale hostname-scoped cookie then prevented resynchronization. PEND-012 is now complete. The packaged check requires a positive browser actor/chunk acknowledgement and verifies the final renamed color/contour state, in addition to the FrmMain/capture/server assertions. See [resynchronization evidence](late-first-topology-resynchronization.md).

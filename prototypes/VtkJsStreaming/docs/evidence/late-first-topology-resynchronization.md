# Late first-topology WebView resynchronization

Date: 2026-09-02

Status: complete

## Defect

PrePoMax can start the renderer and trusted WebView session while the captured scene is empty, then publish the first non-empty topology after model import. If the browser event stream was not established in that interval, it remained on the empty snapshot. Persistent WebView cookies are scoped to the loopback hostname rather than its ephemeral port; a stale cookie could also override the fresh bootstrap credential and make a resync fail with `session-invalid`.

## Resolution

- Explicit `X-PrePoMax-Session` and Bearer credentials now take precedence over cookies. This preserves cookie-only browser navigation while ensuring the exact credential delivered by the trusted host wins during adoption.
- The desktop publishes one fresh bootstrap after its first non-empty topology when startup began empty. It reuses the bounded, expiring server session and does not place credentials in URLs, traces, or files.
- `prepomax.scene.applied-v1` now carries the exact scene ID, revision, actor count, and pipeline-chunk count. Initial loads acknowledge after resource completion and stable paint. Replacement loads defer acknowledgement until retained camera/follower application completes.
- The WinForms acceptance gate requires positive acknowledged browser actor and chunk counts before mutating presentation.

## Verification

- Production session endpoint regression: a stale `prepomax-render-session` cookie plus the valid current explicit token successfully adopts and replaces the cookie.
- Client unit tests: 61/61 passed.
- Renderer .NET tests: 80/80 passed.
- Playwright: 2/2 passed, including exact initial and replacement scene-applied actor/chunk counts.
- Full Release/x64 build and staging passed with the existing warning profile.
- Packaged C3D4 actor-presentation smoke passed in 34.8 s: browser acknowledgement reported one actor/one pipeline chunk, exact renamed RGB, opacity 1, visible true, scalar visibility false, plus clean parent/child shutdown.
- Adjacent packaged visibility/camera smoke passed with `BrowserVisibilityCameraApplied=True`.

Commands:

```powershell
.\scripts\build-vtkjs-mvp.ps1
.\scripts\smoke-vtkjs-winforms.ps1 `
  -ModelPath .\prototypes\VtkJsStreaming\tests\fixtures\sphere-symbol-smoke.inp `
  -ExerciseActorPresentation
.\scripts\smoke-vtkjs-winforms.ps1 `
  -ModelPath .\prototypes\VtkJsStreaming\tests\fixtures\sphere-symbol-smoke.inp `
  -ExerciseVisibilityCamera
```

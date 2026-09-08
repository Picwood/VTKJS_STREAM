# Interactive animation-frame activation retirement

Date: 2026-09-03

## Decision boundary

Interactive frame changes in VTK.js mode no longer call `vtkControl.SetAnimationFrame`. The browser-owned retained animation sources, frame capture, compact `PMXFRM2` transport, server topology-preserving update, stable-paint acknowledgement, camera adjustment, and follower update are the authoritative runtime path.

At this milestone hidden animated actors and metadata remained populated for AVI/PNG export. They were subsequently removed from normal construction, and both PNG sequences and fixed-MJPG AVI now activate and capture browser frames without native materialization. The default ActiViz renderer path is unchanged.

## Implementation

- `FrmMain.SetAnimationFrame` branches before any hidden mutation.
- Legacy mode continues to call `vtkControl.SetAnimationFrame` with camera and follower updates.
- VTK.js mode calls only `VtkJsRendererControl.SetAnimationFrame`, retaining the existing browser camera-bypass trace and adding `result-animation/frame-browser-only`.
- The packaged animation smoke requires the new bypass trace and rejects any nonzero `LegacyActivationMs`.

## Packaged acceptance

```powershell
.\scripts\build-vtkjs-mvp.ps1 -Configuration Release
.\scripts\smoke-vtkjs-winforms.ps1 `
  -ModelPath .\prototypes\VtkJsStreaming\tests\fixtures\follower-redraw-smoke.frd `
  -ExerciseAnimation -AnimationMode ScaleFactor
```

Result:

- 1 actor, 12 render points, 4 triangles;
- 4 locator points and 1 locator cell;
- frame 1 published with changed positions and unchanged topology IDs;
- browser stable activation: 24.6 ms in this run;
- `AnimationFramePublished=True`;
- `AnimationFrameActiVizBypassed=True`;
- `LegacyActivationMs=0`;
- graceful PrePoMax close and renderer-child exit.

Regression results: client 63/63, .NET 80/80, Playwright 2/2, prototype isolation pass, full Release/x64 build and staging pass, and `git diff --check` pass.

## Remaining work

Animated-scalar construction, metadata mirroring, scale-factor lookup, and acceleration selection have since moved to retained/browser-owned paths; see [animated-result construction retirement](animated-result-construction-retirement.md). PNG and AVI capture have also become browser-owned; see [browser PNG export](browser-animation-png-export.md) and [browser AVI export](browser-animation-avi-export.md). Animation has no remaining hidden-renderer dependency in VTK.js mode.

# Animated-result construction retirement

Date: 2026-09-04

## Decision boundary

Opening or playing a result animation in VTK.js mode no longer constructs `vtkMaxActor` base actors or per-frame actor arrays in the hidden ActiViz renderer. Animation sources, frame metadata, scale-factor lookup, acceleration preference, initial topology, and interactive frame activation are retained by the VTK.js adapter.

At this milestone AVI and PNG writing still used the native render-window writers, with their actors and metadata materialized lazily from retained animation state. PNG and AVI were subsequently moved to browser-owned composited capture and the adapter was removed; see [browser PNG export](browser-animation-png-export.md) and [browser AVI export](browser-animation-avi-export.md). The default ActiViz renderer is unchanged.

## Implementation

- `FrmMain.AddAnimatedScalarFieldOn3DCells` now calls only `VtkJsRendererControl.RegisterAnimation` in VTK.js mode. Successful capture emits `result-animation/construction-browser-only`; rejection remains explicit and aborts the controller animation build.
- Registration validates and captures frame zero before retaining the source. It no longer reports success when initial conversion fails.
- `SetAnimationFrameData`, `GetAnimationScaleFactor`, and `SetAnimationAcceleration` branch before touching the hidden renderer. Metadata arrays are cloned and checked against every retained actor's geometry, extrema, and locator frame counts.
- `Clear3D` and `ClearControls` invalidate the export-materialization flag together with retained animation state.
- At this milestone `SaveAnimationAsAVI` and `SaveAnimationAsImages` called one export-only adapter. The later PNG slice removed `SaveAnimationAsImages` from that adapter; `SaveAnimationAsAVI` still reconstructs retained sources in the hidden renderer, applies acceleration and cloned metadata, restores hidden-part visibility, and records `result-animation/export-legacy-materialized`.
- Interactive `SetAnimationFrame` remains browser-only even after export materialization; it never advances the compatibility actors.

## Packaged acceptance

```powershell
.\scripts\smoke-vtkjs-winforms.ps1 `
  -ModelPath '.\prototypes\VtkJsStreaming\tests\fixtures\follower-redraw-smoke.frd' `
  -ExpectFiniteScalars `
  -ExerciseAnimation `
  -ExerciseAnimationPngCapture `
  -ExerciseAnimationAviCapture `
  -AnimationMode ScaleFactor
```

The Release/x64 process run proved:

- pre-export construction retained one animation source and reported `legacyMaterialized=False`;
- `AnimationConstructionBrowserOnly=True`, with no construction rejection;
- the public export seams wrote two distinct PNG frames and an indexed two-frame MJPEG AVI to unique temporary paths and removed them afterward;
- `AnimationExportAdapterMaterialized=False`, `AnimationPngCaptureBrowserOnly=True`, and `AnimationAviCaptureBrowserOnly=True`;
- frame 1 still changed browser position resources while preserving topology;
- `AnimationFrameActiVizBypassed=True` and `LegacyActivationMs=0` after export materialization;
- 1 actor, 12 render points, 4 triangles, 4 locator points, and 1 locator cell remained authoritative;
- PrePoMax and its renderer child shut down cleanly.

Verification:

- client Vitest: 66/66;
- contracts/server .NET tests: 80/80;
- Playwright: 2/2;
- prototype isolation audit: 6 projects, 34 C# files, 0 forbidden references;
- strict client production build, server publish/staging, and Release/x64 PrePoMax build: pass;
- `git diff --check`: pass.

## Remaining boundary

This evidence records the earlier isolation milestone. Browser-owned pixel capture, deterministic synchronization, bounded validation, staged file writing, and error cleanup are now complete for PNG and fixed-MJPG AVI. The export-only materialization has been removed. User-visible cancellation and representative-model/player visual comparison remain open. Browser captures include the composited WebGL surface and DOM overlays, avoiding the old hidden writer's mismatch for browser-only overlays.

PEND-004 also remains open for generic auxiliary actors, OpenFOAM, transformations, result widgets/orientation marker, mesh-editing classification, and final hidden scene-lifecycle cleanup.

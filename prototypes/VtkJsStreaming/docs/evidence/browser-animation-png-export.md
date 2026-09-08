# Browser-owned animation PNG export

Date: 2026-09-04

## Decision boundary

`Save animation as images` is browser-owned when VTK.js is selected. Each requested frame is activated through the retained animation path, acknowledged after stable browser paint, and captured from the composited WebView2 surface as PNG. The command does not create animation actors in the hidden ActiViz renderer. The default ActiViz renderer path is unchanged.

At this PNG milestone AVI export was deliberately not claimed. It was subsequently replaced by a browser-owned fixed-MJPG writer and the compatibility adapter was removed; see [browser-owned animation AVI export](browser-animation-avi-export.md).

## Implementation

- `VtkJsRendererControl.ActivateAnimationFrameAsync` installs a single-flight completion before requesting a frame. It accepts either the stable `prepomax.frame.activated-v1` revision or `prepomax.scene.applied-v1` for a full first-frame replacement. The request times out after 30 seconds and is cancelled on clear, navigation, stop, or disposal.
- `VtkJsRendererControl.CapturePngAsync` uses WebView2 `CapturePreviewAsync`, so the result contains the composited WebGL viewport and browser DOM overlays. It rejects captures below the PNG header size, above 64 MiB, without the PNG/IHDR header, or outside the 1..32768 pixel dimension bound.
- Each PNG is written to a unique same-directory temporary file and atomically moved or replaced. The animation command captures the complete sequence to staging files before committing outputs and cleans every remaining stage file on failure.
- Forward, stepped, and swing frame ordering retains the native command's naming convention. The WinForms save-images handler now awaits the operation, so success is not reported before capture and commit finish.
- The compatibility trace is `result-animation/images-browser-only`. At this milestone native animation materialization remained isolated to `SaveAnimationAsAVI`; the subsequent AVI slice removed it.

## Packaged acceptance

```powershell
.\scripts\smoke-vtkjs-winforms.ps1 `
  -ModelPath '.\prototypes\VtkJsStreaming\tests\fixtures\follower-redraw-smoke.frd' `
  -ExpectFiniteScalars `
  -ExerciseAnimation `
  -ExerciseAnimationPngCapture `
  -AnimationMode ScaleFactor
```

The Release/x64 run proved:

- one retained actor, three animation frames, 12 render points, four triangles, four locator points, and one locator cell;
- two valid nonempty browser PNG captures with positive dimensions and distinct SHA-256 hashes;
- `AnimationPngCaptureBrowserOnly=True` and `AnimationExportAdapterMaterialized=False`;
- `AnimationConstructionBrowserOnly=True` and `AnimationFrameActiVizBypassed=True`;
- subsequent frame-1 activation used `LegacyActivationMs=0` with stable topology;
- output files and same-directory staging files were removed by the acceptance probe; and
- PrePoMax and its renderer child shut down cleanly.

## Remaining boundary

AVI is now browser-owned through the bounded built-in Motion JPEG writer. A user-visible cancellation command and an optional whole-sequence rollback policy remain product refinements; errors already stop capture and remove uncommitted staging files. Manual visual acceptance must compare the composited browser output, including legends and annotations, on representative models and DPI settings.

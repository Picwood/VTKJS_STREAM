# Browser-owned animation AVI export

Date: 2026-09-04

## Codec decision

VTK.js mode exports AVI 1.0 with a built-in Motion JPEG (`MJPG`) video stream. Every frame is a WebView2 JPEG capture taken after the same stable-paint barrier used by PNG export. This avoids an FFmpeg installation, Windows Video-for-Windows codec availability, and modal system compression dialogs while preserving a conventional, independently indexed AVI file.

The container layout and index offsets follow Microsoft's [AVI RIFF file reference](https://learn.microsoft.com/en-us/windows/win32/directshow/avi-riff-file-reference) and [AVIOLDINDEX definition](https://learn.microsoft.com/en-us/previous-versions/ms779634%28v%3Dvs.85%29): `hdrl`, then `movi`, followed by `idx1`, with compressed video chunks identified as `00dc` and offsets relative to the `movi` list.

The format is intentionally bounded: video only, 1..100000 JPEG keyframes, dimensions are 1..32767 pixels, frame rate is 1..1000 FPS, each captured JPEG is at most 64 MiB, and the complete RIFF file must remain below the AVI 1.0 4 GiB limit. The default ActiViz renderer retains its existing `vtkAVIWriter` behavior and optional compression dialog.

## Implementation

- `VtkJsRendererControl.CaptureJpegAsync` captures the composited WebView and validates JPEG SOI/EOI, segment bounds, start-of-frame dimensions, byte limit, and AVI dimension limit.
- `VtkJsMjpegAviWriter` streams one JPEG at a time into `RIFF AVI`, writes `avih`, `strh`, and `strf` metadata, records each `00dc` keyframe, emits `idx1`, and patches checked 32-bit lengths and buffer sizes before a durable flush.
- `FrmMain.SaveAnimationAsAVI` preserves forward/step/swing ordering, validates range/FPS/output, waits for stable paint per frame, writes to a unique same-directory staging file, and atomically moves or replaces the destination only after finalization. Failure removes the stage and preserves an existing destination.
- VTK.js animation export no longer contains a path that materializes retained actors in the hidden renderer. The obsolete adapter and retained export accessors were removed.
- The animation dialog awaits completion. In VTK.js mode it labels and disables the irrelevant graphics-RAM and system-codec controls; direct requests for a codec dialog are rejected rather than silently ignored.

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

- PNG and AVI capture passed together with `AnimationPngCaptureBrowserOnly=True` and `AnimationAviCaptureBrowserOnly=True`;
- `AnimationExportAdapterMaterialized=False`, `AnimationConstructionBrowserOnly=True`, and `AnimationFrameActiVizBypassed=True`;
- the AVI RIFF length was exact, contained `MJPG`, two `00dc` JPEG chunks, and a 32-byte `idx1` index;
- both embedded JPEGs had valid boundaries and distinct SHA-256 hashes;
- the authoritative model remained one actor / 12 points / four triangles / four locator points / one locator cell;
- output and staging files were removed by the probe; and
- PrePoMax and its renderer child shut down cleanly.

Regression verification: client Vitest 66/66, contracts/server .NET 80/80, Playwright 2/2, prototype isolation 6 projects / 34 C# files / zero forbidden references, strict client production build, server staging, Release/x64 application build, and `git diff --check` all pass.

## Remaining boundary

The animation capability no longer needs hidden ActiViz construction, activation, capture, or encoding in VTK.js mode. Manual playback/visual acceptance across representative Windows players and DPI settings remains under PEND-005. AVI 1.0 deliberately rejects output approaching 4 GiB; a future request for longer video, inter-frame compression, audio, MP4, or configurable quality should be a separate encoder feature rather than restoring the renderer dependency. User-visible cancellation remains a shared PNG/AVI export refinement.

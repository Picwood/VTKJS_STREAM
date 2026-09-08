# Coordinate-axis and caption compatibility retirement

Date: 2026-08-31

This slice retires `vtkControl.AddCoordinateAxis` and `vtkControl.AddCaptionActor` from the hidden ActiViz renderer when VTK.js is selected. Default ActiViz mode remains unchanged.

The retained `prepomax.coordinate-overlays.updated-v1` replacement state preserves the axis name suffix, absolute anchor, direction, RGBA color, requested display size, caption text, world anchor, optional display-scaled three-vector offset, and font-scale factor. Both sides limit axes and captions to 4,096 each, names to 1,024 characters, text to 16,384 characters, coordinates to finite values within +/-1e150, symbol size to `(0, 1000]`, and font scale to `(0, 100]`.

The browser uses origin-relative anchors and a shared source matching the legacy one-unit line plus 31-sided cone. Direction arrays orient non-pickable glyphs. Axis scale and caption offset are recomputed from camera world-per-pixel values in parallel and perspective projection. Captions use `textContent` and retain the legacy 16 px base font calculation.

## Verification

- Vitest: 32/32 overall, including invalid direction, size, color, duplicate, collection, and text bounds.
- Playwright: 2/2 overall. A large-origin axis/caption renders with exact safe text; halving orthographic scale halves axis world scale; empty replacement disposes both.
- Builds: strict TypeScript/Vite, `PrePoMax.sln` Release/x64, and complete MVP staging passed. Existing warning profiles remain.
- .NET: 78/78 tests passed.
- Packaged WebView2: the C3D4 fixture published 1 actor / 12 tuples / 4 triangles / 4 locator nodes / 1 cell. Real `FrmMain` seams emitted both bypass traces, browser diagnostics reported `1|true|1|true`, and both processes exited cleanly.

Directional load/constraint glyphs, springs, thermal/flux glyphs, and optional symbol-edge silhouettes remain on the compatibility backend.

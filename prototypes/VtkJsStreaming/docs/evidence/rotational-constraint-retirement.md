# Rotational-constraint glyph retirement

Date: 2026-08-31

This slice retires `vtkControl.AddOrientedRotationalConstraintActor` from hidden ActiViz when VTK.js is selected. Default ActiViz behavior remains unchanged.

The existing bounded `prepomax.directional-glyphs.updated-v1` state now accepts `rotational-constraint` alongside `displacement-cone`. It retains the legacy `:cylinder` name even though the legacy source is a cube-and-line symbol, plus paired absolute anchors/nonzero directions, RGBA, and display size. The shared limits remain 4,096 actors, 100,000 aggregate anchors, 1,024-character names, coordinates within +/-1e150, normalized colors, and sizes in `(0, 1000]`.

The VTK.js source exactly preserves the legacy dimensions: a cube centered at `(-1.55, 0, 0)` with lengths `(0.4, 0.9, 0.9)`, triangulated as 24 face-local points and 12 triangles for flat shading, plus one line from `(0,0,0)` to `(-1.3,0,0)`. Instances use direction orientation and the legacy 0.3 camera-relative scale. They are origin-relative, non-pickable, backface-culled, and use legacy ambient/diffuse values.

## Verification

- Vitest: 36/36 overall, including acceptance of the second explicit kind and rejection of unsupported kinds.
- Playwright: 2/2 overall. One replacement contains two displacement cones and two rotational symbols at large coordinates; diagnostics prove the 26-point / 12-polygon / 1-line rotational source, positive scales, exact 2x zoom response, and empty-state disposal.
- Production: strict TypeScript/Vite, full staging, and `PrePoMax.sln` Release/x64 passed with existing warning profiles.
- Packaged WebView2: the deterministic C3D4 fixture published 1 actor / 12 render tuples / 4 triangles / 4 locator nodes / 1 cell. The real `FrmMain.AddOrientedRotationalConstraintActor` seam emitted its bypass trace, browser diagnostics reported `1|2|true`, and both processes exited cleanly.

The legacy optional camera-dependent silhouette remains outside the claim. Arrows, springs, thermal/flux sources, and general cone actors remain separate slices.

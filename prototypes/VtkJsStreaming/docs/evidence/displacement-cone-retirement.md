# Displacement-constraint cone retirement

Date: 2026-08-31

This slice retires `vtkControl.AddOrientedDisplacementConstraintActor` from hidden ActiViz when VTK.js is selected. The default renderer path remains unchanged. This seam is shared by fixed/displacement constraints, surface-spring faces, and several initial-condition displays.

The full-replacement `prepomax.directional-glyphs.updated-v1` state introduced the explicit `displacement-cone` kind. Each actor retains its legacy `:cones` name, matched absolute anchor and direction arrays, RGBA, and requested display size. Host and browser enforce 4,096 actors, 100,000 aggregate anchors, 1,024-character names, finite coordinates within +/-1e150, nonzero directions, normalized colors, and screen sizes in `(0, 1000]`.

VTK.js uses one shared capped 31-sided cone spanning X=-1 through X=0 with radius 0.5. `vtkGlyph3DMapper` orients the cone by each direction; the legacy 0.3 glyph scale is applied to camera-derived world units per display pixel. Anchors are origin-relative, glyphs are non-pickable, and the legacy ambient/diffuse/backface-culling presentation is retained.

## Verification

- Vitest: 35/35 overall, including matched-array, zero-direction, kind, duplicate, color, size, and aggregate bounds.
- Playwright: 2/2 overall. Two large-origin X/Y cones share one actor, have positive scales, scale exactly through a 2x orthographic zoom, and dispose on empty replacement.
- Production: strict TypeScript/Vite, full staging, and `PrePoMax.sln` Release/x64 passed with existing warning profiles.
- Packaged WebView2: the deterministic C3D4 fixture published 1 actor / 12 render tuples / 4 triangles / 4 locator nodes / 1 cell. The real `FrmMain.AddOrientedDisplacementConstraintActor` seam emitted its compatibility-bypass trace, browser diagnostics reported `1|2|true`, and both processes exited cleanly.

The legacy optional `vtkPolyDataSilhouette` actor is not claimed: this VTK.js distribution has no equivalent camera-dependent polydata silhouette filter. Rotational cylinders and the other directional source geometries remain separate migration slices.

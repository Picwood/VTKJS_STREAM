# General oriented-cone retirement

Date: 2026-08-31

This slice retires `vtkControl.AddOrientedConeActors` from hidden ActiViz when VTK.js is selected. Default ActiViz behavior remains unchanged. The only current production caller is `Controller.HighlightLineWithArrow`, where one or two cone glyphs mark the requested line endpoints in the selection layer.

The bounded `prepomax.directional-glyphs.updated-v1` state now accepts `general-cone`. It uses the existing paired absolute anchors/nonzero directions, normalized RGBA, display size, and global actor/instance/name/coordinate limits. The host preserves the `:cones` name mutation and supplied selection-layer color.

This source is not the displacement-constraint cone. It matches the legacy `vtkConeSource` centered at X=-0.5 with height 1, radius 0.35, resolution 11, and capping enabled. After triangulation it has 13 shared points and 22 triangles spanning X=-1 through 0. The odd angular resolution produces the retained sampled transverse bounds Y=-0.33582255..0.35 and Z=-0.34643751..0.34643751. Overall glyph scale remains `0.3*symbolSize` in camera-relative world units.

The source is shared by all general-cone actors and is owned by the scene view. Actor pipelines, input arrays, and the source are released through replacement, prefix removal, clear, navigation, and shutdown. The optional camera-dependent ActiViz silhouette remains outside this slice because the bundled VTK.js has no equivalent polydata silhouette filter.

## Verification

- Vitest: 40/40 overall, including normalization of the sixth directional kind.
- Playwright: 2/2 overall. The combined large-origin state includes all six directional kinds and proves exact general-cone source counts and bounds, equality with the displacement family's 0.3 scale, 2x zoom response, and disposal.
- Production: strict TypeScript/Vite, full staging, and `PrePoMax.sln` Release/x64 passed with existing warning profiles.
- Packaged WebView2: the deterministic C3D4 scene published 1 actor / 12 render tuples / 4 triangles / 4 locator nodes / 1 cell. The real general-cone seam published two instances, emitted its compatibility-bypass trace, diagnostics reported `1|2|true`, and both processes exited cleanly.

Oriented thermal and flux sources, plus optional camera-dependent silhouettes, remain separate slices.

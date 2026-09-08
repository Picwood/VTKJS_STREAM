# Moment-load double-arrow retirement

Date: 2026-08-31

This slice retires `vtkControl.AddOrientedDoubleArrowsActor` from hidden ActiViz when VTK.js is selected. Default ActiViz behavior remains unchanged. The seam is used by moment loads and other controller paths requesting a double-headed directional symbol.

The bounded `prepomax.directional-glyphs.updated-v1` state now accepts `double-arrow`. It uses the existing paired absolute anchors/nonzero directions, RGBA, display size, and global actor/instance/name/coordinate limits; the arrow-only `invert` and `relativeSize` fields are rejected. The WinForms seam performs the legacy `RemoveZeroLengthNormals` mutation before publishing and retains the `:doubleArrow` name mutation.

VTK.js creates the standard arrow with tip resolution 21, tip length 0.3, tip radius 0.1, shaft resolution 15, and shaft radius 0.03. It appends the legacy second cone with resolution 21, height 0.3, radius 0.1, and center X=0.65, then triangulates the combined polydata and translates it +0.05 along X. Current VTK.js arrow recentering is cancelled before composition, restoring the ActiViz 5.8 source range and final X bounds 0.05 through 1.05. Overall camera scale is the unmodified `symbolSize`.

The composed VTK source algorithms and materialized polydata are owned by the retained directional pipeline and disposed on replacement, actor-prefix removal, clear, navigation, and shutdown. The optional camera-dependent ActiViz silhouette remains explicitly outside this slice because the bundled VTK.js has no equivalent polydata silhouette filter.

## Verification

- Vitest: 38/38 overall, including acceptance of `double-arrow` and continued field/bounds normalization.
- Playwright: 2/2 overall. The combined large-origin state contains all four directional kinds and proves the double-arrow kind, final X bounds, greater polygon count than the standard arrow, expected scale ratio against the 0.3 constraint family, 2x zoom response, and disposal.
- Production: strict TypeScript/Vite, full staging, and `PrePoMax.sln` Release/x64 passed with existing warning profiles.
- Packaged WebView2: a three-anchor request containing one zero normal was reduced to two instances, matching legacy behavior. The deterministic C3D4 scene published 1 actor / 12 render tuples / 4 triangles / 4 locator nodes / 1 cell; the real seam emitted its bypass trace, diagnostics reported `1|2|true`, and both processes exited cleanly.

General cones, oriented springs, thermal/flux sources, and optional camera-dependent silhouettes remain separate slices.

# Oriented load-arrow retirement

Date: 2026-08-31

This slice retires `vtkControl.AddOrientedArrowsActor` from hidden ActiViz when VTK.js is selected. Default ActiViz behavior remains unchanged. The seam is used by concentrated/distributed force, pressure, traction, gravity, centrifugal, pretension, and related displays.

The bounded `prepomax.directional-glyphs.updated-v1` state now accepts `oriented-arrow`. Arrow actors require explicit `invert` and positive `relativeSize` through 1,000, in addition to the existing paired anchors/nonzero directions, RGBA, display size, and global actor/instance/name/coordinate limits. The WinForms seam performs the legacy `RemoveZeroLengthNormals` mutation before publishing and retains the `:arrow` name mutation.

VTK.js uses tip resolution 21, tip length `0.3/relativeSize`, tip radius `0.1/relativeSize`, shaft resolution 15, and shaft radius `0.03/relativeSize`. Overall camera scale is `symbolSize*relativeSize`, keeping arrowhead length/radii at constant screen size while allowing the shaft length to express magnitude. Non-inverted geometry occupies X=0.05 through 1.05; inverted geometry occupies X=-1.05 through -0.05, so both retain the legacy anchor gap and pointing semantics.

Current VTK.js recenters its arrow source by `-0.5 + tipLength/2`, unlike ActiViz 5.8's X=0 through 1 source. Production explicitly cancels that recentering before applying the legacy translation. Headed bounds assertions caught and retain this cross-version correction.

## Verification

- Vitest: 37/37 overall, including required inversion/relative-size fields and rejection of zero relative size.
- Playwright: 2/2 overall. The combined large-origin state includes normal and inverted arrows with relative sizes 1.5 and 2.5. It proves exact X bounds, expected scale ratios against the 0.3 constraint family, positive scales, 2x zoom response, and disposal.
- Production: strict TypeScript/Vite, full staging, and `PrePoMax.sln` Release/x64 passed with existing warning profiles.
- Packaged WebView2: a three-anchor request containing one zero normal was reduced to two instances, matching legacy behavior. The deterministic C3D4 scene published 1 actor / 12 render tuples / 4 triangles / 4 locator nodes / 1 cell; the real seam emitted its bypass trace, diagnostics reported `1|2|true`, and both processes exited cleanly.

Double arrows, general cones, springs, thermal/flux sources, and optional camera-dependent silhouettes remain separate slices.

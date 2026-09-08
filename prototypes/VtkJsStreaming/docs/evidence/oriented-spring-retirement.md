# Oriented spring retirement

Date: 2026-08-31

This slice retires `vtkControl.AddOrientedSpringActor` from hidden ActiViz when VTK.js is selected. Default ActiViz behavior remains unchanged. The seam is used by point and surface spring-constraint displays.

The bounded `prepomax.directional-glyphs.updated-v1` state now accepts `oriented-spring`. It uses the existing paired absolute anchors/nonzero directions, RGBA, display size, and global actor/instance/name/coordinate limits and requires the API's `invert` boolean. The current ActiViz implementation has its conditional inversion translations commented out and always translates by -1.05; VTK.js deliberately retains this observable no-op instead of assigning new semantics to the parameter. The `:spring` name mutation is unchanged.

The source matches `CreateSpringSource(0.2, 1, 8, 10)`: six leading/trailing linear transition points surround an 81-point, eight-turn helical segment, for 93 path points total. The same centered three-point moving average is applied except at the two endpoints. A radius-0.03 tube with 10 shared sides and capped ends produces 950 points. Translation after tubing gives X bounds -1.05 through -0.05 and radial bounds approximately +/-0.127.

VTK.js's current `vtkTriangleFilter` processes polygon cells but ignores triangle strips, unlike the VTK 5.8 filter used by ActiViz. The browser therefore expands the tube filter's strips explicitly with alternating winding into 1,856 triangles while retaining the filter's shared points and normals. The shared source and its algorithms are owned by the scene view; actor pipelines, instances, arrays, and source ownership are released through replacement, prefix removal, clear, navigation, and shutdown.

## Verification

- Vitest: 39/39 overall, including required spring inversion state and rejection of unsupported relative-size state.
- Playwright: 2/2 overall. The combined large-origin state includes both inversion values and all five directional kinds. It proves identical spring scales, exact source counts and bounds, expected scale ratio against the 0.3 constraint family, 2x zoom response, and disposal.
- Production: strict TypeScript/Vite, full staging, and `PrePoMax.sln` Release/x64 passed with existing warning profiles.
- Packaged WebView2: the deterministic C3D4 scene published 1 actor / 12 render tuples / 4 triangles / 4 locator nodes / 1 cell. The real spring seam published two instances, emitted its compatibility-bypass trace, diagnostics reported `1|2|true`, and both processes exited cleanly.

General oriented cones, thermal/flux sources, and optional camera-dependent silhouettes remain separate slices.

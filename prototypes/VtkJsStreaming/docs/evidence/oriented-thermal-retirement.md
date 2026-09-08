# Oriented thermal-symbol retirement

Date: 2026-09-01

This slice retires `vtkControl.AddOrientedThermoActor` from hidden ActiViz when VTK.js is selected. Default ActiViz behavior remains unchanged. The seam serves initial, boundary-condition, and defined-field temperature displays.

The bounded directional state accepts `oriented-thermal` and requires `invert` alongside paired anchors/nonzero directions, RGBA, and screen size. The source matches the legacy thermometer: a radius-0.2 sphere with phi/theta resolution 11 centered at X=0.2, plus a radius-0.1, height-0.8, resolution-7 capped cylinder centered at Y=0.7 and rotated -90 degrees around Z. Convex polygon triangulation retains 129 shared points, explicit transformed normals, and 222 triangles. Translation is +0.05 normally or -1.05 when inverted; scale is `0.5*symbolSize`; naming retains `:thermo`.

Vitest 41/41, Playwright 2/2, strict production build, Release/x64, and full staging passed. Browser acceptance covers both inversion values at large origins, exact counts/bounds, scale, zoom, and disposal. The packaged C3D4 smoke drove the real `FrmMain.AddOrientedThermosActor` seam with two instances, required its bypass trace and `1|2|true` diagnostics, and closed both processes cleanly.

Optional camera-dependent silhouette edging remains outside the source because the bundled vtk.js has no polydata-silhouette filter.

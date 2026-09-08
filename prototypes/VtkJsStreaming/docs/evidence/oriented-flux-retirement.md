# Oriented flux-symbol retirement

Date: 2026-09-01

This slice retires `vtkControl.AddOrientedFluxActor` from hidden ActiViz when VTK.js is selected. Default ActiViz behavior remains unchanged. Concentrated flux uses a centered symbol; film and radiation surface displays use the offset symbol.

The bounded directional state accepts `oriented-flux` and requires both `center` and `invert`. The legacy radius-0.2 sphere uses phi resolution 9 and theta resolution 11, producing 79 shared points and 154 triangles. `center=true` selects local X=0 and `false` selects X=0.2 before translation. Normal and inverted translations are +0.05 and -1.05; scale is `0.5*symbolSize`; naming retains `:flux`.

Vitest 41/41, Playwright 2/2, strict production build, Release/x64, and full staging passed. Browser acceptance covers centered/non-centered and normal/inverted behavior at large origins, exact counts/bounds, scale, zoom, and disposal. The packaged C3D4 smoke drove the real `FrmMain.AddOrientedFluxActor` seam with two instances, required its bypass trace and `1|2|true` diagnostics, and closed both processes cleanly.

Optional camera-dependent silhouette edging remains outside the source because the bundled vtk.js has no polydata-silhouette filter.

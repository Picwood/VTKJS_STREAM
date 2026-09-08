# Sphere-symbol compatibility retirement

Status: passed on Windows x64 on 2026-08-31.

## Boundary

`FrmMain.AddSphereActor` is now a renderer-owned capability. Default mode still calls the original `vtkControl.AddSphereActor`; when the VTK.js feature flag is active, the hidden ActiViz control is not called. The retained host sends unique actor names, absolute model-space anchors, RGBA, and display size as a bounded full replacement.

VTK.js subtracts the scene origin and renders a shared radius-0.2, 13×13 sphere source with `vtkGlyph3DMapper`. Per-anchor scale is recomputed from parallel scale or perspective depth and CSS viewport height on every render. This preserves the legacy screen-relative sizing intent without querying hidden actor or camera transforms. Glyphs are non-pickable.

## Evidence

- Vitest: 29/29 overall; focused cases clone valid state and reject duplicates, invalid coordinates/color/size, and more than 100,000 points.
- Production TypeScript/Vite build: passed; the existing bundle-size warning remains.
- Playwright: 2/2 overall; two large-origin anchors create one actor/two glyphs, all scales are positive, halving orthographic parallel scale halves glyph world scale, and an empty state returns actor/point/scale diagnostics to zero.
- Prototype .NET: 78/78.
- PrePoMax Release/x64: passed using Visual Studio MSBuild with the repository's existing warnings.
- Packaged WebView2 process: `sphere-symbol-smoke.inp` published one actor, 12 rendered point tuples, four triangles, four locator nodes, and one locator cell. The real `FrmMain.AddSphereActor` call emitted `compatibility bypass capability=sphere-symbols operation=add`; browser diagnostics reported `1|1|true`; graceful shutdown left no renderer child.

## Scope

This slice covers sphere primitives only. Coordinate axes, captions, arrows, double arrows, constraint cylinders/cones, springs, thermal/flux glyphs, and optional symbol silhouettes remain on the compatibility backend. Actor-parent transform semantics for those directional families are intentionally deferred until their renderer-neutral orientation/ownership contract is defined.

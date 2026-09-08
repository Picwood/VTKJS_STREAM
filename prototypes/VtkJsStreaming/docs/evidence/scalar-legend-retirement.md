# Scalar-legend retirement

Date: 2026-09-08

## Outcome

The PrePoMax scalar-legend seams no longer call the hidden ActiViz scalar-bar widget when VTK.js mode is selected. Spectrum, title metadata, complex-result label, min/max policy, `G1-G8`/`E1-E8` number format, background, border, visibility, and initial position now flow through retained `prepomax.scalar-legend.updated-v1` state. The default ActiViz branch is unchanged.

The host calculates the effective current-frame or all-animation-frame range and samples the existing managed palette implementation, including manual over/under colors. The browser applies that same range and palette to current/future vtk.js mappers and renders a non-interactive top-left DOM legend using text nodes. State is validated before mutation, survives scene-view reconstruction, is refreshed after topology/scalar/frame publication and actor visibility/contour changes, and is hidden on full scene clear.

## Packaged acceptance

```powershell
.\scripts\build-vtkjs-mvp.ps1 -Configuration Release -Platform x64
.\scripts\smoke-vtkjs-winforms.ps1 `
  -ModelPath '.\prototypes\VtkJsStreaming\tests\fixtures\sphere-symbol-smoke.inp' `
  -ExerciseCorePartConstruction `
  -ExerciseScalarLegend
```

Release/x64 acceptance reported `ScalarLegendBrowserOnly=True`. The process exercised the public spectrum, text, `E3` format, background, border, and position seams plus direct hidden/visible lifecycle. Browser diagnostics returned `Displacements`, `Magnitude`, `mm`, `Complex: Real`, `Automatic`, range `0..1`, nine palette colors, ten labels, and enabled background/border. All relevant calls emitted compatibility-bypass traces, and PrePoMax plus its renderer child exited cleanly.

## Verification

- Client Vitest: 75/75 passed across 21 files.
- Strict TypeScript/Vite production build: passed.
- Full Visual Studio Release/x64 build and staging: passed.
- Packaged scalar-legend process acceptance: passed after separating asynchronous hidden/visible diagnostic stages.
- Renderer contracts/server tests: 80/80 passed.
- Browser Playwright: 2/2 passed.
- Prototype isolation: 6 projects, 34 C# files, zero forbidden references.

## Remaining boundary

The categorical color bar, status block, and shared widget layout subsequently completed. Exact typography/spacing and representative result palettes remain part of PEND-005 visual acceptance. ActiViz assemblies and `vtkControl` are still required at process startup for the broader reasons recorded in `docs/VTKJS_ACTIVIZ_STATUS.md`.

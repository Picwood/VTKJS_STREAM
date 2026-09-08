# Retained 3MF actor input

Date: 2026-09-03

## Scope

Captured VTK.js part surfaces no longer depend on `vtkControl.GetVtkMaxActorData` for 3MF actor geometry readback. Browser-only scenes avoid that query completely. This is still a transitional export boundary: uncaptured auxiliary actors are merged from the legacy registry after a legacy-producing seam has been used, and lookup-table generation remains legacy-owned.

## Implementation

- Scene capture retains the original model-edge exchange data alongside its authoritative triangulated surface.
- `ExportActorData` projects retained coordinates, triangle connectivity, scalars, current ARGB presentation, contour mode, visibility, and model edges into the existing writer input type.
- Visibility, rename, removal, and clear operations maintain the retained export registry consistently.
- `FrmMain.GetVtkMaxActorData` selects the retained projection in VTK.js mode. Browser-only scenes return it without querying the hidden VTK actor registry; scenes known to contain auxiliary legacy actors merge only names not already captured. The default renderer path is unchanged.
- `-ExerciseThreeMfExportInput` drives the public export-data seam after exact browser scene acknowledgement and requires positive actor, point, and cell counts plus both retained-data and browser-only compatibility-bypass traces.

The projection deliberately uses the same triangles rendered by VTK.js, avoiding reverse conversion through a VTK mapper and preserving the browser-authoritative topology.

## Packaged acceptance

```powershell
.\scripts\build-vtkjs-mvp.ps1 -Configuration Release
.\scripts\smoke-vtkjs-winforms.ps1 `
  -ModelPath .\prototypes\VtkJsStreaming\tests\fixtures\sphere-symbol-smoke.inp `
  -ExerciseCorePartConstruction -ExerciseThreeMfExportInput
```

Result: one browser-only actor, 12 render points, 4 triangles, 4 locator points, 1 locator cell, the `actor-data-browser-only` trace, `CorePartConstructionBrowserOnly=True`, `ThreeMfExportRetainedInput=True`, and clean parent/child shutdown.

Regression results: client 63/63, .NET 80/80, Playwright 2/2, prototype isolation pass, full Release/x64 build/staging pass, and `git diff --check` pass.

## Native writer finding

An end-to-end `.3mf` write was attempted during this slice and failed before data consumption because `lib3mf.dll` is absent from both the repository and staged Release output. The managed `ThreeMF.dll` P/Invoke wrapper is present, but no native library was found. Therefore this evidence qualifies retained writer input, not a produced 3MF archive. Native library sourcing, licensing, architecture, staging, and a real archive-content acceptance test remain mandatory packaging work.

## Remaining boundary

- Renderer-neutral palette/range projection is complete; see `retained-3mf-lookup-table.md`.
- Define element-edge export parity; retained model edges are included, while generated VTK element-edge tubes remain available only through the auxiliary legacy merge.
- Cover transformed/exploded export state before eliminating the auxiliary merge.
- Package and validate the native 3MF runtime.

With retained lookup-table input now available, duplicate hidden static scalar-result construction is the next retirement slice.

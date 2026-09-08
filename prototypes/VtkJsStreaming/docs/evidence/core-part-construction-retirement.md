# Core part-construction retirement

Date: 2026-09-03

## Scope

This slice retires hidden ActiViz actor construction for ordinary geometry and model-part surfaces when `PREPOMAX_RENDERER=vtkjs`. It does not claim retirement of result scalar/animation installation, selection-cache actors, point or auxiliary cell actors, OpenFOAM import, transformations, or renderer readback/export.

## Implementation

- `FrmMain.Add3DPart` and `Add3DParts` are explicit no-handle submission seams. The default renderer still calls the original `vtkControl.AddCells` paths unchanged.
- In VTK.js mode, successfully captured renderer-neutral `vtkMaxActorData` is published without constructing a duplicate hidden `vtkMaxActor`.
- Capture now reports success to the adapter. Rejected or exceptional capture falls back to the legacy construction path and emits an explicit fallback trace, preventing silent actor loss during the migration.
- `Controller.DrawGeomPart`, `DrawAllModelParts`, and `DrawModelPart` use the new seam. These builders all explicitly set `IsAPart = true`; generic and selection call sites remain on their existing APIs.
- The packaged smoke option `-ExerciseCorePartConstruction` requires a browser-only construction trace and rejects either single- or batch-part legacy fallback.

## Acceptance evidence

```powershell
.\scripts\build-vtkjs-mvp.ps1 -Configuration Release
.\scripts\smoke-vtkjs-winforms.ps1 `
  -ModelPath C:\Users\fvazeille\Documents\GitHub\SpectraRev\PMX2.4.0\BENDING_NEON36.pmx `
  -ExerciseCorePartConstruction
```

Packaged process result:

- 15 uploaded/visible actors;
- 828,619 render points and 1,623,416 triangles;
- 2,140,668 locator points and 931,033 locator cells;
- `CorePartConstructionBrowserOnly=True` with no legacy-fallback trace;
- normal PrePoMax close and owned renderer-child exit.

Regression results:

- client: 16 files, 63/63 tests;
- renderer contracts/server: 80/80 tests;
- Playwright: 2/2 tests;
- prototype isolation: 6 projects and 34 C# files, zero forbidden references;
- full client/server staging and `PrePoMax.sln` Release/x64 build: pass;
- `git diff --check`: pass.

## Remaining boundary

PEND-004 remains open. Hidden scene cleanup cannot yet be removed because result fields/animation, selection caching, OpenFOAM, points/generic cells, transformations, and export/readback can still create or query legacy actors. The next core-construction slice should separate result scalar/animated-scalar installation from hidden actor storage; selection requires a renderer-neutral handle/cache design rather than returning `vtkMaxActor`.

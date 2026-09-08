# Selection-surface construction retirement

Date: 2026-09-03

## Decision boundary

Cached triangle and quad cell/surface previews no longer require returned `vtkMaxActor` handles when the VTK.js renderer is active. The default ActiViz path retains its existing actor cache. Point previews and line-only edge previews were outside this slice and were subsequently retired through the dedicated transient-primitive transport.

## Implementation

- `SelectionCache` can retain renderer-neutral `vtkMaxActorData[]` entries with bounded approximate memory accounting. Existing `vtkMaxActor[]` entries remain available to the default renderer.
- The controller's element, surface, arbitrary-face, and surface-edge cache seams select renderer-neutral entries in VTK.js mode. Cache hits resubmit data instead of reinserting native actor handles.
- `FrmMain.Add3DSelectionCells` captures supported selection-layer surfaces directly and records an explicit legacy fallback when conversion rejects a cell type.
- `VtkJsRendererControl` owns transient selection actor names, prevents them from replacing retained model actors, removes them through the authenticated discrete actor-removal path, and excludes them from retained 3MF/export input.
- `Clear3DSelection` and `ClearButKeepParts` remove transient selection surfaces in addition to the authoritative selection and actor-highlight states.

## Packaged acceptance

Command:

```powershell
.\scripts\smoke-vtkjs-winforms.ps1 `
  -ModelPath '.\prototypes\VtkJsStreaming\tests\fixtures\sphere-symbol-smoke.inp' `
  -ExerciseCorePartConstruction `
  -ExerciseSelectionSurfaceConstruction
```

Observed result:

- renderer-neutral cache: one actor-data entry stored and retrieved;
- transient surface appeared in browser diagnostics alongside the retained model actor;
- `Clear3DSelection` removed the transient browser actor and retained the model actor;
- `selection-construction/surface-browser-only` was present;
- `selection-construction/surface-legacy-fallback` was absent;
- 1 retained actor, 12 points, 4 triangles, 4 locator points, and 1 locator cell remained after clear;
- PrePoMax and the renderer child shut down cleanly.

Release/x64 compilation through Visual Studio MSBuild and packaged renderer staging also passed. The existing compiler warnings are unchanged.

Regression results:

- client Vitest: 63/63;
- contracts/server .NET tests: 80/80;
- Playwright: 2/2;
- prototype isolation audit: 6 projects, 34 C# files, 0 forbidden references;
- `git diff --check`: pass.

## Remaining boundary

Point and line selection previews are now browser-only; see [selection point/line construction retirement](selection-point-line-construction-retirement.md). PEND-004 remains open for animated result construction, OpenFOAM, transformations, generic auxiliary actors, AVI/PNG export, and final hidden-registry cleanup.

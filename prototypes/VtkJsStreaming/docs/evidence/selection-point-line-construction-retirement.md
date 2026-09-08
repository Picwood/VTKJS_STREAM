# Selection point/line construction retirement

Date: 2026-09-03

## Decision boundary

Selection-layer point actors and VTK line/quadratic-edge previews no longer require hidden `vtkMaxActor` construction when the VTK.js renderer is active. They use a dedicated retained primitive message because display-sized points and lines are not faithfully represented as degenerate streamed surface triangles. The default ActiViz path remains unchanged, and unsupported cell kinds retain an explicit visible legacy fallback.

## Implementation

- `FrmMain.Add3DNodes` submits selection points through the browser-only primitive seam and returns no native actor handle after successful capture.
- The common single-cell `Add3DCells` seam routes selection-layer VTK line (`3`) and quadratic-edge (`21`) data through the same path. Quadratic edges expand from VTK's start/end/mid ordering into start-mid and mid-end line segments.
- `VtkJsRendererControl` retains full-replacement primitive state, rejects names owned by retained topology or transient selection surfaces, republishes it after navigation, and clears it through the public selection and scene-clear lifecycles. Surface capture symmetrically rejects names already owned by point/line primitives.
- The `prepomax.transient-primitives.updated-v1` envelope carries absolute Float64 model coordinates, indexed two-node lines, point/line visibility flags, RGBA, point size, and line width. The host rejects retained-scene name collisions and serialized state above 64 MiB.
- Host and browser bounds are 4,096 actors, 500,000 aggregate points, and 750,000 aggregate line segments. Coordinates must be finite and have magnitude at most `1e150`; style sizes must be finite, positive, and at most 1,000.
- The browser clones validated state, converts coordinates through the current floating origin, constructs non-pickable `vtkPolyData` point/line pipelines, and applies section clipping. Replacement, navigation, selection clear, scene clear, and disposal remove the prior pipelines.
- Transient primitives remain outside retained model topology, picking, locator state, and 3MF/export input.

## Automated and packaged acceptance

Focused client validation covers cloning, point-only input, invalid connectivity/style, duplicate names, and aggregate bounds. Strict TypeScript/Vite production compilation passes.

Packaged point/line command:

```powershell
.\scripts\smoke-vtkjs-winforms.ps1 `
  -ModelPath '.\prototypes\VtkJsStreaming\tests\fixtures\sphere-symbol-smoke.inp' `
  -ExerciseCorePartConstruction `
  -ExerciseSelectionPrimitiveConstruction
```

Observed result:

- the ordinary `Add3DNodes` and common `Add3DCells` seams returned no native handles;
- browser diagnostics reported 2 transient actors, 5 points, and 2 line segments;
- `selection-construction/points-browser-only` and `selection-construction/lines-browser-only` were present;
- no point/line legacy-fallback trace was present;
- `Clear3DSelection` reduced transient diagnostics to 0 actors, 0 points, and 0 lines;
- retained topology remained 1 actor, 12 points, 4 triangles, 4 locator points, and 1 locator cell;
- `SelectionPrimitiveConstructionBrowserOnly=True`, with clean PrePoMax and renderer-child shutdown.

Because the surface and primitive acceptance timers both exercise public selection clearing, the surface regression runs separately. It also passed after the common-cell routing change with `SelectionSurfaceConstructionBrowserOnly=True` and unchanged retained topology.

Regression results:

- client Vitest: 66/66;
- contracts/server .NET tests: 80/80 (11 contracts and 69 server);
- Playwright: 2/2;
- prototype isolation audit: 6 projects, 34 C# files, 0 forbidden references;
- Release/x64 application build and packaged renderer staging: pass.

## Remaining boundary

PEND-004 remains open for animated-result construction and AVI/PNG export, OpenFOAM, transformations, generic auxiliary actors, and the remaining hidden scene-registry cleanup. Point/line smoothing and exact primary/secondary highlight-color policy remain presentation-setting work; they no longer require hidden construction for these selection previews.

# Static scalar-result construction retirement

Date: 2026-09-03

## Decision boundary

`FrmMain.AddScalarFieldOn3DCells` no longer constructs a duplicate hidden `vtkMaxActor` when VTK.js accepts the renderer-neutral part data. Supported static result surfaces are captured and published directly; rejected data still takes an explicit, traced legacy fallback. The default ActiViz renderer path is unchanged.

The associated actor scalar update, `UpdateScalarsAndRedraw`, and `UpdateScalarsAndCameraAndRedraw` VTK.js branches no longer invoke hidden mapper or scalar formatting. Browser publication owns scalar application, while retained camera adjustment preserves the existing host behavior.

## Implementation

- Successful scalar-part capture emits `result-construction/static-browser-only` and does not mark the legacy export registry as populated.
- Capture rejection emits `result-construction/static-legacy-fallback` before using the existing hidden implementation.
- Actor scalar update, scalar-only redraw, and scalar-plus-camera redraw emit browser-only compatibility traces instead of mutating hidden lookup tables or mappers.
- Retained actor export now includes triangle node/element IDs, VTK triangle types, and reconstructed locator nodes/cells, allowing a retained actor to cross the public scalar-construction seam without losing picking data.
- Browser scene acknowledgement is tracked by `(sceneId, revision)`, because full topology replacement legitimately creates a new scene whose revision begins at one.

## Packaged acceptance

Deterministic construction acceptance:

```powershell
.\scripts\smoke-vtkjs-winforms.ps1 `
  -ModelPath .\prototypes\VtkJsStreaming\tests\fixtures\sphere-symbol-smoke.inp `
  -ExerciseCorePartConstruction -ExerciseStaticScalarConstruction
```

Result: one actor, 12 points, four triangles, four locator points, one locator cell, initial scalar range `-3..7`, scalar-only update range `-5..9`, `StaticScalarConstructionBrowserOnly=True`, no legacy-fallback trace, and clean shutdown.

Real result and animation regression:

```powershell
.\scripts\smoke-vtkjs-winforms.ps1 `
  -ModelPath .\prototypes\VtkJsStreaming\tests\fixtures\follower-redraw-smoke.frd `
  -ExpectFiniteScalars -ExerciseAnimation -AnimationMode ScaleFactor `
  -ExerciseStaticScalarConstruction
```

Result: the controller-created `DISP/ALL` static result emitted `static-browser-only`; one actor, 12 render points, four triangles, four locator points, and one locator cell were published with a finite non-default range. Frame 1 then published successfully with `AnimationFrameActiVizBypassed=True`, `LegacyActivationMs=0`, and clean shutdown.

Regression results: client 63/63, .NET 80/80, Playwright 2/2, retained 3MF actor/palette acceptance, prototype isolation pass, full Release/x64 build and staging pass, and `git diff --check` pass.

## Remaining boundary

- Animated scalar construction still builds hidden actors because AVI/PNG export captures the legacy render window.
- Selection paths that require returned `vtkMaxActor` handles, points/generic actors, and OpenFOAM remain legacy-backed.
- Generated element-edge and transformed/auxiliary 3MF content still require parity work.
- Native `lib3mf.dll` packaging remains required for actual archive acceptance.

The next focused result slice should separate browser animation metadata from legacy AVI/PNG export storage. Cached triangle/quad selection surfaces and retained point/line primitives were subsequently retired from hidden construction.

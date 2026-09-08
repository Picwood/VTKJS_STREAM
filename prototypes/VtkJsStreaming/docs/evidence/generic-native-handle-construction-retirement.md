# Generic and native-handle construction retirement

Date: 2026-09-04

## Outcome

VTK.js mode now attempts renderer-neutral capture for every non-selection `Add3DCells(vtkMaxActorData)` submission rather than limiting the single-item seam to non-pickable base auxiliaries. Supported triangle, quad, quadratic-triangle, and quadratic-quad actors preserve their pickable flag and optional locator data in the retained scene. Non-pickable overlay `Add3DNodes` submissions use the persistent bounded primitive collection and return no native handle.

The remaining `AddActor(vtkMaxActor)` calls are legacy selection-cache replay branches. Active VTK.js selection caches store `vtkMaxActorData` and use the already-qualified selection surface/primitive paths. `FrmMain.AddActor` now enforces that invariant: an unexpected native handle in VTK.js mode is diagnosed as `blocked-browser-only` and is not forwarded to the hidden `vtkControl`. Default ActiViz behavior is unchanged.

Persistent primitive capture explicitly rejects pickable input because the primitive pipeline is intentionally non-pickable. This prevents silent loss of interaction semantics. The subsequent cell-classification audit found no production pickable-point producer, so rejection remains the defined safe boundary.

## Packaged acceptance

```powershell
.\scripts\build-vtkjs-mvp.ps1
.\scripts\smoke-vtkjs-winforms.ps1 `
  -ModelPath '.\prototypes\VtkJsStreaming\tests\fixtures\sphere-symbol-smoke.inp' `
  -ExerciseCorePartConstruction `
  -ExerciseGenericConstruction
```

The Release/x64 process reported `GenericConstructionBrowserOnly=True`. A synthetic pickable base surface with locator data and a non-pickable overlay point both returned null native handles. Browser diagnostics reported `True|True`: the retained surface pipeline remained pickable and the named overlay point existed in the persistent primitive set. The native-handle guard executed without hidden renderer mutation, both temporary names were removed from the authoritative registry, and PrePoMax plus its renderer child exited cleanly.

The prior auxiliary packaged smoke was rerun independently and retained `AuxiliaryConstructionBrowserOnly=True`, confirming that the broader classifier did not change base auxiliary routing or lifecycle behavior.

## Regression verification

- Client unit tests: 69/69 passed.
- Renderer contract/server tests: 80/80 passed.
- Browser progressive-rendering and integrity-recovery tests: 2/2 passed.
- Prototype isolation audit: pass, with zero forbidden references.
- Strict client build and full Visual Studio Release/x64 build/staging: pass.

## Remaining boundary

This does not ingest or translate arbitrary already-materialized `vtkMaxActor` instances; they are intentionally rejected in VTK.js mode because renderer-neutral source data is required. The later [cell-type classification](cell-type-classification-retirement.md) slice added triangle-strip, pixel, and polyline support and atomic rejection for every other generic render type. Overlay point actors currently share the main vtk.js renderer, so exact legacy separate-overlay depth ordering remains a visual-parity item. Volumetric OpenFOAM, transformed/persistent-primitive 3MF projection, widgets, and final hidden-registry cleanup remain open under PEND-004/PEND-005.

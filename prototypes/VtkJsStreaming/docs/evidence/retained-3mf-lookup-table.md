# Retained 3MF lookup table

Date: 2026-09-03

## Scope

The VTK.js export path no longer calls `vtkControl.GetLookupTable()`. The host retains the selected PrePoMax color spectrum and derives the 3MF writer's finite scalar range and sampled colors in managed code.

This retires lookup-table readback only. Scalar widgets are still mirrored to the hidden control, and the native 3MF runtime remains a packaging prerequisite.

## Implementation

- `VtkJsExportLookupTable` mirrors all 14 PrePoMax palette definitions and samples them without ActiViz. It supports RGB, HSV, CIE Lab, and Moreland Msh diverging interpolation.
- Brightness, reversed colors, configured color count, automatic range, manual range, and manual below/above colors are retained.
- Current-frame range is computed from finite scalars on visible captured contour actors. Animation-wide range is retained from `SetAnimationFrameData` and selected by the existing frame-range policy.
- `vtkMaxLookupTable` gained an immutable-range managed constructor and range accessors; its existing VTK constructor and default behavior remain unchanged.
- `FrmMain.GetLookupTable` selects the managed projection only in VTK.js mode and emits `lookup-table-managed` evidence.

## Packaged acceptance

```powershell
.\scripts\build-vtkjs-mvp.ps1 -Configuration Release
.\scripts\smoke-vtkjs-winforms.ps1 `
  -ModelPath .\prototypes\VtkJsStreaming\tests\fixtures\sphere-symbol-smoke.inp `
  -ExerciseCorePartConstruction -ExerciseThreeMfExportInput
```

The packaged run passed with one retained actor, 12 points, four triangles, nine default lookup colors, all 14 spectrum types sampled in normal/reversed configurations, an 11-color manually extended Viridis table, `lookup-table-managed`, and clean parent/child shutdown.

## Remaining boundary

- Static scalar-result construction has since moved browser-only; see `static-scalar-result-construction-retirement.md`.
- Generated element-edge and transformed/auxiliary export parity still use the conditional legacy merge.
- AVI/PNG animation export still captures the hidden VTK render window.
- `lib3mf.dll` is not present in the repository or staged Windows output, so an actual archive write remains unqualified.

The next result-construction boundary is animated scalar storage for AVI/PNG export. Cached triangle/quad selection surfaces were subsequently retired; point and line-only preview construction remains a smaller independent candidate.

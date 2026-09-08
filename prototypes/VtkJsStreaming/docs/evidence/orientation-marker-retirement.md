# Coordinate-system orientation marker retirement

Date: 2026-09-07

## Outcome

`FrmMain.SetCoorSysVisibility` no longer mutates the hidden ActiViz control when VTK.js is selected. The desktop retains a Boolean `prepomax.orientation-marker.updated-v1` state, republishes it after navigation, and the browser validates the complete envelope before applying it.

The browser owns a dedicated, non-interactive vtk.js orientation-marker renderer in the lower-left corner. It follows the primary camera, updates its viewport on resize, uses a 50-pixel minimum and 200-pixel maximum, and preserves the legacy red/green/blue palette using the nearest 8-bit colors. View reconstruction reapplies the retained visibility; disposal disables the widget and deletes both widget and axes resources.

The stock vtk.js 36.6 axes actor uses arrow shafts and does not provide the legacy X/Y/Z caption actors. Functional orientation and visibility no longer depend on hidden ActiViz, but exact shaft/caption appearance is not claimed and remains a named PEND-005 visual-review item.

## Defect found during qualification

The first browser run failed before topology load with `getXConfig is not a function`. vtk.js 36.6's public `AxesActor.getXAxisColor`/Y/Z implementations incorrectly call getters on the internal model. Production diagnostics therefore do not invoke those broken methods; they report the immutable palette used to configure the actor. The actor color input was also corrected to its actual 8-bit array contract. Both browser recovery scenarios passed after this fix.

## Packaged acceptance

```powershell
.\scripts\build-vtkjs-mvp.ps1 -Configuration Release -Platform x64
.\scripts\smoke-vtkjs-winforms.ps1 `
  -ModelPath '.\prototypes\VtkJsStreaming\tests\fixtures\sphere-symbol-smoke.inp' `
  -ExerciseCorePartConstruction `
  -ExerciseOrientationMarker
```

The Release/x64 process reported `OrientationMarkerBrowserOnly=True`. Hidden diagnostics were `false|0|true|200`: invisible, no marker renderer actor, camera aligned, and a 200-pixel maximum. Visible diagnostics were `true|1|true|200`. Both states reported the expected normalized 8-bit RGB values. Compatibility traces proved the browser-only visibility seam, and PrePoMax plus its renderer child exited cleanly.

## Regression verification

- Client Vitest: 71/71 passed across 19 files.
- Renderer contracts/server tests: 80/80 passed.
- Browser Playwright: 2/2 passed, including clean-reload recovery.
- Prototype isolation: 6 projects, 34 C# files, zero forbidden references.
- Strict client build and full Visual Studio Release/x64 build/staging: passed.
- PowerShell syntax and `git diff --check`: passed apart from repository line-ending notices.

## Next boundary

The scale-widget visibility/unit slice subsequently completed. PEND-004 next targets scalar-legend state and the remaining result widgets. Exact orientation captions/shaft styling belongs to PEND-005. User-pick mode, smoothing/highlight settings, editing-helper classification, volume-cell display/OpenFOAM parity, transformed export projection, native 3MF packaging, and final hidden-registry cleanup remain explicit.

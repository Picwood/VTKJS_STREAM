# Scale-widget retirement and ActiViz launch audit

Date: 2026-09-07

## Outcome

`FrmMain.SetScaleWidgetVisibility` and the length-unit update no longer mutate hidden ActiViz when VTK.js is selected. The desktop retains `prepomax.scale-widget.updated-v1` state and republishes it after navigation. Both peers require a Boolean visibility and a unit of at most 64 printable characters.

The browser owns a non-interactive DOM overlay. At the camera focal plane it measures the model length represented by the legacy 400-pixel target, rounds the normalized value to the nearest fifth, adjusts the bar width by that ratio, and shows five alternating intervals with six `G4`-equivalent labels. The unit is appended only to the last label using text content. Camera animation, explicit render, resize, and view reconstruction update or restore it; view deletion removes the overlay.

## Packaged acceptance

```powershell
.\scripts\build-vtkjs-mvp.ps1 -Configuration Release -Platform x64
.\scripts\smoke-vtkjs-winforms.ps1 `
  -ModelPath '.\prototypes\VtkJsStreaming\tests\fixtures\sphere-symbol-smoke.inp' `
  -ExerciseCorePartConstruction `
  -ExerciseScaleWidget
```

The Release/x64 process reported `ScaleWidgetBrowserOnly=True`. Browser diagnostics reported hidden and visible states with unit `mm`, six labels, a positive model length of `0.68`, and adjusted pixel width `402.021`; the final label was `0.68 mm`. Compatibility traces covered both visibility and unit bypass, and PrePoMax plus its renderer child exited cleanly.

## ActiViz launch audit

The visible viewport can be launched in VTK.js mode with `scripts/start-prepomax-vtkjs.ps1`, but this is not yet an ActiViz-free process. `PrePoMax` still references `vtkControl`; `vtkControl` references `Kitware.VTK`/`Kitware.mummy.Runtime`; `FrmMain` constructs the legacy control unconditionally; and unported/fallback seams still use it. Removing the ActiViz installation or assemblies is therefore unsupported today. Exact commands, remaining groups, and the exit gate are recorded in `docs/VTKJS_ACTIVIZ_STATUS.md`.

## Verification

- Client Vitest: 73/73 passed across 20 files.
- Strict TypeScript/Vite production build: passed.
- Full Visual Studio Release/x64 build and staging: passed.
- Packaged scale-widget process acceptance: passed.
- Renderer contracts/server tests: 80/80 passed.
- Browser Playwright: 2/2 passed.
- Prototype isolation: 6 projects, 34 C# files, zero forbidden references.
- Launcher and smoke-harness PowerShell syntax: passed.
- `git diff --check`: passed apart from repository line-ending notices.

## Next boundary

The scalar-legend, categorical color-bar, status-block/shared-layout, and graphics-interaction slices subsequently completed. PEND-004 next targets user-pick mode. Physical ActiViz removal remains blocked by the groups and build/runtime dependencies in `docs/VTKJS_ACTIVIZ_STATUS.md`.

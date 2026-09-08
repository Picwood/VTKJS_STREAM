# Status block and result-widget layout retirement

Date: 2026-09-08

## Outcome

The status-block seams and initial result-widget positioning no longer mutate the hidden ActiViz widgets when VTK.js mode is selected. The default ActiViz branch is unchanged.

The retained `prepomax.status-block.updated-v1` state carries visibility, background, border, and three or four complete display lines. PrePoMax formats those lines with the same current-culture date, time, numeric values, field-type terminology, step/increment semantics, and optional scale-factor or harmonic complex-angle line as `vtkMaxStatusBlockWidget`. Text is bounded and rejected when it contains control characters; the browser repeats validation and uses text nodes only.

Animation-frame activation updates step, increment, analysis time/frequency, animation type, and scale/angle before browser publication. Full scene clear hides the status block, while the retained visibility preference allows the next result content update to restore it like the legacy widget.

`prepomax.result-widget-layout.updated-v1` owns bounded integer pixel offsets for the scalar legend, categorical color bar, and status block. `InitializeResultWidgetPositions` resets the scalar legend to top/left 20 pixels and the status block to top/right 20 pixels; `InitializeColorBarWidgetPosition` resets the color bar to top/left 20 pixels. The state is replayed after navigation and scene-view reconstruction.

## Automated evidence

- Client normalization: 4 status/layout tests; complete state cloning plus malformed, unsafe, fractional, negative, and excessive-value rejection.
- Client suite: 81/81 tests and strict TypeScript/Vite production build.
- .NET protocol/server suite: 80/80; Playwright integration: 2/2.
- Prototype isolation audit: six projects, 34 C# files, zero forbidden references.
- Release/x64 application build and renderer staging: pass through `scripts/build-vtkjs-mvp.ps1`.
- Packaged WinForms acceptance:
  `scripts/smoke-vtkjs-winforms.ps1 -ModelPath prototypes/VtkJsStreaming/tests/fixtures/sphere-symbol-smoke.inp -ExerciseCorePartConstruction -ExerciseStatusBlock`.
- Process result: `StatusBlockBrowserOnly=True`, `CorePartConstructionBrowserOnly=True`, one actor, 12 points, four triangles, and graceful parent/renderer shutdown.
- The smoke exercised every public content/visibility/appearance/reset seam, exact 20-pixel shared placement, a frequency result, harmonic frame metadata and complex-angle line, then hidden state. Every call emitted a compatibility-bypass trace.

## Boundaries

Exact font metrics, long-line behavior at unusually small viewport sizes, and double-click settings affordances remain manual/product-interaction review items. ActiViz remains a process dependency for the capability groups and build/startup reasons recorded in `docs/VTKJS_ACTIVIZ_STATUS.md`.

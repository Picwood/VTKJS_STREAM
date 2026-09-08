# Graphics interaction settings retirement

Date: 2026-09-08

## Outcome

`SetSmoothing`, `SetHighlightColor`, and `SetMouseHighlightColor` no longer mutate or recreate the hidden ActiViz render window when VTK.js mode is selected. The default ActiViz path is unchanged.

The retained `prepomax.graphics-interaction.updated-v1` message contains point/line smoothing preferences and normalized primary, secondary, and mouse-highlight RGB triples. The browser validates the complete state atomically, clones it, reapplies it after navigation/view reconstruction, and exposes bounded diagnostics.

Primary color now drives normal selection geometry and actor-highlight wireframes; the legacy 0.7 surface-darkening factor is retained for whole-part/actor surfaces. Selection construction marked `UseSecondaryHighlightColor` is resolved through the retained secondary color for both point/line primitives and captured surface actors. Changing colors updates already-retained selection primitives/surfaces. Mouse color drives the browser's validated-pick marker and remains the authoritative color for the upcoming hover/user-pick interaction slice.

WebGL does not expose the deprecated OpenGL point/line smoothing toggles used by VTK 5.8. The preferences are retained without rebuilding a hidden or visible render window, while edge/point anti-aliasing remains the browser/WebGL context's multisample rasterization behavior. This difference is explicit rather than represented as an exact API equivalence.

## Automated evidence

- Client validator: 2/2 tests for complete-state cloning and atomic rejection of malformed flags/RGB.
- Client suite: 83/83 and strict TypeScript/Vite production build.
- .NET regression suite: 80/80.
- Playwright progressive-rendering/resource-integrity suite: 2/2. The configuration now uses Playwright's pinned Chromium by default and accepts `PREPOMAX_PLAYWRIGHT_CHANNEL` for explicit branded-browser runs.
- Prototype isolation audit: 6 projects, 34 C# files, zero forbidden references.
- Release/x64 application build and staging: pass.
- Packaged command: `scripts/smoke-vtkjs-winforms.ps1 -ModelPath prototypes/VtkJsStreaming/tests/fixtures/sphere-symbol-smoke.inp -ExerciseCorePartConstruction -ExerciseGraphicsInteraction`.
- Packaged result: `GraphicsInteractionBrowserOnly=True`, `CorePartConstructionBrowserOnly=True`, one actor, 12 points, four triangles, and graceful parent/renderer shutdown.
- The smoke drove all three public seams, verified `false/true` smoothing state, exact normalized RGB, browser-WebGL rasterization disclosure, a 0.7-darkened primary actor highlight, a secondary transient-point highlight, and every compatibility-bypass trace.
- PowerShell syntax and repository diff checks: pass.

## Boundaries

Hover highlighting itself belongs to the next user-pick-mode slice. Exact anti-aliasing and pixel-level appearance vary by browser/GPU and remain part of manual visual acceptance. ActiViz is still a startup dependency for the other groups recorded in `docs/VTKJS_ACTIVIZ_STATUS.md`.

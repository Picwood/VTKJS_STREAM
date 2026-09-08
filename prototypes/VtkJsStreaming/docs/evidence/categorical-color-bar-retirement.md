# Categorical color-bar retirement

Date: 2026-09-08

## Outcome

The categorical color-bar seams no longer mutate the hidden ActiViz widget when VTK.js mode is selected. Initial position, ordered replacement, append, visibility, background, border, and hide/clear now use retained `prepomax.color-bar.updated-v1` state. The default ActiViz branch is unchanged.

The host requires equal color/label counts, caps retained entries at 4,096, bounds labels to 256 printable characters, and preserves the legacy underscore/hyphen-to-space normalization. RGB values are serialized as normalized triples. The browser validates the complete state before mutation and creates a non-interactive top-left DOM list exclusively with bounded elements and text nodes. The state survives scene-view reconstruction; hiding and full scene clear remove all retained entries, matching `vtkMaxColorBarWidget.ClearColorsAndLabels` behavior.

## Packaged acceptance

```powershell
.\scripts\build-vtkjs-mvp.ps1 -Configuration Release -Platform x64
.\scripts\smoke-vtkjs-winforms.ps1 `
  -ModelPath '.\prototypes\VtkJsStreaming\tests\fixtures\sphere-symbol-smoke.inp' `
  -ExerciseCorePartConstruction `
  -ExerciseColorBar
```

Release/x64 acceptance reported `ColorBarBrowserOnly=True`. The public seams replaced red/blue entries labelled `Front_face` and `Back-face`, appended a green `Load_A` entry, enabled the background, disabled the border, and reset the top-left position. Browser diagnostics returned three ordered entries labelled `Front face`, `Back face`, and `Load A` with exact normalized RGB. A later public hide returned zero entries while retaining the appearance settings. All operations emitted compatibility-bypass traces; PrePoMax and its renderer child exited cleanly.

## Verification

- Client Vitest: 77/77 passed across 22 files.
- Strict TypeScript/Vite production build: passed.
- Full Visual Studio Release/x64 build and staging: passed.
- Packaged categorical color-bar process acceptance: passed.
- Renderer contracts/server tests: 80/80 passed.
- Browser Playwright: 2/2 passed.
- Prototype isolation: 6 projects, 34 C# files, zero forbidden references.

## Remaining boundary

The status block and shared widget layout subsequently completed. Exact typography, scrolling policy for unusually large categorical sets, and representative-model visual parity remain PEND-005 review items. ActiViz remains a process dependency for the broader reasons recorded in `docs/VTKJS_ACTIVIZ_STATUS.md`.

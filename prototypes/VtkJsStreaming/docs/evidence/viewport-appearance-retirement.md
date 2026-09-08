# Viewport-appearance compatibility retirement

Date: 2026-09-02

This PEND-004 slice retires `vtkControl.SetBackground` and `vtkControl.SetLighting` from the hidden ActiViz renderer when VTK.js is selected. The default renderer still calls the original methods unchanged. Point/line smoothing is deliberately not claimed by this slice and remains in the compatibility inventory.

The retained `prepomax.viewport-appearance.updated-v1` state carries the gradient flag, normalized top and bottom RGB colors, ambient and diffuse coefficients, and the caller's redraw intent. Host and browser reject non-finite or out-of-range lighting/color components. State is replayed after WebView navigation and reapplied whenever the client recreates its scene view.

VTK.js maps the legacy bottom color to `background`, top color to `background2`, and the flag to `gradientBackground`. Ambient and diffuse values apply to every existing streamed surface and server-derived section actor and are installed on later actors as they are created. Selection and engineering-symbol overlays retain their intentionally distinct highlight/symbol material policies.

## Verification

- Vitest: 59/59 overall. New focused tests clone a valid state and reject malformed colors, lighting coefficients, and flags.
- Strict TypeScript/Vite production build: passed.
- Playwright: 2/2 overall. The progressive two-actor scene accepted exact gradient and lighting state, and both actor properties reported `0.35` ambient / `0.65` diffuse.
- .NET prototype suites: 80/80 (11 contracts and 69 server tests).
- `PrePoMax.sln` Release/x64 and complete MVP staging: passed with the existing warning profile.
- Packaged WebView2 process: `sphere-symbol-smoke.inp` published one actor, 12 render tuples, four triangles, four locator nodes, and one locator cell. Real `FrmMain.SetBackground` and `SetLighting` emitted independent compatibility-bypass traces; browser diagnostics reported the exact normalized colors and both lighting coefficients on every streamed actor. PrePoMax and its renderer child exited cleanly.

PEND-004 remains open. The refreshed grouped inventory is [ActiViz compatibility inventory](../activiz-compatibility-inventory.md).

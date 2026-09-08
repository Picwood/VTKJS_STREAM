# Browser-owned transformation copies

Date: 2026-09-04

## Outcome

In VTK.js mode, `FrmMain` symmetry, linear-pattern, circular-pattern, apply, hide, and show seams no longer call the hidden ActiViz renderer. The desktop bridge composes bounded affine matrices and publishes retained state; the browser creates non-pickable `vtkActor` instances that share each source chunk's mapper and polydata.

Composition matches the legacy sequential expansion. Each transform step operates on the complete copy set produced by prior steps and emits powers 1 through `numOfItems - 1`. The identity/original actor is not transmitted. State is capped at 256 matrices, vectors and angles must be finite and bounded, circular axes must be nonzero, and matrices are independently checked as nonsingular affine transforms in the browser.

World matrices use row-major absolute coordinates. The browser converts them to VTK's column-major user matrix and applies `A * worldOrigin + t - worldOrigin`, preserving precision for origin-relative Float32 geometry. Copies share mapper resources, so scalar/frame updates, contour visibility, and clipping-plane changes propagate without geometry duplication. Copies inherit source visibility, color, opacity, and viewport lighting; global transformation hide/show is retained separately. They are deliberately non-pickable and absent from server selection topology.

Navigation and scene recreation retain the state. The browser acknowledges it after direct application and again after a scene finishes loading, closing the bootstrap ordering race discovered by the first packaged probe. Full scene clear replaces it with an empty visible state.

## Automated evidence

- Client tests cover clone isolation, malformed/projective/singular/non-finite/oversized rejection, and exact floating-origin/row-to-column conversion.
- Strict TypeScript/Vite and Release/x64 application builds pass.
- Packaged `BENDING_NEON36.pmx` acceptance exercised symmetry plus two-item linear and circular patterns. Their Cartesian expansion produced seven matrices, seven instances per loaded chunk, a positive copy count, and zero pickable copies; it then acknowledged zero visible copies after hide and all copies after show. The process closed normally and its renderer child exited.

Reproduction:

```powershell
.\scripts\build-vtkjs-mvp.ps1 -Configuration Release -Platform x64
.\scripts\smoke-vtkjs-winforms.ps1 `
  -ModelPath C:\Users\fvazeille\Documents\GitHub\SpectraRev\PMX2.4.0\BENDING_NEON36.pmx `
  -ExerciseTransformations
```

Result: `TransformationsBrowserOnly=True`; 15 base actors, 828,619 render points, 1,623,416 triangles, 2,140,668 locator points, and 931,033 locator cells.

Regression verification: client Vitest 69/69, contracts/server .NET 80/80, Playwright 2/2, prototype isolation 6 projects / 34 C# files / zero forbidden references, strict client production build, Release/x64 application build/staging, and `git diff --check` all pass.

## Deliberate boundaries

Transformation copies are viewport instances, not server actors. Server-generated section cap geometry therefore does not yet include transformed copies, and retained 3MF input does not materialize those copies. Exact reflected-face winding/backface appearance still requires PEND-005 visual review. These are recorded limitations; ordinary mapper clipping and scalar/animation updates apply to the copies.

PEND-004 remains open for generic auxiliary/OpenFOAM construction, transformed export/cap projection, widgets/tools, and final hidden-registry cleanup.

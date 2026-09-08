# Generic cell-type classification retirement

Date: 2026-09-07

## Outcome

The generic VTK.js construction boundary no longer silently omits unknown cells. Retained surface capture accepts VTK triangle (5), triangle strip (6), pixel (8), quad (9), quadratic triangle (22), and quadratic quad (23). Triangle strips expand with alternating winding; pixels use the VTK 0-1-3-2 perimeter. Persistent primitives accept line (3), polyline (4), and quadratic edge (21); polylines expand into adjacent segments.

Classification is atomic for surface actors. If any cell has an unsupported type, capture rejects the complete actor and records the sorted unsupported type set before any retained-scene mutation. This avoids presenting incomplete engineering geometry as if it were valid.

The production caller audit found no `Add3DNodes` submission with `Pickable=true`. Persistent primitives are intentionally non-pickable, so a hypothetical pickable point is rejected rather than silently losing selection behavior. Volume cells remain available through locator topology for server selection; direct generic volume rendering is not claimed.

## Packaged acceptance

```powershell
.\scripts\build-vtkjs-mvp.ps1 -Configuration Release -Platform x64
.\scripts\smoke-vtkjs-winforms.ps1 `
  -ModelPath '.\prototypes\VtkJsStreaming\tests\fixtures\sphere-symbol-smoke.inp' `
  -ExerciseCorePartConstruction `
  -ExerciseCellClassification
```

The Release/x64 process reported `CellClassificationBrowserOnly=True`. A four-node strip plus four-node pixel produced 8 points and 4 triangles. A three-node polyline produced 3 points and 2 line segments. A mixed triangle/tetra actor was rejected with `unsupported render cell types=10`, and direct pickable-point capture was rejected. Browser diagnostics returned `true|true` for the two supported named objects; subsequent removal reported both absent. PrePoMax and the renderer child shut down cleanly.

The generic-construction and auxiliary-construction packaged exercises were then rerun independently on the same fixture. They retained `GenericConstructionBrowserOnly=True` and `AuxiliaryConstructionBrowserOnly=True`, respectively, confirming that strict surface classification did not regress the previously retired paths.

## Regression verification

- Client Vitest: 69/69 passed.
- Renderer contracts/server tests: 80/80 passed.
- Browser Playwright: 2/2 passed.
- Prototype isolation: 6 projects, 34 C# files, zero forbidden references.
- Strict client build and full Visual Studio Release/x64 build/staging: passed.
- `git diff --check`: passed apart from repository line-ending notices.

## Remaining boundary

VTK volume cells are not converted into generic display surfaces; normal model/result rendering continues to supply extracted boundary surfaces while locator topology carries full cells for semantic selection. Exact legacy overlay depth ordering, transformed cap/export projection, widgets/tools, 3MF native packaging, and final hidden-registry cleanup remain under PEND-004/PEND-005/PEND-007.

# Quadratic-hexa section parity evidence

Date: 2026-08-11

## Decision and source chain

The replacement target remains ActiViz/VTK 5.8's piecewise-linear contour, not a new curved isoparametric section. The VTK 5.8 [`vtkQuadraticHexahedron`](https://raw.githubusercontent.com/Kitware/VTK/v5.8.0/Filtering/vtkQuadraticHexahedron.cxx) source generates six face centers and one cell center, divides type 25 into eight fixed linear hexahedra, and contours each subcell. [`vtkTriQuadraticHexahedron`](https://raw.githubusercontent.com/Kitware/VTK/v5.8.0/Filtering/vtkTriQuadraticHexahedron.cxx) uses the supplied 27 nodes with the same eight subcells. [`vtkHexahedron`](https://raw.githubusercontent.com/Kitware/VTK/v5.8.0/Filtering/vtkHexahedron.cxx) delegates connectivity to the 256-case marching-cubes table embedded in [`vtkCell3D.cxx`](https://raw.githubusercontent.com/Kitware/VTK/v5.8.0/Filtering/vtkCell3D.cxx).

## Implemented slice

- VTK types 25 and 29 use the exact eight legacy linear-hexahedron node maps.
- Type 25 evaluates the exact twenty-node serendipity weights at the six face centers and cell center. Coordinates and result scalars are generated together.
- Generated face centers receive stable actor-local identities derived from their sorted eight-node face, so neighboring quadratic cells share cut vertices. Cell centers remain cell-local.
- Every subcell uses the exact VTK 5.8 256 x 16 marching-cubes edge table. A warped, nondegenerate case-90 corpus exercises disconnected ambiguous topology that an angle-ordered polygon fan cannot represent.
- Element-border lines are derived from triangle-edge incidence. Internal subdivision edges cancel while exterior cap edges remain.
- The existing nondegenerate outer-boundary crossing gate prevents the eight-subcell path from running for unrelated cells.

## Verification

| Check | Result |
|---|---|
| Type-25 live ActiViz oracle | Passed: 9 points, 8 triangles, exact generated 3x3 cut grid, and interpolated scalars |
| Type-29 live ActiViz oracle | Passed: canonical 9-point / 8-triangle grid and warped case 90 with 18 points / 14 triangles |
| C# regressions | Passed: type-25 generated samples, type-29 supplied samples, exact ambiguous topology, scalar interpolation, source IDs, winding, and adjacent type-25 shared-face deduplication |
| Release .NET suite | 73/73 passed: 11 contract and 62 server tests after the subsequent wedge and pyramid slices |
| Client unit/build suite | 26/26 passed; strict TypeScript and production build passed |
| Playwright production suite | 2/2 passed |
| Supplied rotor C3D20R process smoke | Passed: 2,560 render points / 4,608 triangles, 2,656 locator points / 368 cells, one section actor, zero skipped cells, create/update/remove lifecycle, and graceful shutdown |
| Full Release/x64 build and staging | Passed with the existing MSIL/AMD64 reference warnings and no errors |

The executable oracles are `scripts/compare-section-quadratic-hexa-activiz.ps1` and `scripts/compare-section-triquadratic-hexa-activiz.ps1`.

## Remaining work

VTK types 26 and 27 were completed in subsequent source-backed wedge and pyramid slices. All supported quadratic volume types now use their VTK 5.8 subcell contour paths.

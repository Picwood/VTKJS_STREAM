# Quadratic-wedge section parity evidence

Date: 2026-08-11

## Decision and source chain

The replacement follows ActiViz/VTK 5.8's piecewise-linear contour. The VTK 5.8 [`vtkQuadraticWedge`](https://raw.githubusercontent.com/Kitware/VTK/v5.8.0/Filtering/vtkQuadraticWedge.cxx) source evaluates three rectangular-face centers with its fifteen-node interpolation functions, divides type 26 into eight fixed linear wedges, and contours each subcell. The dedicated 64-case connectivity and nine linear edges are embedded in [`vtkWedge.cxx`](https://raw.githubusercontent.com/Kitware/VTK/v5.8.0/Filtering/vtkWedge.cxx).

## Implemented slice

- Type 26 uses the exact eight legacy linear-wedge node maps.
- The three generated samples use the exact fifteen-node interpolation weights for coordinates and result scalars.
- Each generated rectangular-face center receives a stable actor-local identity derived from its sorted eight face nodes, allowing adjacent cells to share cut vertices even when the center has a different local generated index.
- Each subcell uses the exact VTK 5.8 64 x 13 wedge edge table. A warped generated case-43 corpus exercises the table's four-triangle topology.
- Triangle-edge incidence retains element-border lines while suppressing internal subdivision edges.
- The existing outer-boundary crossing gate avoids generated samples and eight subcell traversals for uncut cells.

## Verification

| Check | Result |
|---|---|
| Live ActiViz 5.8 oracle | Passed: canonical 12-point / 12-triangle 4x3 grid with interpolated scalars; warped generated case 43 with 20 points / 18 triangles |
| C# canonical regression | Passed: exact point/triangle/line counts, complete in-plane coordinate set, scalars, source IDs, and winding |
| C# adjacent-cell regression | Passed: 21 shared points, 24 triangles, 18 element-border segments, and 12 source triangles per cell |
| Release .NET suite | 73/73 passed: 11 contract and 62 server tests after the subsequent pyramid parity slice |
| Full Release/x64 build and staging | Passed with the existing MSIL/AMD64 reference warnings and no errors |
| Supplied `bitsolo-nodal` process smoke | Passed: 29,912 C3D15 elements across three input blocks; 422,797 render points / 836,744 triangles; 1,127,607 locator points / 657,394 cells; three section actors; zero skipped cells; graceful shutdown |

The executable oracle is `scripts/compare-section-quadratic-wedge-activiz.ps1`.

## Remaining work

VTK type 27 was completed in the subsequent quadratic-pyramid slice. All supported quadratic volume types now use their VTK 5.8 subcell contour paths.

# Quadratic-pyramid section parity evidence

Date: 2026-08-11

## Decision and source chain

The replacement follows ActiViz/VTK 5.8's piecewise-linear contour. The VTK 5.8 [`vtkQuadraticPyramid`](https://raw.githubusercontent.com/Kitware/VTK/v5.8.0/Filtering/vtkQuadraticPyramid.cxx) source evaluates one quadratic base-face center with its thirteen-node interpolation functions. It contours six fixed linear pyramids and then four fixed linear tetrahedra. The 32-case pyramid connectivity and eight linear edges are embedded in [`vtkPyramid.cxx`](https://raw.githubusercontent.com/Kitware/VTK/v5.8.0/Filtering/vtkPyramid.cxx); the final four subcells reuse the already qualified VTK 5.8 marching-tetra table.

## Implemented slice

- Type 27 generates the base-face center with the exact thirteen-node weights for coordinates and result scalars.
- The generated center receives a stable actor-local identity derived from the sorted eight-node quadratic base, so two pyramids sharing that base also share cut vertices.
- The first six subcells use the exact VTK 5.8 linear-pyramid maps and 32 x 13 edge table.
- The final four subcells use the exact legacy tetra maps and marching-tetra table.
- A warped generated case-21 corpus exercises a four-triangle pyramid case alongside the internal tetrahedra.
- Triangle-edge incidence retains element-border lines and suppresses internal subdivision edges; the outer-boundary crossing gate avoids all subcell work for uncut cells.

## Verification

| Check | Result |
|---|---|
| Live ActiViz 5.8 oracle | Passed: canonical 5-point / 3-triangle cut with exact coordinates and scalars; warped generated case 21 with 10 points / 8 triangles |
| C# canonical regression | Passed: exact point/triangle/line counts, coordinate set, scalar interpolation, source IDs, and positive winding |
| C# adjacent-base regression | Passed: 7 shared points, 6 triangles, 8 element-border segments, and 3 source triangles per cell |
| Release .NET suite | 76/76 passed: 11 contract and 65 server tests after the coincident multi-cell slice |
| Client unit/build suite | 26/26 passed; strict TypeScript and production build passed |
| Playwright production suite | 2/2 passed |
| Full Release/x64 build and staging | Passed with the existing MSIL/AMD64 reference warnings and no errors |
| Existing C3D20R rotor regression smoke | Passed: one section actor, zero skipped cells, complete lifecycle, and graceful shutdown |
| Supplied `mesh-C3D13.inp` process smoke | Passed: 156,717 nodes, 112,311 C3D10 elements, and 2,150 genuine C3D13 elements imported through the production INP reader; 8 actors, 13,868 render points / 25,800 triangles, and 163,701 locator points / 116,611 cells published; section create/update/remove produced 6 cap actors with zero skipped cells and shut down cleanly |

The executable oracle is `scripts/compare-section-quadratic-pyramid-activiz.ps1`.

## Result

VTK types 24, 25, 26, 27, and 29 now use their source-backed VTK 5.8 contour subdivisions. No supported quadratic volume type remains on the outer-edge polygon-fan fallback.

The INP smoke also qualifies the solver-to-VTK ordering conversion in `GetParabolicPyramidElement`: the supplied C3D13 connectivity is reordered into the thirteen-node VTK type-27 layout before capture. Model-only startup now accepts the explicit default-unit dialog in the process harness, waits for the full locator snapshot rather than an intermediate scene, and starts section acceptance from published topology instead of the legacy `DisplayedMesh` readiness proxy.

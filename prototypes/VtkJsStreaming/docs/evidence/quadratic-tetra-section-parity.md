# Quadratic-tetra section parity evidence

Date: 2026-08-11

## Decision

The replacement target is ActiViz/VTK 5.8 behavior, not an independently smoother quadratic surface. The VTK 5.8 sources show that `vtkQuadraticTetra::Contour` divides a ten-node tetrahedron into eight fixed linear tetrahedra and invokes `vtkTetra::Contour` for each. `vtkTetra` uses a sixteen-case marching table and linear edge interpolation. Therefore an isoparametric quadratic-root implementation would diverge from the renderer being replaced.

## Implemented slice

- VTK type 24 uses the exact eight legacy sub-tetra node maps.
- Each subcell uses the VTK 5.8 marching-tetra edge cases, including the same two-triangle cases.
- Cut vertices are deduplicated across shared subcell edges, result scalars are linearly interpolated on the same edge, triangles retain the source element ID, and winding is normalized to the requested section normal.
- The original quadratic outer half-edges still provide element-border lines.
- The subdivision loop runs only after a nondegenerate outer boundary crossing. The first large-tube attempt revealed that running eight subcells for all 370,848 cells missed the first acknowledgement window; the gate restored the unchanged create/update/remove acceptance timing.

## Verification

| Check | Result |
|---|---|
| Live ActiViz 5.8 oracle script | Passed: 10 points, 9 triangles, exact plane coordinates and scalar interpolation |
| Quadratic-tetra C# regression | Passed: same point/triangle counts, complete in-plane coordinate set, internal `(0.25, 0.25, 0.25)` vertex, scalar interpolation, source IDs, boundary lines, and positive winding |
| Release .NET suite | 73/73 passed: 11 contract and 62 server tests after all quadratic-volume parity slices |
| Client unit/build suite | 26/26 passed; strict TypeScript and production build passed |
| Playwright production suite | 2/2 passed |
| 370,848-cell C3D10 tube process smoke | Passed: 577,324 locator points, 370,848 cells, one section actor, zero skipped cells, create/update/remove lifecycle, graceful host and renderer shutdown |
| Full Release/x64 build and staging | Passed with the existing MSIL/AMD64 reference warnings and no errors |

The executable oracle is `scripts/compare-section-quadratic-tetra-activiz.ps1`.

## Remaining work

VTK types 25/29, 26, and 27 were completed in subsequent source-backed parity slices. All supported quadratic volume types now use their VTK 5.8 subcell contour paths.

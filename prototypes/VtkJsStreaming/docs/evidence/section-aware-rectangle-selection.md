# Section-aware rectangle-selection evidence

Date: 2026-08-11

## Implemented slice

- Added an optional absolute `SelectionSectionPlane` to both `frustum.resolve` and `area.resolve` without breaking callers that omit it.
- Matched vtk.js clipping semantics exactly: non-negative signed plane distance is retained and the boundary is selectable.
- Applied the plane to full-cell BVH pruning, locator-node filtering, and cell polygon intersection on the server.
- Clipped fallback rendered triangles before the browser's left-to-right projection test and rejected empty clipped polygons for either drag direction.
- Revalidated fallback candidates against immutable server position resources, rejecting triangles wholly behind the plane.
- Removed only the section-view rectangle-selection guard; exploded-preview selection remains unavailable because its actor transforms are not represented in the server locator.

## Acceptance evidence

| Check | Result |
|---|---|
| Release .NET tests | 62/62 passed: 11 contracts and 51 server tests |
| Client unit tests | 26/26 passed across 8 files |
| Strict TypeScript and Vite production build | Passed; 764.14 kB minified / 210.05 kB gzip main bundle |
| Playwright production tests | 2/2 passed; the section case uses an absolute large-origin plane that intersects both fixture triangles, performs Alt-drag, and requires a non-empty validated fallback result |
| Full Release/x64 PrePoMax build and renderer staging | Passed; existing MSIL/AMD64 reference warnings remain, with no build errors |

The C# regression set covers a section crossing a tetrahedron, a wholly clipped cell, retained-side node filtering, and a malformed zero normal. Endpoint tests cover a crossing fallback triangle, a wholly clipped forged candidate, and malformed-plane rejection. TypeScript unit tests cover crossing-edge interpolation, a wholly clipped polygon, and boundary retention.

## Remaining limitation

At the time of this slice, quadratic caps used only outer corner/midside edge chains. Subsequent slices completed the VTK 5.8 type-24 tetra, type-25/type-29 hexa, type-26 wedge, and type-27 pyramid subdivisions.

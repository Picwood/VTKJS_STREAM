# Coincident multi-cell section differential evidence

Date: 2026-08-11

## Scope

`scripts/compare-section-coincident-activiz.ps1` drives the production `SectionGeometryBuilder` and live ActiViz/VTK 5.8 with the same adjacent-cell meshes and exact planes. Unlike the randomized single-cell corpus, this oracle compares output point counts as well as canonical triangle/scalar multisets, so locator merging is observable.

The deterministic corpus covers:

- exact shared faces for linear tetrahedron, voxel, hexahedron, wedge, and pyramid cells (VTK types 10-14);
- exact shared faces for quadratic tetrahedron, hexahedron, wedge, pyramid, and triquadratic hexahedron cells (types 24-27 and 29);
- a two-hexahedron plane passing exactly through shared nodes;
- two adjacent hexahedra whose geometrically identical interface uses distinct input point IDs.

## Differences found and resolved

1. A plane coincident with two tetrahedra's common face produced the same triangle twice. Production now deduplicates actor-wide triangles independently of winding while retaining the first VTK-classified source element.
2. Linear hexahedra used a generic ordered polygon fan, which selected a different valid diagonal when a plane passed through shared nodes. Linear tetrahedron, voxel/hexahedron, wedge, and pyramid paths now use the same legacy marching tables already qualified for quadratic subcells.
3. Vertex reuse was keyed only by topology identity. Production now also performs exact actor-local coordinate lookup, merging coincident cut vertices with distinct source point IDs and retaining the first inserted scalar, matching the VTK locator result.

## Qualification result

All 12 cases match live ActiViz 5.8 exactly: point counts, triangle counts, triangle connectivity by canonical coordinates, and interpolated scalar values. The aggregate output is 53 triangles. Focused C# regressions independently cover shared-face triangle suppression, the VTK marching diagonal through shared nodes, exact-coordinate interface merging, and source-element retention.

Replay:

```powershell
prototypes\VtkJsStreaming\scripts\compare-section-coincident-activiz.ps1
```

## Boundary

The coordinate locator intentionally merges exact derived coordinates. It does not introduce a broader near-coincident tolerance that could collapse distinct thin geometry. Parity with VTK releases other than the supplied 5.8 runtime remains outside this evidence.

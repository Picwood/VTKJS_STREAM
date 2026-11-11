# Mesh parts and visualization extraction

2026-09-11, native Linux, SDK 10.0.401.

The portable CaeMesh project now compiles 53 production source files (approximately
10,200 lines). It includes `BasePart`, `MeshPart`, `ResultPart`, bounding boxes,
visualization data, part-property data, graph/node helpers, and their topology
enums. Geometry/CAD parts, meshing configuration and the `FeMesh` aggregate are
still outside the portable project.

## Topology boundary

`MeshTopology.GetVisualizationEdgeCells` contains the existing edge-construction
implementation extracted from `FeMesh`. The old public `FeMesh` method delegates
to it, retaining the Windows caller API. `VisualizationData` uses the new helper
directly, removing its only direct `FeMesh` dependency. The helper is included in
both project graphs. Its edge ordering and allocation behavior are unchanged.

Visualization and graph code compile from shared sources; an unused WinForms
import was removed. `System.Drawing.Color` remains value data in the part model;
no GDI drawing operations or System.Drawing.Common package were introduced.

## Bounding-box copy policy

The default bounding-box copy handles null and integer tags directly. Integer
tags are used by the existing explosion algorithm. A new constructor overload
and `DeepCopy(Func<object, object> copyTag)` let the caller explicitly copy other
tags. Coordinates always copy independently.

On modern .NET, the default copy rejects other tag types with a
`NotSupportedException` naming the required callback. It does not discard the
tag, share mutable state silently, or enable BinaryFormatter. On the legacy
Windows build, the default retains existing arbitrary-tag serialization copying.

`BasePart` now uses the explicit bounding-box copy and the previously implemented
mass-property copy. Its normal null/integer-tag cases run portably. Copying a part
whose bounding box has an object-valued tag is **not qualified**: the existing
part copy API does not forward a tag callback. The connected-part-list tags in
the older explosion path therefore still require a graph-aware application
policy when that path is ported. This is an explicit remaining boundary.

## Evidence

The mesh harness passes 1,140 assertions, including 66 new assertions across
English and Italian cultures:

- bounding-box geometry/index/null copies, mutable-tag rejection, explicit
  mutable-tag callback and copy isolation;
- literal expected edge ordering for linear/quadratic edges, triangles and quads;
- removal of a shared internal face between two tetrahedra, leaving six exterior
  faces and preserving element IDs;
- visualization binary round trip, full record consumption, null marker and
  independent nested array copies;
- base/mesh/result part subtype, identity, geometric properties and independent
  labels, visualization, bounding box, offsets and mass arrays;
- graph connected components and structural copy isolation.

Run `bash scripts/test-portable.sh`. The full Linux suite passes 60 STL, 178
legacy NCalc, 794 CaeGlobals and 1,140 CaeMesh checks: **2,172 total**.
The Windows/Linux CI workflow already covers these project/source paths.
Full Windows application compilation/execution remains unverified locally.

These checks do not qualify every visualization operation, historical binary
version, section-view algorithm, graph algorithm, part property or object-valued
tag graph. Visualization binary I/O remains the existing format, not a replacement
for `.pmx` persistence. The existing unused-field warning in LinearPyramidElement
also remains.

## Next slice

Bring geometry/CAD part types and meshing settings into the portable graph with
explicit copies for their mutable configuration. Then extract the remaining
`FeMesh` notification, collection and mesher boundaries and characterize complete
mesh workflows using small imported-model fixtures.

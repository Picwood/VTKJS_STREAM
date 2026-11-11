# CaeMesh extraction boundary

2026-09-11: `portable/CaeMesh/CaeMesh.csproj` builds 93 shared production source
files plus a portable notification adapter on .NET 10. It references portable CaeGlobals
and does not reference GmshCommon, WinForms, ActiViz, or a native mesher.

## Included scope

The initial slice includes the active node classes/comparers and merge-settings
data, element base classes, mass/spring/gap/beam elements, linear/quadratic shell
and solid element classes, element/cell identifiers, and `GeometryTools`.
The subsequent [copy/I/O slice](MESH_COPY_IO_EVIDENCE.md) adds mesh groups, node
sets, element sets, INP set data and mass properties, with explicit copies for
selection history and mass-property arrays.
The latest [parts/visualization slice](MESH_PARTS_VISUALIZATION_EVIDENCE.md) adds
base/mesh/result parts, bounding boxes with an explicit tag-copy boundary,
visualization, graphs and shared topology helpers.
The [geometry/configuration slice](GEOMETRY_CONFIGURATION_EVIDENCE.md) adds geometry and compound parts and common meshing configuration, including typed selection copies and invariant mesher numeric output.
The [mesh region slice](MESH_REGION_EVIDENCE.md) adds surfaces, reference points, coordinate systems and mesh representation metadata.
The [Gmsh setup slice](GMSH_SETUP_EVIDENCE.md) adds the seven setup classes and eight enums without the native bridge.
The [Gmsh data/topology slice](GMSH_TOPOLOGY_EVIDENCE.md) adds in-memory data and topology helpers while retaining the legacy file writer only in the Windows build.
The [mesh helper extraction](MESH_HELPER_EVIDENCE.md) adds geometry-ID and point-transform helpers shared with FeMesh, geometry classifications and the remaining import/face-side enums.
The [FeMesh aggregate milestone](FEMESH_AGGREGATE_EVIDENCE.md) compiles the full shared aggregate source, extracts collection helpers, and validates mesh construction, renumbering and copied transforms.
The explicit source list follows the legacy project's compile entries.
`Elements/WireElement.cs` exists on disk but is excluded from the Windows project
and is obsolete; it is likewise excluded here.

This is a partial `CaeMesh` assembly for incremental development. It is not a
replacement binary for an existing Windows release and does not yet include
legacy aggregate `DeepCopy()`, mesh file import/export, or native mesh generation. Methods that
already throw `NotImplementedException` remain unimplemented; in particular,
quadratic pyramid triangular-face equivalent-force methods are not qualified.

The pre-existing removal of an unused WinForms import in `GeometryTools.cs`
was preserved. No element algorithms were changed for this slice.

## Validation

`CaeMesh.Characterization` passes **1,600 checks** on native Linux using SDK
10.0.401. It exercises:

- linear and quadratic tetrahedra, hexahedra, wedges and pyramids with straight
  edges and midside nodes at edge midpoints;
- independently specified analytic volume, centroid and total surface area;
- translation and scaling at two sizes, under English and Italian cultures;
- VTK cell identifiers and connectivity, including the wedge permutation;
- Gmsh node membership (not a native Gmsh integration or complete permutation test);
- face membership, outward normals, positive areas and finite face centroids;
- independent element copies and double-mirror connectivity restoration;
- linear triangular shell area, beam length/centroid, node rotation/translation.

The same run also passes 60 STL, 178 legacy evaluator, and 794 CaeGlobals checks,
for **2,632 assertions** across the four core executables. The additional
[mesh I/O harness](MESH_IO_EVIDENCE.md) passes 156 checks, bringing the suite to 2,784.
[Gmsh export evidence](GMSH_EXPORT_EVIDENCE.md) includes native reader acceptance. CI now triggers on CaeMesh
source changes and runs the mesh harness on Windows/Linux. Windows execution of
this new slice has not been performed locally.

Curved midside geometry, every 0D/1D/2D element behavior, equivalent-force
integration, degenerate/inverted elements and real imported models need further
coverage. Compilation of a class is not a claim that all its operations work.
Existing mesh warnings: CS0414 for `LinearPyramidElement.a` and CS0168 for unused exception variables in reference point and coordinate system equation checks.

## Remaining seams, in implementation order

| Area | Concrete dependency to resolve |
| --- | --- |
| Bounding boxes and parts | Base/mesh/result parts and null/integer-tag box copies are portable. Object-valued tags require an explicit copy policy; geometry/CAD part data and common meshing settings are now included. |
| Visualization data | Compiles with portable I/O, graphs and extracted topology helper; broader algorithm and historical-format qualification remains. |
| Mesh collections | Groups, node/element sets, surfaces, reference points and coordinate systems now compile. Full mesh ownership and additional collection extensions remain to migrate. |
| Meshing setup/data | Setup classes and enums now compile without GmshCommon. GmshData and topology helpers compile; the legacy file writer and native API remain outside the portable build. |
| `FeMesh` | Now compiles with typed selection copies and a portable warning event/trace fallback. Full aggregate DeepCopy remains Windows-only; selected-part copies power transforms. Broader ownership and workflow tests remain. |
| Native meshers and import/export | Follow with cross-platform Gmsh interop and real model fixtures. A node-order method named `GetGmshNodeIds` does not exercise a mesher. |

Do not enable BinaryFormatter or substitute empty mesh/mesher implementations to
get a broader build. Next: broaden aggregate workflow coverage, then portable mesh file I/O and native process integration.

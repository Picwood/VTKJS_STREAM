# Geometry parts and meshing configuration

Validated on Linux with .NET SDK 10.0.401 on 2026-09-11 using
`bash scripts/test-portable.sh`: 60 STL, 178 legacy NCalc oracle, 794 CaeGlobals,
and 1,166 CaeMesh checks passed (2,198 total). Windows CI is configured but was
not executed locally.

The portable mesh project now links 62 production files. This slice adds
GeometryPart, CompoundGeometryPart, MeshSetupItem, MeshingParameters,
RemeshingParameters, LocalMeshSize, ThickenShellMesh, SplitPartMeshData and the
legacy FeMeshRefinement data type.

Geometry meshing settings and mesh setup selection history now use explicit
copies instead of serialization-based cloning. Remeshing copies retain region
metadata and runtime type. Compound copies retain their subtype and inherited
geometry state and independently copy source/subpart name arrays. BasePart's
copy constructor now uses FeGroup's copy constructor so inherited flags survive.

New regression checks run in both en-US and it-IT. They cover inherited flags,
compound/remeshing type preservation, mutation isolation of settings, selection
history and diagnostic arrays, compressed CAD text and a file round trip in a
Unicode path containing spaces, local size rounding, and absolute/relative mesh
configuration output. The configuration test reproduced decimal commas before
the writer was changed to invariant numeric formatting. Basic mode still uses
absolute sizes even if relative sizing is stored.

CAD coverage is payload storage, not an OpenCascade parser or geometry operation.
Configuration output is checked as text; no native mesher was executed. The
newly included shell-thickening, split data and legacy refinement types compile
but do not have dedicated behavioral coverage in this slice. Full FeMesh,
mesh import/export, native meshing, arbitrary bounding-box tag graphs and legacy
project persistence remain outside this qualification. Existing warnings and
expected legacy evaluator parse diagnostics remain unchanged.

Next: inventory the remaining FeMesh aggregate dependencies and extract the
smallest coherent collection/meshing boundary without adding desktop or native
bridge references to the portable graph.

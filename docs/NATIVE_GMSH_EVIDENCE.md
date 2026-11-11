# Native Gmsh generation and portable volume/boundary round trips

Validated on Linux through 2026-10-27 with Gmsh CLI/Python API 4.14.0-git and .NET
SDK 10.0.401. This is an optional native integration smoke test, separate from
the 5,141-check default portable suite.

## Run

Install a working Gmsh CLI, Python Gmsh module and .NET 10 SDK, then run:

```bash
python3 scripts/test-native-gmsh.py
```

PREPOMAX_GMSH overrides CLI discovery; PREPOMAX_DOTNET overrides SDK discovery.
Otherwise PATH is used, with the same user-local SDK fallback as the portable
shell script. The Python module must be importable by the selected Python.
No packages are downloaded by this script. It reports both CLI and Python Gmsh
versions so differences are visible.

The script uses a temporary directory with spaces and Unicode and removes it
on completion or failure. Processes receive argument arrays, a fixed working
directory and timeouts (120 seconds for Gmsh, 180 for .NET). Failures return
nonzero and report subprocess output. Gmsh generation now runs through the
portable CaeJob.Native runner. The application also owns a cancellable
STEP-to-MSH2-to-PMA job used by CLI and GTK; distribution discovery and
packaging are covered by the Debian workflow.

## Workflow and evidence

1. Gmsh CLI meshes an OpenCASCADE unit cube with linear tetrahedra and one thread.
2. The native reader loads the output. An independent determinant calculation
   checks every tetrahedron has positive volume and their total is 1.
3. Gmsh exports Medit `.mesh` volume data and the cube boundary to STL.
   The production MmgFileReader.Read3D imports the volume into FeMesh and the
   production MSH writer exports it in both locales. Native re-reading checks
   positive tetra orientations and unit total volume.
4. The portable MeshIO executable imports it through the production STL reader
   into FeMesh and validates a connected triangle boundary, positive triangle
   areas, total area 6, cube bounds, and nodes lying on the boundary.
5. The production Gmsh writer exports that surface in en-US and it-IT.
6. Native Gmsh reads both exports; independent triangle cross products verify
   their total area is 6.
7. The application STEP workflow reconstructs six `CAD_FACE_*` PMA surfaces.
   A separate GEO fixture verifies that `BODY` and six physical surface names
   survive Gmsh, PMA publication and reopen.
8. PMA inspection verifies the retained STEP filename, byte length and SHA-256;
   CLI extraction reproduces the source byte for byte. The GTK X11 acceptance
   requires the retained source identity after reopen.
9. The CLI remeshes the retained STEP with explicit min/max, Delaunay and
   optimization controls. The fresh PMA retains the identical source metadata,
   one elastic material and six regenerated CAD-face regions.
10. A second CLI remesh supplies no sizing arguments and verifies that the PMA's
    stored global Gmsh setup drives the job and survives the next archive.

Observed: **387 positive tetrahedra**, total volume **1**; the imported/exported
boundary has **134 nodes and 264 triangles**, total area **6** in both locales.
Exact mesh counts are diagnostic only; future Gmsh versions may mesh differently.
The assertions depend on geometry and element validity rather than these counts.

The **5,141-check** default portable suite also passes. Native execution is opt-in and
is not added to the Windows/Linux CI matrix, which does not provision Gmsh.

## Limits and next step

Both the generated volume and boundary now enter FeMesh. The Medit reader and
ElementsToImport enum bring portable CaeModel to five shared source files.
Its unused CaeResults import was removed. Decimal coordinate parsing now uses
InvariantCulture; the native fixture previously failed under Italian locale.
The shared Tools file-read/unlock methods were extracted unchanged into
Tools.FileIO.cs and included in both builds, preserving the existing timeout and
file-sharing behavior. Cross-process lock semantics are not qualified here.

The default suite retains assertions for an independent decimal-coordinate
Medit tetra fixture and missing-file behavior. It now passes **5,141 checks**.
Observed volume import/export: **143 nodes, 387 tetrahedra**, volume **1** in both
locales. Native re-reading validates positive orientations and volume as well.
Netgen VOL import was investigated but not included: this Gmsh installation
cannot write that format.

The bounded application workflow is documented in
[Linux CAD-to-mesh workflow](LINUX_GMSH_WORKFLOW.md). Generated CAD-face regions
and GEO physical names now survive as editable mesh regions. The original source
bytes persist in PMA v5, can be inspected or extracted, and can drive a fresh
bounded remesh through CLI and GTK. Global Gmsh controls now persist as bounded
native setup objects. There is still no parsed editable CAD/B-rep topology,
local/selection GmshData setup handoff or native C
API bridge. Geometry repair/partition, Medit surface remeshing, mixed-material
boundaries, quadratic conversion, malformed CAD inputs and broader CAD models
require further coverage.
The Python API remains an independent smoke-test reader, not an application
runtime dependency. Windows execution was not performed locally.

Next: parse the retained CAD source into persistent named topology and expose
repair/partition plus local sizing and setup entities.

# Linux CAD-to-mesh workflow

Validated: 2026-10-28.

The native GTK application can create a new editable PMA project directly from
a STEP, STP, IGES, IGS, BREP or Gmsh GEO solid. **Mesh CAD…** collects Young's
modulus, Poisson's ratio and bounded Gmsh controls, runs the distribution
`gmsh` executable as a cancellable child job, imports its linear or straight-sided quadratic ASCII MSH
2.2 output, reconstructs CAD-face regions, saves the exact input geometry in the
new PMA and opens the generated project.

The portable command-line boundary exposes the same workflow:

```sh
prepomax-linux-cli mesh-cad solid.step solid-mesh.pma 210000 0.3 2.5
prepomax-linux-cli remesh-cad solid-mesh.pma solid-finer.pma 1.25 \
  --min 0.2 --algorithm delaunay --optimize --second-order
# Reuse the controls stored by that mesh/remesh:
prepomax-linux-cli remesh-cad solid-finer.pma solid-copy.pma
```

Both commands accept `--min SIZE`, `--algorithm auto|delaunay|frontal|hxt` and
`--optimize`, `--second-order` and the bounded
`--size-box SIZE XMIN YMIN ZMIN XMAX YMAX ZMAX` local refinement control. An optional executable can follow the maximum size for backward
compatibility, while `--gmsh EXECUTABLE` is unambiguous. GTK exposes the same
minimum/maximum, algorithm, optimization, quadratic-element and local box controls. It accepts `--gmsh` or
`PREPOMAX_GMSH`; otherwise both paths discover `gmsh` on `PATH`. The Debian
package recommends the distribution `gmsh` package and
`prepomax-linux-viewer --check-gmsh` validates discovery and startup.

Every generated CAD project stores its global controls as a bounded native
`MeshingParameters` and `TetrahedralGmsh` pair in PMA metadata v27/v28
(first order), v29/v30 (quadratic), or v31/v32 when a local box is present.
`inspect` exposes those values, GTK prefills them for **Remesh CAD…**, and the
two-argument CLI form above reuses them. See [local mesh sizing](LINUX_LOCAL_SIZING.md) for the optional persistent box.
Retained STEP/IGES/BREP remeshing also accepts
`--size-face TAG SIZE TRANSITION_DISTANCE`; see [CAD face refinement](LINUX_CAD_FACE_SIZING.md)
for its source/topology identity guard and GTK controls.
Projects created before this schema
remain readable; a remesh without explicit controls requires a stored pair.

## Process and publication boundary

The application invokes Gmsh with an argument array, private temporary working
directory, five-minute deadline, bounded output capture, linear or straight-sided quadratic elements,
one native thread and ASCII MSH 2.2 output. Shell interpretation is never used. The
native job supports cancellation and kills its process tree through the shared
`CaeJob.Native` runner.

The destination must be a new `.pma` file in an existing directory. Gmsh output
is accepted only after the existing mesh importer validates finite coordinates,
positive tetrahedron corner volumes and quadratic edge midpoints, record limits and the requested material
values. The importer writes a hidden staging file beside the destination, checks
cancellation again, and atomically moves it into place as the commit point.
Failure, timeout or cancellation does not publish a partial project, and both
temporary meshing files and the staged PMA are removed.

The source is read into an owned snapshot before Gmsh starts and is retained as a
checksummed `geometry.source` payload. New CAD projects use PMA v6 to retain a
[source-bound topology graph](LINUX_CAD_TOPOLOGY.md) alongside it; older PMA-v5
source-only projects remain readable. The source retains its original base filename.
Every supported project edit and solved result publication preserves this
payload. `prepomax-linux-cli inspect` reports its name, length and SHA-256;
`extract-geometry PROJECT.pma OUTPUT.step` and GTK's **Export CAD…** restore the
exact bytes to a new file. The source is limited to 128 MiB. The generated MSH mesh retains the existing
32 MiB and 100,000-node/volume-element import bounds. Material units and maximum element
size use the source geometry's coordinate system; the application performs no
unit conversion.

**Remesh CAD…** and `remesh-cad` read the owned PMA-v5/v6 source payload rather
than an external file. Remeshing requires exactly one constant elastic material
assigned across the solid sections. It creates a fresh project with those
elastic values, regenerated volume parts and face regions, and the exact source
payload. Supports, loads, steps beyond the default output step, results and
other model edits are reset because node, element and face identities can change.
The source project is never modified.

## Quadratic boundary

`--second-order` and GTK's **Quadratic tetrahedra (straight-sided)** checkbox
request ten-node tetrahedra using Gmsh's `Mesh.SecondOrderLinear=1`. Midside nodes
stay at edge midpoints, including on curved CAD boundaries. The importer converts
Gmsh ordering to the production CalculiX/VTK ordering, checks every midpoint,
and matches all six surface nodes to their owning solid faces. Curved quadratic
MSH elements fail before PMA publication. This bounded path retains positive
affine Jacobians without claiming curved-element quality validation. It exports
C3D10 and uses the existing quadratic renderer, pressure and archive paths.

## Acceptance

The real native acceptance generates an OpenCASCADE unit cube, exports STEP,
runs the application `mesh-cad` service through distribution Gmsh 4.14.0 and
opens the resulting PMA. It records 334 nodes and 1,093 positive tetrahedra with
one elastic material. Counts are diagnostic because Gmsh versions may produce a
different valid mesh.

The same acceptance then invokes `remesh-cad` on that PMA with explicit minimum
and maximum sizes, Delaunay selection and optimization. The remeshed archive has
positive nodes/elements, six CAD faces and the identical retained-source
filename, size and SHA-256. HXT also completed locally; algorithm availability
can depend on how a distribution builds Gmsh, and a missing backend fails the
job without publishing a destination. It then remeshes that output again with
no control arguments and verifies that its stored values and exact source
identity are unchanged.

The GTK X11 and Wayland smokes drive the actual STEP chooser, parameter dialog, save chooser,
background job polling, PMA reopen and VTK.js rendering. Its generated scene has
one actor, 272 surface points and 540 triangles, then the companion and renderer
listener close cleanly. A second GTK X11 smoke drives **Remesh CAD…**, its
bounded controls and save chooser, then verifies material transfer, six surfaces,
PMA reopen and the rendered scene. The GTK smoke also requires the retained source identity.
The portable suite passes 5,301 checks, including setup round-trip, bounded
schema rejection, input validation and a pre-cancelled application job that launches no tool or leaves a
staged project. A native
process test cancels a running mesher, verifies its child process is gone within
the deadline and confirms that no PMA was published. The clean Ubuntu 24.04
package test installs distribution Gmsh and repeats STEP-to-PMA generation,
explicit retained-CAD remeshing and stored-control remeshing through the
installed private-runtime CLI.

## Remaining geometry and meshing scope

Each Gmsh surface entity becomes an editable PMA element surface named
`CAD_FACE_<tag>`. Gmsh GEO physical surface and volume names replace those
generated names and persist as surfaces, parts and element sets. PMA v6 now stores the exact source CAD/GEO bytes and a parsed entity/boundary
graph. It does not yet provide editable B-rep geometry. Topology naming across remeshes, geometry
repair/partition, edge sizing, multiple face refinements and part/selection setup entities, curved quadratic elements and
MMG/Netgen/OpenFOAM remain part of the full geometry and meshing gate.

## Quadratic acceptance (2026-10-28)

`bash scripts/test-portable.sh` passes 5,159 checks, including exact midside
ordering, six-node face matching, C3D10 export, renderer upload, versioned setup
round-trip and rejection of curved/inverted/mismatched quadratic records.
`/usr/bin/python3 scripts/test-native-gmsh.py` passes real STEP quadratic remesh,
stored-control reuse and production C3D10 export with Gmsh 4.14.0.
`/usr/bin/python3 scripts/test-native-calculix.py` passes the existing solver
cases plus a Gmsh-generated quadratic cube with pressure on its named XMAX face:
all 2,091 node displacements match u=(x/1000,0,0) within 1e-8 for E=1000, nu=0
and unit axial traction using CalculiX 2.23. Actual results reopen from PMA.

GTK X11 functional acceptance passes import and remesh with
`--smoke-second-order`, rendering 1,082 points and 2,160 triangles and closing
the renderer listener. These runs use Xvfb, `LIBGL_ALWAYS_SOFTWARE=1` and
`WEBKIT_DISABLE_DMABUF_RENDERER=1`; they do not establish hardware GPU or Wayland
acceptance for the new path. The clean Ubuntu harness now passes quadratic remeshing with a local box, saved
order/control reuse and C3D10 export; see [local sizing](LINUX_LOCAL_SIZING.md).

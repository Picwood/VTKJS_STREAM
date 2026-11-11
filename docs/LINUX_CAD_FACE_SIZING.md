# Linux CAD face refinement

Validated: 2026-10-28.

**Remesh CAD…** now offers one **Refine near a retained CAD face** control for
STEP, STP, IGES, IGS and BREP projects with a captured topology graph. Select a
face from its source-specific tag and bounds, enter a local element size and a
positive transition distance. The saved controls prefill the next remesh dialog.
Disabling the control removes it from the new project. One coordinate refinement
box can be used at the same time; the smaller local target takes precedence.

The CLI provides the same operation:

```sh
prepomax-linux-cli cad-topology source.pma
prepomax-linux-cli remesh-cad source.pma refined.pma 0.45 \
  --size-face 1 0.12 0.4 --second-order
# Reuse global, box and face controls from the saved project:
prepomax-linux-cli remesh-cad refined.pma another.pma
```

`--size-face TAG SIZE TRANSITION_DISTANCE` requires an existing captured CAD
face, a finite positive size within global min/max limits, and a finite positive
transition distance. The tag must come from that project's topology inventory;
the example's tag 1 is not a universal face identity. Raw `mesh-cad` inputs have
no captured topology to select from: mesh them once, then refine through
`remesh-cad`. Legacy source-only projects likewise need one topology-capturing
remesh first. GEO sources are excluded from this bounded control because Gmsh's
surface-distance field does not support all geometry kernels. Their existing
meshing and coordinate-box workflows remain available.

## Identity and publication

The shared `CaeMesh.CadFaceMeshSize` setup item stores tag, size and transition
alongside SHA-256 bindings to the exact source and complete captured topology.
Metadata v33/v34 introduces this item; projects without it retain their previous
metadata versions. The geometry payload remains PMA archive v6. Full editable
archive opening and saving validate the setup against the retained source,
selected dimension-two entity and topology fingerprint. Model edits and solved
result archives preserve this binding. Rehashing modified model metadata does
not bypass the cross-payload identity check.

The mesher uses a
[Gmsh Distance field on the selected surface, followed by a Threshold field](https://gmsh.info/doc/texinfo/gmsh.html#Specifying-mesh-element-sizes).
Surface-distance sampling is fixed at 50 samples per parametric dimension. The
target starts at the local size near the surface and grows to the global maximum
at the transition distance. A Min field combines this with the optional box.
As with box refinement, explicit local controls replace source sizing fields,
disable point/curvature contributions and boundary extension, apply the requested
global limits, and reset source size scaling to one. Targets describe Gmsh's
sizing field, not strict tetrahedron edge-length guarantees.

After Gmsh runs, the application compares the fresh source-bound topology
fingerprint with the selected project's snapshot before importing/publishing the
new PMA. A changed graph or bounds fails closed; no project is published and
temporary/staging files are removed. To work with changed topology, create a
fresh CAD project and select the desired face again. The original project stays
unchanged. Remeshing still resets analysis edits and results.

This is a source/snapshot-specific selection contract. It does not solve general
stable geometric naming across CAD edits or kernel changes. A matching inventory
also does not certify identical analytic geometry across different kernels.
Editable CAD, repair/partition, edge sizing, multiple face refinements and
viewport-based CAD-face picking remain open.

## Acceptance

- `bash scripts/test-portable.sh`: **5,301 checks**, including independent setup
  copying, metadata v33/v34, archive/source/topology binding, model-edit
  preservation, global-limit validation, old-schema rejection, duplicate guards,
  unsupported-source refusal and pre-cancelled stored-control remeshing.
- Native Gmsh 4.14.0 refines the selected planar STEP face. Mean maximum corner
  edge lengths are 0.2270 near the face and 0.4906 far away. C3D10 export and
  stored-control remeshing pass. A native wrapper that alters the topology
  snapshot causes refusal before project publication.
- Native CalculiX 2.23 solves the STEP-derived C3D10 cube with both box and face
  refinement. All **1,291** nodal displacements match u=(x/1000,0,0) within 1e-8
  for E=1000, nu=0 and unit axial pressure. The result PMA retains the exact graph
  and face controls.
- GTK X11 functional acceptance passes face selection, combined box/face
  remeshing and a second remesh that checks saved-control prefill. The output
  renders 730 points and 1,456 triangles and closes the renderer listener. These
  runs use Xvfb, software WebGL and `WEBKIT_DISABLE_DMABUF_RENDERER=1`; they do
  not establish physical-GPU or Wayland qualification.

- Clean Ubuntu 24.04 installed-package acceptance passes with Gmsh 4.12.1.
  The harness derives the face tag from the retained STEP graph, combines face,
  box and quadratic controls, reuses the saved setup and exports C3D10. Upgrade
  from `0.1.1~preview+facesize.1` to `.2` preserves/reopens the exact XDG-stored
  PMA. Runtime/tool revalidation and purge pass, preserving user data. Distribution
  CalculiX 2.21 separately passes the existing analytical C3D8 solve.

Artifact hashes and command summaries are recorded in
[the acceptance evidence](evidence/linux-cad-face-sizing-2026-10-28.txt).

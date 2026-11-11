# Linux local mesh sizing

Validated: 2026-10-28.

The GTK CAD import and remesh dialogs now support one coordinate-defined local
refinement box. Enable **Refine inside a coordinate box**, expand its controls,
and enter an element size plus minimum/maximum X, Y and Z in CAD coordinate
units. **Remesh CAD…** prefills the box saved in the project. Disabling it removes
the box from the newly generated project. The source project is unchanged.

The CLI uses the same application service:

```sh
prepomax-linux-cli mesh-cad solid.step first.pma 1000 0.25 0.45 \
  --size-box 0.16 -0.01 -0.01 -0.01 0.35 1.01 1.01
prepomax-linux-cli remesh-cad first.pma quadratic.pma 0.45 \
  --min 0.12 --second-order \
  --size-box 0.16 -0.01 -0.01 -0.01 0.35 1.01 1.01
prepomax-linux-cli remesh-cad quadratic.pma same-controls.pma
```

`--size-box` takes `SIZE XMIN YMIN ZMIN XMAX YMAX ZMAX`. All numbers must be finite,
each minimum must be less than its maximum, and the size must be positive and
within the global min/max size limits. A box outside the model can have no effect.
Meshing still obeys the existing five-minute deadline and 32 MiB/100,000-node and
volume-element import bounds. This supports linear and straight-sided quadratic
tetrahedra. It does not change the support/load reset behavior of remeshing.

## Persistence and mesher boundary

The shared `CaeMesh.BoxMeshSize` setup item stores the seven numeric values
alongside the existing global sizing and tetrahedral setup pair. PMA inline
metadata v31 and external-mesh metadata v32 introduce this item; earlier versions
remain readable and reject a box mislabeled as an older schema. Projects without
a box retain their previous metadata versions. Invalid bounds, non-finite values,
sizes outside the global limits, inactive/non-default setup shapes, duplicate
boxes and incomplete global setup pairs fail closed. Existing model edits and
solved-result publication preserve both the setup and exact source geometry.
`inspect` exposes the optional `MeshingSetup.SizeBox` object.

For local sizing, the job writes a private GEO driver using a fixed source
basename and validated invariant numbers. CAD is merged through OpenCASCADE;
GEO source is included using its own geometry factory. A fresh `newf` identifier
avoids collisions with fields in the source. The driver's background
[Box field](https://gmsh.info/doc/texinfo/gmsh.html#Specifying-mesh-element-sizes)
uses the local size inside the bounds and the global maximum outside. When this
explicit local control is present, it replaces the source background field,
disables point/curvature size contributions and boundary-size extension, applies
the requested global limits, and resets the source size scale to one. Otherwise
source sizing could suppress the requested refinement. No-box jobs retain the
existing meshing path. Gmsh size targets are not strict bounds on tetrahedron
edge lengths. The retained source bytes are never rewritten.

The box is independent of CAD entity labels. It provides a bounded local sizing
workflow alongside [one source-bound CAD face refinement](LINUX_CAD_FACE_SIZING.md).
Editable B-rep geometry, stable face naming, edge/multiple-face sizing, graphical
box editing and repair/partition remain open.

## Acceptance

- `bash scripts/test-portable.sh`: 5,193 checks, including shared box copying,
  inline/archive persistence, editing preservation, stored remesh preparation,
  schema-version guards, duplicate rejection and invalid numeric/bound checks.
- `/usr/bin/python3 scripts/test-native-gmsh.py`: Gmsh 4.14.0 generates smaller
  tetrahedra in the box and remeshes using the persisted settings. Mean maximum
  corner-edge lengths are 0.1868 near the requested region and 0.4557 far away.
  A GEO source with an existing field, conflicting minimum size and scale still
  preserves physical names and applies the requested refinement.
- `/usr/bin/python3 scripts/test-native-calculix.py`: the locally refined C3D10
  cube with unit axial pressure matches u=(x/1000,0,0) at all 1,301 nodes within
  1e-8 for E=1000 and nu=0 using CalculiX 2.23; production result PMA reopens.
- GTK X11 functional smokes pass with `--smoke-size-box --smoke-second-order`,
  including saved-control prefill. Import renders 782 points/1,560 triangles;
  remesh renders 734 points/1,464 triangles, and both close the renderer listener.
  These runs use Xvfb, `LIBGL_ALWAYS_SOFTWARE=1` and
  `WEBKIT_DISABLE_DMABUF_RENDERER=1`; physical-GPU and Wayland qualification
  remain separate gates.

- Clean Ubuntu 24.04 installed-package acceptance passes with distribution Gmsh
  4.12.1 and CalculiX 2.21. It generates a STEP project, creates a locally refined
  quadratic remesh, reuses the saved controls, and exports C3D10 input. An in-place
  upgrade from `0.1.0~preview+localbox.1` to `.2` preserves the exact XDG-stored
  PMA and reopens it with identical inspection data. Private-runtime/tool checks
  pass again; purge removes application integration while preserving user data.
  The distribution solver separately passes the existing analytical C3D8 solve.

Artifact hashes and command summaries are recorded in
[the acceptance evidence](evidence/linux-local-sizing-2026-10-28.txt).

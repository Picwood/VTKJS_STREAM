# Retained Linux CAD topology

Validated: 2026-10-28.

CAD import and remeshing now retain a source-bound inventory of geometric points,
curves, faces and volumes. Each entity has its dimension/tag, finite bounding box,
physical-group memberships and signed boundary references. This is a parsed
boundary graph; analytic curves, trimmed surfaces and other kernel data remain
in the exact retained CAD source. It does not yet provide editable B-rep geometry.

GTK's **Inspect CAD…** opens a native entity table with counts, physical names,
oriented boundaries and bounds. It previews up to 500 entities. The CLI exposes
the full inventory, including unnamed physical groups:

```sh
prepomax-linux-cli inspect project.pma
prepomax-linux-cli cad-topology project.pma > topology.json
```

`inspect` adds a `CadTopology` summary when the project contains an inventory.
Old projects without topology remain readable; **Inspect CAD…** is disabled and
CLI topology inspection explains that a retained-source remesh can capture it:

```sh
prepomax-linux-cli remesh-cad old-project.pma with-topology.pma
```

As before, the destination is a fresh project and analysis edits/results are
reset by remeshing. The exact source bytes and stored meshing controls survive.
Normal model edits and solve/result publication preserve the captured graph.

## Capture and persistence

The existing distribution Gmsh process now reads a private GEO driver. It includes
GEO using its selected geometry factory or merges CAD through OpenCASCADE, then
saves a geometry-only ASCII MSH 4.1 snapshot before generating the normal MSH 2.2
volume mesh. This uses Gmsh's existing
[entity/boundary representation and Save operation](https://gmsh.info/doc/texinfo/gmsh.html).
No additional library, Python Gmsh binding or native process is required by the
application. The wrapper uses fixed private filenames and restores MSH 2.2 after
the snapshot; mesh output uses the unscaled CAD coordinate system.

`CadTopologyCodec` parses the geometric entity records, physical names, and
oriented boundary graph. It validates dimensions, positive unique entity/group
tags, finite ordered bounds, physical memberships and every boundary reference.
Repeated signed boundary tags are allowed because seams can use an entity more
than once. This is structural validation, not manifoldness, geometric repair or
solver-quality certification. The volume importer also verifies that mesh entity
and physical tags agree with this same snapshot before publishing the project.

PMA archive v6 adds the checksummed `geometry.topology.json` payload alongside
`geometry.source`. The topology document's own version is 1 and contains the
SHA-256 of the exact retained source. Full archive opening validates both payload
checksums and this source binding. Changing the topology's source hash and
recomputing its payload checksum still fails. Earlier PMA versions remain readable;
archives without topology continue to use their earlier version. Editable model
metadata versions are unchanged by this separate geometry payload.

The snapshot and JSON each have a 4 MiB bound, with at most 20,000 entities,
2,000 physical groups and 200,000 boundary references. Unsupported snapshot
sections, premeshed GEO snapshots, invalid references, missing topology output
or oversized records fail before project publication. The usual cancellable
Gmsh job, temporary-file cleanup and atomic new-PMA publication remain in place.

## Identity boundary

Tags identify entities in one captured source/model. Native acceptance reproduces
the complete cube graph across linear/quadratic and different-density remeshes
using the same source and Gmsh version. That is not a general stable-naming
contract across geometry edits, CAD healing or Gmsh/OpenCASCADE version changes.
Loads/supports are not transferred to a remesh based on these tags. One [source-bound CAD face refinement](LINUX_CAD_FACE_SIZING.md) now uses this
inventory with a fail-closed fingerprint guard. Editable CAD operations, stable
geometric naming, edge/multiple-face setup, repair and partition
remain separate gates.

## Acceptance

- `bash scripts/test-portable.sh`: 5,251 checks. Coverage includes oriented
  graph parsing, Unicode physical names, numeric/reference/count guards, JSON
  round-trips, PMA v6 checksums/source binding, v5 compatibility and model-edit
  preservation.
- Native Gmsh 4.14.0 captures an 8-point/12-curve/6-face/1-volume STEP cube and
  reproduces its complete graph across remeshing. GEO physical names persist.
  A sphere validates seam references; fragmented adjacent volumes retain their
  shared face with opposite volume orientations.
- Native CalculiX 2.23 still matches the analytical displacement at all 1,301
  nodes of the locally refined quadratic pressure cube. Its result archive
  retains the identical CAD topology.
- A native retained-source PMA v5 remesh produces PMA v6 with the same exact CAD
  identity and controls, adding the captured graph.
- GTK X11 software-WebGL acceptance passes import, remesh and the native
  **Inspect CAD…** dialog (`--smoke-cad-topology`), including renderer shutdown.
  Physical-GPU and Wayland qualification remain separate.

- Clean Ubuntu 24.04 installed-package acceptance passes using distribution Gmsh
  4.12.1: STEP topology capture, source binding, archive v6 layout, saved-control
  remeshing, CLI full inventory and C3D10 export. The package upgrade from
  `0.1.0~preview+topology.1` to `.2` preserves/reopens the exact XDG-stored PMA;
  runtime/tool checks and purge pass while user data remains unchanged.

Artifact hashes and command summaries are recorded in
[the acceptance evidence](evidence/linux-cad-topology-2026-10-28.txt).

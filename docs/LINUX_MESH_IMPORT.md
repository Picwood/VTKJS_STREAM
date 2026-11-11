# Linux mesh import: INP first

Validated: 2026-10-21.

**Import mesh…** in GTK defaults to CalculiX/Abaqus `.inp` files. ASCII Medit
`.mesh` remains a secondary chooser filter, and bounded ASCII Gmsh MSH 2.2
`.msh` is available for region-preserving import. All create a new editable `.pma`
project. Enter Young’s modulus and Poisson’s ratio explicitly, using units
consistent with the mesh coordinates. No unit conversion is performed.

For CAD solids, **Mesh CAD…** now runs distribution Gmsh and opens its generated
tetrahedral PMA; see [Linux CAD-to-mesh workflow](LINUX_GMSH_WORKFLOW.md).

```sh
scripts/start-gtk-viewer.sh
# Or import directly:
dotnet portable/PrePoMax.Cli/bin/Release/net10.0/PrePoMax.Cli.dll \
  import-mesh portable/tests/fixtures/import/tetra.inp /path/to/new-project.pma 1000 0
```

Build current artifacts with `scripts/build-linux-viewer.sh`. The destination
must be new. Successful GTK import opens the new project; failures leave the
current project or empty startup state intact. Source files stay unchanged.

## INP mesh scope

The bounded mesh-only reader accepts case-insensitive keywords and options:

- `*HEADING` and `**` comments.
- `*NODE`, optionally with an implicit NSET; finite coordinates, including
  Fortran D exponents. Missing Y/Z coordinates default to zero.
- `*ELEMENT, TYPE=C3D4`, `C3D8` or `C3D8R`, optionally with an implicit ELSET.
  Connectivity may continue over multiple lines. Sparse positive node and
  element IDs and reduced-integration formulation are preserved.
- `*NSET` and `*ELSET` with numeric members, named-set references or `GENERATE` ranges.
  Repeated set declarations merge case-insensitively. Named sets survive
  project save/reopen. Forward references resolve after reading the mesh;
  missing or cyclic dependencies are rejected.
- `*SURFACE, NAME=..., TYPE=ELEMENT` with `S1` through `S6` face labels.
  Rows may reference an element set or a positive element ID. Repeated sources
  for one surface face are deduplicated into an internal element set. Unknown
  or empty sets and faces that do not exist on the referenced element topology
  are rejected.
- `*INCLUDE, INPUT=filename`, including nested relative paths and quoted names
  with spaces. Windows path separators are accepted on Linux. Includes may
  contain complete sections or data fragments.

Unknown keywords/options, full analysis definitions, parts/assemblies,
transforms, unsupported element types, malformed connectivity, invalid set
members and degenerate volume elements are rejected before publication. This
is not the complete original PrePoMax INP model importer: it does not silently
strip materials or analysis commands from full decks. Export a mesh-only INP
for this path. Mesh import currently supports at most 100,000 nodes/elements
and 32 MiB of combined input, at most 1,000 surfaces, 100,000 surface rows,
64 file expansions and 16 include levels. Named-set dependency depth is limited to 64.

The application builds volume parts grouped by supported element formulation,
assigns the supplied elastic material, and creates one static step with field
and nodal-history output. Named element sets remain available as sets rather
than being treated as material assignments. Imported element surfaces are
available immediately in **Add force / traction…** as total-force traction
targets. A generated all-node history set
uses a distinct name if ALLNODES already exists. No supports or loads are
invented; use the GTK authoring tools before solving.

## Medit scope

ASCII MeshVersionFormatted 1/2 and Dimension 3 are supported, with Vertices,
Tetrahedra, optional Edges/Triangles, and End. Input is validated then passed
to the existing MmgFileReader. Only solid elements remain in the analysis mesh.
The Medit path retains its 100,000-records-per-section limit and rejects
unsupported/repeated/truncated sections and nonfinite or degenerate data.
Binary Medit, higher-order cells and hexahedral Medit input remain unsupported.

## Gmsh MSH 2.2 scope

ASCII MSH 2.2 accepts bounded point, line, triangle and linear tetrahedron
records. Physical volume names become part names and element sets. Physical
surface names become editable solid element surfaces; unnamed geometrical
entities receive stable `CAD_VOLUME_<tag>` and `CAD_FACE_<tag>` names. Every
triangle must resolve to one or two tetrahedron faces. Binary/newer MSH layouts,
higher-order or mixed volume cells, undefined physical tags, unknown nodes,
inverted tetrahedra and unmatched surface triangles fail before PMA publication.

## Project files: PMA versus PMX

`.pma` is the Linux port’s versioned editable archive. Inline metadata v27 supports
linear tetrahedra/hexahedra and quadratic tetrahedra/wedges/hexahedra, including
their solver formulations, plus elastic, density, plastic and thermal material
properties, numeric reference points, tabular amplitudes, part-derived element
sets, auxiliary cylinders, surface interactions, contact pairs, ties, rigid
bodies, surface springs, contact field-output requests, bounded CalculiX user keywords,
coordinate-defined 3D coordinate systems, finite constant pressure on named
element surfaces and bounded global Gmsh setup. Newly saved archive-version-3 projects use metadata v28 and place
numeric mesh arrays in a checksummed `model.mesh` payload. Metadata versions
1–27 and archive versions 1–2 remain readable; older portable builds
cannot read the new archive layout.

The GTK **Open project…** dialog accepts original PrePoMax `.pmx` files through
a standalone fail-closed converter. It migrates the supported analysis model from
the supplied unprotected v2.5.1 dev PMX into PMA; see
[PMX conversion](LINUX_PMX_CONVERSION.md). The converter also retains a
validated legacy binary result stream in PMA v4,
but it does not migrate UI/controller state; protected or older layouts remain
unsupported. Changing a filename extension does not convert it. Exporting a
mesh-only `.inp` remains
the simpler path when only supported mesh and sets are needed.

## Verification

The portable suite passes **5,141 checks**, including **2,126 mesh I/O checks**.
INP checks cover sparse IDs,
implicit/explicit/generated sets, repeated case-insensitive names, ALLNODES
collision avoidance, D exponents, continued connectivity, C3D8/C3D8R
formulations, named/direct/multi-source element surfaces, invalid topology
faces, malformed/unsupported input and overwrite refusal. Existing
Medit, tetrahedral/hexahedral persistence and scene tests remain passing.

GTK X11 imports the INP fixture through the default chooser, preserves explicit
material values, renders one actor/four nodes/four triangles and closes its
renderer. The Medit chooser remains usable. The native acceptance helper uses
the imported LEFT/RIGHT sets and IDs 10/20/30/40 to add supports and a force:

```sh
dotnet run --project portable/tests/MeshIO.Characterization -c Release -- \
  --prepare-imported-inp /path/to/new-project.pma /path/to/ready-project.pma
dotnet portable/PrePoMax.Cli/bin/Release/net10.0/PrePoMax.Cli.dll \
  solve /path/to/ready-project.pma /path/to/jobs /path/to/ccx
```

For the fixture with E=1000, nu=0, unit axis coordinates, fixed transverse
translations and force 1 on node 20, CalculiX 2.23 gives node-20 U1=0.006;
other displacements are zero. The result archive displays DISP/U1 contours
and scaled deformation in GTK. This helper is fixture-specific.

```sh
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh \
  --smoke-import-mesh portable/tests/fixtures/import/tetra.inp \
  --smoke-import-output /path/to/new-import.pma --smoke-timeout 40
```

The UI smoke first rejects NaN, then imports with E=1000 and nu=0.25. Rebuild
old viewer bundles for INP support. These are bounded X11/software-rendering
checks, not general INP/PMX compatibility or clean-machine qualification.

Nested includes and named-set references are also covered by the portable
suite in both en-US and it-IT, including forward references, deduplication,
missing files, invalid options and cycles. The GTK include fixture is
`portable/tests/fixtures/import/includes/tetra.inp`.

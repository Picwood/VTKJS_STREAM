# Portable Gmsh MSH export

2026-09-12: portable CaeModel now links three production files: the STL reader,
STL writer and GmshMshFileWriter. An unused CaeModel namespace import was removed
from the Gmsh writer; the legacy project still compiles the same source.

## Corrections

The writer now emits a plain MSH 4.1 header and culture-invariant numeric fields.
Element counts and minimum/maximum tags describe the elements actually exported,
including selected parts and sparse tags. Empty selections use zero counts and
zero tag bounds. Shared nodes are written once, on their lowest incident element
dimension, instead of being duplicated across dimensional blocks. The existing
single synthetic entity tag per dimension is retained; physical groups and part
identity are not preserved in the file.

These decisions follow the [Gmsh MSH specification](https://gmsh.info/doc/texinfo/gmsh.html#MSH-file-format):
node tags are global, and section headers carry actual counts and tag bounds.
The test parser first rejected the old header; the completed suite also checks
subset bounds, numeric parsing, empty sections and mixed-dimensional sharing.

## Validation

Linux SDK 10.0.401: `bash scripts/test-portable.sh` passes **2,776 checks**:
60 STL + 178 legacy evaluator + 794 CaeGlobals + 1,596 CaeMesh + 148 mesh I/O.
The new parser checks emitted section/block counts, tags, connectivity references
and complete file consumption under en-US and it-IT. Fixtures include sparse
triangle IDs, selected parts, an empty selection and a beam sharing triangle
nodes. Shell syntax and diff whitespace checks pass.

Installed native Gmsh **4.14.0-git** accepted all eight exported fixtures through
its Python API. Node/element IDs, coordinates, element types and connectivity
matched independent expected values. The optional native check is reproducible:

```bash
PREPOMAX_MESH_IO_ARTIFACTS=/tmp/prepomax-gmsh-export-check bash scripts/test-portable.sh
python3 portable/tests/MeshIO.Characterization/verify-gmsh.py /tmp/prepomax-gmsh-export-check
```

The second command requires a working Gmsh Python package/native library. It is
not part of the default portable CI job; the .NET parser tests are. Windows
execution was not performed locally.

This qualifies the tested ASCII export/reader path, not all element permutations,
physical groups, CAD entity reconstruction or native mesh generation. Existing
quadratic/solid mappings still need exported-file fixtures. The process-level
GmshData handoff, legacy project persistence and Linux desktop remain pending.
Next: broaden solid/quadratic export fixtures and establish a native meshing
workflow with portable process/tool discovery.

Subsequent status: the bounded STEP/IGES/BREP/GEO-to-tetrahedral-PMA process and
GTK workflow now pass; see [Linux CAD-to-mesh workflow](LINUX_GMSH_WORKFLOW.md).
The broader GmshData setup handoff and export permutations above remain open.

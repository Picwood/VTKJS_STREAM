# Mesh copy and primitive I/O extraction

2026-09-11, native Linux, .NET SDK 10.0.401.

The portable graph now includes `CaeGlobals.ReadWrite`, mesh groups, node sets,
element sets, INP element-set data and `PartMassProperties`. CaeGlobals contains
154 shared source files and CaeMesh contains 35. Full parts and `FeMesh` remain
outside this graph.

## Explicit copies

`Selection.DeepCopy` copies the selection list and all existing selection settings.
Node copies preserve operation and subtype-specific state, copy part IDs and
mutable coordinate/ID arrays, and reset the transient cache hash to -1. They do
not access or modify the global selection cache. `SelectionNodeIds` and
`SelectionNodeMouse` override the base copy to handle their arrays; inversion
nodes have no extra mutable fields. Future subclasses with mutable fields must
override this method too.

The `FeElementSet` copy constructor now uses this method instead of
BinaryFormatter-based `DeepClone`. `PartMassProperties.DeepCopy` copies the center
of mass and both jagged inertia matrices, preserving null arrays/rows. The shared
`BasePart` copy constructor now uses that method for mass properties. It still
uses legacy copying for its bounding box and is not claimed portable yet.

These are in-memory copy operations, not new project serialization. They preserve
values and isolate mutable arrays; preservation of arbitrary shared-reference
graphs is not a contract of these typed copies. No persisted field names or
formats were changed.

## Primitive binary format

The existing `ReadWrite` implementation builds unchanged. Its integer, double
and jagged-integer records use a little-endian Int32 length prefix, with -1 written
for null, followed by primitive elements or nested records. Empty and null remain
distinct. Tests compare literal expected bytes, preserve special double bit
patterns, use streams returning one byte per read, check caller stream ownership,
and require `EndOfStreamException` for truncated records.

This does not qualify `ReadWrite` as a bounded reader for arbitrary untrusted
files: existing readers accept negative lengths as null and allocate according
to declared nonnegative lengths. Versioned persistence and allocation limits
remain separate migration work.

## Validation

`bash scripts/test-portable.sh` passes:

| Executable | Assertions |
| --- | ---: |
| STL | 60 |
| Legacy NCalc oracle | 178 |
| CaeGlobals | 794 |
| CaeMesh | 1,074 |
| Total | 2,106 |

New tests exercise primitive I/O and copies under en-US and it-IT, including
selection settings, all three node subtypes, cache independence, mutation of every
copied array category, null creation data and uninitialized mass properties.
Existing Windows/Linux CI runs the same harnesses. Full Windows application
build/execution has not been validated locally; `BasePart` integration is source
reviewed, while the new mass-copy implementation is exercised by the portable test.

## Next dependencies

Bounding-box `Tag` is not just a scalar: explosion code assigns both integer
indices and lists of connected `BasePart` objects. Its old arbitrary object
cloning cannot be replaced with a shallow copy. Resolve that contract with the
part graph before including it.

`VisualizationData` can now use the portable primitive I/O, but still depends on
bounding boxes, graph helpers and `FeMesh.GetVisualizationEdgeCells`. Extract the
real topology helper and remaining dependencies before adding visualization/parts.

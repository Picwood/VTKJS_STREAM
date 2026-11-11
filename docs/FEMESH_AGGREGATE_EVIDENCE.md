# FeMesh aggregate on Linux

2026-09-11: the portable project compiles 93 shared CaeMesh production sources
plus one portable notification adapter. CaeGlobals gains the shared collection
extension source (155 shared files in its current subset).

## What changed

FeMesh now explicitly uses CaeGlobals.OrderedDictionary to avoid the .NET 10
name collision. Unused framework/WinForms/Gmsh bridge imports are removed.
Dictionary naming, validity, range insertion and free-ID-interval helpers moved
unchanged into Extensions.Collections.cs, preserving their ExtensionMethods
class and Windows compile entries. Selection splitting uses typed copies.

The 9,900-part warning calls a partial method. Windows retains MessageBoxes;
the portable implementation raises FeMesh.Warning, or uses Trace.TraceWarning
when no handler is registered. No desktop dependency or silent empty handler
is introduced. This routing compiles; reaching the part-limit workflow was not
tested in this slice.

Full aggregate DeepCopy remains unchanged in FeMesh.Legacy.cs, compiled only on
Windows. It is not exposed in the portable API. Four transform paths previously
cloned the entire aggregate before AddPartsFromMesh cloned only the requested
parts again. They now pass the source directly to AddPartsFromMesh, whose
existing selected-part constructor owns new nodes, elements and parts. Thus
copied transforms run without serialization. This is not a replacement for
arbitrary full-graph cloning; region/setup ownership and object-valued box tags
still need explicit policies for that operation.

## Validation

Linux SDK 10.0.401: `bash scripts/test-portable.sh` passes 60 STL, 178 legacy
NCalc, 794 CaeGlobals and 1,600 CaeMesh checks: **2,632 total**.
The assertions run in en-US and it-IT and exercise:

- tetrahedral part extraction, sorted IDs, analytic volume, bounds and topology;
- selected-part node/element independence;
- node and element renumbering, set membership and visualization references;
- copied translation, rotation, mirroring and scaling with original geometry
  preserved, disjoint IDs and independently specified transformed coordinates;
- multiple translation copies and in-place translation;
- case-insensitive name allocation and free ID gaps;
- binary node/element/visualization payload restoration into an existing part
  graph and complete stream consumption;
- reconstruction of unnamed pre-v2.4.4 result parts into an initially empty
  result mesh, including complete visualization-block consumption.

The binary test is not a project-file round trip: part metadata is prepared
before reading, and the payload has neither a new schema nor untrusted-input
bounds. Native meshers, external files, large/degenerate models, all FeMesh
methods and Windows execution are not qualified by these fixtures. Compiling
the aggregate exposes existing unused variable/field warnings (CS0168, CS0219,
CS0169), alongside prior warnings. Shell syntax and diff whitespace checks pass.

## Next

Broaden aggregate fixtures to multi-part selections, surfaces and mesh updates;
then bring file import/export and native process boundaries across. Full-graph
copying, portable Gmsh file transport, legacy project loading, CaeModel/CaeResults
extraction and Linux desktop hosting remain open. The desktop application still
requires Windows.

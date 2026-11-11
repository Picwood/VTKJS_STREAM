# Application STL import/export on Linux

2026-09-12: portable/CaeModel/CaeModel.csproj links the existing production
StlFileReader and StlFileWriter sources. It references portable CaeMesh and STL.
This is the first two-file CaeModel slice, not the complete model assembly or a
replacement for a Windows release binary. No production STL source changes
were needed for this slice.

The distinction from the earlier STL library harness is the application path:
a file is read into FeMesh, shared/nearby vertices are welded, connected parts
are extracted, selected parts are exported, and the result is imported again.

## Validation

`bash scripts/test-portable.sh` passes with Linux SDK 10.0.401:
60 STL + 178 legacy evaluator + 794 CaeGlobals + 1,596 CaeMesh + 32 mesh I/O =
**2,660 checks**. The new fifth executable runs on both configured CI platforms;
CaeModel source changes now trigger the portable workflow. Windows execution
was not performed locally. Shell syntax and diff whitespace checks pass.

Fixtures run in en-US and it-IT, under temporary paths with spaces and Unicode.
An independently specified ASCII square verifies shared vertices, triangle
geometry, area, bounds, unit normals, winding and export/re-import coordinates.
An independently encoded binary triangle verifies binary import and analytic
area. Additional checks cover the raw-triangle export overload, missing-file
null behavior, disconnected part extraction, selected-part-only export, and
near-coincident vertex welding using the existing model-relative tolerance.

This is valid-file coverage, not complete STL conformance or malformed-file
hardening. The writer supports linear triangles only and uses float coordinates;
it is not a general solid-element surface exporter. Welding retains the
existing component-wise tolerance and collapse behavior. No CAD kernel,
native mesher, complete model import or project persistence is exercised.

## Next

GmshMshFileWriter is a suitable next portable file source. Inspection found
current-culture coordinate formatting and an element-section header based on
the whole mesh even when selecting parts. Add independent format checks and
correct those before qualifying it. Mixed-dimensional node ownership and empty
selections also need examination. Native Gmsh acceptance remains a separate gate.

Subsequent status: native Gmsh and the bounded GTK/CLI CAD-to-PMA path pass; see
[Linux CAD-to-mesh workflow](LINUX_GMSH_WORKFLOW.md). Mixed-dimensional and
broader format coverage remain open.

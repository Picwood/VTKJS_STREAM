# Portable Gmsh configuration

2026-09-11: CaeMesh now builds 81 shared production source files on .NET 10.
The seven Gmsh setup classes and eight existing enums join the portable project.
Their GmshCommon imports were unused and have been removed. Enum namespaces,
values and legacy serialization fields remain unchanged; no substitute bridge
or duplicate contract assembly was introduced.

## Copy corrections

ExtrudeMesh and RevolveMesh checked destination arrays before copying source
arrays. Fresh copies therefore lost their direction/center or axis data, and
populated destinations could throw when copying null source arrays. They now
copy source arrays independently and clear destination arrays for null sources.

SweepMesh had the same problem, discarded the side-surface copy result, and
assigned new topology arrays into the source object. Its CopyFrom now copies
all geometry arrays and each level of LayerGroupEdgeIds, preserving null groups
and entries and leaving the source untouched. Existing constructors and method
signatures are retained. The existing SweepMesh(ExtrudeMesh) conversion is not
redefined or qualified as a sweep-specific copy constructor.

## Validation

`bash scripts/test-portable.sh` passes with Linux SDK 10.0.401:
60 STL + 178 legacy NCalc oracle + 794 CaeGlobals + 1,242 CaeMesh = **2,274 checks**.
The new tests first failed on extrusion geometry loss, then passed after the
copy corrections. The 34 additional assertions run in en-US and it-IT and cover
geometry/layer/selection independence, source preservation, copying null arrays
into populated destinations, sweep smoothing values, shared parameter copying,
angle conversion and numeric clamps. Existing compiler warnings remain.
`git diff --check` and shell syntax validation also pass. Windows CI is
configured but was not executed locally.

## Next boundary

GmshData and GmshIdLocation already live in CaeMesh, but GmshData.WriteToFile calls
DumpToFile, a legacy serialization operation. They remain outside the portable
build until the file handoff has an explicit persistence boundary. GmshAPI,
SweepMethods and native mesher execution are also excluded. No native algorithm,
mesh quality, topology reconstruction or Windows bridge execution is validated
by these configuration tests.

Continue with that data handoff and its consumers, then FeMesh's desktop warning
and aggregate clone ownership. Full FeMesh and the Linux desktop are still
pending.

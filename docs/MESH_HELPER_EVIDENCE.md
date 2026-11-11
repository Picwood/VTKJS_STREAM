# FeMesh helper extraction

2026-09-11: 91 production source files compile in portable CaeMesh.
MeshGeometryIds and MeshTransforms contain the implementations extracted from
FeMesh. Existing public FeMesh methods delegate to them, including the private
rotation-matrix helper used by part rotation. Both projects compile the shared
helpers. GeometryType, ImportOptions, FrontBackBothFaceSideEnum and mesh
classification extensions also join the portable build.

The geometry encoding remains item * 100000 + type * 10000 + part. This slice
does not change identifier validity rules, overflow behavior or saved IDs.

## Fix and validation

MirrorPoint previously wrapped the caller array, then MirrorPoints replaced the
wrapped entry with a reflected array. The caller's original point never changed.
The wrapper now copies the reflected three coordinates back. Batch reflection
still replaces its entries as before. A regression test failed before the fix.

Linux SDK 10.0.401: `bash scripts/test-portable.sh` passes 60 STL, 178 legacy NCalc,
794 CaeGlobals and 1,532 CaeMesh checks (**2,564 total**). New assertions exercise
32 combinations of item/type/part identifiers per culture, component accessors,
part selections, null and duplicate inputs, geometry classification, translation,
nonuniform scaling, rotation about a translated center using a nonunit axis,
and single/batch reflection. Both en-US and it-IT run. Existing warnings remain.
Shell syntax and git diff whitespace checks pass. Windows execution was not
performed locally; CI remains configured for both platforms.

## Compile probe and remaining work

A temporary inclusion of full FeMesh failed and was removed. The first compiler
errors identify WindowsRuntime/WinForms/GmshCommon imports, missing local enums,
and ambiguous OrderedDictionary references between CaeGlobals and .NET 10.
The local enums are now included. Framework import cleanup and explicit
CaeGlobals dictionary qualification remain to do before a deeper compiler pass.
This was an initial probe, not an exhaustive inventory of method-body errors.

The desktop warning, selection clones, aggregate clone ownership, arbitrary
bounding-box tag graphs and remaining collection extensions still require work.
Full FeMesh is not compiled by the portable project. Native Gmsh execution,
portable file transport and Linux desktop integration remain pending.

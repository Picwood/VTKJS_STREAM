# Portable project-to-renderer scene bridge

Implemented 2026-09-22. `ProjectScene.Export` in PrePoMax.Application reopens an
editable static project and writes the existing renderer host-upload version 1
format (`application/vnd.prepomax.render-scene-v1`). It uses the production
mesh's exterior visualization faces, triangulates each linear quad, and preserves
part names, source node IDs and source element IDs. Each part becomes one actor.
The application library has no VTK, WinForms or renderer-server dependency.

```bash
dotnet build portable/PrePoMax.Core.slnx -c Release
dotnet portable/PrePoMax.Cli/bin/Release/net10.0/PrePoMax.Cli.dll scene model.pma scene.bin
```

Output is published from a temporary sibling file with no overwrite; failure
cleans up the temporary file. The input project is unchanged. Actor, point,
triangle, name and byte limits match the renderer upload limits. The supported
project schema currently supplies linear hexahedra; other exterior face shapes
are rejected. Coordinates remain double precision until the renderer reader
performs its existing origin-relative conversion.

This is undeformed mesh geometry with a uniform color and no scalar field. It
uses version 1 surface topology, not version 2 volumetric selection locators.
It does not transfer units, original display styling, result fields or deformation.
The exporter itself creates no session and sends nothing over HTTP. The
[portable renderer host](LINUX_RENDERER_HOST.md) now owns process startup,
authenticated session creation and scene publication. The later GTK host supplies
browser bootstrap, navigation and interactive workflows. PEND-006 is complete;
large-model GPU acceptance remains open.

Portable acceptance links the actual `HostSceneUploadReader` source and renderer
contract/resource projects into the characterization executable. A saved two-part
project must load through that reader, pass contract validation, preserve part
names and bounds, and supply 16 node identities and 24 exterior triangles. Tests
also cover no-overwrite behavior, source preservation and temporary-file cleanup
under English and Italian cultures. This is protocol compatibility evidence,
not browser or visual acceptance.

Validation: the Linux portable suite passes 3,771 checks, including 760 mesh I/O
checks and 16 new scene bridge assertions across both cultures.

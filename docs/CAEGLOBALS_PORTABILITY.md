# CaeGlobals extraction boundary

2026-09-10: `portable/CaeGlobals/CaeGlobals.csproj` compiles 154 production source
files, including primitive binary I/O on .NET 10. It is an incremental subset, not a
drop-in replacement for the full Windows `CaeGlobals.dll`. Do not copy it over a
Windows release. The namespace and assembly name are retained for eventual core
integration; old project serialization is not validated by retaining names alone.

## Compiled subset

| Area | Included behavior |
| --- | --- |
| Geometry | `Vec3D`, `Geometry`, `Geometry2`, `Matrix` |
| Spatial indexing | Point/bounds octrees, bounding boxes, points, planes, rays, point clouds |
| Collections | Ordered dictionary, unique queue, sorted node keys, comparers |
| Domain foundations | `CaeException`, named objects, event arguments, display-name attribute |
| Data transfer | Nodes, cells, parts and animation exchange data |
| Text | String splitter and string compression |
| Selection | Selection nodes/state, cache, selection enums, region interfaces |
| Metadata | Shared `StandardValueAttribute`, region labels |
| Numeric helpers | Significant-digit rounding, numeric array conversion and flattening |
| Units/equations | Unit systems, converters, equation containers and NCalc wrapper |

The project file is the authoritative source inclusion list. It uses UnitsNet
4.89.1 and NCalc 7.1.0, with no UI framework references, native libraries, or unsafe
serialization compatibility package. Serializable data contracts remain present; this does not
enable BinaryFormatter persistence on .NET 10.

## Remaining dependency groups

| Group | Concrete boundary to extract next |
| --- | --- |
| `Extensions.cs` | Split collection/numeric operations from WinForms controls, GDI operations and BinaryFormatter cloning. |
| `Tools.cs`, `ReadWrite.cs`, serialization | Separate algorithms, filesystem/process services and persistence; retain a dedicated legacy-format migration gate. |
| Desktop-only helpers | Hooks, dialog centering, message boxes, filename editor, property-grid editors stay outside the portable assembly. |
| First-run settings | Review filesystem/storage assumptions before bringing `FirstRunChecker` across. |

`CaeException` was moved intact from `ExceptionTools.cs` into its own shared file,
and added to the legacy project. Dialog presentation stays in `ExceptionTools`.
Unused WinForms, Drawing2D and WindowsRuntime imports were removed from four
otherwise portable files. No compatibility shims were introduced.

The second extraction moves `StandardValueAttribute` intact out of the desktop
descriptor implementation. `ExtensionMethods` and `Tools` are now partial classes:
selection classification, numeric array conversion, and significant-digit rounding
live in shared files included by both project graphs. Public names and method
bodies are retained, including the internal metadata members used by the desktop
editor. Units and equations were subsequently added with a tested evaluator API
boundary; see [equation migration evidence](EQUATION_PORT_COMPATIBILITY.md).

Package contents were inspected directly from the pinned NuGet archives. Unused
Remoting imports in equation sources and a WinForms import in a unit converter
were removed during this migration.

## Regressions found during extraction

The new characterization tests initially failed on two existing implementations:

- `Geometry.PointToTriangleDistance`: an interior projection above the unit
  right triangle did not return its perpendicular distance. It now uses
  barycentric coordinates of the projection and the minimum edge distance for
  outside/degenerate cases. Tests cover both windings, both plane sides, edge and
  vertex regions, collinear vertices and collapsed triangles.
- `StringCompressor.DecompressString`: one `GZipStream.Read` truncated a large
  Unicode string on .NET 10. It now loops until the declared length is filled and
  rejects both shorter and longer decompressed payloads. The existing four-byte
  length + gzip/base64 representation is retained; resource limits and a new
  persistence format remain separate work.

Both fixes use APIs available in .NET Framework 4.8 and affect the shared Windows
sources too. A full net48 application build has not been run on this Linux host.

## Evidence

Run `bash scripts/test-portable.sh` with .NET 10, or set `PREPOMAX_DOTNET` to the
SDK executable. On Ubuntu 26.04.1 x64 with SDK 10.0.401:

- 794 CaeGlobals checks pass under English and Italian cultures: geometry,
  matrix inversion requiring pivoting, collection ordering, domain errors,
  Unicode names, deep-copy isolation, compression and malformed length headers.
- Octree queries use 256 deterministic points and 40 radius queries per culture,
  compared with independent brute-force distances; growth/removal are checked.
- Selection checks cover every `vtkSelectBy` classification and region label,
  ID-copy isolation, mixed selection classification, cache removal, exclusive
  selection limits, and pick/plane offsets without altering plane normals.
  Numeric tests cover rounding and row-major array conversion/isolation.
- All 60 existing STL checks also pass.
- The separate legacy NCalc oracle passes 178 checks using the same formula
  corpus as the modern evaluator; its net48 target also builds on Linux.

The CI matrix now includes changes to `CaeGlobals/**` and runs both harnesses on
Ubuntu and Windows with .NET 10. CI execution and full Windows desktop regression
remain unverified locally. Bounds-octree behavior, all geometry algorithms,
malformed STL, unit/equation evaluation and persistence are not exhaustively
covered by this slice.

Selection serialization constructors and legacy field names remain in shared
source, but this slice does not validate serialized history or `.pmx` import.
Cache behavior is characterized for add/lookup/first/last removal; this is not a
claim of thread safety or comprehensive cache lifecycle testing.

The latest [mesh copy/I/O slice](MESH_COPY_IO_EVIDENCE.md) adds explicit selection
copy methods and validates primitive binary records without BinaryFormatter.

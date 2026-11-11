# Mesh regions and coordinate systems

2026-09-11: the portable CaeMesh project links 66 production source files.
This slice adds FeSurface, FeReferencePoint, CoordinateSystem and
MeshRepresentation, all also compiled by the legacy Windows project.

FeSurface's copy constructor now uses Selection.DeepCopy rather than a
serialization-based clone. Its existing semantics remain: arrays, element-face
maps and history belong to the copy; temporary parent-region references are
omitted. CoordinateSystem had an unused System.Net.Configuration import removed.
No coordinate algorithm or reference-point update behavior was changed.

## Evidence

`bash scripts/test-portable.sh` passed on Linux with SDK 10.0.401:
60 STL + 178 legacy NCalc oracle + 794 CaeGlobals + 1,208 CaeMesh = **2,240 checks**.
The 42 new assertions run under en-US and it-IT and cover:

- surface metadata, independent arrays/maps/history and omitted temporary parents;
- node-set construction, invalid empty faces, and creation-mode/face clearing;
- reference-point coordinates, bounding-box center, centroid updates and reset;
- identity and translated/rotated rectangular transforms;
- vector orientation independent of translation;
- cylindrical radius, angle in radians, height and radial vector orientation;
- incomplete selection and coincident-point frame rejection.

Windows execution is delegated to the configured CI matrix and was not run
locally. Existing unused-exception warnings in the newly included reference-point
and coordinate-system sources are now visible, alongside the existing pyramid
unused-field warning. Full equation callback, degenerate cylindrical-axis,
serialization and arbitrary region graph behavior are not qualified by this
slice. Reference-point and coordinate-system serialization-based caller copies
still need explicit migration when their owning aggregate is brought across.

## Remaining aggregate boundary

FeMesh still cannot join the portable build unchanged:

- Its validity checks and deletion handling depend on ShellGmsh,
  TetrahedralGmsh, TransfiniteMesh and related setup classes, whose sources import
  GmshCommon. Extract configuration contracts from bridge dependencies next.
- GetPartTopologyForGmsh exposes GmshData and GmshIdLocation. These data contracts
  need a portable home while retaining the legacy bridge's consumers.
- Part creation calls MessageBoxes.ShowWarning at the 9,900-part limit. Introduce
  a notification boundary that keeps the warning visible in the Windows caller.
- Selection splitting uses DeepClone, and aggregate copying still clones the
  entire FeMesh. Resolve ownership of all dictionaries, cached state and
  object-valued bounding-box tags before replacing that aggregate operation.

No native mesher, mesh import/export, full FeMesh, legacy project loader or Linux
desktop execution is claimed by these tests.

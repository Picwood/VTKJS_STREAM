# Multipart editable project persistence

Implemented 2026-09-17. Static model metadata version 3 preserves multiple mesh
parts: names, order, sparse positive IDs, element membership and linear-hexahedron
formulation. Reopening rebuilds mesh topology and restores part-based solid
sections, allowing distinct materials to remain assigned to distinct parts.
Node coordinates belong to the reopened model independently of the original.

Every element must belong to exactly one part. Invalid membership, missing or
unknown elements, duplicate part IDs and invalid formulation values are rejected.
The production input comparison remains the guard against lossy project saves.
New writes use metadata version 3; single-part version 1 and version 2 metadata
remain readable. The outer archive format remains version 2.

Validation:

- `bash scripts/test-portable.sh`: 3,733 passing checks, including 722 mesh I/O
  checks. The extension adds 22 assertions across English and Italian locales
  for multipart identity, membership, C3D8R formulation, distinct material
  assignments, part-based sections, independent edits, malformed membership and
  version 2 compatibility. Existing version 1 compatibility checks also pass.
- `python3 scripts/test-native-calculix.py`: seven native cases. The new case
  saves and reopens two independent unit cubes with E=1000 and E=2000, Poisson
  ratio zero and unit axial force on each cube. CalculiX gives loaded-face
  displacements U1=0.001 and U1=0.0005 respectively. English/Italian generated
  decks match apart from model IDs. All seven cases attach actual FRD/DAT bytes
  to editable archives and verify those bytes after reopening.
- Native artifacts: `/tmp/prepomax calculix job 872fdqsa` (temporary local files).

The editable model scope remains numeric static analysis with linear hexahedral
mesh elements and the explicitly supported definitions in
[static persistence](STATIC_PROJECT_PERSISTENCE.md) and
[mechanical persistence](MECHANICAL_PROJECT_PERSISTENCE.md). Saving still requires
all mesh parts active. This milestone does not add CAD/geometry metadata, general
nonlinear/contact definitions, legacy PMX import, or a Linux desktop executable.
Windows execution was not tested locally.

Next: broaden supported model definitions and connect persistence and solver
services to the Linux application host described in the
[port progress record](LINUX_PORT_PROGRESS.md).

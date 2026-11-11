# Portable constraint definitions and export

Implemented 2026-09-16 as a dependency slice toward FeModel/CalculixFileWriter.

The portable CaeModel project now compiles 138 production sources, including
all eight constraint source files: Constraint, Tie, RigidBody, SpringConstraint,
PointSpring, PointSpringData, SurfaceSpring and CompressionOnly. It also compiles
CalTie, CalRigidBody and CalEquation. An unused System.Runtime.Remoting.Contexts
import was removed from Tie; no replacement types or copied implementations
were introduced.

CalEquation now formats numeric terms with invariant culture and always ends its
last data line, including equations with exactly four/eight terms. Previously
Italian decimal commas changed the input token layout and a following keyword
could concatenate with a full final line. Null, empty and incomplete equations
are rejected at construction (the latter was already rejected).

The scoped cube/bar acceptance exporter adds three axial equality equations on
the loaded face using fractional coefficients. This retains the uniform-traction
analytic displacement and exercises production equation keywords in native solves.
The existing single/two-step and parameterized cases still compare English and
Italian decks byte-for-byte.

Validation:

- `bash scripts/test-portable.sh`: 3,513 checks pass, including 30 new assertions
  across en-US and it-IT. They cover tie tolerance/defaults/surface order/swap,
  identical-surface rejection, rigid-body reference-node/surface resolution,
  equation decimal formatting, continuation/final newlines, empty/incomplete
  terms and spring stiffness directions/2D restrictions.
- `python3 scripts/test-native-calculix.py`: single/two-step cubes, long bar,
  compression and unloaded cases pass with the equation constraints. Production
  FRD/DAT readers verify cubes; independent DAT checks verify bar displacement.
- `git diff --check`: passes. Existing compiler warnings remain.

Native artifacts: `/tmp/prepomax calculix job 1ds8psq6` (temporary local files).
Windows compilation was not run. Tie and rigid-body writers have portable
characterization, not native solve acceptance. SurfaceSpring and CompressionOnly
are build coverage only; automatic constraint generation, reference-point node
allocation, spring/gap assembly, contacts and complete exporter integration remain
pending. FeModel and CalculixFileWriter still do not compile in the portable graph.

Next integration target: resolve remaining FeModel-owned export dependencies
and compile the actual aggregate/exporter. Project persistence and the Linux
shell/renderer hosting gate remain separate application blockers.

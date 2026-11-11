# GTK legacy PMX import

Validated: 2026-10-12.

The Linux GTK **Open project…** chooser accepts `.pma` and `.pmx`. Selecting a
PMX opens a second native chooser for a new `.pma` destination. GTK runs the
standalone `PrePoMax.PmxInspect --convert` process and opens the PMA only after
conversion, archive reopen and solver-input equivalence succeed.

The converter retains its existing boundary: the source is read-only, source
and destination must differ, the destination must not exist, and NRBF records
are decoded without `BinaryFormatter` or arbitrary legacy object construction.
Protected and pre-2.0.6 projects, unsupported definitions and malformed model or
result data fail closed. A refusal leaves the currently displayed project and
renderer scene unchanged and publishes no destination. Closing GTK terminates
an active converter child process.

Large conversions run off the GTK thread. The status label explains that they
can take several minutes. The current process limit is 15 minutes; there is no
progress percentage or in-window cancellation control yet. The supplied large
fixture previously measured about 98 seconds and 11.4 GiB peak resident memory.

## Acceptance evidence

- A native X11 smoke drives both file choosers with a synthetic convertible PMX,
  creates the PMA, reopens it and reaches a stable renderer frame with one actor,
  four points and four triangles.
- A second native smoke starts with that PMA displayed, selects an invalid PMX,
  verifies that the scene ID is unchanged and confirms that no PMA was created.
- Both smokes verify that closing GTK also closes the renderer listener.
- The real 702 MiB converted fixture opens in GTK as 14 model actors. Selecting
  `STRESS/MISES` produces a stable 7-actor contour scene with 714,210 visible
  points and 1,428,456 triangles. After bounded result and model-scene loading
  plus managed heap compaction between large stages, this takes 33.55 seconds
  end to end and peaks at 3,632,488 KiB resident memory on this workstation,
  down from 58.9 seconds and 12,477,460 KiB.
- The 3.6 MiB relocatable preview passes all staged SHA-256 checks and runtime
  probes and contains `PrePoMax.PmxInspect.dll` plus `System.Formats.Nrbf.dll`.
- The converter's independent synthetic suite remains at 22 record, 20 mesh,
  3 result and 5 conversion cases. The portable suite now passes 5,141 checks.

This proves UI routing, fail-closed publication and full-fixture GTK result
rendering. Field and rendered-scene comparison against original PrePoMax and
broader PMX versions remain open.

# Start the Linux viewer without a project

Validated: 2026-09-30.

```sh
scripts/start-gtk-viewer.sh
```

The GTK window offers **Open project…** and **Import mesh…**. It does not load
a sample project. Solve, material/load/boundary editing, node-set creation and
result controls remain unavailable until a project is opened. F6 does not
switch into the unloaded WebView. The renderer companion is started and owned
by the window, but the WebView loads only after successful project publication.

Open a `.pma`; select a supported unprotected `.pmx` and choose a new `.pma`
destination; or import a supported mesh-only `.inp` or tetrahedral `.mesh` with
explicit material values and a new output filename. Failed Open/Import leaves the application
usable without a project. The companion independently rejects model commands
in this state; UI controls are not the sole guard. Successful first Open or
Import initializes the viewer normally. Passing a project on the command line
continues to work.

This is a no-project startup state, not a CAD document/template creator.
Import and persistence restrictions remain as documented in
[mesh import](LINUX_MESH_IMPORT.md). Current build artifacts are required;
rebuild old preview bundles to obtain this behavior.
Legacy conversion restrictions are documented in
[GTK PMX import](LINUX_PMX_GTK_IMPORT.md).

## Verification

GTK X11 checks pass for empty startup, failed first Open, refusal of Solve
without a project, first Open through the native chooser, first Import through
the material/save dialogs, failed first Import, PMX conversion/open, refused PMX
conversion with scene preservation and ordinary project startup.
All successful smoke runs verify renderer shutdown. The managed companion
build has zero warnings/errors; Python and shell syntax and whitespace checks
pass. The portable engineering suite baseline is **5,141 checks**; this
slice changes startup and host routing rather than engineering calculations.

```sh
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh --smoke-empty
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh --smoke \
  --smoke-switch /path/to/project.pma
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh \
  --smoke-import-mesh portable/tests/fixtures/import/tetra.mesh \
  --smoke-import-output /path/to/new-import.pma
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh \
  --smoke-import-pmx /path/to/project.pmx \
  --smoke-pmx-output /path/to/new-project.pma --smoke-timeout 180
```

No additional Wayland, physical-GPU or clean-machine qualification is claimed.

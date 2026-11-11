# Linux shell decision record

Date: 2026-10-22. Status: accepted.

## Decision

Use GTK 3 with distribution WebKitGTK 4.1 as the production Linux shell. Keep
the Chromium/Playwright host as an automated renderer reference and fault-test
tool; it is not a shipped runtime dependency.

The GTK host now runs as `Gtk.Application` with the stable application ID
`si.um.fs.prepomax.LinuxViewer`, matching its desktop and AppStream IDs. It uses
non-unique application instances so each opened project retains explicit
ownership of its companion, renderer and solver-job lifecycle. The installed
`prepomax-linux-viewer` command and desktop entry launch the same host from the
private-runtime Debian payload without a source checkout.

## Evidence behind the selection

| Criterion | Chromium/Playwright reference | Selected GTK/WebKitGTK host |
|---|---|---|
| Project, session and rendering | Headed/headless reference passes | Native embedded window, reload and project switching pass |
| Domain workflow | Geometry reference | PMA/PMX opening, INP mesh import, model editing, solve/cancel and result contours pass for the documented scope |
| Input and selection | Browser camera controls | Native shortcuts plus browser camera, click, rectangle and connected-face selection pass |
| Dialogs and file access | Not qualified | Native open/import/save/cancel flows and focus restoration pass |
| Clipboard and accessibility | Not qualified | Bounded native summary copy passes on X11; accessible names, mnemonics, shortcuts, native Tab traversal and 1x/2x scale checks pass |
| Recovery and ownership | Browser/listener closure passes | WebKit termination, startup/frame failure, explicit retry, companion shutdown and listener closure pass |
| Wayland | Not selected | Native occlusion pauses frame deadlines; cover/uncover and recovery acceptance pass |
| Distribution and upgrade | Requires Node and a browser distribution | Reproducible Debian package, clean Ubuntu install, desktop/MIME integration, in-place upgrade, user-state preservation and purge pass |
| GPU/WebGL | Software reference retained | DRM-backed WebGL 2 passes on X11 and Wayland using Intel `renderD129` (`0x8086`, `i915`); forced software rendering is rejected by the hardware smoke |

The physical-GPU acceptance reads the trusted client WebGL 2 diagnostics and
also requires a live WebKit descendant to hold a `/dev/dri/renderD*` descriptor.
This avoids relying on WebKit's privacy-facing renderer string, which reports a
generic identity on this machine. The recorded device is the host's integrated
Intel GPU. This one device does not qualify the wider driver matrix.

## Consequences and remaining qualification

Python/PyGObject, GTK 3 and WebKitGTK remain distribution-managed dependencies,
allowing the distribution to deliver WebKit security updates. The application
keeps its private .NET runtime and authenticated loopback renderer. Navigation,
bootstrap and native bridge messages remain bounded to the trusted main frame.

PEND-006 is complete because both leading candidates were measured, the GTK
path has representative renderer and domain workflows, and this record selects
the shipping framework. The following work remains independently tracked:

- PEND-001: large-model physical-GPU benchmark on the supported matrix.
- PEND-005: manual visual comparison with the original application.
- PEND-007: signing, update delivery, additional distributions/GPUs and uninstall policy.
- Full screen-reader, Wayland clipboard persistence, complete cross-WebView Tab
  order and fractional/mixed-monitor scale qualification.
- Broader PrePoMax feature parity, especially CAD and application-integrated
  meshing, model-definition breadth and remaining legacy project state.

Evidence: [Chromium reference](LINUX_VIEWER_SPIKE.md),
[GTK host](LINUX_GTK_VIEWER_SPIKE.md),
[accessibility](LINUX_GTK_ACCESSIBILITY.md),
[recovery](LINUX_GTK_RECOVERY.md),
[Wayland scheduling](LINUX_WAYLAND_FRAME_SCHEDULING.md), and
[Debian packaging](LINUX_DEBIAN_PACKAGE.md).

# GTK/WebKitGTK production Linux host

Implemented 2026-09-22 and selected for production Linux use on 2026-10-22.
`scripts/start-gtk-viewer.py` opens a GTK 3 application window containing
WebKitGTK and the existing renderer client. It uses the portable
`PrePoMax.ViewerHost` companion. No Windows UI or ActiViz component participates
in this path.

The host uses `Gtk.Application` with ID `si.um.fs.prepomax.LinuxViewer`, matching
the installed desktop and AppStream IDs. Non-unique instances preserve the
existing one-window/one-companion ownership boundary. The accepted
[shell decision record](LINUX_SHELL_CANDIDATES.md) retains Chromium/Playwright as
an automation reference without making it a shipped dependency.

The GTK adapter polls a bounded queue installed only in the top frame. WebKit's
[JavaScript evaluation API](https://webkitgtk.org/reference/webkit2gtk/stable/method.WebView.evaluate_javascript.html)
executes in the main-frame context. The adapter checks the native current URI and
navigation generation before and after evaluation, asks the companion to validate
readiness, and delivers the returned bootstrap only to the expected page. It uses
an ephemeral WebKit context, blocks other document URLs/new windows, and never
logs bootstrap responses. Complete subresource-policy and hostile-frame
hardening remain security follow-up. Companion exchanges run on one background worker; callbacks return through
the GTK main loop. Startup, readiness and project publication do not wait for
pipe replies on the UI thread. Navigation generations discard stale bootstrap
callbacks, and pending readiness is retried after an earlier reply completes.

## Run

Requires Python 3, PyGObject, GTK 3 and WebKit2 4.1 introspection packages, a desktop
display, the built client and portable companion, and the renderer's .NET runtime.
Use the builds described in [the Chromium spike](LINUX_VIEWER_SPIKE.md), then:

```bash
python3 scripts/start-gtk-viewer.py /absolute/path/to/dotnet /absolute/path/to/model.pma \
  "$PWD/prototypes/VtkJsStreaming/src/PrePoMax.Render.Server/bin/Release/net8.0/PrePoMax.Render.Server.dll" \
  "$PWD/prototypes/VtkJsStreaming/src/client/dist"
```

Add `--smoke` to verify scene acknowledgement, resize/reload, a second
acknowledgement, and renderer listener shutdown. Optional
`PREPOMAX_BROWSER_SCREENSHOT=/path/to/frame.png` captures a WebKit snapshot during
smoke; this additionally requires the GI Cairo bridge (`python3-gi-cairo`).

## Evidence and limits

Local smoke passed with WebKitGTK 2.52.6, Python 3.14.4 and the existing desktop
display. The two-part project produced two actors, 16 points and 24 triangles,
with stable-frame acknowledgements before and after resize/reload. Companion
exit closed the renderer's listener. The unchanged net8.0 renderer ran on .NET
10.0.12 using explicit `DOTNET_ROLL_FORWARD=Major`.

On 2026-10-22, `--smoke-gpu` passed the same render/reload/lifecycle path on
both Wayland and X11. It required WebGL 2, rejected known software renderer
identities, and verified that a live WebKit descendant held
`/dev/dri/renderD129`; sysfs identifies that node as Intel vendor `0x8086` using
the `i915` driver. `LIBGL_ALWAYS_SOFTWARE=1` was a negative control and failed
the hardware requirement. This qualifies the recorded integrated GPU and driver,
not the large-model or multi-device support matrix.

WebKit snapshot `/tmp/prepomax-gtk-webkit.png` was inspected and contains the
client and geometry. Generic GDK window capture returned a black image and is
not accepted as evidence. The GI Cairo bridge was absent from the workstation;
for this probe its distro package was downloaded/extracted under `/tmp` and made
available locally, without changing system packages. Default runs need no Cairo
bridge. Temporary screenshots are not durable reviewer acceptance artifacts.

Visible issues remain: the camera selector has poor contrast in the native
WebKit theme, and the loaded-state overlay overlaps the scale overlay. The
initial view also overlaps the two cubes in projection. These require UI/camera
review; geometry counts and stable-frame messages do not establish visual parity.

Native file-open acceptance is covered below. Clipboard and accessibility
groundwork, recovery, distribution and package upgrades now have evidence.
Screen-reader, mixed-monitor DPI, untrusted-content hardening, parent-crash
recovery and signed updates remain qualification work. See the
[shell decision record](LINUX_SHELL_CANDIDATES.md).

## Native project opening and asynchronous IPC

The GTK toolbar now has **Open project…**, using `Gtk.FileChooserNative` with a
`.pma` filter. Cancelling the chooser leaves the scene untouched. Selecting a file
sends an `open` command over the private companion pipe; the companion validates
the archive before publishing a replacement scene. The same renderer/window stays
alive, and the browser reloads with a bootstrap for the new scene. Missing/invalid
archives fail before publication and leave the old bootstrap valid. Transport
failures after publication are not claimed to roll back the server's scene.

One worker serializes all companion reads and writes, preventing reply mix-ups.
The GTK main loop handles callbacks, native dialogs and controls. Open is disabled
during a publication. Closing the window drops pending callbacks and shuts down
the companion, with a bounded process-group fallback.

Validation (2026-09-22):

- Existing GTK scene/reload smoke passes.
- `--smoke --smoke-switch /path/to/single.inp.pma` opens and cancels a real native
  chooser, verifies missing-file recovery, then programmatically selects a second
  project and emits the native dialog response. The multipart fixture changes
  from 2 actors/16 points/24 triangles to 1 actor/8 points/12 triangles and receives
  a matching stable-frame acknowledgement. This is automated native-dialog
  integration, not manual mouse/keyboard usability acceptance.
- `scripts/test-viewer-switch.py DOTNET MULTIPART.pma SINGLE.pma SERVER.dll` verifies
  invalid-open bootstrap preservation, successful scene replacement, new-scene
  bootstrap and unchanged source bytes through the companion protocol.
- Chromium viewer regression passes with the updated companion; build succeeds
  without warnings. These are opt-in integration checks, not changes to the
  portable characterization total.

The [native solve/cancel workflow](LINUX_GTK_SOLVER_WORKFLOW.md) connects the
portable job service and reopens completed result projects. Result contours,
clipboard and accessibility groundwork are implemented. Broader parity and
platform qualification continue under their dedicated gates; PEND-006 is complete.

## Browser process recovery

[GTK view recovery](LINUX_GTK_RECOVERY.md) adds native failure reporting and
Retry view, with real WebKit termination and same-scene restoration acceptance.

## Native clipboard

[Native scene-summary copying](LINUX_GTK_CLIPBOARD.md) connects the client Copy
summary button to GTK without exposing a clipboard-read bridge. X11 private
selection acceptance passes; Wayland and cross-application qualification remain open.

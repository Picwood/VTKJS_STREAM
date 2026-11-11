# Wayland frame scheduling: identified occlusion mismatch

Validated 2026-09-24 on GTK 3.24.52, WebKitGTK 2.52.6, Mutter 50.1,
with three monitors. This resolves the reproduced covered-window startup stall;
it is not a qualification of every Wayland/GPU combination.

## Cause and evidence

The failing viewer was mapped, but GTK reported `fully-obscured` and its frame
clock stopped at 24. JavaScript evaluation, bootstrap, resource loading and
geometry construction continued. The page reported `document.hidden=false` and
never emitted scene-applied. This was not a lost bridge message or a network
failure. An exposed minimal WebGL probe completed 51 idle/draw/two-callback
cycles; loss of focus alone did not reproduce the stall.

The upstream source at the installed release explains the discrepancy:
[`ToplevelWindow::isSuspended()`](https://github.com/WebKit/WebKit/blob/webkitgtk-2.52.6/Source/WebKit/UIProcess/API/gtk/ToplevelWindow.cpp#L118)
checks compositor suspension only for GTK 4.12+, returning false in GTK 3.
[`webkitWebViewBaseUpdateVisibility()`](https://github.com/WebKit/WebKit/blob/webkitgtk-2.52.6/Source/WebKit/UIProcess/API/gtk/WebKitWebViewBase.cpp#L497)
uses that suspension state when calculating page visibility. Thus the GTK 3
page can think it is visible while the compositor withholds surface frames.
Our wall-clock frame/startup deadlines converted that legitimate suspension
into a load failure. Merely raising the deadlines cannot correct the mismatch.

## Fix

The GTK host subscribes to native visibility notifications and sends a bounded
`prepomax.host.visibility-v1` message through its existing trusted page bridge.
It replays current visibility before bootstrap, covering initially obscured
windows. The embedded client validates the boolean and uses it alongside browser
visibility to pause its frame-wait deadline. On exposure it resumes the remaining
budget, rather than granting a fresh budget every time visibility changes.
The native startup watchdog likewise pauses its remaining budget while fully
obscured. Exposed stalls still fail; cancellation still cleans up pending work.

No frame callback is synthesized, no acknowledgement is fabricated, and the
application does not force background redraws, disable security, switch display
backends or automatically reload a covered scene. Once exposed, the pending real
callbacks complete the original scene normally. The overall smoke deadline is
still wall-clock bounded, so unattended tests fail rather than hang if another
window keeps the viewer covered indefinitely.

## Real desktop acceptance

With the normal four launcher arguments, use:

```bash
GDK_BACKEND=wayland DOTNET_ROLL_FORWARD=Major python3 scripts/start-gtk-viewer.py \
  /absolute/path/to/dotnet /absolute/path/to/two-step-result.pma \
  "$PWD/prototypes/VtkJsStreaming/src/PrePoMax.Render.Server/bin/Release/net8.0/PrePoMax.Render.Server.dll" \
  "$PWD/prototypes/VtkJsStreaming/src/client/dist" \
  --smoke-occlusion --view-timeout 5 --smoke-timeout 180
```

This briefly creates a fullscreen cover on the **viewer's monitor**, waits for
real native full occlusion, loads the scene, waits for resources, and holds the
cover for 12 more seconds. That exceeds both the 5-second native startup budget
and the 10-second client frame budget. It then removes the cover and requires
actual stable topology on the same page generation, with exactly one bootstrap
handshake and no retry. This passed, including renderer-listener shutdown.

Wayland may refuse programmatic activation. If another desktop window remains
above the viewer after the test cover closes, expose the viewer manually. Earlier
attempts remained `fully-obscured` even after removing our cover and correctly
never claimed a presented scene. Running the test on the wrong monitor is not
an occlusion test; the helper explicitly selects the viewer's monitor.

Unit acceptance covers real two-callback success, exposed stalls, cancellation,
late callbacks, native occlusion, remaining-budget preservation, and browser
visibility. Separate screen-reader, physical GPU, mixed-scale monitor and full
shell acceptance gates remain open.

Negative control: a temporary launcher copy forced the forwarded host visibility
to `true`, emulating the missing visibility information while keeping the same
real cover test. It failed with `frame-timeout` before uncovering, as expected.
The fixed launcher passed the equivalent covered interval and same-page resume.

Final validation: all **92 client tests in 26 files** pass, as do the production
client build, Python compilation and whitespace checks. The X11 exposed-frame
fault-injection regression still rejects stalled callbacks, offers Retry view,
renders the retained scene and closes the renderer listener. Occlusion handling
therefore does not remove the failure path for an actually exposed stalled view.
The 3,805 portable numerical baseline is unchanged and was not rerun.

# GTK keyboard and accessibility groundwork

Implemented 2026-09-23. Native controls now expose explicit accessible names,
with descriptions for field selection and displacement scaling. Status labels
retain their changing text as their accessible name. Result and scale labels
provide Alt+R and Alt+D mnemonics. Open project is first in the toolbar; the
wrapping status line sits below it instead of competing for horizontal space.

| Action | Shortcut |
|---|---|
| Switch native/page focus | F6 or Shift+F6 |
| Open project | Ctrl+O |
| Solve | Ctrl+Enter |
| Cancel analysis | Ctrl+Shift+Escape |
| Open result project | Ctrl+Shift+Enter |
| Retry view | Ctrl+R |

Shortcuts use GTK button accelerators and retain button sensitivity and existing
operation guards. Tooltips expose their key combinations.

## Acceptance

Run the opt-in integration smoke in an active desktop, **one viewer at a time**:

```bash
GDK_BACKEND=x11 GDK_SCALE=1 DOTNET_ROLL_FORWARD=Major \
  python3 scripts/start-gtk-viewer.py /absolute/path/to/dotnet \
  /tmp/two-step-result.pma \
  "$PWD/prototypes/VtkJsStreaming/src/PrePoMax.Render.Server/bin/Release/net8.0/PrePoMax.Render.Server.dll" \
  "$PWD/prototypes/VtkJsStreaming/src/client/dist" --smoke-accessibility
```

Repeat with `GDK_SCALE=2`. Both passed locally with a stable rendered scene and
renderer-listener shutdown. The test checks key accessible names, native focus
advancement using GTK's Tab-direction traversal, disabled Solve acceleration,
Ctrl+O opening the real native chooser, chooser cancellation, the native scale
factor and positive control allocations. It yields to GTK after requesting
focus; competing windows can invalidate this focus-dependent test. Accelerators
are invoked through GTK, not physical key injection.

WebKit process termination/retry regression also passes after the layout change.
These checks are separate from the unchanged 3,805 portable assertion baseline.

This is groundwork, not complete accessibility or visual DPI acceptance. An
actual screen reader, all-control Tab/Shift+Tab order, complete Tab navigation across
the WebView boundary and within the page, focus restoration after successful project switching, text clipping,
fractional/mixed-monitor scaling and physical keyboard operation remain to be
qualified. The default Wayland session did not honor the requested `GDK_SCALE`
in this experiment; the deterministic scale assertions above use X11. PEND-006
is now complete with GTK selected. Wayland clipboard, screen-reader and broader
display-scale qualification remain support-matrix work.

## Explicit native/page focus switching

F6 and Shift+F6 now switch between native project controls and the embedded
scene. Entry focuses the page's enabled Reset camera button. Return restores
the previous native widget when available; if disabled, it uses an available
Open project, Cancel analysis or Retry view control. Switching is guarded while
opening a project or showing a native chooser, and does not enter a failed view.
The scene tooltip and accessible description expose this shortcut.

Use the command above with `--smoke-focus` instead of `--smoke-accessibility`.
The X11 smoke passes after a stable scene: it emits GTK key-press signals for F6
and Shift+F6, asynchronously reads the actual WebKit document's focused element,
and verifies native focus restoration and renderer shutdown. This exercises the
host key handler and DOM boundary, not physical key delivery, all page controls,
or a screen reader. The existing X11 accessible-name, native Tab, shortcut and
dialog smoke also passes, along with Python compilation and whitespace checks.
Normal Tab order within the page remains to be qualified.

A default Wayland run of `--smoke-focus` timed out waiting for the initial stable
frame, before reaching the focus assertions. No Wayland focus acceptance is
claimed; investigate rendering/startup there before repeating this check.

### Wayland startup follow-up (2026-09-23)

The first instrumented Wayland run again timed out: the page was mapped and
finished loading, with 79 successful bridge evaluations, one client-ready
message and one bootstrap delivery. No native load/process failure occurred.
This localizes that occurrence beyond page loading and bootstrap dispatch;
it does not establish whether scene loading or frame scheduling stalled.

After adding bounded page-state diagnostics, a Wayland focus run passed, followed
by three sequential passing repeats, each with renderer shutdown verified.
This is conditional Wayland focus evidence, not a demonstrated fix for the
intermittent startup failure. No rendering backend override or security setting
was changed. Screen-reader, physical-key and full Tab-order acceptance remain open.

`--smoke-timeout SECONDS` now uses a monotonic deadline (default 90), replacing
poll-count timing. On timeout it reports backend, load stage, mapped/opening/
failure flags, evaluation/ready/bootstrap counts, and page-state booleans for
visibility, a first animation callback, JavaScript errors, an error banner and
a canvas. It excludes URLs, page text, exception messages, paths and credentials.
Use `--smoke-focus --smoke-timeout 20` for short sequential reproductions;
retain the default deadline for slower machines. A first animation callback or
canvas presence alone is not proof that the engineering scene rendered.

Wayland WebKit termination/Retry view also passed with the diagnostics enabled,
including same-scene restoration and listener shutdown. A deliberate X11
`--smoke-timeout 0.01` run exited nonzero with pre-load stage diagnostics, as
expected. Python compilation and whitespace checks pass. The portable numeric
suite was not rerun because this change affects only the GTK adapter.

## Cancelled chooser focus restoration

Cancelling Open project now explicitly presents the parent window and restores
its previous visible, sensitive control. When opened from the WebView, GTK
returns focus to that view without refocusing a different DOM element. Successful
project opening retains its existing load workflow. The remembered chooser
focus is cleared on response.

`--smoke-focus` now opens the real native chooser from the page camera button,
checks that F6 switching is guarded while the chooser is open, cancels it, and
asynchronously verifies the camera button remains the focused DOM element. It
then checks Shift+F6 returns to the original native button and verifies native
chooser cancellation from that button too. It uses GTK response/key signals;
physical key delivery, screen-reader announcements and focus after successful
project switching remain separate acceptance items.

The extended chooser-focus smoke passed on X11 with renderer shutdown verified.
Python compilation and whitespace checks pass. The Wayland run timed out before
reaching the focus assertions: load finished, 108 bridge evaluations, one ready
message and one bootstrap, with visible page, canvas and first animation callback
present, and no reported JavaScript error or error banner. This confirms the
intermittent startup issue persists; it does not qualify Wayland chooser focus
or prove completion of scene loading/rendering.

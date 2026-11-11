# GTK native scene-summary clipboard

Implemented 2026-09-23. The existing client **Copy summary** button now uses GTK's
native clipboard when hosted by the GTK viewer. A top-frame injected capture
handler intercepts only that button and sends a no-payload copy request through
the existing bounded message queue. Other hosts retain their browser clipboard
implementation. The GTK adapter accepts it only while its expected page and
current scene are available; native navigation-generation checks still apply.

The copied JSON is constructed from native host state, not arbitrary web-page
text. It contains a schema identifier, scene ID, actor/point/triangle counts,
the applied result field/frame and the applied displacement scale. It excludes
session credentials, project paths and raw diagnostic payloads. A rejected field
selection or an unapplied scale edit does not change the recorded applied state.
The native status line confirms the copy. There is no new clipboard-read bridge
available to the page.

## Acceptance

Use the normal GTK arguments with:

```bash
GDK_BACKEND=x11 DOTNET_ROLL_FORWARD=Major python3 scripts/start-gtk-viewer.py \
  /absolute/path/to/dotnet /tmp/two-step-result.pma \
  "$PWD/prototypes/VtkJsStreaming/src/PrePoMax.Render.Server/bin/Release/net8.0/PrePoMax.Render.Server.dll" \
  "$PWD/prototypes/VtkJsStreaming/src/client/dist" \
  --smoke-clipboard --smoke-field DISP/U1 --deformation-scale 100
```

This activates the actual client button after a stable selected result, writes to
a per-process private GTK/X11 selection, reads it asynchronously and checks the
summary, field and scale. It never reads or overwrites the desktop CLIPBOARD.
The native clipboard round trip and renderer shutdown passed locally. WebKit
crash/retry regression also passes with the injected button adapter present.
Python compilation and whitespace checks pass. These are opt-in integration
checks, separate from the 3,805 portable assertion baseline.

The first attempt using the workstation's default Wayland backend could not
round-trip a private selection. The smoke now explicitly requires X11 and rejects
other backends before launching a companion. Interactive usage writes to the
standard GTK CLIPBOARD, with a clipboard-manager persistence request. Wayland
copy/paste and persistence after application exit are not qualified by this test.
No claim of general clipboard interoperability, formatted data or clipboard read
support is made. The selector-specific adapter must be updated if the client's
Copy summary button ID changes; the smoke exercises that exact integration.

Next: Wayland clipboard persistence, screen-reader and mixed-monitor DPI
qualification under the selected GTK host's support matrix.

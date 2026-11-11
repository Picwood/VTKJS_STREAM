# Linux Chromium viewer candidate

Implemented and tested 2026-09-22. `scripts/start-linux-viewer.mjs` opens a real
Chromium window displaying a supported editable project. Unlike the earlier test
harness, it remains open for interaction and owns the renderer until the window
closes. This is an experimental viewer, not the complete PrePoMax desktop or a
final shell choice.

The launcher uses the new `PrePoMax.ViewerHost` companion from the portable
solution, not a characterization executable. The companion calls the production
application services, publishes the model and performs the trusted bootstrap.
Bounded private pipe messages carry credentials; stdout from the companion is
never a user log. The companion refuses terminal I/O and returns generic errors
instead of echoing sensitive messages. EOF disposes its renderer process.

The browser adapter checks native frame/navigation metadata, serializes readiness
messages and rechecks navigation before delivering credentials. It blocks
external requests, other document navigation, subframes and popups. It uses a
fresh browser context rather than a persistent profile. Browser closure stops
the companion; shutdown has a deadline and owned-process-group fallback. A
companion exit also closes the browser. This spike uses Chromium browser chrome,
not a custom native application frame.

## Run

Prerequisites: .NET 10 SDK, renderer runtime (.NET 8 or explicitly tested
roll-forward), Node, client dependencies from `npm ci`, and Playwright Chromium
from `npx playwright install chromium` in the client directory. A desktop display
is required for the interactive launcher.

From the repository root:

```bash
dotnet build portable/PrePoMax.Core.slnx -c Release
dotnet build prototypes/VtkJsStreaming/src/PrePoMax.Render.Server -c Release
npm run build --prefix prototypes/VtkJsStreaming/src/client
node scripts/start-linux-viewer.mjs /absolute/path/to/dotnet /absolute/path/to/model.pma \
  "$PWD/prototypes/VtkJsStreaming/src/PrePoMax.Render.Server/bin/Release/net8.0/PrePoMax.Render.Server.dll" \
  "$PWD/prototypes/VtkJsStreaming/src/client/dist"
```

Close the window or press Ctrl+C in its launching terminal to stop. Optional
`PREPOMAX_VIEWER_SOFTWARE=1` forces SwiftShader for troubleshooting. The default
launcher does not force software rendering, but physical GPU support is not yet
qualified. Existing project schema restrictions apply, and the view currently
shows undeformed mesh geometry only.

## Candidate evidence and open decisions

`scripts/test-linux-viewer.mjs` accepts the same four arguments. It defaults to
headless Chromium; `PREPOMAX_VIEWER_HEADED=1` tests the actual windowed path.
Both modes passed locally with the multipart fixture: trusted bootstrap, two
actors, 16 points, 24 triangles, reload, camera controls, resize, external request
blocking, window closure and renderer listener shutdown. The headed test used the
workstation's existing X display and SwiftShader. A screenshot was captured at
`/tmp/prepomax-viewer-headed.png` (temporary local artifact).

Local environment: Node 22.16.0, Playwright 1.62.1, Chromium 151.0.7922.34, .NET
10.0.12 with `DOTNET_ROLL_FORWARD=Major` for the unchanged net8.0 renderer target.
The solution build passed without warnings. These integration checks are separate
from the portable characterization count.

| PEND-006 concern | Chromium spike evidence | Remaining work |
|---|---|---|
| Renderer/domain connection | Editable project to real window; reload and stable-frame acknowledgement | Result fields, editing, mesh/solve UI |
| Input and resize | Camera controls and viewport resize exercised | Keyboard workflow, DPI, selection and clipboard |
| Ownership | Window/companion/renderer orderly shutdown tested | Crash recovery and parent-death handling |
| Navigation and bootstrap | Trusted source checks, private pipes, external requests blocked | Independent security review and hostile-frame coverage |
| Dialogs and accessibility | Not measured | Native file dialogs, accessibility tree and assistive input |
| Distribution and updates | Existing Playwright browser install used | Runtime size, licensing/staging, updates and uninstall |
| Graphics | Headed/headless software WebGL passes | Native physical GPU/driver matrix and manual visual review |

This comparison input is superseded by the accepted
[shell decision](LINUX_SHELL_CANDIDATES.md): GTK/WebKitGTK is the production
Linux host, while Chromium/Playwright remains the automation reference.

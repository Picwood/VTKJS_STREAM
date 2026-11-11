# Portable renderer process host

Implemented 2026-09-22. `RendererHost` in PrePoMax.Application owns a renderer
child process, host credential, HTTP client and render session. `StartAsync`
starts a supplied server assembly using a supplied dotnet executable, forces
Production mode and requests an ephemeral loopback port. Readiness must identify
an HTTP endpoint at 127.0.0.1 with no credentials, path, query or fragment.
Startup is cancellable and limited to 30 seconds.

`PublishAsync` exports the supported editable project's undeformed mesh, uploads
it using the existing authenticated host endpoint, creates/reuses a render
session and reads the authenticated manifest back. It verifies scene identity
and actor count before returning non-secret scene metadata. Calls must be serial;
model export remains synchronous, so GUI callers should dispatch it off the UI
thread. Temporary scene files are removed on completion or failure.

Credentials use child environment and HTTP headers, never command arguments,
URLs, files or console output. HTTP redirects and proxy use are disabled. Child
output is drained with bounded buffers rather than echoed. `DisposeAsync` asks
the renderer to shut down, then kills the child process tree if it has not exited
within the shutdown deadline. The caller must dispose the host.

The CLI provides a one-shot integration probe:

```bash
dotnet portable/PrePoMax.Cli/bin/Release/net10.0/PrePoMax.Cli.dll render-check \
  model.pma /absolute/path/to/dotnet \
  /absolute/path/to/PrePoMax.Render.Server.dll
```

This starts, publishes, verifies and disposes the renderer, printing only scene
ID and actor/point/triangle counts. It does not launch a browser or leave a server
running. Errors use the existing CLI failure/cancellation exit codes.

Run real-process acceptance from the repository root:

```bash
python3 scripts/test-renderer-host.py
```

The script builds the CLI, fixture generator, host characterization executable
and original renderer server. It checks a two-part project (2 actors, 16 points,
24 triangles), repeated scene replacement, rejection of unauthenticated session,
upload and manifest requests, pre-cancelled publication, listener shutdown,
unchanged source bytes, CLI output and startup/publication failure paths.

Local validation used .NET SDK 10.0.401 and runtime 10.0.12. The renderer retains
its net8.0 target; this workstation has no .NET 8 runtime, so acceptance explicitly
used `DOTNET_ROLL_FORWARD=Major` and `DOTNET_PROCESSOR_COUNT=2`. This does not
qualify deployment on the renderer's intended .NET 8 runtime.

The [browser bootstrap boundary](LINUX_BROWSER_BOOTSTRAP.md) now provides the
trusted adapter handshake. The selected GTK host supplies WebView integration,
interactive input/result workflows, packaging and recorded DRM-backed WebGL
acceptance. This milestone itself establishes process and authenticated transport
ownership; later documents close PEND-006.

Real-process acceptance passed locally, including the authenticated and negative
paths above. These opt-in checks are separate from the portable assertion total.

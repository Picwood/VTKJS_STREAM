# Portable embedded-browser bootstrap boundary

Implemented 2026-09-22. `RendererHost.TryBootstrapBrowser` handles the existing
client's `prepomax.client.ready-v1` message and delivers
`prepomax.session.bootstrap-v1` JSON through a trusted adapter callback. The
message includes session ID, session token and the currently verified scene ID.
It is available only after successful scene publication and manifest verification.
Repeated readiness reuses the existing session. Beginning another publication
invalidates the retained bootstrap until the replacement is verified; disposal
also clears it.

`BrowserAddress` is the renderer's private loopback URL with
`?embedded=1&surfaceSelection=1` and contains no credentials. The second flag
asks the shared client to return validated rendered triangles for area selection
instead of the node/element frustum result used by other hosts. Readiness
requires that exact origin, a root or `index.html` path, exactly that query and no fragment. Subframes,
malformed/oversized messages, unknown message types and extra properties are
rejected before the callback runs.

The adapter supplies the source URI and top-level status from its native WebView
event, never from page-controlled JSON. It must serialize navigation, publication
and message handling, recheck navigation before delivery and post to the same
frame. The callback is a credential boundary: do not log it, persist it, place it
in a URL, or forward it through an untrusted transport. This API does not replace
the platform adapter's navigation controls.

Validation uses the real renderer process through
`python3 scripts/test-renderer-host.py`. It covers pre-publication readiness,
wrong origins/paths/query/fragment, subframes, malformed messages, repeated
readiness, replacement-scene identity, and session adoption followed by a
cookie-authenticated manifest request. Existing process lifecycle and CLI tests
run alongside these checks. The browser message structure matches the client's
`sessionBootstrap.ts` contract. Local validation uses the same explicit .NET 10
roll-forward described in [renderer hosting](LINUX_RENDERER_HOST.md).

The handshake API itself launches no browser. Subsequent
[real-browser acceptance](LINUX_BROWSER_RENDER_EVIDENCE.md) exercises it through
a test adapter, including stable frame acknowledgement, resize and reload. The
selected GTK host now supplies the production Linux adapter, trusted navigation
metadata, native bridge transport and stable-frame validation. PEND-006 is
complete; broader DPI and GPU-matrix qualification remain separate.

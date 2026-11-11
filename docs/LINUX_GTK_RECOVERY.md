# GTK renderer-view recovery

Implemented and validated 2026-09-23. The GTK shell now handles WebKit process
termination, page-load failure and startup deadline expiry with a visible message and **Retry view**
action. Retry reloads the trusted loopback page and runs the existing bootstrap
handshake against the current scene. It does not restart the renderer server,
replace the project, or start/cancel a solver job.

The failure path invalidates pending browser evaluations by advancing the
navigation generation, clears deferred readiness, and pauses browser polling.
Native job-status polling continues. A retry or another approved document load
resumes browser polling; a stable-scene acknowledgement clears the recovery
message. Navigation-cancellation errors are ignored so intentional reloads do
not report a failure. Recovery is user-triggered rather than an automatic loop.

## Acceptance

The GTK launcher accepts `--smoke-recovery`. After the first eligible stable
frame it terminates the actual WebKit process, waits for the native termination
signal, activates Retry view and requires a new stable-frame acknowledgement for
the same scene ID and geometry counts. Combining it with `--smoke-field DISP/U1
--deformation-scale 100` tests recovery of an already-selected deformed result.

Using the two-step result archive from [contour acceptance](LINUX_RESULT_CONTOURS.md):

```bash
python3 scripts/start-gtk-viewer.py /absolute/path/to/dotnet /tmp/two-step-result.pma \
  "$PWD/prototypes/VtkJsStreaming/src/PrePoMax.Render.Server/bin/Release/net8.0/PrePoMax.Render.Server.dll" \
  "$PWD/prototypes/VtkJsStreaming/src/client/dist" \
  --smoke-recovery --smoke-field DISP/U1 --deformation-scale 100
```

The real-process recovery check passed locally, including renderer listener
shutdown on exit. Normal initial-render/resize/reload acceptance also passes.
Python compilation and whitespace checks pass. No portable numerical code changed;
these opt-in GTK checks are separate from the 3,805 portable assertion baseline.
The test uses WebKitGTK and the existing net8.0 server with explicit .NET 10
runtime roll-forward on this workstation.

## Scope

This recovers the browser process while the companion and renderer server remain
healthy and the retained session is valid. Companion/server crash recovery,
session renewal, recovery during active solver execution, and broad network-error
injection have not been qualified. The page-load-failure handler is implemented;
automated fault injection covers WebKit process termination and a withheld startup bootstrap.
Clipboard, accessibility, DPI, GPU and distribution gates remain open.

## Stalled startup recovery

A page navigation now has a monotonic 60-second deadline to acknowledge the
applied scene. `--view-timeout SECONDS` adjusts this for slower or larger scenes;
it must be finite and positive. Successful scene application or an explicit
load/process failure clears the deadline. Expiry uses the same native failure
message and Retry view action, invalidates pending callbacks and pauses browser
polling. Normal use requires the user to retry; it never loops automatically.
The deadline covers page loading and scene application, not companion startup,
project preparation or subsequent field updates.

`--smoke-startup-recovery --view-timeout 8 --smoke-timeout 30` deliberately
withholds the first bootstrap delivery. It requires deadline expiry and an
enabled Retry view button, activates that button, then checks stable topology
and the same scene ID after a fresh handshake. This tests recovery from a silent
startup stall; it does not reproduce the unknown cause of the intermittent
Wayland post-bootstrap timeout. Use the usual launcher arguments above with
`GDK_BACKEND=wayland` or `GDK_BACKEND=x11` and, where needed,
`DOTNET_ROLL_FORWARD=Major`.

Five sequential ordinary Wayland focus runs passed during this investigation.
The intermittent failure was not reproduced in those runs; no root-cause fix
is claimed. The watchdog closes the previously unbounded interactive wait.

Injected startup-stall acceptance passed on both Wayland and X11, including
renderer-listener shutdown. Wayland real WebKit-process termination/retry also
passed with the watchdog enabled. Python compilation and whitespace checks pass;
no portable numeric sources changed.

## Confirmed bootstrap handoff

The GTK adapter now finishes the WebKit evaluation used to deliver bootstrap
and requires a positive listener count. A JavaScript evaluation failure or zero
listeners immediately exposes Retry view instead of silently waiting for the
startup deadline. Completion from an obsolete navigation is ignored. Diagnostic
`bootstrap` counts dispatch attempts; `confirmed` counts successful synchronous
page-listener handoffs. Neither count proves asynchronous scene rendering.
No bootstrap payload, URI or exception text is logged.

`--smoke-bootstrap-recovery --smoke-timeout 30` injects one rejected JavaScript
handoff, requires the native failure/retry path, then requires a confirmed
handoff and stable topology for the same retained scene after retry.

Three ordinary Wayland focus runs passed in this follow-up. The injected-handoff
Wayland run exercised retry and confirmed the second handoff, but then timed out:
the page reported a manifest, geometry and transferred data, visibility and an
initial animation callback, with no error banner or reported JavaScript error.
It did not acknowledge a stable scene. The remaining intermittent issue is thus
observed after the successful handoff and receipt of geometry; frame scheduling
or scene completion still needs investigation. This is not a Wayland recovery
pass or a root-cause fix.

Timeout diagnostics now include those bounded booleans plus a status index:
0 means session adoption, 1 manifest fetching, 2 load failure, and -1 any other
status. The adapter never prints the underlying status or diagnostics text.

X11 injected-handoff recovery passed, including confirmation, same-scene rendering
and listener shutdown. X11 real WebKit termination/retry regression also passed.
Python compilation and whitespace checks pass; portable numeric sources are
unchanged.

## Bounded bridge notification handling (2026-09-24)

The previous 32-entry FIFO accepted all short client notifications, including
camera/selection updates that GTK never consumed. A burst could occupy all slots
and drop a later ready or scene-applied message. The adapter now accepts only
client-ready, scene-applied and copy-summary requests and retains the latest
pending message of each type. This bounds storage to three strings of at most
1,024 characters each, without allowing ignored notifications or repeated copy
requests to displace lifecycle messages. Repeated copy requests before the next
poll intentionally collapse to one copy of the current native scene summary.
Future native actions must be explicitly added to both the filter and consumer.

`--smoke-bridge-pressure --smoke-timeout 25` injects 64 ignored camera updates and
64 duplicate copies before each real post. It requires a stable scene, no rejected
messages, exactly one ready/dispatch/confirmed-bootstrap exchange, and renderer
shutdown. This is opt-in fault injection in the GTK adapter, not a client change.
The initial pressure smoke passed on X11 and Wayland; X11 also passed the
strengthened exact-one-handshake assertion. A subsequent Wayland run timed out. This fixes a reproducible queue
saturation risk; it does not establish that queue saturation caused the earlier
intermittent Wayland stall. Frame completion and physical-GPU acceptance remain
open. No synthetic scene-applied message is generated to bypass rendering.

The subsequent Wayland timeout reported `resourcesComplete=true`,
`sceneApplied=false`, `dropped=false`, geometry and manifest present, and exactly
one ready/dispatch/confirmed handoff. This occurrence was not a lost queue
acknowledgement: the page had not emitted scene-applied. The next investigation
is the client completion/frame wait after resources finish. X11 private-selection
clipboard regression, Python compilation and whitespace checks also pass.

## Bounded client frame completion (2026-09-24)

The client previously awaited two animation callbacks without a cancellation
path or deadline. `stableFrame.ts` now rejects with `frame-timeout` after ten
seconds without completion, cancels any pending callback and clears its timer
and abort listener. Initial scene loading passes its AbortSignal so replacement
or unload cancels an obsolete wait. Other existing frame waits use the same
bounded helper. Success still requires two actual callbacks; elapsed time never
produces a scene-applied acknowledgement. This is a scheduling milestone, not
physical presentation or GPU qualification.

The GTK adapter recognizes the exact safe `frame-timeout` diagnostic code and
enables Retry view immediately through the existing failure path. It does not
log page error text. Normal users choose whether to retry. The broader native
startup watchdog remains for failures before the frame wait.

`--smoke-frame-recovery --smoke-timeout 35` suppresses animation callbacks after
resources complete on the first page only. The check requires the client's
frame rejection, native Retry view, and real stable topology for the same scene
after reload. It cannot pass merely because a deadline elapsed. X11 and Wayland acceptance
passed with renderer shutdown verified.

All 88 client tests across 26 files pass, including five focused frame-wait tests
covering two-callback success, stalls before/after the first callback, late
callbacks after rejection, cancellation between callbacks and pre-cancellation.
The production client build, Python compilation and whitespace checks pass.
This eliminates the unbounded wait and gives the user recovery; it does not
establish why Wayland intermittently stops delivering frame completion.

## Scheduling cause resolved for the reproduced occlusion case

The [2026-09-24 investigation](LINUX_WAYLAND_FRAME_SCHEDULING.md) supersedes the
earlier unresolved-cause notes above: GTK 3 native occlusion was not reflected
in page visibility. Frame and startup deadlines now pause while fully covered,
and real cover/uncover acceptance completes the same page without retry.

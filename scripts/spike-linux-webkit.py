#!/usr/bin/env python3
"""Experimental GTK/WebKit shell; uses the private-pipe acceptance host, not a production package."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import threading
import sys
import gi

gi.require_version('Gtk', '3.0')
gi.require_version('WebKit2', '4.1')
from gi.repository import Gtk, WebKit2, GLib

parser = argparse.ArgumentParser()
parser.add_argument('dotnet')
parser.add_argument('project')
parser.add_argument('server')
parser.add_argument('client_dist')
parser.add_argument('--timeout', type=int, default=60)
parser.add_argument('--smoke', action='store_true', help='Exit after verifying rendered scene; fail after 60 seconds')
args = parser.parse_args()
repo = Path(__file__).resolve().parent.parent
host = subprocess.Popen([args.dotnet, str(repo / 'portable/tests/BrowserHost.Characterization/bin/Release/net10.0/BrowserHost.Characterization.dll'),
                         args.project, args.dotnet, args.server],
                        env={**os.environ, 'PREPOMAX_RENDER_CLIENT_ROOT': str(Path(args.client_dist).resolve())},
                        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True)
# Pipe responses contain credentials. Never log them or write them to disk.
result = 1
window = Gtk.Window(title='PrePoMax — WebKitGTK spike')
window.set_default_size(1200, 800)
manager = WebKit2.UserContentManager()
manager.add_script(WebKit2.UserScript.new('''(() => {
  window.__nativeErrors = [];
  window.addEventListener('error', e => window.__nativeErrors.push(String(e.message).slice(0, 200)));
  window.addEventListener('unhandledrejection', e => window.__nativeErrors.push(String(e.reason).slice(0, 200)));
  const callbacks = [];
  window.chrome = {webview: {
    addEventListener: (type, cb) => { if (type === 'message') callbacks.push(cb); },
    postMessage: message => { try { if (JSON.parse(message).type === 'prepomax.client.ready-v1') window.__nativeReady = true; } catch {} }
  }};
  window.__prepomaxDeliver = value => callbacks.forEach(cb => cb({data: value}));
  window.__nativeListenerCount = () => callbacks.length;
})();''', WebKit2.UserContentInjectedFrames.TOP_FRAME, WebKit2.UserScriptInjectionTime.START, None, None))
view = WebKit2.WebView.new_with_user_content_manager(manager)
view.get_settings().set_enable_webgl(True)
window.add(view)
address = None
scene = None
generation = 0
closing = False

def close(*_):
    global closing
    if closing:
        return
    closing = True
    Gtk.main_quit()

window.connect('destroy', close)
view.connect('create', lambda *_: None)

def policy(webview, decision, kind):
    if kind in (WebKit2.PolicyDecisionType.NAVIGATION_ACTION, WebKit2.PolicyDecisionType.NEW_WINDOW_ACTION):
        uri = decision.get_navigation_action().get_request().get_uri()
        if kind == WebKit2.PolicyDecisionType.NEW_WINDOW_ACTION or uri != address:
            decision.ignore()
            return True
    return False

view.connect('decide-policy', policy)

def bootstrap_done(json_text, epoch):
    if args.smoke: print('Native bootstrap delivered:', bool(json_text), flush=True)
    if not closing and epoch == generation and view.get_uri() == address:
        # evaluate_javascript executes in the current main frame; credentials never enter a URL.
        view.evaluate_javascript('window.__prepomaxDeliver(' + json_text + ')', -1, None, None, None, None, None)
    return False

pipe_lock = threading.Lock()
def bootstrap(epoch):
    try:
        with pipe_lock:
            host.stdin.write(json.dumps({'source': address, 'topLevel': True,
                                        'message': '{"type":"prepomax.client.ready-v1"}'}) + '\n')
            host.stdin.flush()
            response = json.loads(host.stdout.readline())['bootstrap']
        if response is not None:
            GLib.idle_add(bootstrap_done, response, epoch)
    except Exception:
        GLib.idle_add(close)

def loaded(webview, event):
    global generation
    if args.smoke:
        print("Native load:", event.value_nick, flush=True)
    if event == WebKit2.LoadEvent.STARTED:
        generation += 1
    if event == WebKit2.LoadEvent.FINISHED and webview.get_uri() == address:
        threading.Thread(target=bootstrap, args=(generation,), daemon=True).start()

view.connect('load-changed', loaded)

def started(value):
    global address, scene
    address, scene = value['address'], value['scene']
    view.load_uri(address)
    window.show_all()
    return False

def startup():
    try:
        value = json.loads(host.stdout.readline())
        GLib.idle_add(started, value)
    except Exception:
        GLib.idle_add(close)

threading.Thread(target=startup, daemon=True).start()

def inspected(webview, task, _):
    global result
    try:
        value = webview.evaluate_javascript_finish(task).to_string()
        observation = json.loads(value)
        if not getattr(inspected, 'state_reported', False):
            print('Client ready/listeners:', observation.get('ready'), observation.get('listeners'), flush=True)
            inspected.state_reported = True
        errors = observation.get('errors', [])
        if observation.get('error'): errors.append(observation['error'])
        if errors and not getattr(inspected, 'errors_reported', False):
            print('Client startup error:', errors[0], flush=True)
            inspected.errors_reported = True
        data = json.loads(observation.get('diagnostics') or '{}')
        renderer = data.get('renderer', {})
        if args.smoke and data.get('sceneId') and not getattr(inspected, 'reported', False):
            print('Client diagnostics available; WebGL2:', renderer.get('webgl2Available'), flush=True)
            inspected.reported = True
        if scene and data.get('timing', {}).get('firstStableFrameMs') is not None and data.get('sceneId') == scene['SceneId'] and renderer.get('pipelineChunks') == 2:
            if renderer.get('pipelinePoints') != 16 or renderer.get('pipelinePolys') != 24:
                return
            print('PASS: native WebKitGTK loaded published scene: 2 pipelines, 16 points, 24 triangles', flush=True)
            result = 0
            close()
    except Exception as error:
        if not getattr(inspected, 'failed', False):
            print('Native script evaluation failed:', type(error).__name__, flush=True)
            inspected.failed = True

def poll():
    if closing:
        return False
    if address and view.get_uri() == address:
        view.evaluate_javascript("JSON.stringify({diagnostics: document.querySelector('#diagnostics')?.textContent, errors: window.__nativeErrors || [], error: document.querySelector('#error')?.textContent, ready: window.__nativeReady, listeners: window.__nativeListenerCount?.()})", -1, None, None, None, inspected, None)
    return True

if args.smoke:
    GLib.timeout_add(500, poll)
GLib.timeout_add_seconds(args.timeout, lambda: (close(), False)[1] if args.smoke or address is None else False)
try:
    Gtk.main()
finally:
    closing = True
    host.stdin.close()
    try:
        host.wait(timeout=10)
    except subprocess.TimeoutExpired:
        host.terminate()
        host.wait(timeout=5)
    window.destroy()
if args.smoke and result:
    print('FAIL: native WebKitGTK did not produce the expected scene within 60 seconds', file=sys.stderr)
sys.exit(result if args.smoke else 0)

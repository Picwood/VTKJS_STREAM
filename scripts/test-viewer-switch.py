#!/usr/bin/env python3
"""Companion project switching regression; credential-bearing replies stay in memory."""
import json
import os
from pathlib import Path
import select
import signal
import subprocess
import sys

dotnet, first, second, server = sys.argv[1:]
repo = Path(__file__).resolve().parent.parent
original = [Path(p).read_bytes() for p in (first, second)]
process = subprocess.Popen([dotnet, str(repo / 'portable/PrePoMax.ViewerHost/bin/Release/net10.0/PrePoMax.ViewerHost.dll'),
    first, dotnet, server], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
    text=True, start_new_session=True)
def exchange(message=None):
    if message is not None:
        process.stdin.write(json.dumps(message) + '\n')
        process.stdin.flush()
    assert select.select([process.stdout], [], [], 45)[0], 'Companion timed out'
    line = process.stdout.readline(16384)
    assert line.endswith('\n'), 'Companion response incomplete'
    return json.loads(line)
try:
    initial = exchange()
    ready = {'source': initial['address'], 'topLevel': True, 'message': '{"type":"prepomax.client.ready-v1"}'}
    before = json.loads(exchange(ready)['bootstrap'])
    rejected = exchange({'command': 'open', 'path': str(Path(second).parent / '__missing_project__.pma')})
    assert not rejected['ok']
    assert json.loads(exchange(ready)['bootstrap']) == before, 'Invalid path changed bootstrap'
    opened = exchange({'command': 'open', 'path': second})
    assert opened['ok'] and opened['scene']['SceneId'] != initial['scene']['SceneId']
    assert opened['scene']['Actors'] == 1 and opened['scene']['Points'] == 8 and opened['scene']['Triangles'] == 12
    after = json.loads(exchange(ready)['bootstrap'])
    assert after['sceneId'] == opened['scene']['SceneId']
    assert [Path(p).read_bytes() for p in (first, second)] == original
finally:
    process.stdin.close()
    try:
        process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        os.killpg(process.pid, signal.SIGKILL)
        process.wait()
assert process.returncode == 0
print('PASS: invalid-open recovery, project switching, current-scene bootstrap and source preservation')

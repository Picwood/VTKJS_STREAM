#!/usr/bin/env python3
"""Real renderer field/frame transport; requires the two-step result fixture project."""
import json
import math
import os
from pathlib import Path
import select
import signal
import struct
import subprocess
import sys
import urllib.request
import zipfile

dotnet, project, server = sys.argv[1:]
repo = Path(__file__).resolve().parent.parent
before = Path(project).read_bytes()
with zipfile.ZipFile(project) as archive:
    binary = 'results.bin' in archive.namelist()
process = subprocess.Popen([dotnet, str(repo / 'portable/PrePoMax.ViewerHost/bin/Release/net10.0/PrePoMax.ViewerHost.dll'),
    project, dotnet, server], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
    text=True, start_new_session=True)
def exchange(value=None):
    if value is not None:
        process.stdin.write(json.dumps(value) + '\n')
        process.stdin.flush()
    assert select.select([process.stdout], [], [], 45)[0]
    return json.loads(process.stdout.readline(65536))
try:
    initial = exchange()
    ready = {'source': initial['address'], 'topLevel': True, 'message': '{"type":"prepomax.client.ready-v1"}'}
    listed = exchange({'command': 'fields'})
    fields = listed['fields']
    assert listed['resultAccess']['Retained'] and not listed['resultAccess']['CacheHit']
    assert listed['resultAccess']['Indexed'] == binary
    warm = exchange({'command': 'fields'})['resultAccess']
    assert warm['CacheHit'] and warm['DecodedFieldRecords'] == 0
    displacement = [f for f in fields if f['Name'] == 'DISP' and f['Component'] == 'U1']
    assert len(displacement) == 2
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    for field in displacement:
        reply = exchange({'command': 'field', 'field': field})
        assert reply['ok']
        assert reply['resultAccess']['CacheHit'] and reply['resultAccess']['Load']['DecodeMilliseconds'] == 0
        assert reply['resultAccess']['DecodedFieldRecords'] == (1 if binary else 0)
        assert reply['publication']['SceneBytes'] > 0
        assert all(reply['publication'][key] >= 0 for key in ('ExportMilliseconds', 'UploadMilliseconds', 'SessionMilliseconds', 'TotalMilliseconds'))
        session = json.loads(exchange(ready)['bootstrap'])
        base = initial['address'].split('?')[0].rstrip('/')
        def get(path):
            request = urllib.request.Request(base + path, headers={'X-PrePoMax-Session': session['accessToken']})
            with opener.open(request, timeout=10) as response:
                return response.read()
        manifest = json.loads(get('/api/v1/scenes/' + session['sceneId'] + '/manifest'))
        assert manifest['field']['component'] == 'U1' and 'DISP' in manifest['field']['name']
        assert 'step ' + str(field['Step']) in manifest['field']['name']
        expected = .001 * field['Step']
        assert math.isclose(manifest['field']['validRange'][1], expected, rel_tol=1e-6)
        def values(semantic, format):
            resource = next(r for r in manifest['resources'] if r['semantic'] == semantic)
            data = get(resource['uri'])
            return struct.unpack('<' + format * (len(data) // 4), data)
        ids, scalars = values('sourcePointIds', 'i'), values('pointScalars', 'f')
        assert all(math.isclose(v, expected if node in (2,3,6,7) else 0, abs_tol=1e-8) for node, v in zip(ids, scalars))
        rejected = exchange({'command': 'field', 'field': {**field, 'Component': 'unknown'}})
        assert not rejected['ok']
        assert json.loads(exchange(ready)['bootstrap']) == session, 'Rejected field invalidated existing bootstrap'
    for scale in (100, -100, 0):
        field = displacement[-1]
        assert exchange({'command': 'field', 'field': field, 'deformationScale': scale})['ok']
        session = json.loads(exchange(ready)['bootstrap'])
        deformed = json.loads(get('/api/v1/scenes/' + session['sceneId'] + '/manifest'))
        assert math.isclose(deformed['bounds'][1], 1 + scale * .001 * field['Step'], abs_tol=1e-7)
        assert math.isclose(deformed['field']['validRange'][1], .001 * field['Step'], rel_tol=1e-6)
    preserved = json.loads(exchange(ready)['bootstrap'])
    assert not exchange({'command': 'field', 'field': displacement[-1], 'deformationScale': 1000001})['ok']
    assert json.loads(exchange(ready)['bootstrap']) == preserved
    assert exchange({'command': 'field', 'field': None})['ok']
    session = json.loads(exchange(ready)['bootstrap'])
    reset = json.loads(get('/api/v1/scenes/' + session['sceneId'] + '/manifest'))
    assert reset['field']['name'] == 'Mesh geometry' and reset['field']['component'] == 'none'
    assert Path(project).read_bytes() == before
    assert not exchange({'command': 'fields'})['resultAccess']['CacheHit'], 'Geometry reset retained decoded results'
    assert exchange({'command': 'fields'})['resultAccess']['CacheHit']
    assert exchange({'command': 'open', 'path': project})['ok']
    assert not exchange({'command': 'fields'})['resultAccess']['CacheHit'], 'Project reopen retained decoded results'
    # A failed project switch must keep both the existing view and its valid result session.
    assert not exchange({'command': 'open', 'path': project + '.missing'})['ok']
    assert exchange({'command': 'fields'})['resultAccess']['CacheHit']
finally:
    process.stdin.close()
    try:
        process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        os.killpg(process.pid, signal.SIGKILL)
        process.wait()
assert process.returncode == 0
print('PASS: two-step contours, deformation, content-checked result reuse, stage timings, rejected-field recovery, reset/reopen eviction and unchanged archive')

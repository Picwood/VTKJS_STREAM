#!/usr/bin/env python3
"""Linux companion job lifecycle and native solve acceptance; no credential output."""
import json
import os
from pathlib import Path
import select
import signal
import subprocess
import sys
import tempfile
import time
import zipfile

dotnet, project, server, solver = sys.argv[1:]
repo = Path(__file__).resolve().parent.parent
original = Path(project).read_bytes()
with tempfile.TemporaryDirectory(prefix='viewer jobs ') as directory:
    root = Path(directory)
    slow = root / 'slow-solver'
    slow.write_text('#!/usr/bin/python3\nimport os,time\nopen("solver.pid","w").write(str(os.getpid()))\ntime.sleep(60)\n')
    slow.chmod(0o700)
    process = subprocess.Popen([dotnet, str(repo / 'portable/PrePoMax.ViewerHost/bin/Release/net10.0/PrePoMax.ViewerHost.dll'),
        project, dotnet, server], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
        text=True, start_new_session=True)
    def request(command=None, **kwargs):
        if command:
            process.stdin.write(json.dumps({'command': command, **kwargs}) + '\n')
            process.stdin.flush()
        assert select.select([process.stdout], [], [], 45)[0], 'Companion timed out'
        return json.loads(process.stdout.readline(16384))
    def terminal():
        deadline = time.monotonic() + 30
        while time.monotonic() < deadline:
            job = request('job-status')['job']
            if not job['active']:
                return job
            time.sleep(.05)
        raise AssertionError('Job failed to finish')
    def start_slow():
        assert request('solve', jobRoot=str(root), solver=str(slow))['ok']
        deadline = time.monotonic() + 15
        while time.monotonic() < deadline:
            job = request('job-status')['job']
            if job['jobDirectory']:
                pidfile = Path(job['jobDirectory']) / 'solver.pid'
                if pidfile.is_file() and pidfile.read_text():
                    return int(pidfile.read_text())
            time.sleep(.05)
        raise AssertionError('Slow solver never started')
    try:
        request()
        assert request('fields')['ok']
        assert request('fields')['resultAccess']['CacheHit']
        pid = start_slow()
        assert not request('solve', jobRoot=str(root), solver=solver)['ok']
        assert not request('open', path=project)['ok']
        request('cancel')
        cancelled = terminal()
        assert cancelled['status'] == 'Cancelled' and cancelled['archivePath'] is None
        assert not Path('/proc/' + str(pid)).exists(), 'Cancelled solver still exists'
        assert not request('fields')['resultAccess']['CacheHit'], 'Solver start retained the previous result session'
        assert request('solve', jobRoot=str(root), solver='/bin/false')['ok']
        assert terminal()['status'] == 'Failed'
        assert request('solve', jobRoot=str(root), solver=solver)['ok']
        solved = terminal()
        assert solved['status'] == 'Succeeded' and Path(solved['archivePath']).is_file()
        with zipfile.ZipFile(solved['archivePath']) as archive:
            for extension in ('.frd', '.dat'):
                payload = [name for name in archive.namelist() if name.endswith(extension)]
                assert len(payload) == 1 and archive.read(payload[0])
        assert request('open', path=solved['archivePath'])['ok'], 'Result archive must reopen'
        assert Path(project).read_bytes() == original
        pid = start_slow()
    finally:
        process.stdin.close()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            os.killpg(process.pid, signal.SIGKILL)
            process.wait()
    assert process.returncode == 0
    assert not Path('/proc/' + str(pid)).exists(), 'EOF did not cancel solver'
print('PASS: native solve/archive reopen, cancellation, failure/retry, active-job guards, source preservation and EOF cleanup')

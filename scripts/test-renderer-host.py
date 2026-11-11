#!/usr/bin/env python3
"""Opt-in real renderer lifecycle acceptance. Requires its ASP.NET runtime or explicit roll-forward."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

repo = Path(__file__).resolve().parent.parent
dotnet = os.environ.get('PREPOMAX_DOTNET') or shutil.which('dotnet') or str(Path.home() / '.local/share/prepomax/dotnet/dotnet')
server_project = repo / 'prototypes/VtkJsStreaming/src/PrePoMax.Render.Server'
for project in [repo / 'portable/PrePoMax.Cli', repo / 'portable/tests/MeshIO.Characterization', repo / 'portable/tests/RendererHost.Characterization', server_project]:
    subprocess.run([dotnet, 'build', str(project), '-c', 'Release', '-m:1'], cwd=repo, check=True, timeout=180)
server = server_project / 'bin/Release/net8.0/PrePoMax.Render.Server.dll'
cli = [dotnet, str(repo / 'portable/PrePoMax.Cli/bin/Release/net10.0/PrePoMax.Cli.dll')]
fixture = repo / 'portable/tests/MeshIO.Characterization/bin/Release/net10.0/MeshIO.Characterization.dll'
with tempfile.TemporaryDirectory(prefix='renderer host ') as directory:
    work = Path(directory)
    subprocess.run([dotnet, str(fixture), '--write-multipart-cube', str(work / 'cube.inp'), '1', 'en-US'], check=True)
    source = work / 'cube.inp.pma'
    before = source.read_bytes()
    subprocess.run([dotnet, str(repo / 'portable/tests/RendererHost.Characterization/bin/Release/net10.0/RendererHost.Characterization.dll'), str(source), dotnet, str(server)], check=True, timeout=60)
    completed = subprocess.run(cli + ['render-check', str(source), dotnet, str(server)], capture_output=True, text=True, timeout=60)
    if completed.returncode:
        raise SystemExit(completed.stderr)
    summary = json.loads(completed.stdout)
    assert (summary['Actors'], summary['Points'], summary['Triangles']) == (2, 16, 24)
    assert source.read_bytes() == before
    assert 'accessToken' not in completed.stdout and 'Host-Token' not in completed.stdout
    (work / 'invalid.dll').write_text('not an assembly')
    for invalid in [work / 'missing.dll', work / 'invalid.dll']:
        failure = subprocess.run(cli + ['render-check', str(source), dotnet, str(invalid)], capture_output=True, text=True, timeout=45)
        assert failure.returncode == 1
    failure = subprocess.run(cli + ['render-check', str(work / 'missing.pma'), dotnet, str(server)], capture_output=True, text=True, timeout=60)
    assert failure.returncode == 1
print('PASS: renderer startup, authenticated publication/session/manifest, CLI shutdown and failure paths')

#!/usr/bin/env python3
"""M00-T02: compare three bounded source-backed cases through Prepomac and real web HTTP.

This proves the differential mechanism, not full source-script/feature parity.
"""
import argparse
import copy
import hashlib
import http.cookiejar
import importlib.util
import json
import os
from pathlib import Path
import shutil
import signal
import socket
import subprocess
import time
import urllib.error
import urllib.parse
import urllib.request

TRIAL = Path(__file__).resolve().parents[1]
REPO = TRIAL.parents[2]
SPEC = importlib.util.spec_from_file_location("parity_comparator", TRIAL / "tests/parity/compare.py")
COMPARE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(COMPARE)


def write(path, value):
    path.write_text(json.dumps(value, indent=2, allow_nan=False) + "\n")


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


class Web:
    def __init__(self, base):
        self.base = base
        self.client = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
        self.revision = 0

    def call(self, path, body=None, expected=200, headers=None):
        request_headers = {"X-Workspace-Request": "1", "If-Match": str(self.revision)}
        if isinstance(body, dict):
            body = json.dumps(body).encode()
            request_headers["Content-Type"] = "application/json"
        request_headers.update(headers or {})
        request = urllib.request.Request(self.base + path, data=body, headers=request_headers,
                                         method="POST" if body is not None else "GET")
        try:
            response = self.client.open(request, timeout=45)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            data = response.read()
            if response.code != expected:
                raise RuntimeError(f"{path}: {response.code}, expected {expected}: {data[:500]!r}")
            return data

    def describe(self):
        state = json.loads(self.call("/api/workspace"))
        self.revision = state["revision"]
        return state


def execute(command, cwd, log, env=None, timeout=180):
    with log.open("wb") as stream:
        process = subprocess.Popen(command, cwd=cwd, env=env, stdout=stream, stderr=subprocess.STDOUT, start_new_session=True)
        try:
            code = process.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            os.killpg(process.pid, signal.SIGKILL)
            process.wait()
            raise RuntimeError(f"Command timed out; its process group was stopped. Inspect {log}.")
    if code:
        raise RuntimeError(f"Command failed ({code}); inspect {log}.")


def desktop_snapshot(mode, source, directory, executable):
    directory.mkdir()
    output = directory / "snapshot.json"
    report = directory / "run.json"
    env = dict(os.environ, PREPOMAX_PARITY_MODE=mode, PREPOMAX_PARITY_OUTPUT=str(output), PREPOMAX_GPU="system")
    command = [str(executable), "--noSettingsSave", "-f", str(source), "-u", "M_KG_S_C",
               "--script", str(TRIAL / "tests/parity/reference_snapshot.py"), "-g", "No", "-x", "Yes",
               "-w", str(directory), "--report", str(report), "--timeout", "90"]
    if shutil.which("xvfb-run"):
        command = ["xvfb-run", "-a", *command]
    execute(command, REPO, directory / "process.log", env, timeout=110)
    body = json.loads(report.read_text())
    if body["exitCode"] != 0 or body["exitStatus"] != "Ok" or body["invocation"]["settingsSaved"]:
        raise RuntimeError("Reference run status/settings-preservation contract failed.")
    if "TEST PASSED: bounded reference snapshot" not in (directory / "process.log").read_text():
        raise RuntimeError("Reference snapshot did not reach its assertions.")
    write(directory / "invocation.json", {"command": command, "snapshotScriptSha256": digest(TRIAL / "tests/parity/reference_snapshot.py")})
    return json.loads(output.read_text())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--baseline", type=Path, required=True)
    parser.add_argument("--exe", type=Path, default=REPO / "macOS/PrePoMax/bin/Release/net10.0/PrePoMax.com")
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    baseline = args.baseline.resolve()
    baseline_manifest = json.loads(baseline.read_text())
    for entry in baseline_manifest["referenceBinaries"]:
        if digest(REPO / entry["path"]) != entry["sha256"]:
            raise RuntimeError("The reference runtime differs from the captured baseline: " + entry["path"])
    portable = TRIAL / "portable"
    dotnet = shutil.which("dotnet")
    if not dotnet:
        raise RuntimeError("The .NET SDK is required.")
    for name in ["tests/Parity.Probe/Parity.Probe.csproj", "PrePoMax.Web/PrePoMax.Web.csproj"]:
        execute([dotnet, "build", name, "-c", "Release", "--nologo", "-v:q"], portable, output / (Path(name).stem + "-build.log"))
    fixtures = {
        "sparse": TRIAL / "tests/parity/fixtures/tetra-sparse.inp",
        "cube": TRIAL / "tests/parity/fixtures/cube-mesh.inp",
        "frd": TRIAL / "portable/tests/fixtures/calculix/cube.frd",
    }
    probe = portable / "tests/Parity.Probe/bin/Release/net10.0/Parity.Probe.dll"
    for name in ["sparse", "cube"]:
        execute([dotnet, str(probe), "prepare", str(fixtures[name]), str(output / (name + ".pma"))], portable, output / (name + "-prepare.log"))
    write(output / "inputs.json", {
        "baselineManifestSha256": digest(baseline),
        "implementationSha256": {str(path.relative_to(REPO)): digest(path) for path in [
            Path(__file__).resolve(), TRIAL / "tests/parity/reference_snapshot.py", TRIAL / "tests/parity/compare.py",
            portable / "PrePoMax.Application/ProjectParitySnapshot.cs", portable / "PrePoMax.Web/ParityEndpoints.cs",
            portable / "PrePoMax.Web/Program.cs", portable / "tests/Parity.Probe/Program.cs",
        ]},
        "webAssemblies": {path.name: digest(path) for path in sorted((portable / "PrePoMax.Web/bin/Release/net10.0").glob("*.dll"))},
        "fixtures": {name: {"path": str(path.relative_to(REPO)), "sha256": digest(path)} for name, path in fixtures.items()},
        "scope": "Bounded document/elastic-material readback, sparse node-set membership and complete immutable DISP.U1 field values; not complete source scripts.",
        "unitSetup": "Both adapters receive M_KG_S_C. Web PMAs are prepared fixtures; the current mesh-import UI's default Undefined unit state is not claimed equivalent.",
    })
    reference_model = desktop_snapshot("model", fixtures["sparse"], output / "desktop-model", args.exe.resolve())
    reference_field = desktop_snapshot("field", fixtures["frd"], output / "desktop-field", args.exe.resolve())
    with socket.socket() as socket_probe:
        socket_probe.bind(("127.0.0.1", 0))
        port = socket_probe.getsockname()[1]
    env = dict(os.environ, PREPOMAX_WEB_PORT=str(port), PREPOMAX_WEB_PARITY="1")
    with (output / "web-host.log").open("wb") as log:
        process = subprocess.Popen(["bash", "scripts/start-web-workspace.sh"], cwd=TRIAL, env=env, stdout=log, stderr=subprocess.STDOUT)
        try:
            web = Web(f"http://localhost:{port}")
            deadline = time.monotonic() + 30
            while True:
                try:
                    web.call("/workspace.html")
                    break
                except (urllib.error.URLError, ConnectionError):
                    if process.poll() is not None or time.monotonic() > deadline:
                        raise RuntimeError("Web host did not become ready; inspect web-host.log.")
                    time.sleep(.1)
            web.call("/api/parity/snapshot", expected=401)
            web.call("/api/workspace", b"", expected=204)
            web.call("/api/workspace/import", (output / "sparse.pma").read_bytes(), headers={"X-File-Extension": ".pma"})
            web.describe()
            web.call("/api/workspace/edit", {"action": "nodeset-add", "name": "PARITY_SELECTED", "nodes": [10, 30]})
            web.describe()
            before = web.call("/api/workspace/download/project")
            before_revision = web.revision
            web.call("/api/workspace/edit", {"action": "nodeset-add", "name": "PARITY_INVALID", "nodes": [999999]}, expected=400)
            if before != web.call("/api/workspace/download/project"):
                raise RuntimeError("A refused web edit changed the project.")
            if web.describe()["revision"] != before_revision:
                raise RuntimeError("A refused web edit changed the revision.")
            candidate_model = json.loads(web.call("/api/parity/snapshot?nodeSet=PARITY_SELECTED"))
            if before != web.call("/api/workspace/download/project"):
                raise RuntimeError("The read-only snapshot changed the project.")
            if web.describe()["revision"] != before_revision:
                raise RuntimeError("The read-only snapshot changed the revision.")
            write(output / "web-model.json", candidate_model)
            web.call("/api/workspace/import", (output / "cube.pma").read_bytes(), headers={"X-File-Extension": ".pma"})
            web.describe()
            web.call("/api/workspace/results", fixtures["frd"].read_bytes())
            web.describe()
            candidate_field = json.loads(web.call("/api/parity/snapshot?fieldName=DISP&component=U1&step=1&increment=1"))
            write(output / "web-field.json", candidate_field)
        finally:
            process.terminate()
            try:
                process.wait(timeout=20)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()
    model_report = COMPARE.compare(reference_model, candidate_model)
    field_report = COMPARE.compare(reference_field, candidate_field)
    write(output / "model-comparison.json", model_report)
    write(output / "field-comparison.json", field_report)
    negative = []
    for name, reference, candidate, mutate in [
        ("wrong-property", reference_model, candidate_model, lambda value: value["document"].update(modelSpace="PlaneStress")),
        ("wrong-material-value", reference_model, candidate_model, lambda value: value["materials"][0].update(youngsModulus=2000)),
        ("wrong-source-id", reference_model, candidate_model, lambda value: value["nodeSet"]["nodes"].__setitem__(0, 999999)),
        ("wrong-field-value", reference_field, candidate_field, lambda value: value["field"]["values"][0].update(float32Bits=1)),
    ]:
        altered = copy.deepcopy(candidate)
        mutate(altered)
        report = COMPARE.compare(reference, altered)
        write(output / (name + ".json"), report)
        negative.append({"case": name, "detected": not report["passed"], "paths": [item["path"] for item in report["differences"]]})
    passed = model_report["passed"] and field_report["passed"] and all(item["detected"] for item in negative)
    summary = {"case": "M00-T02", "passed": passed,
               "documentAndSparseSelectionMatch": model_report["passed"], "allFixedFieldValuesMatch": field_report["passed"],
               "negativeControls": negative, "readOnlySnapshotAndRejectedEditPreservedProject": True,
               "claim": "Bounded harness qualification only; full Prepomac scripts and feature parity remain separately tracked."}
    write(output / "summary.json", summary)
    print(json.dumps(summary, indent=2))
    return 0 if passed else 1


if __name__ == "__main__":
    raise SystemExit(main())

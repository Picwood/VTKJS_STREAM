#!/usr/bin/env python3
"""Compare bounded material create/assign/unused-remove/recreate states with a captured Prepomac runtime."""
import argparse
import copy
import importlib.util
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import time
import urllib.error
import uuid

TRIAL = Path(__file__).resolve().parents[1]
REPO = TRIAL.parents[2]
SPEC = importlib.util.spec_from_file_location("material_lifecycle_runner", TRIAL / "scripts/run-web-parity.py")
RUN = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RUN)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    baseline = args.baseline.resolve()
    capture = json.loads(baseline.read_text())

    def verify_reference():
        for item in capture["sources"] + capture["referenceBinaries"]:
            if RUN.digest(REPO / item["path"]) != item["sha256"]:
                raise RuntimeError("Reference source/runtime drift: " + item["path"])

    verify_reference()
    dotnet = shutil.which("dotnet")
    if not dotnet:
        raise RuntimeError("The .NET SDK is required.")
    for project in ["tests/Parity.Probe/Parity.Probe.csproj", "PrePoMax.Web/PrePoMax.Web.csproj"]:
        RUN.execute([dotnet, "build", project, "-c", "Release", "--nologo", "-v:q"], TRIAL / "portable", output / (Path(project).stem + "-build.log"))
    probe = TRIAL / "portable/tests/Parity.Probe/bin/Release/net10.0/Parity.Probe.dll"
    fixture = TRIAL / "tests/parity/fixtures/cube-mesh.inp"
    RUN.execute([dotnet, str(probe), "prepare-material", str(fixture), str(output / "initial.pma")], TRIAL, output / "prepare.log")
    adapter = TRIAL / "tests/parity/reference_material_lifecycle.py"
    command = [str(REPO / "macOS/PrePoMax/bin/Release/net10.0/PrePoMax.com"), "--noSettingsSave", "-f", str(fixture), "-u", "M_KG_S_C",
               "--script", str(adapter), "-g", "No", "-x", "Yes", "-w", str(output), "--report", str(output / "reference-run.json"), "--timeout", "90"]
    if shutil.which("xvfb-run"):
        command = ["xvfb-run", "-a", *command]
    files = [Path(__file__), adapter, TRIAL / "scripts/run-web-parity.py", TRIAL / "tests/parity/compare.py",
             TRIAL / "portable/tests/Parity.Probe/Program.cs", TRIAL / "prototypes/VtkJsStreaming/src/client/src/workspace.ts",
             TRIAL / "prototypes/VtkJsStreaming/src/client/workspace.html"]
    for folder in ["PrePoMax.Application", "PrePoMax.Persistence", "PrePoMax.Web"]:
        files.extend((TRIAL / "portable" / folder).glob("*.cs"))
    RUN.write(output / "inputs.json", {"captureSha256": RUN.digest(baseline), "fixtureSha256": RUN.digest(fixture), "referenceInvocation": command,
                                      "implementationSha256": {str(path.relative_to(REPO)): RUN.digest(path) for path in sorted(set(files))},
                                      "webAssemblies": {path.name: RUN.digest(path) for path in (TRIAL / "portable/PrePoMax.Web/bin/Release/net10.0").glob("*.dll")}})
    RUN.execute(command, REPO, output / "reference-process.log", dict(os.environ, PREPOMAX_PARITY_OUTPUT=str(output / "reference.json"), PREPOMAX_GPU="system"), timeout=110)
    report = json.loads((output / "reference-run.json").read_text())
    if report["exitCode"] != 0 or report["exitStatus"] != "Ok" or report["invocation"]["settingsSaved"]:
        raise RuntimeError("Reference report/settings-preservation contract failed.")
    if "TEST PASSED: bounded material lifecycle reference" not in (output / "reference-process.log").read_text():
        raise RuntimeError("Reference adapter did not finish.")
    reference = json.loads((output / "reference.json").read_text())
    with socket.socket() as port_probe:
        port_probe.bind(("127.0.0.1", 0))
        port = port_probe.getsockname()[1]
    stages, receipts, refused = [], [], []
    with (output / "web-host.log").open("wb") as log:
        process = subprocess.Popen(["bash", "scripts/start-web-workspace.sh"], cwd=TRIAL, env=dict(os.environ, PREPOMAX_WEB_PORT=str(port)), stdout=log, stderr=subprocess.STDOUT)
        try:
            web = RUN.Web(f"http://localhost:{port}")
            deadline = time.monotonic() + 30
            while True:
                try:
                    web.call("/workspace.html")
                    break
                except (urllib.error.URLError, ConnectionError):
                    if process.poll() is not None or time.monotonic() > deadline:
                        raise RuntimeError("Web host not ready; inspect web-host.log.")
                    time.sleep(.1)
            web.call("/api/workspace", b"", expected=204)
            web.call("/api/workspace/import", (output / "initial.pma").read_bytes(), headers={"X-File-Extension": ".pma"})
            original_id = web.describe()["materialIdentities"][0]["id"]

            def snapshot(index):
                project = output / f"web-stage-{index}.pma"
                project.write_bytes(web.call("/api/workspace/download/project"))
                target = output / f"web-stage-{index}.json"
                RUN.execute([dotnet, str(probe), "read-identity", str(project), str(target)], TRIAL, output / f"read-{index}.log")
                stages.append(json.loads(target.read_text()))

            def submit(kind, **fields):
                request = dict(fields, operationId=str(uuid.uuid4()), expectedRevision=web.revision)
                reply = json.loads(web.call("/api/workspace/commands/material/" + kind, request))
                receipts.append(reply["receipt"])
                web.describe()
                return reply["receipt"]

            def rejected_create(**fields):
                before = web.call("/api/workspace/download/project")
                revision = web.revision
                web.call("/api/workspace/commands/material/create", dict(fields, operationId=str(uuid.uuid4()), expectedRevision=revision), expected=400)
                if before != web.call("/api/workspace/download/project") or revision != web.describe()["revision"]:
                    raise RuntimeError("Rejected web creation changed state.")

            snapshot(0)
            created = submit("create", name="NEW", youngsModulus=700, poissonsRatio=.2)
            new_id = created["materialId"]
            snapshot(1)
            for name in reference["refusedNames"]:
                rejected_create(name=name, youngsModulus=700, poissonsRatio=.2)
                refused.append(name)
            rejected_create(name="INVALID", youngsModulus=-1, poissonsRatio=.2)
            submit("assign", sectionName="solid-1", materialId=new_id)
            snapshot(2)
            submit("assign", sectionName="solid-1", materialId=original_id)
            snapshot(3)
            submit("remove", materialId=new_id)
            snapshot(4)
            recreated = submit("create", name="NEW", youngsModulus=800, poissonsRatio=.25)
            snapshot(5)
            if recreated["materialId"] == new_id or not any(item["id"] == original_id for item in web.describe()["materialIdentities"]):
                raise RuntimeError("Recreation reused a deleted ID or changed an unrelated ID.")
        finally:
            process.terminate()
            try:
                process.wait(timeout=20)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()
    verify_reference()
    candidate = {"stages": stages, "refusedNames": refused, "negativeModulusRefused": True}
    differences = RUN.COMPARE.differences(reference, candidate)
    negative = []
    for name, mutate in [
        ("wrong-created-modulus", lambda value: value["stages"][1]["materials"][1]["properties"][0]["data"][0].__setitem__(0, 701)),
        ("wrong-assignment", lambda value: value["stages"][2]["sections"][0].update(material="ELASTIC")),
        ("removed-wrong-material", lambda value: value["stages"][4]["materials"][0].update(name="NEW")),
        ("wrong-recreated-ratio", lambda value: value["stages"][5]["materials"][1]["properties"][0]["data"][0].__setitem__(1, .3)),
    ]:
        altered = copy.deepcopy(candidate)
        mutate(altered)
        found = RUN.COMPARE.differences(reference, altered)
        negative.append({"case": name, "detected": bool(found), "differences": found})
    RUN.write(output / "web.json", candidate)
    RUN.write(output / "comparison.json", {"passed": not differences, "differences": differences})
    RUN.write(output / "receipts.json", receipts)
    RUN.write(output / "negative-controls.json", negative)
    summary = {"schema": "prepomac.material-lifecycle-evidence/1", "passed": not differences and all(item["detected"] for item in negative),
               "exactCheckpoints": len(stages), "negativeControlsDetected": sum(item["detected"] for item in negative),
               "refusedNames": refused, "negativeModulusRefused": True, "recreatedIdIsNew": True, "referenceSourceAndRuntimeDrift": [],
               "claim": "Bounded numeric elastic create, solid-section assignment, unused removal and recreation. Assigned/opaque-reference removal remains refused; full M01 and full source scripts are open."}
    RUN.write(output / "summary.json", summary)
    print(json.dumps(summary, indent=2))
    return 0 if summary["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())

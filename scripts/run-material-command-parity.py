#!/usr/bin/env python3
"""Compare the first M01 material-command slice against the frozen Prepomac runtime.

Retains both material readbacks, exact differences, negative controls and receipts.
Does not qualify whole source scripts, PMX interoperability or journal replay.
"""
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
SPEC = importlib.util.spec_from_file_location("web_parity_runner", TRIAL / "scripts/run-web-parity.py")
RUN = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RUN)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--baseline", type=Path, required=True)
    parser.add_argument("--exe", type=Path, default=REPO / "macOS/PrePoMax/bin/Release/net10.0/PrePoMax.com")
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    baseline_path = args.baseline.resolve()
    baseline = json.loads(baseline_path.read_text())

    def verify_reference():
        for entry in baseline["sources"] + baseline["referenceBinaries"]:
            if RUN.digest(REPO / entry["path"]) != entry["sha256"]:
                raise RuntimeError("Reference source/runtime drift: " + entry["path"])

    verify_reference()
    dotnet = shutil.which("dotnet")
    if not dotnet:
        raise RuntimeError("The .NET SDK is required.")
    portable = TRIAL / "portable"
    for project in ["tests/Parity.Probe/Parity.Probe.csproj", "PrePoMax.Web/PrePoMax.Web.csproj"]:
        RUN.execute([dotnet, "build", project, "-c", "Release", "--nologo", "-v:q"], portable, output / (Path(project).stem + "-build.log"))
    fixture = TRIAL / "tests/parity/fixtures/cube-mesh.inp"
    probe = portable / "tests/Parity.Probe/bin/Release/net10.0/Parity.Probe.dll"
    RUN.execute([dotnet, str(probe), "prepare-material", str(fixture), str(output / "initial.pma")], TRIAL, output / "prepare.log")
    reference = output / "reference.json"
    report = output / "reference-run.json"
    adapter = TRIAL / "tests/parity/reference_material_command.py"
    command = [str(args.exe.resolve()), "--noSettingsSave", "-f", str(fixture), "-u", "M_KG_S_C", "--script", str(adapter),
               "-g", "No", "-x", "Yes", "-w", str(output), "--report", str(report), "--timeout", "90"]
    if shutil.which("xvfb-run"):
        command = ["xvfb-run", "-a", *command]
    implementation = [Path(__file__), adapter, TRIAL / "scripts/run-web-parity.py", TRIAL / "tests/parity/compare.py",
                      portable / "tests/Parity.Probe/Program.cs", portable / "PrePoMax.Application/ProjectCommandSession.cs",
                      portable / "PrePoMax.Application/ProjectMaterials.cs", portable / "PrePoMax.Web/Workspace.cs",
                      portable / "PrePoMax.Web/Program.cs", TRIAL / "prototypes/VtkJsStreaming/src/client/src/workspace.ts"]
    RUN.write(output / "inputs.json", {"baselineSha256": RUN.digest(baseline_path), "fixtureSha256": RUN.digest(fixture),
                                      "referenceInvocation": command,
                                      "implementationSha256": {str(path.relative_to(REPO)): RUN.digest(path) for path in implementation}})
    RUN.execute(command, REPO, output / "reference-process.log", dict(os.environ, PREPOMAX_PARITY_OUTPUT=str(reference), PREPOMAX_GPU="system"), timeout=110)
    run_report = json.loads(report.read_text())
    if run_report["exitCode"] != 0 or run_report["exitStatus"] != "Ok" or run_report["invocation"]["settingsSaved"]:
        raise RuntimeError("Reference process/report contract failed.")
    if "TEST PASSED: bounded material command reference" not in (output / "reference-process.log").read_text():
        raise RuntimeError("Reference adapter did not finish.")
    with socket.socket() as port_probe:
        port_probe.bind(("127.0.0.1", 0))
        port = port_probe.getsockname()[1]
    stages, receipts = [], []
    with (output / "web-host.log").open("wb") as log:
        process = subprocess.Popen(["bash", "scripts/start-web-workspace.sh"], cwd=TRIAL,
                                   env=dict(os.environ, PREPOMAX_WEB_PORT=str(port)), stdout=log, stderr=subprocess.STDOUT)
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
            web.describe()

            def snapshot(index):
                project = output / f"web-stage-{index}.pma"
                project.write_bytes(web.call("/api/workspace/download/project"))
                target = output / f"web-stage-{index}.json"
                RUN.execute([dotnet, str(probe), "read-material", str(project), str(target)], TRIAL, output / f"read-{index}.log")
                stages.append(json.loads(target.read_text()))

            snapshot(0)
            for index, patch in enumerate([{"youngsModulus": 2000}, {"poissonsRatio": .2}], 1):
                request = dict(patch, operationId=str(uuid.uuid4()), expectedRevision=web.revision, materialName="ELASTIC")
                reply = json.loads(web.call("/api/workspace/commands/material", request))
                receipts.append(reply["receipt"])
                web.describe()
                snapshot(index)
            before = web.call("/api/workspace/download/project")
            revision = web.revision
            web.call("/api/workspace/commands/material", {"operationId": str(uuid.uuid4()), "expectedRevision": revision,
                                                       "materialName": "ELASTIC", "youngsModulus": -1}, expected=400)
            if before != web.call("/api/workspace/download/project") or web.describe()["revision"] != revision:
                raise RuntimeError("Rejected web command changed state.")
        finally:
            process.terminate()
            try:
                process.wait(timeout=20)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()
    verify_reference()
    candidate = {"stages": stages, "rejectedEditPreservedState": True}
    reference_body = json.loads(reference.read_text())
    differences = RUN.COMPARE.differences(reference_body, candidate)
    negative = []
    for name, mutate in [
        ("density", lambda value: value["stages"][1]["properties"][0]["data"][0].__setitem__(0, 9)),
        ("hardening", lambda value: value["stages"][1]["properties"][2].update(hardening="Isotropic")),
        ("description", lambda value: value["stages"][1].update(description="lost")),
        ("property-order", lambda value: value["stages"][1]["properties"].reverse()),
    ]:
        altered = copy.deepcopy(candidate)
        mutate(altered)
        found = RUN.COMPARE.differences(reference_body, altered)
        negative.append({"case": name, "detected": bool(found), "differences": found})
    RUN.write(output / "web.json", candidate)
    RUN.write(output / "receipts.json", receipts)
    RUN.write(output / "comparison.json", {"passed": not differences, "differences": differences})
    RUN.write(output / "negative-controls.json", negative)
    summary = {"schema": "prepomac.material-command-evidence/1", "passed": not differences and all(item["detected"] for item in negative),
               "stagesCompared": len(stages), "negativeControlsDetected": sum(item["detected"] for item in negative),
               "referenceSourceAndRuntimeDrift": [], "rejectedEditsPreservedState": True,
               "scope": "Bounded numeric material edits; M01-T01/T03 remain in progress. No PMX/equation/rename/full-script/journal parity claim."}
    RUN.write(output / "summary.json", summary)
    print(json.dumps(summary, indent=2))
    return 0 if summary["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())

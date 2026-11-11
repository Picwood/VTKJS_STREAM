#!/usr/bin/env python3
"""Qualify material rename, persisted IDs and the explicitly recorded Prepomac section-reference gap."""
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
SPEC = importlib.util.spec_from_file_location("identity_web_runner", TRIAL / "scripts/run-web-parity.py")
RUN = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RUN)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--baseline", type=Path, required=True)
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    capture_path = args.baseline.resolve()
    capture = json.loads(capture_path.read_text())

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
    RUN.execute([dotnet, str(probe), "prepare-identity", str(fixture), str(output / "initial.pma")], TRIAL, output / "prepare.log")
    adapter = TRIAL / "tests/parity/reference_material_identity.py"
    command = [str(REPO / "macOS/PrePoMax/bin/Release/net10.0/PrePoMax.com"), "--noSettingsSave", "-f", str(fixture), "-u", "M_KG_S_C",
               "--script", str(adapter), "-g", "No", "-x", "Yes", "-w", str(output), "--report", str(output / "reference-run.json"), "--timeout", "90"]
    if shutil.which("xvfb-run"):
        command = ["xvfb-run", "-a", *command]
    files = [Path(__file__), adapter, TRIAL / "scripts/run-web-parity.py", TRIAL / "tests/parity/compare.py",
             TRIAL / "portable/tests/Parity.Probe/Program.cs", TRIAL / "prototypes/VtkJsStreaming/src/client/src/workspace.ts"]
    for folder in ["PrePoMax.Application", "PrePoMax.Persistence", "PrePoMax.Web"]:
        files.extend((TRIAL / "portable" / folder).glob("*.cs"))
    RUN.write(output / "inputs.json", {"captureSha256": RUN.digest(capture_path), "fixtureSha256": RUN.digest(fixture),
                                      "referenceInvocation": command,
                                      "implementationSha256": {str(path.relative_to(REPO)): RUN.digest(path) for path in sorted(set(files))},
                                      "webAssemblies": {path.name: RUN.digest(path) for path in (TRIAL / "portable/PrePoMax.Web/bin/Release/net10.0").glob("*.dll")}})
    RUN.execute(command, REPO, output / "reference-process.log", dict(os.environ, PREPOMAX_PARITY_OUTPUT=str(output / "reference.json"), PREPOMAX_GPU="system"), timeout=110)
    report = json.loads((output / "reference-run.json").read_text())
    if report["exitCode"] != 0 or report["exitStatus"] != "Ok" or report["invocation"]["settingsSaved"]:
        raise RuntimeError("Reference report/settings-preservation contract failed.")
    if "TEST PASSED: bounded material identity reference" not in (output / "reference-process.log").read_text():
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
            state = web.describe()
            material_id = next(item["id"] for item in state["materialIdentities"] if item["name"] == "ELASTIC")

            def snapshot(index):
                project = output / f"web-stage-{index}.pma"
                project.write_bytes(web.call("/api/workspace/download/project"))
                target = output / f"web-stage-{index}.json"
                RUN.execute([dotnet, str(probe), "read-identity", str(project), str(target)], TRIAL, output / f"read-{index}.log")
                stages.append(json.loads(target.read_text()))

            snapshot(0)
            for index, name in enumerate(["ALLOY", "alloy"], 1):
                request = {"operationId": str(uuid.uuid4()), "expectedRevision": web.revision, "materialId": material_id, "newName": name}
                reply = json.loads(web.call("/api/workspace/commands/material/rename", request))
                receipts.append(reply["receipt"])
                state = web.describe()
                if next(item["id"] for item in state["materialIdentities"] if item["name"] == name) != material_id:
                    raise RuntimeError("Rename changed material identity.")
                snapshot(index)
            for name in reference["refusedNames"]:
                before = web.call("/api/workspace/download/project")
                revision = web.revision
                web.call("/api/workspace/commands/material/rename", {"operationId": str(uuid.uuid4()), "expectedRevision": revision,
                         "materialId": material_id, "newName": name}, expected=400)
                if before != web.call("/api/workspace/download/project") or revision != web.describe()["revision"]:
                    raise RuntimeError("Rejected web rename changed state.")
                refused.append(name)
        finally:
            process.terminate()
            try:
                process.wait(timeout=20)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()
    verify_reference()
    candidate = {"stages": stages, "refusedNames": refused}
    differences = RUN.COMPARE.differences(reference, candidate)
    material_differences = RUN.COMPARE.differences([stage["materials"] for stage in reference["stages"]], [stage["materials"] for stage in stages])
    expected_sections = [[{"name": "solid-1", "material": name}] for name in ["ELASTIC", "ALLOY", "alloy"]]
    section_differences = RUN.COMPARE.differences(expected_sections, [stage["sections"] for stage in stages])
    known = [{"path": f"$.stages[{index}].sections[0].material", "reference": "ELASTIC", "candidate": name}
             for index, name in enumerate(["ALLOY", "alloy"], 1)]
    unexpected = [item for item in differences if item not in known]
    negative = []
    for name, mutate in [
        ("density", lambda value: value["stages"][1]["materials"][0]["properties"][0]["data"][0].__setitem__(0, 9)),
        ("hardening", lambda value: value["stages"][1]["materials"][0]["properties"][2].update(hardening="Isotropic")),
        ("material-order", lambda value: value["stages"][1]["materials"].reverse()),
        ("dangling-section", lambda value: value["stages"][1]["sections"][0].update(material="ELASTIC")),
    ]:
        altered = copy.deepcopy(candidate)
        mutate(altered)
        material_bad = RUN.COMPARE.differences([stage["materials"] for stage in reference["stages"]], [stage["materials"] for stage in altered["stages"]])
        section_bad = RUN.COMPARE.differences(expected_sections, [stage["sections"] for stage in altered["stages"]])
        negative.append({"case": name, "detected": bool(material_bad or section_bad), "materialDifferences": material_bad, "sectionPolicyDifferences": section_bad})
    RUN.write(output / "web.json", candidate)
    RUN.write(output / "receipts.json", receipts)
    RUN.write(output / "comparison.json", {"passed": not differences, "differences": differences})
    RUN.write(output / "material-comparison.json", {"passed": not material_differences, "differences": material_differences})
    RUN.write(output / "section-policy.json", {"passed": not section_differences, "differences": section_differences,
                                             "policy": "A renamed material must remain assigned to its sections. The raw reference comparison is retained separately."})
    RUN.write(output / "negative-controls.json", negative)
    summary = {"schema": "prepomac.material-identity-evidence/1", "qualificationPassed": not unexpected and not material_differences and not section_differences and all(item["detected"] for item in negative),
               "rawExactParityPassed": not differences, "materialParityPassed": not material_differences, "sectionReferenceIntegrityPassed": not section_differences,
               "referenceDeviations": differences, "unexpectedDifferences": unexpected, "negativeControlsDetected": sum(item["detected"] for item in negative),
               "checkpoints": len(stages), "refusedNames": refused, "referenceSourceAndRuntimeDrift": [],
               "claim": "Bounded material identity/rename slice. Raw section-reference differences remain explicit; full M01 and full-script parity are open."}
    RUN.write(output / "summary.json", summary)
    print(json.dumps(summary, indent=2))
    return 0 if summary["qualificationPassed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())

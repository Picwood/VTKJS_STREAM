#!/usr/bin/env python3
"""Execute Prepomac's unchanged custom verifiers and retain outcome/log evidence.

The untracked personal CHAMFER.pmx regression requires a separately supplied
consented --real-model. Other verifiers use their generated or shipped fixtures.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import signal
import subprocess
import tarfile
import time

TRIAL = Path(__file__).resolve().parents[1]
REPO = TRIAL.parents[2]


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--solver", type=Path, required=True)
    parser.add_argument("--real-model", type=Path)
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    solver = args.solver.resolve()
    if not solver.is_file():
        parser.error("The selected solver does not exist.")
    fixture_manifest = []
    with tarfile.open(output / "shipped-pmx-fixtures.tar.gz", "w:gz") as archive:
        for path in sorted((REPO / "assets_composites").rglob("*.pmx")):
            name = path.relative_to(REPO).as_posix()
            fixture_manifest.append({"path": name, "sha256": digest(path)})
            archive.add(path, arcname=name)
    if not fixture_manifest:
        raise RuntimeError("No shipped PMX fixtures found; compatibility cannot pass an empty loop.")
    cylinder_json = output / "cylinders.json"
    cylinder_json.write_text(json.dumps({"cylinders": [{"name": "BaselineImported", "center": [20, 0, 0], "radius": 12, "height": 2, "target_radius": 11}]}))
    exe = REPO / "macOS/PrePoMax/bin/Release/net10.0/PrePoMax.com"
    outcomes = []
    for path in sorted((REPO / "macOS").glob("verify-*.py")):
        name = path.name
        work = output / name.removesuffix(".py")
        work.mkdir()
        if name == "verify-chamfer-model.py" and args.real_model is None:
            outcomes.append({"path": "macOS/" + name, "status": "blocked", "reason": "Real-model regression requires an explicitly supplied consented --real-model; personal CHAMFER.pmx is not read.", "command": None})
            (output / "outcomes.json").write_text(json.dumps(outcomes, indent=2) + "\n")
            print("BLOCKED: " + name + " (real model not supplied)", flush=True)
            continue
        command = ["python3", str(path), "Release"]
        mode = "source-default"
        if name == "verify-calculix.py":
            command = ["bash", str(REPO / "macOS/verify-calculix.sh"), str(solver), "Release"]
        elif name == "verify-cylinders.py":
            command = [str(exe), "--noSettingsSave", "-f", str(REPO / "APIs/Python/Test_Scripts/Models/cube.inp"), "-u", "MM_TON_S_C", "--script", str(path),
                       "--scriptArgs", str(cylinder_json), "-g", "No", "-x", "Yes", "-w", str(work), "--report", str(work / "report.json")]
        elif name == "verify-fatigue-backend.py":
            command = ["python3", str(path)]
        elif name == "verify-file-tools.py":
            command.append("--integration")
            mode = "offline-and-application"
        elif name == "verify-individual-colors.py":
            command.append("--gpu")
            mode = "application-and-source-gpu-check"
        elif name == "verify-pressure-springs.py":
            command.append(str(solver))
        elif name == "verify-chamfer-model.py":
            command.append(str(args.real_model.resolve()))
        if shutil.which("xvfb-run"):
            command = ["xvfb-run", "-a", *command]
        env = dict(os.environ, PREPOMAX_GPU="system")
        log = work / "process.log"
        started = time.monotonic()
        with log.open("wb") as stream:
            process = subprocess.Popen(command, cwd=REPO, stdout=stream, stderr=subprocess.STDOUT, env=env, start_new_session=True)
            timeout = False
            try:
                code = process.wait(timeout=600)
            except subprocess.TimeoutExpired:
                timeout = True
                os.killpg(process.pid, signal.SIGKILL)
                code = process.wait()
        # PMX assets and solver bytes are part of the recorded configuration.
        outcomes.append({"path": "macOS/" + name, "status": "blocked" if timeout else "passed" if code == 0 else "failed",
                         "exit": code, "seconds": time.monotonic() - started, "mode": mode, "command": command,
                         "log": str(log.relative_to(output)), "sourceSha256": digest(path), "timeout": timeout})
        (output / "outcomes.json").write_text(json.dumps(outcomes, indent=2) + "\n")
        print(f"{outcomes[-1]['status'].upper()}: {name} ({outcomes[-1]['seconds']:.1f}s)", flush=True)
    changed = [entry["path"] for entry in fixture_manifest if digest(REPO / entry["path"]) != entry["sha256"]]
    summary = {"schema": "prepomac.custom-baseline/1", "outcomes": outcomes,
               "solver": {"sha256": digest(solver), "path": str(solver)}, "shippedFixtures": fixture_manifest,
               "changedFixtures": changed,
               "counts": {status: sum(item["status"] == status for item in outcomes) for status in ["passed", "failed", "blocked"]},
               "claim": "Unchanged reference verifier outcomes only; not web parity or large-model GPU qualification."}
    (output / "summary.json").write_text(json.dumps(summary, indent=2) + "\n")
    print(json.dumps(summary["counts"]))
    return 1 if changed else 0


if __name__ == "__main__":
    raise SystemExit(main())

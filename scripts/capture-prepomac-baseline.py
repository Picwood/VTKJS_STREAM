#!/usr/bin/env python3
"""Capture and execute the unchanged Prepomac reference in disposable workspaces.

Outputs are local evidence, never credentials/settings or web parity claims.
The existing API collector, expected exits and report assertions stay authoritative.
"""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import platform
import shlex
import shutil
import signal
import subprocess
import tarfile
import time
from datetime import datetime, timezone

TRIAL = Path(__file__).resolve().parents[1]
REPO = TRIAL.parents[2]
API = REPO / "APIs/Python/Test_Scripts"


def write_json(path, value):
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n")
    temporary.replace(path)


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def probe(command):
    try:
        done = subprocess.run(command, cwd=REPO, capture_output=True, text=True, timeout=15)
        return {"command": command, "exit": done.returncode, "output": (done.stdout + done.stderr)[:12000]}
    except (OSError, subprocess.TimeoutExpired) as error:
        return {"command": command, "error": str(error)}


def capture_candidate(output):
    allowed = ("CaeGlobals/", "CaeJob/", "CaeMesh/", "CaeModel/", "CaeResults/", "STL/",
               "portable/", "prototypes/VtkJsStreaming/", "scripts/", "tests/parity/")
    names = subprocess.check_output(["git", "ls-files", "-z", "--cached", "--others", "--exclude-standard"], cwd=TRIAL).decode().split("\0")
    entries = []
    for name in sorted(set(filter(None, names))):
        path = TRIAL / name
        if not path.is_file() or path.is_symlink():
            continue
        if not name.startswith(allowed) and name not in {"README.md", "global.json"} and path.suffix not in {".props", ".targets", ".slnx"}:
            continue
        if any(part.lower() in {"bin", "obj", "node_modules", "dist", "build", "__pycache__", "test-results"} for part in Path(name).parts):
            continue
        if path.suffix.lower() in {".pmx", ".pma", ".log", ".pyc"}:
            continue
        entries.append({"path": name, "bytes": path.stat().st_size, "sha256": digest(path)})
    with tarfile.open(output / "sources.tar.gz", "w:gz") as archive:
        for entry in entries:
            archive.add(TRIAL / entry["path"], arcname=entry["path"], recursive=False)
    manifest = {"schema": "prepomac.web-candidate-capture/1", "sources": entries,
                "gitHead": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=TRIAL, text=True).strip(),
                "scope": "Nested repository portable/domain/renderer/test/script sources and build metadata; generated/runtime/project files excluded.",
                "archiveSha256": digest(output / "sources.tar.gz")}
    write_json(output / "manifest.json", manifest)
    print(f"Captured {len(entries)} candidate inputs from the independent nested Git repository.")


def capture(output, executable):
    paths = subprocess.check_output(["git", "ls-files", "-z", "--cached", "--others", "--exclude-standard"], cwd=REPO).decode().split("\0")
    source = []
    excluded = []
    for name in sorted(set(filter(None, paths))):
        path = REPO / name
        if not path.is_file() or path.is_symlink():
            continue
        if name.startswith("PERSONAL/"):
            # VTKJS_STREAM has its own Git repository and must be captured independently.
            continue
        if any(part.lower() in {"bin", "obj", "node_modules", "dist", "build", "__pycache__", "test-results"} for part in Path(name).parts):
            continue
        if path.suffix.lower() in {".pmx", ".pma", ".log", ".pyc"}:
            excluded.append({"path": name, "reason": "User/project or generated artifact; not a production source fixture."})
            continue
        if "settings" in path.name.lower() and path.suffix.lower() in {".json", ".dat", ".xml", ".pmx"}:
            excluded.append({"path": name, "reason": "Runtime settings are not baseline source or evidence."})
            continue
        source.append({"path": name, "bytes": path.stat().st_size, "sha256": digest(path)})
    with tarfile.open(output / "sources.tar.gz", "w:gz") as archive:
        for entry in source:
            archive.add(REPO / entry["path"], arcname=entry["path"], recursive=False)
    binaries = []
    for path in sorted(executable.parent.rglob("*")):
        if not path.is_file() or path.is_symlink():
            continue
        if path.suffix.lower() not in {".dll", ".so", ".dylib"} and not path.name.endswith((".deps.json", ".runtimeconfig.json")) and not os.access(path, os.X_OK):
            continue
        if "settings" in path.name.lower() or path.suffix in {".py", ".sh"}:
            continue
        binaries.append({"path": path.relative_to(REPO).as_posix(), "bytes": path.stat().st_size, "sha256": digest(path)})
    with tarfile.open(output / "reference-binaries.tar.gz", "w:gz") as archive:
        for entry in binaries:
            archive.add(REPO / entry["path"], arcname=entry["path"], recursive=False)
    manifest = {
        "schema": "prepomac.reference-baseline/1", "capturedUtc": datetime.now(timezone.utc).isoformat(),
        "gitHead": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=REPO, text=True).strip(),
        "workingTreeStatus": subprocess.check_output(["git", "status", "--porcelain=v1", "--untracked-files=all", "--", ".", ":(exclude)PERSONAL"], cwd=REPO, text=True).splitlines(),
        "scope": "Tracked and nonignored outer Prepomac sources; nested repositories, user project files and runtime settings excluded explicitly.",
        "sources": source, "excluded": excluded, "referenceBinaries": binaries,
        "archives": {name: digest(output / name) for name in ["sources.tar.gz", "reference-binaries.tar.gz"]},
        "environment": {"platform": platform.platform(), "python": platform.python_version(),
            "tools": [probe(command) for command in [["dotnet", "--info"], ["node", "--version"], ["npm", "--version"], ["ccx", "-v"], ["gmsh", "--version"], ["nvidia-smi", "--query-gpu=name,driver_version", "--format=csv,noheader"]]]},
        "referenceSourceChangedDuringRun": None,
    }
    write_json(output / "manifest.json", manifest)
    print(f"Captured {len(source)} source inputs and {len(binaries)} runtime inputs.", flush=True)
    return manifest


def api_suite(output, executable, filters, timeout_cap, settings=None):
    spec = importlib.util.spec_from_file_location("prepomac_reference_collector", API / "Run_Tests.py")
    runner = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(runner)
    # A wrapper adds settings preservation and a dedicated virtual display without changing assertions.
    wrapper = output / "reference-launcher.sh"
    command = [str(executable), "--noSettingsSave"]
    if settings is not None:
        runner.SETTINGS_FILE = str(settings)
    if shutil.which("xvfb-run"):
        command = ["xvfb-run", "-a", *command]
    invocation = " ".join(shlex.quote(part) for part in command)
    script = "#!/bin/sh\n"
    if settings is not None:
        script += 'has_settings=0\nfor argument do\n  if [ "$argument" = --settings ]; then has_settings=1; fi\ndone\n'
        script += 'if [ "$has_settings" = 0 ]; then\n  exec ' + invocation + ' --settings ' + shlex.quote(str(settings)) + ' "$@"\nfi\n'
    wrapper.write_text(script + "exec " + invocation + ' "$@"\n')
    wrapper.chmod(0o700)
    run_env = dict(os.environ, PREPOMAX_GPU="system")
    if settings is not None:
        run_env["PPM_TEST_SETTINGS"] = str(settings)
        # Several unchanged reference clients use LOCALAPPDATA even on Linux.
        # Align it with .NET's XDG location and isolate these test sessions.
        data_root = output / "session-data"
        data_root.mkdir(mode=0o700)
        run_env["XDG_DATA_HOME"] = str(data_root)
        run_env["LOCALAPPDATA"] = str(data_root)
    outcomes = []
    for name in runner.collect_offline(filters):
        started = time.monotonic()
        done = subprocess.run(["python3", str(API / name)], cwd=API, capture_output=True, text=True, timeout=runner.OFFLINE_TIMEOUT, env=run_env)
        log = output / (name + ".log")
        log.write_text(done.stdout + done.stderr)
        item = {"kind": "offline", "path": "APIs/Python/Test_Scripts/" + name, "label": name,
                "status": "passed" if done.returncode == 0 else "failed", "exit": done.returncode,
                "expectedExit": 0, "seconds": time.monotonic() - started, "log": log.name}
        outcomes.append(item)
        write_json(output / "outcomes.json", outcomes)
        print(f"{item['status'].upper()}: {name} ({item['seconds']:.1f}s)", flush=True)
    work = output / "api-work"
    work.mkdir()
    runner.clear_result_fixture(str(work))
    for test in runner.collect(filters):
        report = work / (test.file_stem + ".json")
        command = runner.build_command(test, str(wrapper), str(work), str(report))
        started = time.monotonic()
        process = subprocess.Popen(command, cwd=REPO, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, env=run_env, start_new_session=True)
        timed_out = False
        limit = test.timeout + runner.PROCESS_TIMEOUT_MARGIN
        if timeout_cap is not None:
            limit = min(limit, timeout_cap)
        try:
            data = process.communicate(timeout=limit)[0]
        except subprocess.TimeoutExpired:
            timed_out = True
            os.killpg(process.pid, signal.SIGKILL)
            data = process.communicate()[0]
        text = data.decode("utf-8", "replace")
        log = output / (test.file_stem + ".log")
        log.write_text(text)
        problems = []
        if timed_out:
            problems.append(f"Baseline timeout after {limit}s; original declared timeout {test.timeout}s.")
        if process.returncode != test.expected_code:
            problems.append(f"Exit {process.returncode}, expected {test.expected_code}.")
        problems.extend(runner.output_problems(test, text))
        if report.exists():
            try:
                body = json.loads(report.read_text())
                if body.get("exitCode") != test.expected_code:
                    problems.append("Run report exitCode differs from the declared expectation.")
                expected_status = runner.STATUS_BY_CODE.get(test.expected_code)
                if expected_status and body.get("exitStatus") != expected_status:
                    problems.append("Run report exitStatus differs from the declared expectation.")
                problems.extend(runner.report_problems(body, test.report_checks))
            except (ValueError, TypeError) as error:
                problems.append("Invalid run report: " + str(error))
        else:
            problems.append("No run report was written.")
        item = {"kind": "desktop", "path": "APIs/Python/Test_Scripts/" + test.name, "label": test.label,
                "status": "passed" if not problems else "blocked" if timed_out else "failed",
                "exit": process.returncode, "expectedExit": test.expected_code, "expectedReportChecks": test.report_checks,
                "seconds": time.monotonic() - started, "command": command,
                "log": log.name, "report": str(report.relative_to(output)) if report.exists() else None, "problems": problems}
        outcomes.append(item)
        write_json(output / "outcomes.json", outcomes)
        print(f"{item['status'].upper()}: {test.label} ({item['seconds']:.1f}s)" + (" — " + "; ".join(problems) if problems else ""), flush=True)
    return outcomes


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True, help="New directory; never overwrites an existing baseline.")
    parser.add_argument("--exe", type=Path, default=REPO / "macOS/PrePoMax/bin/Release/net10.0/PrePoMax.com")
    parser.add_argument("--run-api", action="store_true")
    parser.add_argument("--filter", nargs="+")
    parser.add_argument("--settings", type=Path, help="Private, disposable test profile; never captured or printed.")
    parser.add_argument("--reference-manifest", type=Path, help="Reuse a frozen source/runtime capture for a targeted reference cohort.")
    parser.add_argument("--candidate-only", action="store_true", help="Capture the independent nested VTKJS source tree without running reference tests.")
    parser.add_argument("--timeout-cap", type=int, help="Optional baseline-only cap; capped runs are blocked, never passed.")
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    if args.candidate_only:
        capture_candidate(output)
        return 0
    executable = args.exe.resolve()
    if not executable.is_file():
        parser.error("Build Prepomac first or supply --exe.")
    if args.reference_manifest:
        original = args.reference_manifest.resolve()
        manifest = json.loads(original.read_text())
        for entry in manifest["referenceBinaries"]:
            if digest(REPO / entry["path"]) != entry["sha256"]:
                raise RuntimeError("Reference binary drift: " + entry["path"])
        (output / "reference-capture.json").write_bytes(original.read_bytes())
        write_json(output / "capture-link.json", {"sourceManifest": str(original), "snapshot": "reference-capture.json", "sha256": digest(original)})
    else:
        manifest = capture(output, executable)
    outcomes = api_suite(output, executable, args.filter, args.timeout_cap, args.settings.resolve() if args.settings else None) if args.run_api else []
    changed = [entry["path"] for entry in manifest["sources"] if not (REPO / entry["path"]).is_file() or digest(REPO / entry["path"]) != entry["sha256"]]
    trial_prefix = TRIAL.relative_to(REPO).as_posix() + "/"
    reference_changes = [path for path in changed if not path.startswith(trial_prefix)]
    candidate_changes = [path for path in changed if path.startswith(trial_prefix)]
    verification = {"referenceSourceChangedDuringRun": reference_changes, "candidateSourceChangedDuringRun": candidate_changes}
    write_json(output / "source-verification.json", verification)
    summary = {"baseline": str(output), "sourcesUnchanged": not reference_changes,
               "counts": {status: sum(item["status"] == status for item in outcomes) for status in ["passed", "failed", "blocked"]},
               "claim": "Reference execution outcomes only. No web parity is established by these runs."}
    write_json(output / "summary.json", summary)
    print(json.dumps(summary, indent=2))
    return 0 if not reference_changes else 1


if __name__ == "__main__":
    raise SystemExit(main())

#!/usr/bin/env python3
"""Publish small, reviewable M00 evidence and reconcile reference outcomes with the plan.

Large archives/logs stay in ignored local artifacts. Full source-script web
coverage remains uncovered; the initial differential cases qualify the harness.
"""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil

TRIAL = Path(__file__).resolve().parents[1]
REPO = TRIAL.parents[2]
PLAN = TRIAL / "docs/parity/plan.json"


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def write(path, value):
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, indent=2) + "\n")
    temporary.replace(path)


def relative(path):
    return path.resolve().relative_to(REPO).as_posix()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reference", type=Path, required=True)
    parser.add_argument("--additional-reference", type=Path, action="append", default=[])
    parser.add_argument("--custom", type=Path, required=True)
    parser.add_argument("--harness", type=Path, required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    parser.add_argument("--candidate-capture", type=Path, help="Optional independently captured nested VTKJS repository source manifest.")
    parser.add_argument("--complete-m00", action="store_true")
    args = parser.parse_args()
    root = args.reference.resolve()
    custom = args.custom.resolve()
    harness = args.harness.resolve()
    evidence = args.evidence.resolve()
    evidence.mkdir(parents=True, exist_ok=True)
    capture_path = root / ("capture.json" if (root / "capture.json").exists() else "manifest.json")
    capture = json.loads(capture_path.read_text())
    harness_inputs = json.loads((harness / "inputs.json").read_text())
    if harness_inputs["baselineManifestSha256"] != digest(capture_path):
        raise RuntimeError("The harness did not use this immutable reference capture.")
    source_prefix = TRIAL.relative_to(REPO).as_posix() + "/"
    reference_sources = [entry for entry in capture["sources"] if not entry["path"].startswith(source_prefix)]
    reference_drift = [entry["path"] for entry in reference_sources if not (REPO / entry["path"]).is_file() or digest(REPO / entry["path"]) != entry["sha256"]]
    binary_drift = [entry["path"] for entry in capture["referenceBinaries"] if digest(REPO / entry["path"]) != entry["sha256"]]
    if reference_drift or binary_drift:
        raise RuntimeError("The reference changed after capture: " + ", ".join(reference_drift + binary_drift))
    for name, expected in capture["archives"].items():
        if digest(root / name) != expected:
            raise RuntimeError("Captured archive hash mismatch: " + name)
    qualified = json.loads((harness / "summary.json").read_text())
    if not qualified["passed"]:
        raise RuntimeError("The differential harness has unresolved differences.")
    spec = importlib.util.spec_from_file_location("parity_compare_evidence", TRIAL / "tests/parity/compare.py")
    comparator = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(comparator)
    for kind in ["model", "field"]:
        ref = json.loads((harness / ("desktop-" + kind) / "snapshot.json").read_text())
        web = json.loads((harness / ("web-" + kind + ".json")).read_text())
        report = comparator.compare(ref, web)
        if not report["passed"]:
            raise RuntimeError("Current comparator disagrees with the captured pair: " + kind)
        write(evidence / ("reference-" + kind + ".json"), ref)
        write(evidence / ("web-" + kind + ".json"), web)
        write(evidence / (kind + "-comparison.json"), report)
    for name in ["summary.json", "wrong-property.json", "wrong-material-value.json", "wrong-source-id.json", "wrong-field-value.json"]:
        shutil.copyfile(harness / name, evidence / ("harness-" + name))
    by_label = {}
    offline = {}
    for directory in [root, *[path.resolve() for path in args.additional_reference]]:
        for item in json.loads((directory / "outcomes.json").read_text()):
            attempt = {key: item[key] for key in ["label", "status", "exit", "expectedExit", "seconds"]}
            attempt["cohort"] = relative(directory)
            log = directory / item["log"]
            attempt["log"] = {"path": relative(log), "sha256": digest(log)}
            if item.get("report"):
                report = directory / item["report"]
                attempt["report"] = {"path": relative(report), "sha256": digest(report)}
            attempt["problems"] = item.get("problems", [])
            target = offline if item["kind"] == "offline" else by_label
            target.setdefault(item["path"] if item["kind"] == "offline" else item["label"], []).append(attempt)
    custom_summary = json.loads((custom / "summary.json").read_text())
    checks = dict(offline)
    for item in custom_summary["outcomes"]:
        record = {key: item[key] for key in ["status", "reason", "exit", "seconds", "mode"] if key in item}
        if item.get("log"):
            path = custom / item["log"]
            record["log"] = {"path": relative(path), "sha256": digest(path)}
        checks[item["path"]] = [record]
    plan = json.loads(PLAN.read_text())
    missing = [run["label"] for run in plan["declared_desktop_runs"] if run["label"] not in by_label]
    missing += [check["path"] for check in plan["reference_checks"] if check["path"] not in checks]
    if args.complete_m00 and (missing or not (root / "summary.json").is_file()):
        raise RuntimeError("Reference execution is incomplete: " + ", ".join(missing))
    outcome_file = evidence / "reference-outcomes.json"
    write(outcome_file, {"desktopRuns": by_label, "offlineAndCustom": checks,
                         "selectionPolicy": "Last supplied cohort per label, in CLI argument order; controlled follow-up cohorts override the original configuration, not by wall-clock ordering. Earlier attempts are retained. Reference sources/binaries are unchanged.",
                         "claim": "Reference outcomes only; no full source script has matched web coverage from these results."})
    for run in plan["declared_desktop_runs"]:
        if run["label"] in by_label:
            run["status"] = by_label[run["label"]][-1]["status"]
            run["evidence"] = [relative(outcome_file)]
    for check in plan["reference_checks"]:
        if check["path"] in checks:
            check["status"] = checks[check["path"]][-1]["status"]
            check["evidence"] = [relative(outcome_file)]
    counts = {status: sum(attempts[-1]["status"] == status for attempts in by_label.values()) for status in ["passed", "failed", "blocked"]}
    check_counts = {status: sum(attempts[-1]["status"] == status for attempts in checks.values()) for status in ["passed", "failed", "blocked"]}
    summary = {
        "schema": "prepomac.M00-evidence/1", "milestoneComplete": args.complete_m00,
        "gitHead": capture["gitHead"], "capturedUtc": capture["capturedUtc"],
        "immutableCapture": {"path": relative(capture_path), "sha256": digest(capture_path)},
        "rootReferenceSources": len(reference_sources), "referenceRuntimeFiles": len(capture["referenceBinaries"]),
        "referenceSourceDrift": reference_drift, "referenceBinaryDrift": binary_drift,
        "sourceAndRuntimeArchives": capture["archives"],
        "sourceScope": "Root Prepomac tracked/nonignored source inputs and runtime binaries. The nested VTKJS repository is captured independently; personal projects and runtime settings are excluded.",
        "externalSolver": {"sha256": custom_summary["solver"]["sha256"]},
        "shippedPmxFixtures": custom_summary["shippedFixtures"],
        "desktopRunsRecorded": len(by_label), "desktopOutcomes": counts,
        "offlineAndCustomChecksRecorded": len(checks), "offlineAndCustomOutcomes": check_counts,
        "remainingUnattempted": missing, "harnessPassed": True,
        "harnessInputsSha256": digest(harness / "inputs.json"), "harnessImplementation": harness_inputs["implementationSha256"],
        "claim": "M00 baseline/harness qualification. Reference failures/preconditions remain open. Full feature and full-script web parity is not claimed.",
    }
    if args.candidate_capture:
        candidate_path = args.candidate_capture.resolve()
        candidate = json.loads(candidate_path.read_text())
        for entry in candidate["sources"]:
            if digest(TRIAL / entry["path"]) != entry["sha256"]:
                raise RuntimeError("Candidate source drift: " + entry["path"])
        if digest(candidate_path.parent / "sources.tar.gz") != candidate["archiveSha256"]:
            raise RuntimeError("Candidate archive hash mismatch.")
        summary["candidateCapture"] = {"path": relative(candidate_path), "sha256": digest(candidate_path),
                                       "gitHead": candidate["gitHead"], "sourceInputs": len(candidate["sources"]),
                                       "archiveSha256": candidate["archiveSha256"]}
    write(evidence / "summary.json", summary)
    for milestone in plan["milestones"]:
        if milestone["id"] == "M00":
            milestone["status"] = "complete" if args.complete_m00 else "in_progress"
            milestone["completion_evidence"] = [relative(evidence / "summary.json")] if args.complete_m00 else []
    for case in plan["test_cases"]:
        if case["id"] == "M00-T01":
            case["status"] = "passed" if args.complete_m00 else "in_progress"
            case["implementation_paths"] = [relative(TRIAL / "scripts" / name) for name in ["capture-prepomac-baseline.py", "run-prepomac-custom-baseline.py", "record-parity-evidence.py"]]
            case["evidence"] = [relative(evidence / "summary.json"), relative(outcome_file)]
        elif case["id"] == "M00-T02":
            case["status"] = "passed"
            case["implementation_paths"] = [relative(TRIAL / path) for path in ["scripts/run-web-parity.py", "tests/parity/compare.py", "tests/parity/reference_snapshot.py", "portable/PrePoMax.Application/ProjectParitySnapshot.cs", "portable/PrePoMax.Web/ParityEndpoints.cs", "portable/tests/Parity.Probe/Program.cs"]]
            case["evidence"] = [relative(evidence / (name + ".json")) for name in ["harness-summary", "model-comparison", "field-comparison", "harness-wrong-property", "harness-wrong-material-value", "harness-wrong-source-id", "harness-wrong-field-value"]]
    plan["baseline"]["full_source_capture_complete"] = True
    plan["baseline"]["reference_baseline_executed"] = args.complete_m00
    plan["baseline"]["evidence"] = [relative(evidence / "summary.json")]
    # Validate before publishing registry state; numerical outcomes are not synthesized here.
    validator_spec = importlib.util.spec_from_file_location("parity_plan_evidence_validator", TRIAL / "scripts/check-web-parity-plan.py")
    validator = importlib.util.module_from_spec(validator_spec)
    validator_spec.loader.exec_module(validator)
    validator.validate(plan, check_hashes=True)
    write(PLAN, plan)
    validator.CATALOGUE.write_text(validator.render_catalogue(plan))
    print(json.dumps({"M00": "complete" if args.complete_m00 else "in_progress", "desktop": counts, "offlineAndCustom": check_counts, "unattempted": len(missing)}, indent=2))


if __name__ == "__main__":
    main()

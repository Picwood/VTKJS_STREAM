#!/usr/bin/env python3
"""Validate the parity roadmap registry; optionally generate its Markdown catalogue.

This checks planning metadata and source inventory, not engineering parity.
No application, solver, browser or reference test is executed.
"""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import sys

TRIAL = Path(__file__).resolve().parents[1]
REPOSITORY = TRIAL.parents[2]
REGISTRY = TRIAL / "docs/parity/plan.json"
CATALOGUE = TRIAL / "docs/PREPOMAC_PARITY_TESTS.md"
API = "APIs/Python/Test_Scripts/"


def source_file(path):
    if not isinstance(path, str) or Path(path).is_absolute():
        raise ValueError(f"Expected a repository-relative file path: {path!r}")
    resolved = (REPOSITORY / path).resolve()
    if not resolved.is_relative_to(REPOSITORY) or not resolved.is_file():
        raise ValueError(f"Missing or out-of-repository file: {path}")
    return resolved


def indexed(items, kind):
    result = {}
    for item in items:
        key = item["id"]
        if key in result:
            raise ValueError(f"Duplicate {kind} ID: {key}")
        result[key] = item
    return result


def collect_reference_runs():
    spec = importlib.util.spec_from_file_location("prepomac_parity_reference_headers", source_file(API + "Run_Tests.py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    runs = []
    for test in module.collect(None):
        runs.append({
            "label": test.label, "path": API + test.name,
            "switches": test.switches, "expected_exit": test.expected_code,
            "expected_report_checks": test.report_checks,
            "needs_generated_result": test.needs_result,
            "fixture": "generated:Test_Analysis.py/PyTest_Job.frd" if test.needs_result
            else API + "Models/" + test.model if test.model else None,
        })
    # Convert parser tuples to the registry's JSON arrays without altering semantics.
    return json.loads(json.dumps(runs)), module.collect_offline(None)


def validate(plan, check_hashes):
    if plan["schema_version"] != 1 or plan["path_base"] != "repository-root":
        raise ValueError("Unsupported registry schema or path base")
    milestones = indexed(plan["milestones"], "milestone")
    cases = indexed(plan["test_cases"], "case")
    indexed(plan["releases"], "release")
    source_paths = set()
    drift = []
    for entry in plan["reference_sources"]:
        path = entry["path"]
        if path in source_paths:
            raise ValueError(f"Duplicate source path: {path}")
        source_paths.add(path)
        actual = hashlib.sha256(source_file(path).read_bytes()).hexdigest()
        if actual != entry["sha256"]:
            drift.append(path)
        expected_cases = sorted(c["id"] for c in cases.values() if path in c["reference_paths"])
        if sorted(entry["cases"]) != expected_cases or not expected_cases:
            raise ValueError(f"Source-to-case mapping mismatch: {path}")

    policies = {"exact-domain", "geometry-visual", "native-numerical", "field-numerical-visual",
                "meshing-semantic", "composite-source-tolerances", "fatigue-source-tolerances", "hardware-qualified"}
    dependencies = {key: set(value["depends_on"]) for key, value in milestones.items()}
    for case_id, case in cases.items():
        milestone = case["milestone"]
        if milestone not in milestones or not re.fullmatch(re.escape(milestone) + r"-T\d{2}", case_id):
            raise ValueError(f"Invalid case/milestone association: {case_id}")
        if case["status"] not in {"planned", "in_progress", "passed", "failed", "blocked", "not_run", "excluded"}:
            raise ValueError(f"Unknown case status: {case_id}")
        if case["comparison_policy"] not in policies:
            raise ValueError(f"Unknown comparison policy: {case_id}")
        if case["reference_mode"] not in {"desktop", "web_only", "mixed"}:
            raise ValueError(f"Unknown reference mode: {case_id}")
        if case["reference_mode"] == "web_only" and not case["notes"]:
            raise ValueError(f"Web-only case needs a rationale: {case_id}")
        if not case["reference_paths"] or not set(case["reference_paths"]) <= source_paths:
            raise ValueError(f"Unregistered reference source: {case_id}")
        if not case["fixtures"] or not case["steps"] or not case["expected"]:
            raise ValueError(f"Incomplete acceptance specification: {case_id}")
        dependencies[milestone].update(case["additional_dependencies"])
        for path in case["implementation_paths"] + case["evidence"]:
            source_file(path)
        if case["status"] == "passed" and (not case["implementation_paths"] or not case["evidence"]):
            raise ValueError(f"Passed case lacks implementation/evidence: {case_id}")
        if case["status"] == "excluded" and (not case["notes"] or not case["evidence"]):
            raise ValueError(f"Excluded case needs an explicit recorded scope decision: {case_id}")

    visiting, visited = set(), set()

    def visit(milestone):
        if milestone not in milestones:
            raise ValueError(f"Unknown dependency: {milestone}")
        if milestone in visiting:
            raise ValueError(f"Dependency cycle at {milestone}")
        if milestone in visited:
            return
        visiting.add(milestone)
        for dependency in dependencies[milestone]:
            visit(dependency)
        visiting.remove(milestone)
        visited.add(milestone)

    for milestone, entry in milestones.items():
        visit(milestone)
        matching = [case for case in cases.values() if case["milestone"] == milestone]
        if not matching:
            raise ValueError(f"Milestone has no acceptance cases: {milestone}")
        if entry["status"] not in {"planned", "in_progress", "complete", "blocked"}:
            raise ValueError(f"Unknown milestone status: {milestone}")
        if entry["status"] == "complete":
            if any(case["status"] != "passed" for case in matching) or not entry["completion_evidence"]:
                raise ValueError(f"Complete milestone has unresolved cases or no evidence: {milestone}")
            if any(milestones[dep]["status"] != "complete" for dep in dependencies[milestone]):
                raise ValueError(f"Complete milestone has incomplete prerequisites: {milestone}")
        for path in entry["completion_evidence"]:
            source_file(path)

    for release in plan["releases"]:
        if release["status"] not in {"planned", "implemented_subset", "complete"}:
            raise ValueError(f"Unknown release status: {release['id']}")
        for milestone in release["required_milestones"]:
            if milestone not in milestones:
                raise ValueError(f"Unknown release prerequisite: {milestone}")
            if release["status"] == "complete" and milestones[milestone]["status"] != "complete":
                raise ValueError(f"Release marked complete before its milestones: {release['id']}")

    runs, offline = collect_reference_runs()
    registered = [{key: value for key, value in run.items() if key not in {"status", "evidence", "web_coverage", "web_evidence"}}
                  for run in plan["declared_desktop_runs"]]
    if runs != registered:
        raise ValueError("Declared desktop invocations/fixtures/expected reports changed; reconcile the registry with Run_Tests.py")
    if len({run["label"] for run in runs}) != len(runs):
        raise ValueError("Duplicate desktop run labels")
    required = {API + file.name for file in (REPOSITORY / API).glob("Test_*.py")}
    required.update(API + name for name in offline)
    required.update("macOS/" + file.name for file in (REPOSITORY / "macOS").glob("verify-*.py"))
    if required - source_paths:
        raise ValueError("Unmapped reference scripts: " + ", ".join(sorted(required - source_paths)))
    baseline = plan["baseline"]
    counts = (len({run["path"] for run in runs}), len(runs), len(list((REPOSITORY / "macOS").glob("verify-*.py"))))
    if counts != (baseline["desktop_scripts"], baseline["declared_desktop_runs"], baseline["custom_verification_scripts"]):
        raise ValueError("Reference inventory counts changed")
    if offline != baseline["offline_reference_checks"]:
        raise ValueError("Offline reference inventory changed")
    for run in plan["declared_desktop_runs"]:
        if run["status"] not in {"not_run", "passed", "failed", "blocked"}:
            raise ValueError(f"Unknown desktop outcome: {run['label']}")
        if run["status"] == "passed" and not run["evidence"]:
            raise ValueError(f"Desktop pass lacks evidence: {run['label']}")
        if run["web_coverage"] not in {"uncovered", "partial", "full"}:
            raise ValueError(f"Unknown web coverage state: {run['label']}")
        if run["web_coverage"] != "uncovered" and not run["web_evidence"]:
            raise ValueError(f"Web run coverage lacks evidence: {run['label']}")
        fixture = run["fixture"]
        if fixture and not fixture.startswith("generated:"):
            source_file(fixture)
        for path in run["evidence"] + run["web_evidence"]:
            source_file(path)
    check_paths = set()
    for check in plan["reference_checks"]:
        if check["path"] in check_paths or check["path"] not in source_paths:
            raise ValueError(f"Duplicate or unregistered reference check: {check['path']}")
        check_paths.add(check["path"])
        if check["kind"] not in {"offline", "custom"} or check["status"] not in {"not_run", "passed", "failed", "blocked"}:
            raise ValueError(f"Unknown reference check kind/status: {check['path']}")
        if not check["invocation_notes"] or check["status"] == "passed" and not check["evidence"]:
            raise ValueError(f"Reference check lacks invocation/evidence: {check['path']}")
        for path in check["evidence"]:
            source_file(path)
    expected_checks = {API + name for name in offline} | {"macOS/" + file.name for file in (REPOSITORY / "macOS").glob("verify-*.py")}
    if check_paths != expected_checks:
        raise ValueError("Offline/custom reference check outcome inventory is incomplete")
    if milestones["M00"]["status"] == "complete":
        if not baseline["full_source_capture_complete"] or not baseline["reference_baseline_executed"]:
            raise ValueError("M00 cannot complete before production baseline capture and reference outcomes")
        if any(run["status"] == "not_run" for run in plan["declared_desktop_runs"] + plan["reference_checks"]):
            raise ValueError("M00 reference inventory still has unattempted outcomes")
    if cases["M14-T03"]["status"] == "passed":
        if any(run["status"] != "passed" or run["web_coverage"] != "full" for run in plan["declared_desktop_runs"]):
            raise ValueError("Final parity cannot pass before every declared reference run has matched web coverage")
        if any(check["status"] != "passed" for check in plan["reference_checks"]):
            raise ValueError("Final parity still has unresolved offline/custom reference checks")
    if drift and check_hashes:
        raise ValueError("Reference source hash drift; review before accepting a new baseline: " + ", ".join(drift))
    return drift


def source_link(path):
    relative = Path(os.path.relpath(REPOSITORY / path, CATALOGUE.parent)).as_posix()
    return f"[{Path(path).name}]({relative})"


def render_catalogue(plan):
    baseline = plan["baseline"]
    case_statuses = {status: sum(case["status"] == status for case in plan["test_cases"])
                     for status in sorted({case["status"] for case in plan["test_cases"]})}
    lines = ["# Prepomac parity acceptance catalogue", "",
             f"Generated from [parity/plan.json](parity/plan.json), updated {plan['updated']}. "
             "Edit the registry and regenerate with `python3 scripts/check-web-parity-plan.py --write-catalogue` "
             "from the trial root.", "",
             f"**{len(plan['test_cases'])} acceptance cases** ({', '.join(f'{count} {status}' for status, count in case_statuses.items())}) map {baseline['desktop_scripts']} desktop scripts "
             f"({baseline['declared_desktop_runs']} declared runs), {len(baseline['offline_reference_checks'])} offline checks "
             f"and {baseline['custom_verification_scripts']} custom verifiers. These are mappings, not passing test results. "
             "The registry separately tracks desktop run outcomes and web-case evidence.", "",
             "Implementation order, release gates, comparison rules, numerical thresholds and evidence requirements "
             "are in the [roadmap](PREPOMAC_IMPLEMENTATION_ROADMAP.md). Cases with `web_only` reference mode "
             "cover online behavior absent from desktop Prepomac. `mixed` cases retain desktop semantics and add "
             "an explicitly adapted hosted/native boundary.", "",
             "## Common execution contract", "",
             "Use identical immutable fixture copies, units and recorded toolchains. Run the reference operation "
             "and web/application operation, then exercise the actual browser task. Compare every listed output "
             "with its declared policy, save/reopen, and check rejection/cancellation behavior. Retain both outputs "
             "and a machine-readable diff. Where no original oracle/fixture exists, create and review one before "
             "claiming a pass. Do not delete assertions to fit a different host.", ""]
    for milestone in plan["milestones"]:
        lines.extend([f"## {milestone['id']} — {milestone['title']}", "",
                      f"Milestone prerequisites: {', '.join(milestone['depends_on']) or 'none'}. "
                      f"Status: **{milestone['status']}**.", ""])
        for case in (case for case in plan["test_cases"] if case["milestone"] == milestone["id"]):
            lines.extend([f"### {case['id']} — {case['title']}", "",
                          f"Status: **{case['status']}**. Policy: `{case['comparison_policy']}`. "
                          f"Reference mode: `{case['reference_mode']}`.", "",
                          "References: " + ", ".join(source_link(path) for path in case["reference_paths"]) + ".", "",
                          "Fixtures: " + "; ".join(case["fixtures"]) + ".", ""])
            if case["additional_dependencies"]:
                lines.extend(["Additional completion prerequisites: " + ", ".join(case["additional_dependencies"]) + ".", ""])
            lines.extend([f"{index}. {expected}" for index, expected in enumerate(case["expected"], 1)])
            lines.append("")
            if case["notes"]:
                lines.extend([case["notes"], ""])
            if case["implementation_paths"]:
                lines.extend(["Implementation: " + ", ".join(source_link(path) for path in case["implementation_paths"]) + ".", ""])
            lines.extend(["Evidence: " + (", ".join(source_link(path) for path in case["evidence"]) or "none recorded") + ".", ""])
    lines.extend(["## Declared Prepomac desktop runs", "",
                  "Collected using the existing runner's header parser. Keep each invocation's expected exit "
                  "and report checks from the registry; successful handling of an expected error is not a failing test. "
                  "Generated `PyTest_Job.frd` cases depend on `Test_Analysis.py`. No run below was executed "
                  "as part of writing this roadmap.", "",
                  "| Declared run | Source fixture | Expected exit | Reference outcome | Matched web coverage |",
                  "| --- | --- | ---: | --- | --- |"])
    for run in plan["declared_desktop_runs"]:
        fixture = ("Generated by Test_Analysis.py" if run["needs_generated_result"] else
                   source_link(run["fixture"]) if run["fixture"] else "No initial model")
        relative = Path(os.path.relpath(REPOSITORY / run["path"], CATALOGUE.parent)).as_posix()
        lines.append(f"| [{run['label']}]({relative}) | {fixture} | {run['expected_exit']} | {run['status']} | {run['web_coverage']} |")
    lines.extend(["", "## Offline and custom reference checks", "",
                  "Reference execution is tracked separately from matched web-case evidence. An offline "
                  "numerical check and its application/GPU integration path are distinct qualifications.", "",
                  "| Check | Kind | Outcome | Invocation notes |", "| --- | --- | --- | --- |"])
    for check in plan["reference_checks"]:
        lines.append(f"| {source_link(check['path'])} | {check['kind']} | {check['status']} | {check['invocation_notes']} |")
    lines.extend(["", "The JSON registry also records every declared switch/report condition and source SHA-256. "
                  "A source hash identifies a reference input, not a production build, numerical qualification or pass.", ""])
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check-source-hashes", action="store_true")
    parser.add_argument("--write-catalogue", action="store_true")
    args = parser.parse_args()
    try:
        plan = json.loads(REGISTRY.read_text())
        drift = validate(plan, args.check_source_hashes)
        rendered = render_catalogue(plan)
        if args.write_catalogue:
            CATALOGUE.write_text(rendered)
        elif not CATALOGUE.is_file() or CATALOGUE.read_text() != rendered:
            raise ValueError("Catalogue is missing or stale; regenerate with --write-catalogue")
    except (KeyError, TypeError, ValueError, OSError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        return 1
    if drift:
        print("SOURCE HASH DRIFT (review required): " + ", ".join(drift))
    print(f"PASS: {len(plan['milestones'])} milestones, {len(plan['test_cases'])} acceptance cases, "
          f"{len(plan['reference_sources'])} source mappings and {len(plan['declared_desktop_runs'])} "
          "declared desktop runs; dependencies, fixtures, expected outcomes and evidence rules validated.")
    print("Metadata integrity only: this validator runs no application/solver/GPU acceptance; recorded outcomes are tracked separately.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

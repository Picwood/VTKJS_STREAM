#!/usr/bin/env python3
"""Opt-in CalculiX acceptance; preserves all job files for diagnosis."""
import json
import math
import os
import re
from pathlib import Path
import shutil
import subprocess
import tempfile
import zipfile

repo = Path(__file__).resolve().parent.parent
builder = Path(os.environ.get("PREPOMAX_CALCULIX_BUILDER", str(Path.home() / "CalculiX-Builder")))
bundled = repo / "CalculiX/ccx_2.23/src/ccx_2.23"
default_solver = builder / "src/ccx_2.21_MT" if os.environ.get("PREPOMAX_CALCULIX_BUILDER") or not bundled.is_file() else bundled
solver = Path(os.environ.get("PREPOMAX_CCX", str(default_solver))).resolve()
runtime = Path(os.environ.get("PREPOMAX_CCX_LIBRARY_PATH", str(
    Path(os.environ.get("XDG_DATA_HOME", str(Path.home() / ".local/share"))) / "prepomax/ccx-runtime/usr/lib/x86_64-linux-gnu")))
solver_environment = os.environ.copy()
if runtime.is_dir():
    solver_environment["LD_LIBRARY_PATH"] = str(runtime) + (os.pathsep + solver_environment["LD_LIBRARY_PATH"] if solver_environment.get("LD_LIBRARY_PATH") else "")
if not solver.is_file() or not os.access(solver, os.X_OK):
    raise SystemExit(f"CalculiX build pending: no executable at {solver}. Set PREPOMAX_CCX to a completed build.")
dotnet = os.environ.get("PREPOMAX_DOTNET") or shutil.which("dotnet") or str(
    Path(os.environ.get("XDG_DATA_HOME", str(Path.home() / ".local/share"))) / "prepomax/dotnet/dotnet")
work = Path(tempfile.mkdtemp(prefix="prepomax calculix job "))
def generate_cube(path, steps, parameters=None, mechanical=False, multipart=False):
    # Compare complete generated inputs across locales before solving the Italian one.
    english = path.with_suffix(".en-US.inp")
    for output, culture in [(english, "en-US"), (path, "it-IT")]:
        subprocess.run([dotnet, "run", "--project", str(repo / "portable/tests/MeshIO.Characterization"),
                        "--configuration", "Release", "--",
                        "--write-multipart-cube" if multipart else ("--write-mechanical-block" if mechanical else ("--write-static-block" if parameters else "--write-static-cube")),
                        str(output), str(steps), culture] + ([str(x) for x in parameters] if parameters else []),
                       cwd=repo / "portable", timeout=180, check=True)
    # Independently constructed FeModels intentionally have different random IDs.
    def comparable(deck):
        return re.sub(rb"(?m)^Hash: [^,\r\n]+,", b"Hash: <model-id>,", deck.read_bytes())
    if comparable(english) != comparable(path):
        raise SystemExit(f"Locale-dependent generated input: {path}")
    print(f"PASS: complete {steps}-step input matches in en-US and it-IT (excluding model ID)", flush=True)
generate_cube(work / "cube.inp", 1)
print(f"Solver: {solver}\nJob files: {work}", flush=True)
result = subprocess.run([dotnet, "run", "--project", str(repo / "portable/tests/MeshIO.Characterization"),
                         "--configuration", "Release", "--", "--run-solver", str(solver), str(work), "cube"],
                        cwd=repo / "portable", timeout=180, env=solver_environment)
if result.returncode:
    raise SystemExit(f"Solver invocation failed; inspect {work}")
dat = work / "cube.dat"
if not dat.is_file():
    raise SystemExit(f"Missing solver displacement output; inspect {work}")
found = {}
reading = False
for line in dat.read_text().splitlines():
    if "displacements" in line.lower():
        reading = True
        continue
    if not reading:
        continue
    fields = line.split()
    if len(fields) == 4 and fields[0].isdigit():
        node = int(fields[0])
        if node in (2, 3, 6, 7):
            found[node] = [float(x.replace("D", "E")) for x in fields[1:]]
if set(found) != {2, 3, 6, 7} or any(not all(math.isfinite(x) for x in u) or abs(u[0] - .001) > 1e-8 or abs(u[1]) > 1e-10 or abs(u[2]) > 1e-10 for u in found.values()):
    raise SystemExit(f"Unexpected cube displacements: {found}; inspect {work}")
print("PASS: CalculiX unit cube right-face U1=0.001; persistent logs and results retained.")

subprocess.run([dotnet, "run", "--project", str(repo / "portable/tests/CaeResults.Characterization"),
                "--configuration", "Release", "--", "--check-cube", str(dat)],
               cwd=repo / "portable", timeout=180, check=True)

step_work = work / "two-step"
step_work.mkdir()
generate_cube(step_work / "cube-two-step.inp", 2)
subprocess.run([dotnet, "run", "--project", str(repo / "portable/tests/MeshIO.Characterization"),
                "--configuration", "Release", "--", "--run-solver", str(solver), str(step_work), "cube-two-step"],
               cwd=repo / "portable", timeout=180, env=solver_environment, check=True)
subprocess.run([dotnet, "run", "--project", str(repo / "portable/tests/CaeResults.Characterization"),
                "--configuration", "Release", "--", "--check-two-step", str(step_work / "cube-two-step.frd")],
               cwd=repo / "portable", timeout=180, check=True)

# Analytic rectangular-bar cases vary geometry, stiffness and force independently.
# For nu=0 and uniform axial traction, u = F L / (E width height).
for name, parameters in [
    ("long-bar", (2.5, 1.25, 0.5, 2000, 3.75)),
    ("compression", (0.75, 2, 1.5, 750, -2.25)),
    ("unloaded", (1.5, 0.75, 2, 1250, 0)),
    ("mechanical", (1, 1, 1, 1000, 1)),
]:
    case_work = work / name
    case_work.mkdir()
    generate_cube(case_work / (name + ".inp"), 1, parameters, mechanical=name == "mechanical")
    subprocess.run([dotnet, "run", "--project", str(repo / "portable/tests/MeshIO.Characterization"),
                    "--configuration", "Release", "--", "--run-solver", str(solver), str(case_work), name],
                   cwd=repo / "portable", timeout=180, env=solver_environment, check=True)
    found = {}
    reading = False
    for line in (case_work / (name + ".dat")).read_text().splitlines():
        if "displacements" in line.lower():
            reading = True
            continue
        fields = line.split()
        if reading and len(fields) == 4 and fields[0].isdigit():
            node = int(fields[0])
            if node in (2, 3, 6, 7):
                found[node] = [float(x.replace("D", "E")) for x in fields[1:]]
    length, width, height, modulus, force = parameters
    expected = force * length / (modulus * width * height)
    if name == "mechanical":
        # Uniform axial body force adds rho*g*L^2/(2E), with nu=0.
        expected += 2.5 * .8 * length ** 2 / (2 * modulus)
    if set(found) != {2, 3, 6, 7} or any(
        not all(math.isfinite(x) for x in u)
        or not math.isclose(u[0], expected, rel_tol=1e-5, abs_tol=1e-10)
        or abs(u[1]) > 1e-10 or abs(u[2]) > 1e-10 for u in found.values()
    ):
        raise SystemExit(f"{name}: expected U1={expected}, got {found}; inspect {case_work}")
    print(f"PASS: {name} analytical displacement U1={expected}", flush=True)

# Two independent cubes with E=1000 and E=2000 validate preserved part assignments.
multipart_work = work / "multipart"
multipart_work.mkdir()
generate_cube(multipart_work / "multipart.inp", 1, multipart=True)
subprocess.run([dotnet, "run", "--project", str(repo / "portable/tests/MeshIO.Characterization"),
                "--configuration", "Release", "--", "--run-solver", str(solver), str(multipart_work), "multipart"],
               cwd=repo / "portable", timeout=180, env=solver_environment, check=True)
found = {}
for line in (multipart_work / "multipart.dat").read_text().splitlines():
    fields = line.split()
    if len(fields) == 4 and fields[0].isdigit() and int(fields[0]) in (2, 3, 6, 7, 10, 11, 14, 15):
        found[int(fields[0])] = [float(x.replace("D", "E")) for x in fields[1:]]
if set(found) != {2, 3, 6, 7, 10, 11, 14, 15} or any(
    not all(math.isfinite(x) for x in u)
    or not math.isclose(u[0], .001 if node < 9 else .0005, rel_tol=1e-5, abs_tol=1e-10)
    or abs(u[1]) > 1e-10 or abs(u[2]) > 1e-10 for node, u in found.items()
):
    raise SystemExit(f"Unexpected multipart displacements: {found}")
print("PASS: multipart material assignments give U1=0.001 and U1=0.0005", flush=True)

# Persist each actual solve alongside editable metadata and verify reopening.
for input_path in [work / "cube.inp", step_work / "cube-two-step.inp"] + [
    work / name / (name + ".inp") for name in ("long-bar", "compression", "unloaded", "mechanical", "multipart")
]:
    subprocess.run([dotnet, "run", "--project", str(repo / "portable/tests/MeshIO.Characterization"),
                    "--configuration", "Release", "--", "--attach-project-results",
                    str(input_path) + ".pma", str(input_path.with_suffix(".frd")), str(input_path.with_suffix(".dat"))],
                   cwd=repo / "portable", timeout=180, check=True)

# Exercise the application executable independently of the characterization runner.
cli_project = repo / "portable/PrePoMax.Cli"
subprocess.run([dotnet, "build", str(cli_project), "-c", "Release"],
               cwd=repo / "portable", timeout=180, check=True, stdout=subprocess.DEVNULL)
cli = [dotnet, str(cli_project / "bin/Release/net10.0/PrePoMax.Cli.dll")]
source = multipart_work / "multipart.inp.pma"
before = source.read_bytes()
inspection = subprocess.run(cli + ["inspect", str(source)], capture_output=True, text=True, check=True)
assert len(json.loads(inspection.stdout)["Parts"]) == 2
exported = work / "cli exported.inp"
subprocess.run(cli + ["export", str(source), str(exported)], check=True)
export_before = exported.read_bytes()
assert subprocess.run(cli + ["export", str(source), str(exported)], capture_output=True).returncode == 1
assert exported.read_bytes() == export_before
solved = subprocess.run(cli + ["solve", str(source), str(work / "cli jobs"), str(solver)],
                        env=solver_environment, capture_output=True, text=True, timeout=180, check=True)
result_archive = Path(solved.stdout.strip())
assert result_archive.is_file()
result_info = json.loads(subprocess.run(cli + ["inspect", str(result_archive)],
                        capture_output=True, text=True, check=True).stdout)
assert result_info["HasFrd"] and result_info["HasDat"] and len(result_info["Parts"]) == 2
with zipfile.ZipFile(result_archive) as archive:
    dat_entries = [name for name in archive.namelist() if name.endswith(".dat")]
    assert len(dat_entries) == 1
    assert archive.read(dat_entries[0]) == (multipart_work / "multipart.dat").read_bytes()
assert source.read_bytes() == before
if os.name == "posix":
    for executable in ("/bin/false", "/bin/true"):
        failed_root = work / Path(executable).name
        failure = subprocess.run(cli + ["solve", str(source), str(failed_root), executable],
                                 capture_output=True, text=True, timeout=30)
        assert failure.returncode == 1
        assert not list(failed_root.rglob("result.pma"))
assert subprocess.run(cli + ["inspect", str(work / "missing.pma")], capture_output=True).returncode == 1
assert subprocess.run(cli + ["unknown"], capture_output=True).returncode == 2
print("PASS: CLI inspect/export/solve, native multipart results, source preservation and failure handling", flush=True)

# Original-style static step authoring through the application CLI, then a real two-step solve.
step_source = work / "cube.inp.pma"
step_source_bytes = step_source.read_bytes()
step_added = work / "app-step-added.pma"
step_edited = work / "app-step-edited.pma"
step_loaded = work / "app-step-loaded.pma"
step_duplicated = work / "app-step-duplicated.pma"
step_prepared = work / "app-step-prepared.pma"
subprocess.run(cli + ["add-step", str(step_source), str(step_added), "preload", "--mode", "automatic",
                     "--initial", "1", "--max-increment", "1"], check=True, timeout=180)
subprocess.run(cli + ["edit-step", str(step_added), str(step_edited), "preload", "reload", "--mode", "direct"],
               check=True, timeout=180)
subprocess.run([dotnet, "run", "--project", str(repo / "portable/tests/MeshIO.Characterization"),
                "--configuration", "Release", "--", "--edit-step-force", str(step_edited), str(step_loaded), "reload", "2"],
               cwd=repo / "portable", check=True, timeout=180)
subprocess.run(cli + ["duplicate-step", str(step_loaded), str(step_duplicated), "reload", "unused"], check=True)
subprocess.run(cli + ["step-active", str(step_duplicated), str(step_prepared), "unused", "false"], check=True)
authored = json.loads(subprocess.run(cli + ["steps", str(step_prepared)], capture_output=True, text=True, check=True).stdout)
assert [step["Name"] for step in authored] == ["load-1", "reload", "unused"]
assert [step["Active"] for step in authored] == [True, True, False]
step_solve = subprocess.run(cli + ["solve", str(step_prepared), str(work / "authored-step jobs"), str(solver)],
                            env=solver_environment, capture_output=True, text=True, check=True, timeout=180)
step_result = step_solve.stdout.strip()
fields = json.loads(subprocess.run(cli + ["fields", step_result], capture_output=True, text=True, check=True).stdout)
for step_id, maximum in ((1, .001), (2, .002)):
    selected = max((field for field in fields if field["Name"] == "DISP" and field["Component"] == "U1" and field["Step"] == step_id),
                   key=lambda field: (field["Time"], field["Increment"]))
    fingerprint = json.loads(subprocess.run(cli + ["result-fingerprint", step_result, "DISP", "U1", str(step_id),
                              str(selected["Increment"]), str(selected["Time"]), "--json"],
                              capture_output=True, text=True, check=True).stdout)
    assert abs(fingerprint["Maximum"] - maximum) < 1e-8 and abs(fingerprint["Minimum"]) < 1e-10
    assert abs(selected["Time"] - step_id) < 1e-6
# Original default field outputs do not imply history output or a nonempty DAT file.
field_only = work / "app-step-field-only.pma"
subprocess.run(cli + ["step-active", str(step_prepared), str(field_only), "load-1", "false"], check=True)
field_solve = subprocess.run(cli + ["solve", str(field_only), str(work / "field-only jobs"), str(solver)],
                             env=solver_environment, capture_output=True, text=True, check=True, timeout=180)
field_result = field_solve.stdout.strip()
field_info = json.loads(subprocess.run(cli + ["inspect", field_result], capture_output=True, text=True, check=True).stdout)
assert field_info["HasFrd"] and not field_info["HasDat"]
field_list = json.loads(subprocess.run(cli + ["fields", field_result], capture_output=True, text=True, check=True).stdout)
selected = max((field for field in field_list if field["Name"] == "DISP" and field["Component"] == "U1"), key=lambda field: field["Time"])
field_fingerprint = json.loads(subprocess.run(cli + ["result-fingerprint", field_result, "DISP", "U1", str(selected["Step"]),
                                 str(selected["Increment"]), str(selected["Time"]), "--json"],
                                 capture_output=True, text=True, check=True).stdout)
assert abs(field_fingerprint["Maximum"] - .002) < 1e-8
print("PASS: default static field-output step solves and reopens FRD without requiring an unrequested DAT history file", flush=True)
removed_step = work / "app-step-removed.pma"
subprocess.run(cli + ["remove-step", step_result, str(removed_step), "unused"], check=True)
assert len(json.loads(subprocess.run(cli + ["steps", str(removed_step)], capture_output=True, text=True, check=True).stdout)) == 2
assert not json.loads(subprocess.run(cli + ["fields", str(removed_step)], capture_output=True, text=True, check=True).stdout)
assert step_source.read_bytes() == step_source_bytes
print("PASS: authored static add/edit/duplicate/deactivate/remove workflow; native steps U1=0.001/0.002 at times 1/2, independent loads and stale-result invalidation", flush=True)

# Named-region section authoring and overlap precedence on two independent elastic blocks.
section_source = multipart_work / "multipart.inp.pma"
section_before = section_source.read_bytes()
section_material = work / "section-material.pma"
section_partial = work / "section-partial.pma"
section_empty = work / "section-empty.pma"
section_uniform = work / "section-uniform.pma"
section_duplicate = work / "section-duplicated.pma"
section_override = work / "section-override.pma"
section_inactive = work / "section-inactive.pma"
section_restored = work / "section-restored.pma"
section_final = work / "section-final.pma"
subprocess.run(cli + ["add-material", str(section_source), str(section_material), "uniform", "4000", "0"], check=True)
subprocess.run(cli + ["remove-section", str(section_material), str(section_partial), "solid"], check=True)
subprocess.run(cli + ["remove-section", str(section_partial), str(section_empty), "section2"], check=True)
empty_info = json.loads(subprocess.run(cli + ["sections", str(section_empty)], capture_output=True, text=True, check=True).stdout)
assert empty_info["Coverage"]["UnassignedElements"] == 2 and not empty_info["Coverage"]["CanSolve"]
blocked_root = work / "unassigned jobs"
blocked = subprocess.run(cli + ["solve", str(section_empty), str(blocked_root), str(solver)],
                         env=solver_environment, capture_output=True, text=True)
assert blocked.returncode == 1 and not blocked_root.exists()
subprocess.run(cli + ["add-section", str(section_empty), str(section_uniform), "all-blocks", "uniform", "set", "ALL_PARTS"], check=True)
subprocess.run(cli + ["duplicate-section", str(section_uniform), str(section_duplicate), "all-blocks", "override"], check=True)
section_data = json.loads(subprocess.run(cli + ["sections", str(section_duplicate)], capture_output=True, text=True, check=True).stdout)
first_part = next(region["Name"] for region in section_data["Regions"] if region["RegionType"] == "PartName" and region["Name"] != "second_block")
subprocess.run(cli + ["edit-section", str(section_duplicate), str(section_override), "override", "left-material", "stiffer", "part", first_part], check=True)
subprocess.run(cli + ["section-active", str(section_override), str(section_inactive), "left-material", "false"], check=True)
subprocess.run(cli + ["section-active", str(section_inactive), str(section_restored), "left-material", "true"], check=True)
for section_project, maximum, mean in ((section_uniform, .00025, .000125),
                                        (section_restored, .0005, .0001875), (section_inactive, .00025, .000125)):
    solved_sections = subprocess.run(cli + ["solve", str(section_project), str(work / (section_project.stem + " jobs")), str(solver)],
                                    env=solver_environment, capture_output=True, text=True, timeout=180, check=True).stdout.strip()
    selections = json.loads(subprocess.run(cli + ["fields", solved_sections], capture_output=True, text=True, check=True).stdout)
    selected = max((field for field in selections if field["Name"] == "DISP" and field["Component"] == "U1"), key=lambda field: field["Time"])
    fingerprint = json.loads(subprocess.run(cli + ["result-fingerprint", solved_sections, "DISP", "U1", str(selected["Step"]),
                                str(selected["Increment"]), str(selected["Time"]), "--json"], capture_output=True, text=True, check=True).stdout)
    assert abs(fingerprint["Maximum"] - maximum) < 1e-8 and abs(fingerprint["Mean"] - mean) < 1e-8
subprocess.run(cli + ["remove-section", str(section_restored), str(section_final), "left-material"], check=True)
final_data = json.loads(subprocess.run(cli + ["sections", str(section_final)], capture_output=True, text=True, check=True).stdout)
assert final_data["Coverage"]["CanSolve"] and final_data["Coverage"]["OverlappingElements"] == 0
assert section_source.read_bytes() == section_before
print("PASS: solid section create/edit/duplicate/activate/remove, empty coverage refusal and recovery; native two-block material/overlap displacements and independent source preservation", flush=True)

# Original-style multiline plastic material property authoring and production export.
plastic_source_bytes = Path(step_result).read_bytes()
plastic_data = json.loads(subprocess.run(cli + ["plasticity", step_result], capture_output=True, text=True, check=True).stdout)
plastic_material = plastic_data[0]["Name"]
plastic_set = work / "plastic-set.pma"
plastic_edited = work / "plastic-edited.pma"
plastic_removed = work / "plastic-removed.pma"
subprocess.run(cli + ["set-plasticity", step_result, str(plastic_set), plastic_material, "kinematic",
                      "100:0,120:0.01,150:0.05"], check=True)
set_data = json.loads(subprocess.run(cli + ["plasticity", str(plastic_set)], capture_output=True, text=True, check=True).stdout)
set_property = next(item for item in set_data if item["Name"] == plastic_material)
assert set_property["Hardening"] == "Kinematic" and set_property["Points"] == [
    {"YieldStress": 100, "PlasticStrain": 0}, {"YieldStress": 120, "PlasticStrain": .01},
    {"YieldStress": 150, "PlasticStrain": .05}]
plastic_input = work / "plastic.inp"
subprocess.run(cli + ["export", str(plastic_set), str(plastic_input)], check=True)
plastic_deck = plastic_input.read_text()
assert "*Plastic, Hardening=Kinematic\n100, 0\n120, 0.01\n150, 0.05" in plastic_deck
plastic_solve = subprocess.run(cli + ["solve", str(plastic_set), str(work / "plastic jobs"), str(solver)],
                               env=solver_environment, capture_output=True, text=True, timeout=180, check=True)
plastic_result = plastic_solve.stdout.strip()
plastic_fields = json.loads(subprocess.run(cli + ["fields", plastic_result], capture_output=True, text=True, check=True).stdout)
for step_id, maximum in ((1, .001), (2, .002)):
    selected = max((field for field in plastic_fields if field["Name"] == "DISP" and field["Component"] == "U1" and field["Step"] == step_id),
                   key=lambda field: field["Time"])
    fingerprint = json.loads(subprocess.run(cli + ["result-fingerprint", plastic_result, "DISP", "U1", str(step_id),
                                str(selected["Increment"]), str(selected["Time"]), "--json"], capture_output=True, text=True, check=True).stdout)
    assert abs(fingerprint["Maximum"] - maximum) < 1e-8
subprocess.run(cli + ["set-plasticity", str(plastic_set), str(plastic_edited), plastic_material, "combined",
                      "90:0,130:0.02"], check=True)
edited_data = json.loads(subprocess.run(cli + ["plasticity", str(plastic_edited)], capture_output=True, text=True, check=True).stdout)
edited_property = next(item for item in edited_data if item["Name"] == plastic_material)
assert edited_property["Hardening"] == "Combined" and len(edited_property["Points"]) == 2
subprocess.run(cli + ["remove-plasticity", str(plastic_edited), str(plastic_removed), plastic_material], check=True)
removed_data = json.loads(subprocess.run(cli + ["plasticity", str(plastic_removed)], capture_output=True, text=True, check=True).stdout)
removed_property = next(item for item in removed_data if item["Name"] == plastic_material)
assert removed_property["Hardening"] is None and removed_property["Points"] == []
removed_input = work / "plastic-removed.inp"
subprocess.run(cli + ["export", str(plastic_removed), str(removed_input)], check=True)
assert "*Plastic" not in removed_input.read_text()
assert Path(step_result).read_bytes() == plastic_source_bytes
print("PASS: multiline plasticity set/edit/remove, three hardening modes, production keyword export, native solve and source preservation", flush=True)

# Original-style field-output authoring, propagation and a real two-step result solve.
field_source_bytes = plastic_set.read_bytes()
field_added = work / "field-added.pma"
field_edited = work / "field-edited.pma"
field_duplicated = work / "field-duplicated.pma"
field_inactive = work / "field-inactive.pma"
field_active = work / "field-active.pma"
field_removed = work / "field-removed.pma"
field_propagated = work / "field-propagated.pma"
subprocess.run(cli + ["add-field-output", str(plastic_set), str(field_added), "load-1", "response", "nodal", "U,RF"], check=True)
subprocess.run(cli + ["edit-field-output", str(field_added), str(field_edited), "load-1", "response", "response-all",
                      "nodal", "U,RF", "--last-iterations", "true", "--global", "false"], check=True)
subprocess.run(cli + ["duplicate-field-output", str(field_edited), str(field_duplicated), "load-1", "response-all", "response-copy"], check=True)
subprocess.run(cli + ["field-output-active", str(field_duplicated), str(field_inactive), "load-1", "response-copy", "false"], check=True)
subprocess.run(cli + ["field-output-active", str(field_inactive), str(field_active), "load-1", "response-copy", "true"], check=True)
subprocess.run(cli + ["remove-field-output", str(field_active), str(field_removed), "load-1", "response-copy"], check=True)
subprocess.run(cli + ["propagate-field-output", str(field_removed), str(field_propagated), "load-1", "response-all"], check=True)
field_data = json.loads(subprocess.run(cli + ["field-outputs", str(field_propagated)], capture_output=True, text=True, check=True).stdout)
assert [output["Step"] for output in field_data["Outputs"] if output["Name"] == "response-all"] == ["load-1", "reload", "unused"]
assert all(output["Variables"] == ["RF", "U"] and output["LastIterations"] and not output["Global"]
           for output in field_data["Outputs"] if output["Name"] == "response-all")
field_input = work / "field-output.inp"
subprocess.run(cli + ["export", str(field_propagated), str(field_input)], check=True)
assert field_input.read_text().count("*Node file, Last iterations, Global=No") == 2
assert not json.loads(subprocess.run(cli + ["fields", str(field_propagated)], capture_output=True, text=True, check=True).stdout)
field_solve = subprocess.run(cli + ["solve", str(field_propagated), str(work / "field-output jobs"), str(solver)],
                             env=solver_environment, capture_output=True, text=True, timeout=180, check=True)
field_result = field_solve.stdout.strip()
field_results = json.loads(subprocess.run(cli + ["fields", field_result], capture_output=True, text=True, check=True).stdout)
for step_id, maximum in ((1, .001), (2, .002)):
    selected = max((field for field in field_results if field["Name"] == "DISP" and field["Component"] == "U1" and field["Step"] == step_id),
                   key=lambda field: field["Time"])
    fingerprint = json.loads(subprocess.run(cli + ["result-fingerprint", field_result, "DISP", "U1", str(step_id),
                                str(selected["Increment"]), str(selected["Time"]), "--json"], capture_output=True, text=True, check=True).stdout)
    assert abs(fingerprint["Maximum"] - maximum) < 1e-8
assert plastic_set.read_bytes() == field_source_bytes
print("PASS: nodal/element/contact field-output inspection and nodal add/edit/duplicate/activation/remove/propagation; native two-step results and source preservation", flush=True)

# Original-style region history-output authoring, DAT generation and a real two-step solve.
history_source_bytes = Path(field_result).read_bytes()
history_added = work / "history-added.pma"
history_edited = work / "history-edited.pma"
history_duplicated = work / "history-duplicated.pma"
history_inactive = work / "history-inactive.pma"
history_active = work / "history-active.pma"
history_removed = work / "history-removed.pma"
history_propagated = work / "history-propagated.pma"
subprocess.run(cli + ["add-history-output", field_result, str(history_added), "load-1", "response-history",
                      "nodal", "U", "node-set", "RIGHT"], check=True)
subprocess.run(cli + ["edit-history-output", str(history_added), str(history_edited), "load-1", "response-history",
                      "response-history-all", "nodal", "U,RF", "node-set", "RIGHT",
                      "--totals", "yes", "--global", "false"], check=True)
subprocess.run(cli + ["duplicate-history-output", str(history_edited), str(history_duplicated), "load-1",
                      "response-history-all", "response-history-copy"], check=True)
subprocess.run(cli + ["history-output-active", str(history_duplicated), str(history_inactive), "load-1",
                      "response-history-copy", "false"], check=True)
subprocess.run(cli + ["history-output-active", str(history_inactive), str(history_active), "load-1",
                      "response-history-copy", "true"], check=True)
subprocess.run(cli + ["remove-history-output", str(history_active), str(history_removed), "load-1",
                      "response-history-copy"], check=True)
subprocess.run(cli + ["propagate-history-output", str(history_removed), str(history_propagated), "load-1",
                      "response-history-all"], check=True)
history_data = json.loads(subprocess.run(cli + ["history-outputs", str(history_propagated)],
                                         capture_output=True, text=True, check=True).stdout)
requests = [output for output in history_data["Outputs"] if output["Name"] == "response-history-all"]
assert [output["Step"] for output in requests] == ["load-1", "reload", "unused"]
assert all(output["Kind"] == "Nodal" and output["Variables"] == ["RF", "U"] and
           output["RegionType"] == "NodeSetName" and output["Region"] == "RIGHT" and
           output["Totals"] == "Yes" and not output["Global"] for output in requests)
history_input = work / "history-output.inp"
subprocess.run(cli + ["export", str(history_propagated), str(history_input)], check=True)
history_deck = history_input.read_text()
assert history_deck.count("*Node print, Nset=RIGHT, Totals=Yes") == 2 and history_deck.count("RF, U") >= 2
assert not json.loads(subprocess.run(cli + ["fields", str(history_propagated)], capture_output=True, text=True, check=True).stdout)
history_solve = subprocess.run(cli + ["solve", str(history_propagated), str(work / "history-output jobs"), str(solver)],
                               env=solver_environment, capture_output=True, text=True, timeout=180, check=True)
history_result = history_solve.stdout.strip()
history_summary = json.loads(subprocess.run(cli + ["inspect", history_result], capture_output=True, text=True, check=True).stdout)
assert history_summary["HasDat"]
history_results = json.loads(subprocess.run(cli + ["fields", history_result], capture_output=True, text=True, check=True).stdout)
for step_id, maximum in ((1, .001), (2, .002)):
    selected = max((field for field in history_results if field["Name"] == "DISP" and field["Component"] == "U1" and field["Step"] == step_id),
                   key=lambda field: field["Time"])
    fingerprint = json.loads(subprocess.run(cli + ["result-fingerprint", history_result, "DISP", "U1", str(step_id),
                                str(selected["Increment"]), str(selected["Time"]), "--json"], capture_output=True, text=True, check=True).stdout)
    assert abs(fingerprint["Maximum"] - maximum) < 1e-8
history_catalog = json.loads(subprocess.run(cli + ["history-results", history_result],
                                            capture_output=True, text=True, check=True).stdout)
history_series = next(item for item in history_catalog["Series"] if item["Component"] == "U1" and item["Samples"] > 0)
history_table = json.loads(subprocess.run(cli + ["history-series", history_result, history_series["Set"],
                                                 history_series["Field"], history_series["Component"]],
                                          capture_output=True, text=True, check=True).stdout)
assert len(history_table["Columns"]) >= 2 and history_table["Rows"]
history_values = [float(value) for row in history_table["Rows"] for value in row if value is not None]
assert history_values and all(math.isfinite(value) for value in history_values)
history_csv = work / "history-results.csv"
subprocess.run(cli + ["export-history", history_result, str(history_csv), "--delimiter", "semicolon"], check=True)
history_export = history_csv.read_text(encoding="utf-8")
assert "History output component" in history_export and ";" in history_export and len(history_export.splitlines()) >= 3
assert Path(field_result).read_bytes() == history_source_bytes
print("PASS: four-type history-output inspection and nodal add/edit/duplicate/activation/remove/propagation; native DAT catalog/table/CSV result workflow and source preservation", flush=True)

# Optional combined native gate when distribution Gmsh is installed.
if shutil.which("gmsh"):
    cad = work / "quadratic cube.geo"
    cad.write_text('SetFactory("OpenCASCADE"); Box(1) = {0,0,0,1,1,1};\n'
                   'Physical Surface("XMAX") = {2}; Physical Volume("BODY") = {1};\n')
    cad_step = work / "quadratic cube.step"
    subprocess.run([shutil.which("gmsh"), str(cad), "-0", "-format", "step", "-o", str(cad_step), "-v", "1"],
                   check=True, timeout=180)
    seed_cad = work / "seed cube.pma"
    subprocess.run(cli + ["mesh-cad", str(cad_step), str(seed_cad), "1000", "0", "0.4"], check=True, timeout=180)
    topology = json.loads(subprocess.run(cli + ["cad-topology", str(seed_cad)], capture_output=True, text=True, check=True).stdout)
    left_face = next(entity["Tag"] for entity in topology["Entities"] if entity["Dimension"] == 2 and
                     abs(entity["Bounds"][0]) < 1e-6 and abs(entity["Bounds"][3]) < 1e-6)
    cad_project = work / "quadratic cube.pma"
    subprocess.run(cli + ["remesh-cad", str(seed_cad), str(cad_project), "0.4", "--second-order",
                          "--size-face", str(left_face), ".16", ".4",
                          "--size-box", ".16", "-.01", "-.01", "-.01", ".35", "1.01", "1.01"],
                   check=True, timeout=180)
    loaded_cad = work / "quadratic loaded.pma"
    subprocess.run([dotnet, "run", "--project", str(repo / "portable/tests/MeshIO.Characterization"),
                    "--configuration", "Release", "--", "--prepare-cad-cube", str(cad_project), str(loaded_cad)],
                   cwd=repo / "portable", check=True, timeout=180)
    solved_cad = subprocess.run(cli + ["solve", str(loaded_cad), str(work / "quadratic jobs"), str(solver)],
                               env=solver_environment, capture_output=True, text=True, timeout=180, check=True)
    cad_summary = json.loads(subprocess.run(cli + ["inspect", solved_cad.stdout.strip()],
                             capture_output=True, text=True, check=True).stdout)
    assert cad_summary["CadTopology"] == {"Points": 8, "Curves": 12, "Surfaces": 6, "Volumes": 1, "PhysicalGroups": 0}
    assert cad_summary["MeshingSetup"]["FaceSize"] == {"FaceTag": left_face, "MeshSize": .16, "TransitionDistance": .4}
    assert json.loads(subprocess.run(cli + ["cad-topology", solved_cad.stdout.strip()],
                      capture_output=True, text=True, check=True).stdout) == json.loads(
        subprocess.run(cli + ["cad-topology", str(cad_project)], capture_output=True, text=True, check=True).stdout)
    with zipfile.ZipFile(solved_cad.stdout.strip()) as archive:
        dat_text = archive.read(next(name for name in archive.namelist() if name.endswith(".dat"))).decode()
    # Every node follows the exact uniaxial field u=(x/1000, 0, 0), including midside nodes.
    cad_input = work / "quadratic loaded.inp"
    subprocess.run(cli + ["export", str(loaded_cad), str(cad_input)], check=True)
    coordinates = {}
    reading_nodes = False
    for line in cad_input.read_text().splitlines():
        if line.startswith("*"):
            reading_nodes = line.split(",")[0].strip().upper() == "*NODE"
            continue
        fields = line.split(",")
        if reading_nodes and len(fields) == 4:
            coordinates[int(fields[0])] = [float(value) for value in fields[1:]]
    displacements = {}
    reading_displacements = False
    for line in dat_text.splitlines():
        if "displacements" in line.lower():
            reading_displacements = True
            continue
        fields = line.split()
        if reading_displacements and len(fields) == 4 and fields[0].isdigit():
            displacements[int(fields[0])] = [float(value.replace("D", "E")) for value in fields[1:]]
    assert coordinates and set(displacements) == set(coordinates)
    assert all(abs(displacements[node][0] - xyz[0] / 1000) < 1e-8 and
               abs(displacements[node][1]) < 1e-8 and abs(displacements[node][2]) < 1e-8
               for node, xyz in coordinates.items())
    print(f"PASS: native CAD -> locally refined C3D10 -> pressure solve -> archived results: {len(coordinates)} analytical nodal displacements with preserved CAD topology", flush=True)

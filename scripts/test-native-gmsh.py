#!/usr/bin/env python3
"""Optional Linux smoke: native tetra meshing -> STL -> portable mesh -> MSH."""
import math
import statistics
import json
import hashlib
import os
from pathlib import Path
import shutil
import signal
import subprocess
import tempfile
import time
import gmsh

repo = Path(__file__).resolve().parent.parent

def executable(variable, default, fallback=None):
    configured = os.environ.get(variable)
    found = shutil.which(configured or default)
    if found:
        return found
    if not configured and fallback and fallback.is_file():
        return str(fallback)
    raise RuntimeError(f"Missing {default}; install it or set {variable}.")

local_sdk = Path(os.environ.get("XDG_DATA_HOME", str(Path.home() / ".local/share"))) / "prepomax/dotnet/dotnet"
dotnet = executable("PREPOMAX_DOTNET", "dotnet", local_sdk)
gmsh_command = executable("PREPOMAX_GMSH", "gmsh")

def run(arguments, timeout=120):
    result = subprocess.run(arguments, cwd=repo / "portable", text=True,
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=timeout)
    if result.returncode:
        raise RuntimeError(f"Command failed ({result.returncode}): {arguments[0]}\n{result.stdout}")
    return result.stdout

with tempfile.TemporaryDirectory(prefix="prepomax native géométrie ") as directory:
    work = Path(directory)
    geometry = work / "unit cube.geo"
    mesh = work / "volume.msh"
    step = work / "unit cube.step"
    application_project = work / "application mesh.pma"
    remeshed_project = work / "application remesh.pma"
    stored_remesh_project = work / "stored setup remesh.pma"
    extracted_step = work / "extracted unit cube.step"
    named_geometry = work / "named cube.geo"
    named_project = work / "named application mesh.pma"
    cancelled_project = work / "cancelled mesh.pma"
    surface = work / "boundary.stl"
    output = work / "portable boundary"
    geometry.write_text('SetFactory("OpenCASCADE");\nBox(1) = {0, 0, 0, 1, 1, 1};\n'
                        'Mesh.MeshSizeMin = 0.3;\nMesh.MeshSizeMax = 0.3;\nMesh.ElementOrder = 1;\n')
    named_geometry.write_text('SetFactory("OpenCASCADE");\nBox(1) = {0, 0, 0, 1, 1, 1};\n'
                              'Physical Surface("XMIN") = {1};\nPhysical Surface("XMAX") = {2};\n'
                              'Physical Surface("YMIN") = {3};\nPhysical Surface("YMAX") = {4};\n'
                              'Physical Surface("ZMIN") = {5};\nPhysical Surface("ZMAX") = {6};\n'
                              'Physical Volume("BODY") = {1};\n'
                              'Field[1] = Box; Field[1].VIn = 0.3; Background Field = 1;\n'
                              'Mesh.MeshSizeMin = 0.4; Mesh.MeshSizeFactor = 2;\n')
    print("CLI Gmsh:", run([gmsh_command, "-version"]).strip(), flush=True)
    run([gmsh_command, str(geometry), "-0", "-format", "step", "-o", str(step), "-v", "1"])
    print(run([dotnet, "run", "--project", str(repo / "portable/PrePoMax.Cli"),
               "--configuration", "Release", "--", "mesh-cad", str(step), str(application_project),
               "1000", "0.25", "0.3", gmsh_command], 180), end="", flush=True)
    application = json.loads(run([dotnet, "run", "--project", str(repo / "portable/PrePoMax.Cli"),
                                  "--configuration", "Release", "--", "inspect", str(application_project)], 180))
    if (application["Nodes"] <= 0 or application["Elements"] <= 0 or application["Materials"] != ["ELASTIC"] or
            application["Parts"] != ["CAD_VOLUME_1"] or "CAD_VOLUME_1" not in application["ElementSets"] or
            len(application["Surfaces"]) != 6 or not all(name.startswith("CAD_FACE_") for name in application["Surfaces"]) or
            application["GeometrySource"]["FileName"] != step.name or
            application["GeometrySource"]["Bytes"] != step.stat().st_size or
            application["GeometrySource"]["Sha256"] != hashlib.sha256(step.read_bytes()).hexdigest() or
            application["MeshingSetup"] != {"MaximumElementSize": 0.3, "MinimumElementSize": None,
                                             "Algorithm": "Delaunay", "Optimize": False, "SecondOrder": False}):
        raise RuntimeError("Application Gmsh project did not preserve the volume mesh, material, CAD regions and source geometry.")
    topology_cli = [dotnet, str(repo / "portable/PrePoMax.Cli/bin/Release/net10.0/PrePoMax.Cli.dll")]
    topology = json.loads(run(topology_cli + ["cad-topology", str(application_project)]))
    if ([sum(entity["Dimension"] == dim for entity in topology["Entities"]) for dim in range(4)] != [8, 12, 6, 1] or
            topology["SourceSha256"].lower() != application["GeometrySource"]["Sha256"] or
            application["CadTopology"] != {"Points": 8, "Curves": 12, "Surfaces": 6, "Volumes": 1, "PhysicalGroups": 0}):
        raise RuntimeError("STEP CAD boundary graph or source binding was not retained.")
    run([dotnet, "run", "--project", str(repo / "portable/PrePoMax.Cli"),
         "--configuration", "Release", "--", "extract-geometry", str(application_project), str(extracted_step)], 180)
    if extracted_step.read_bytes() != step.read_bytes():
        raise RuntimeError("Application did not extract the retained STEP source exactly.")
    print(f"PASS: application STEP -> Gmsh -> PMA: {application['Nodes']} nodes, {application['Elements']} elements, "
          f"{len(application['Surfaces'])} CAD faces and exact source geometry", flush=True)
    print(run([dotnet, "run", "--project", str(repo / "portable/PrePoMax.Cli"),
               "--configuration", "Release", "--", "remesh-cad", str(application_project),
               str(remeshed_project), "0.45", gmsh_command, "--min", "0.12",
               "--algorithm", "delaunay", "--optimize", "--second-order"], 180), end="", flush=True)
    remeshed = json.loads(run([dotnet, "run", "--project", str(repo / "portable/PrePoMax.Cli"),
                               "--configuration", "Release", "--", "inspect", str(remeshed_project)], 180))
    if (remeshed["Nodes"] <= 0 or remeshed["Elements"] <= 0 or remeshed["Materials"] != ["ELASTIC"] or
            remeshed["Parts"] != ["CAD_VOLUME_1"] or len(remeshed["Surfaces"]) != 6 or
            remeshed["GeometrySource"] != application["GeometrySource"] or
            remeshed["MeshingSetup"] != {"MaximumElementSize": 0.45, "MinimumElementSize": 0.12,
                                          "Algorithm": "Delaunay", "Optimize": True, "SecondOrder": True}):
        raise RuntimeError("Retained-CAD remesh did not publish a valid fresh project with exact source identity.")
    print(f"PASS: retained STEP remesh with min/max, Delaunay, optimization and quadratic controls: "
          f"{remeshed['Nodes']} nodes, {remeshed['Elements']} elements and exact source geometry", flush=True)
    print(run([dotnet, "run", "--project", str(repo / "portable/PrePoMax.Cli"),
               "--configuration", "Release", "--", "remesh-cad", str(remeshed_project),
               str(stored_remesh_project)], 180), end="", flush=True)
    stored_remesh = json.loads(run([dotnet, "run", "--project", str(repo / "portable/PrePoMax.Cli"),
                                    "--configuration", "Release", "--", "inspect", str(stored_remesh_project)], 180))
    if stored_remesh["MeshingSetup"] != remeshed["MeshingSetup"] or stored_remesh["GeometrySource"] != remeshed["GeometrySource"]:
        raise RuntimeError("Stored Gmsh controls were not reused by the CLI remesh path.")
    print("PASS: PMA-stored Gmsh controls drive a later remesh without repeated CLI values", flush=True)
    remesh_topology = json.loads(run(topology_cli + ["cad-topology", str(stored_remesh_project)]))
    if remesh_topology != topology:
        raise RuntimeError("Unchanged STEP source did not reproduce the same topology across linear/quadratic remeshes.")
    print("PASS: retained STEP boundary graph: 8 points, 12 curves, 6 faces, 1 volume; identical across remeshing", flush=True)

    quadratic_input = work / "quadratic.inp"
    run([dotnet, "run", "--project", str(repo / "portable/PrePoMax.Cli"),
         "--configuration", "Release", "--", "export", str(stored_remesh_project), str(quadratic_input)], 180)
    if "C3D10" not in quadratic_input.read_text():
        raise RuntimeError("Quadratic remesh did not export C3D10 solver elements.")
    print("PASS: quadratic stored-control remesh exports production C3D10 solver input", flush=True)

    # Validate spatial refinement independently from the saved control values.
    cli = [dotnet, str(repo / "portable/PrePoMax.Cli/bin/Release/net10.0/PrePoMax.Cli.dll")]
    box_project = work / "local sizing.pma"
    box_copy = work / "stored local sizing.pma"
    box_input = work / "local sizing.inp"
    run(cli + ["mesh-cad", str(step), str(box_project), "1000", "0", ".45", "--gmsh", gmsh_command,
               "--size-box", ".12", "-.01", "-.01", "-.01", ".35", "1.01", "1.01"])
    box_summary = json.loads(run(cli + ["inspect", str(box_project)]))
    expected_box = {"MeshSize": .12, "XMin": -.01, "YMin": -.01, "ZMin": -.01,
                    "XMax": .35, "YMax": 1.01, "ZMax": 1.01}
    if box_summary["MeshingSetup"]["SizeBox"] != expected_box or len(box_summary["Surfaces"]) != 6:
        raise RuntimeError("Local sizing did not retain the box and CAD regions.")
    run(cli + ["export", str(box_project), str(box_input)])
    nodes, tetrahedra, section = {}, [], ""
    for line in box_input.read_text().splitlines():
        if line.startswith("*"):
            section = line.split(",")[0].strip().upper()
            continue
        fields = line.split(",")
        if section == "*NODE":
            nodes[int(fields[0])] = [float(value) for value in fields[1:]]
        elif section == "*ELEMENT":
            tetrahedra.append([int(value) for value in fields[1:]])
    near, far = [], []
    for element in tetrahedra:
        points = [nodes[node] for node in element[:4]]
        center_x = sum(point[0] for point in points) / 4
        diameter = max(math.dist(a, b) for a in points for b in points)
        if center_x < .25:
            near.append(diameter)
        elif center_x > .75:
            far.append(diameter)
    if not near or not far or statistics.mean(near) >= .75 * statistics.mean(far):
        raise RuntimeError("Local box did not produce measurably finer tetrahedra in the requested region.")
    run(cli + ["remesh-cad", str(box_project), str(box_copy)])
    box_reopened = json.loads(run(cli + ["inspect", str(box_copy)]))
    if (box_reopened["MeshingSetup"] != box_summary["MeshingSetup"] or
            box_reopened["GeometrySource"] != box_summary["GeometrySource"]):
        raise RuntimeError("Stored local sizing was not reused with the same retained geometry.")
    print(f"PASS: local refinement box: near/far mean tetrahedron diameters "
          f"{statistics.mean(near):.4g}/{statistics.mean(far):.4g}; saved setup remeshes unchanged", flush=True)
    face_project, face_copy = work / "CAD face refinement.pma", work / "stored CAD face refinement.pma"
    face_input = work / "CAD face refinement.inp"
    left_face = next(entity["Tag"] for entity in topology["Entities"] if entity["Dimension"] == 2 and
                     abs(entity["Bounds"][0]) < 1e-6 and abs(entity["Bounds"][3]) < 1e-6)
    face_settings = {"FaceTag": left_face, "MeshSize": .12, "TransitionDistance": .4}
    run(cli + ["remesh-cad", str(application_project), str(face_project), ".45", "--second-order",
               "--size-face", str(left_face), ".12", ".4"])
    face_summary = json.loads(run(cli + ["inspect", str(face_project)]))
    if face_summary["MeshingSetup"]["FaceSize"] != face_settings:
        raise RuntimeError("CAD face refinement did not preserve the selected face and numeric controls.")
    run(cli + ["export", str(face_project), str(face_input)])
    face_nodes, face_elements, section = {}, [], ""
    for line in face_input.read_text().splitlines():
        if line.startswith("*"):
            section = line.split(",")[0].strip().upper()
            continue
        fields = line.split(",")
        if section == "*NODE":
            face_nodes[int(fields[0])] = [float(value) for value in fields[1:]]
        elif section == "*ELEMENT":
            face_elements.append([int(value) for value in fields[1:]])
    near_face, far_face = [], []
    for element in face_elements:
        points = [face_nodes[node] for node in element[:4]]
        x = sum(point[0] for point in points) / 4
        diameter = max(math.dist(a, b) for a in points for b in points)
        if x < .15:
            near_face.append(diameter)
        elif x > .75:
            far_face.append(diameter)
    if (not near_face or not far_face or statistics.mean(near_face) >= .75 * statistics.mean(far_face) or
            "C3D10" not in face_input.read_text()):
        raise RuntimeError("Face refinement did not produce the expected local quadratic mesh.")
    run(cli + ["remesh-cad", str(face_project), str(face_copy)])
    if json.loads(run(cli + ["inspect", str(face_copy)]))["MeshingSetup"] != face_summary["MeshingSetup"]:
        raise RuntimeError("Stored CAD face refinement was not reused.")
    print(f"PASS: CAD face refinement: near/far mean tetrahedron diameters "
          f"{statistics.mean(near_face):.4g}/{statistics.mean(far_face):.4g}; stored controls remesh unchanged", flush=True)
    guarded_mesher = work / "topology-changing gmsh"
    guarded_mesher.write_text("#!/usr/bin/env python3\nimport subprocess,sys\nfrom pathlib import Path\n" +
        f"result=subprocess.run([{gmsh_command!r}]+sys.argv[1:])\n" +
        "if result.returncode == 0:\n" +
        "    path=Path('topology.msh'); lines=path.read_text().splitlines()\n" +
        "    row=lines.index('$Entities')+2; fields=lines[row].split(); fields[1]=str(float(fields[1])+.001)\n" +
        "    lines[row]=' '.join(fields); path.write_text('\\n'.join(lines)+'\\n')\n" +
        "sys.exit(result.returncode)\n")
    guarded_mesher.chmod(0o755)
    refused_project = work / "refused changed topology.pma"
    refused = subprocess.run(cli + ["remesh-cad", str(face_project), str(refused_project), ".45",
                             "--size-face", str(left_face), ".12", ".4", "--gmsh", str(guarded_mesher)],
                             cwd=repo / "portable", text=True, capture_output=True, timeout=30)
    if refused.returncode != 1 or "CAD topology changed" not in refused.stderr or refused_project.exists():
        raise RuntimeError("Changed CAD topology did not fail closed for stored face sizing: " + refused.stdout + refused.stderr)
    print("PASS: native face refinement rejects changed CAD topology without publishing a project", flush=True)
    print(run([dotnet, "run", "--project", str(repo / "portable/PrePoMax.Cli"),
               "--configuration", "Release", "--", "mesh-cad", str(named_geometry), str(named_project),
               "1000", "0.25", "0.3", gmsh_command, "--second-order",
               "--size-box", ".12", "-.01", "-.01", "-.01", ".35", "1.01", "1.01"], 180), end="", flush=True)
    named = json.loads(run([dotnet, "run", "--project", str(repo / "portable/PrePoMax.Cli"),
                            "--configuration", "Release", "--", "inspect", str(named_project)], 180))
    expected_surfaces = {"XMIN", "XMAX", "YMIN", "YMAX", "ZMIN", "ZMAX"}
    if (named["Parts"] != ["BODY"] or "BODY" not in named["ElementSets"] or set(named["Surfaces"]) != expected_surfaces or
            named["GeometrySource"]["FileName"] != named_geometry.name or
            named["MeshingSetup"]["SizeBox"] != expected_box or
            named["Elements"] < 500):
        raise RuntimeError("Application Gmsh project did not retain physical volume and surface names.")
    print("PASS: application GEO retains physical names and local sizing overrides source field/minimum/scale", flush=True)
    named_topology = json.loads(run(topology_cli + ["cad-topology", str(named_project)]))
    if {group["Name"] for group in named_topology["PhysicalGroups"]} != expected_surfaces | {"BODY"}:
        raise RuntimeError("Topology did not retain all named GEO physical groups.")
    for primitive, text in (
            ("sphere", 'SetFactory("OpenCASCADE"); Sphere(1)={0,0,0,1};'),
            ("interface", 'SetFactory("OpenCASCADE"); Box(1)={0,0,0,.5,1,1}; Box(2)={.5,0,0,.5,1,1}; '
                          'BooleanFragments{Volume{1}; Delete;}{Volume{2}; Delete;}')):
        primitive_source = work / (primitive + ".geo")
        primitive_source.write_text(text)
        primitive_project = work / (primitive + ".pma")
        run(topology_cli + ["mesh-cad", str(primitive_source), str(primitive_project), "1000", "0", ".4"])
        captured = json.loads(run(topology_cli + ["cad-topology", str(primitive_project)]))
        entity_keys = {(entity["Dimension"], entity["Tag"]) for entity in captured["Entities"]}
        if any((entity["Dimension"] - 1, abs(tag)) not in entity_keys
               for entity in captured["Entities"] for tag in entity["BoundaryTags"]):
            raise RuntimeError("Primitive topology has dangling boundary references.")
        volumes = [entity for entity in captured["Entities"] if entity["Dimension"] == 3]
        if primitive == "interface":
            if len(volumes) != 2:
                raise RuntimeError("Two-volume CAD topology lost a volume.")
            shared = {abs(tag) for tag in volumes[0]["BoundaryTags"]} & {abs(tag) for tag in volumes[1]["BoundaryTags"]}
            if len(shared) != 1:
                raise RuntimeError("Two-volume topology lost its shared CAD face.")
            face = shared.pop()
            if not ((face in volumes[0]["BoundaryTags"] and -face in volumes[1]["BoundaryTags"]) or
                    (-face in volumes[0]["BoundaryTags"] and face in volumes[1]["BoundaryTags"])):
                raise RuntimeError("Shared CAD face does not retain opposite volume orientations.")
        elif len(volumes) != 1:
            raise RuntimeError("Spherical CAD topology lost its volume.")
        print(f"PASS: native {primitive} CAD boundary graph, including seam/shared-face references", flush=True)
    seed_driver = work / "seed-driver.geo"
    seed_mesh, seed_topology = work / "seed-volume.msh", work / "seed-topology.msh"
    seed_driver.write_text('Include "unit cube.geo"; Mesh.MshFileVersion=4.1; Mesh.Binary=0; '
                           'Save "seed-topology.msh"; Mesh.MshFileVersion=2.2;')
    run([gmsh_command, str(seed_driver), "-3", "-format", "msh2", "-o", str(seed_mesh), "-v", "2"])
    mesh_lines = seed_mesh.read_text().splitlines()
    for row, line in enumerate(mesh_lines):
        fields = line.split()
        if len(fields) >= 9 and fields[1] == "4" and fields[2] == "2":
            fields[4] = "999999"
            mesh_lines[row] = " ".join(fields)
            break
    else:
        raise RuntimeError("Native topology mismatch fixture contains no tetrahedron.")
    mismatched_mesh = "\n".join(mesh_lines) + "\n"
    invalid_topology = "$MeshFormat\n4.1 0 8\n$EndMeshFormat\n$Entities\n0 0 0 1\n1 0 0 0 1 1 1 0 1 999\n$EndEntities\n"
    for failure in ("entity-mismatch", "dangling-topology"):
        fake = work / (failure + " gmsh")
        bad_output = work / (failure + ".pma")
        mesh_content = mismatched_mesh if failure == "entity-mismatch" else seed_mesh.read_text()
        topology_content = invalid_topology if failure == "dangling-topology" else seed_topology.read_text()
        fake.write_text("#!/usr/bin/env python3\nfrom pathlib import Path\n" +
                        f"Path('volume.msh').write_text({mesh_content!r})\n" +
                        f"Path('topology.msh').write_text({topology_content!r})\n")
        fake.chmod(0o755)
        refused = subprocess.run(topology_cli + ["mesh-cad", str(step), str(bad_output), "1000", "0", ".4", str(fake)],
                                 cwd=repo / "portable", capture_output=True, text=True, timeout=30)
        if refused.returncode != 1 or bad_output.exists() or list(work.glob("*.prepomax-*.pma")):
            raise RuntimeError("Invalid native topology output published a project or left a staging archive.")
    print("PASS: native topology/mesh mismatches and dangling CAD references publish no project or staging archive", flush=True)
    slow_gmsh = work / "slow gmsh"
    slow_pid = work / "slow-gmsh.pid"
    slow_gmsh.write_text(f"#!/usr/bin/env python3\nimport os, time\nopen({str(slow_pid)!r}, 'w').write(str(os.getpid()))\ntime.sleep(30)\n")
    slow_gmsh.chmod(0o755)
    cli = repo / "portable/PrePoMax.Cli/bin/Release/net10.0/PrePoMax.Cli.dll"
    started = time.monotonic()
    cancelled = subprocess.Popen([dotnet, str(cli), "mesh-cad", str(step), str(cancelled_project),
                                  "1000", "0.25", "0.3", str(slow_gmsh)], cwd=repo / "portable",
                                 text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    for _ in range(100):
        if slow_pid.is_file():
            break
        time.sleep(0.02)
    if not slow_pid.is_file():
        cancelled.kill()
        raise RuntimeError("Application cancellation fixture did not start.")
    cancelled.send_signal(signal.SIGINT)
    cancelled_output, _ = cancelled.communicate(timeout=10)
    child_pid = int(slow_pid.read_text())
    if cancelled.returncode != 130 or cancelled_project.exists() or Path(f"/proc/{child_pid}").exists() or time.monotonic() - started >= 10:
        raise RuntimeError(f"Application Gmsh cancellation failed ({cancelled.returncode}):\n{cancelled_output}")
    print("PASS: application cancellation kills running Gmsh and publishes no PMA", flush=True)
    run([dotnet, "run", "--project", str(repo / "portable/tests/MeshIO.Characterization"),
         "--configuration", "Release", "--", "--run-native", gmsh_command, str(work),
         str(geometry), "-3", "-format", "msh4", "-o", str(mesh), "-nt", "1", "-v", "2"], 180)
    gmsh.initialize()
    gmsh.option.setNumber("General.Terminal", 0)
    try:
        print("Python Gmsh:", gmsh.option.getString("General.Version"), flush=True)
        gmsh.open(str(mesh))
        tags, flat, _ = gmsh.model.mesh.getNodes()
        points = {int(tag): flat[3*i:3*i+3] for i, tag in enumerate(tags)}
        types, ids, connectivity = gmsh.model.mesh.getElements(3)
        if list(types) != [4] or len(ids[0]) == 0:
            raise RuntimeError("Expected linear tetrahedra.")
        volume = 0.0
        for i in range(0, len(connectivity[0]), 4):
            a, b, c, d = [points[int(tag)] for tag in connectivity[0][i:i+4]]
            u, v, w = b-a, c-a, d-a
            determinant = u[0]*(v[1]*w[2]-v[2]*w[1])-u[1]*(v[0]*w[2]-v[2]*w[0])+u[2]*(v[0]*w[1]-v[1]*w[0])
            if not math.isfinite(determinant) or determinant <= 0:
                raise RuntimeError("Invalid tetrahedron orientation/volume.")
            volume += determinant / 6
        if abs(volume - 1) > 1e-9:
            raise RuntimeError(f"Wrong unit cube volume: {volume}")
        print(f"PASS: native mesh: {len(ids[0])} positive tetrahedra, volume {volume:.12g}", flush=True)
        volume_file = work / "native volume.mesh"
        volume_output = work / "portable volume"
        gmsh.write(str(volume_file))
        print(run([dotnet, "run", "--project", str(repo / "portable/tests/MeshIO.Characterization"),
                   "--configuration", "Release", "--", "--native-volume", str(volume_file), str(volume_output)], 180), end="", flush=True)
        gmsh.write(str(surface))
        print(run([dotnet, "run", "--project", str(repo / "portable/tests/MeshIO.Characterization"),
                   "--configuration", "Release", "--", "--native-surface", str(surface), str(output)], 180), end="", flush=True)
        for culture in ("en-US", "it-IT"):
            gmsh.clear()
            gmsh.open(str(output) + "." + culture + ".msh")
            tags, flat, _ = gmsh.model.mesh.getNodes()
            points = {int(tag): flat[3*i:3*i+3] for i, tag in enumerate(tags)}
            types, ids, connectivity = gmsh.model.mesh.getElements(2)
            if list(types) != [2] or len(ids[0]) == 0:
                raise RuntimeError("Expected exported triangles.")
            area = 0.0
            for i in range(0, len(connectivity[0]), 3):
                a, b, c = [points[int(tag)] for tag in connectivity[0][i:i+3]]
                u, v = b-a, c-a
                cross = (u[1]*v[2]-u[2]*v[1], u[2]*v[0]-u[0]*v[2], u[0]*v[1]-u[1]*v[0])
                area += math.sqrt(sum(x*x for x in cross)) / 2
            if abs(area - 6) > 1e-5:
                raise RuntimeError(f"Wrong exported surface area: {area}")
            print(f"PASS: native re-read ({culture}): {len(ids[0])} triangles, area {area:.12g}", flush=True)
        for culture in ("en-US", "it-IT"):
            gmsh.clear()
            gmsh.open(str(volume_output) + "." + culture + ".msh")
            tags, flat, _ = gmsh.model.mesh.getNodes()
            points = {int(tag): flat[3*i:3*i+3] for i, tag in enumerate(tags)}
            types, ids, connectivity = gmsh.model.mesh.getElements(3)
            if list(types) != [4] or len(ids[0]) == 0:
                raise RuntimeError("Expected exported tetrahedra.")
            exported_volume = 0.0
            for i in range(0, len(connectivity[0]), 4):
                a, b, c, d = [points[int(tag)] for tag in connectivity[0][i:i+4]]
                u, v, w = b-a, c-a, d-a
                determinant = u[0]*(v[1]*w[2]-v[2]*w[1])-u[1]*(v[0]*w[2]-v[2]*w[0])+u[2]*(v[0]*w[1]-v[1]*w[0])
                if not math.isfinite(determinant) or determinant <= 0:
                    raise RuntimeError("Invalid exported tetrahedron orientation/volume.")
                exported_volume += determinant / 6
            if abs(exported_volume - 1) > 1e-8:
                raise RuntimeError(f"Wrong exported cube volume: {exported_volume}")
            print(f"PASS: native volume re-read ({culture}): {len(ids[0])} tetrahedra, volume {exported_volume:.12g}", flush=True)
    finally:
        gmsh.finalize()

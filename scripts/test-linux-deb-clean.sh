#!/usr/bin/env bash
set -euo pipefail

if [[ "${1:-}" == --help ]]; then
    echo 'Usage: scripts/test-linux-deb-clean.sh PACKAGE.deb [UPGRADE.deb]'
    echo 'Install, optionally upgrade, validate and purge in a fresh Ubuntu 24.04 container.'
    echo 'PREPOMAX_UBUNTU_IMAGE overrides the default ubuntu:24.04 image.'
    echo 'PREPOMAX_UBUNTU_MIRROR optionally replaces archive.ubuntu.com.'
    exit 0
fi
[[ $# == 1 || $# == 2 ]] || { echo 'Supply an initial package, optional upgrade package, or --help.' >&2; exit 2; }
command -v docker >/dev/null 2>&1 || { echo 'Docker is required.' >&2; exit 2; }
linux_deb="$(realpath -- "$1")"
[[ -f "$linux_deb" && "$linux_deb" == *.deb ]] || { echo 'Package must be an existing .deb file.' >&2; exit 2; }
linux_upgrade=''
if [[ $# == 2 ]]; then
    linux_upgrade="$(realpath -- "$2")"
    [[ -f "$linux_upgrade" && "$linux_upgrade" == *.deb ]] || { echo 'Upgrade must be an existing .deb file.' >&2; exit 2; }
fi
linux_image="${PREPOMAX_UBUNTU_IMAGE:-ubuntu:24.04}"
linux_mirror="${PREPOMAX_UBUNTU_MIRROR:-}"
[[ -z "$linux_mirror" || "$linux_mirror" =~ ^https?://[^\|[:space:]]+/?$ ]] || {
    echo 'PREPOMAX_UBUNTU_MIRROR must be an HTTP(S) URL without spaces or |.' >&2
    exit 2
}

linux_docker_args=(--rm \
    --mount "type=bind,src=$linux_deb,dst=/tmp/prepomax-linux-viewer.deb,readonly" \
    --env "PREPOMAX_UBUNTU_MIRROR=$linux_mirror")
linux_expect_upgrade=0
if [[ -n "$linux_upgrade" ]]; then
    linux_docker_args+=(--mount "type=bind,src=$linux_upgrade,dst=/tmp/prepomax-linux-viewer-upgrade.deb,readonly")
    linux_expect_upgrade=1
fi

docker run "${linux_docker_args[@]}" \
    --env "PREPOMAX_EXPECT_UPGRADE=$linux_expect_upgrade" \
    "$linux_image" bash -euo pipefail -c '
export DEBIAN_FRONTEND=noninteractive
if [[ -n "$PREPOMAX_UBUNTU_MIRROR" ]]; then
    sed -i "s|http://archive.ubuntu.com/ubuntu/|${PREPOMAX_UBUNTU_MIRROR%/}/|g" \
        /etc/apt/sources.list.d/ubuntu.sources
fi
printf "Preparing clean Ubuntu package sources\n"
apt-get -qq update
printf "Installing package and declared dependencies\n"
apt-get install -y -qq --no-install-recommends /tmp/prepomax-linux-viewer.deb

check_installed_package() {
    test "$(dpkg-query -W -f="\${Status}" prepomax-linux-viewer)" = "install ok installed"
    test "$(dpkg-query -W -f="\${Architecture}" prepomax-linux-viewer)" = amd64
    test "$(readlink -f "$(command -v prepomax-linux-viewer)")" = /usr/bin/prepomax-linux-viewer
    test "$(readlink -f "$(command -v prepomax-linux-cli)")" = /usr/bin/prepomax-linux-cli
    test -x /usr/lib/prepomax-linux-viewer/runtime/dotnet
    test -f /usr/share/applications/si.um.fs.prepomax.LinuxViewer.desktop
    test -f /usr/share/metainfo/si.um.fs.prepomax.LinuxViewer.metainfo.xml
    test -f /usr/share/mime/packages/prepomax-linux-viewer.xml
    test -f /usr/share/pixmaps/prepomax-linux-viewer.ico
    desktop-file-validate /usr/share/applications/si.um.fs.prepomax.LinuxViewer.desktop
    grep -Fq "application/x-prepomax-pma:*.pma" /usr/share/mime/globs2
    grep -Fq "application/x-prepomax-pmx:*.pmx" /usr/share/mime/globs2
    grep -Fq "si.um.fs.prepomax.LinuxViewer.desktop" /usr/share/applications/mimeinfo.cache
    (cd /usr/lib/prepomax-linux-viewer && sha256sum --check SHA256SUMS >/dev/null)
    mkdir -p /root/.local/share
    env -u PREPOMAX_DOTNET HOME=/root XDG_DATA_HOME=/root/.local/share \
        prepomax-linux-viewer --check
}
check_installed_package

case "$(dpkg-query -W -f="\${Recommends}" prepomax-linux-viewer)" in
    *calculix-ccx*) ;;
    *) echo "Package does not recommend calculix-ccx." >&2; exit 1 ;;
esac
case "$(dpkg-query -W -f="\${Recommends}" prepomax-linux-viewer)" in
    *gmsh*) ;;
    *) echo "Package does not recommend gmsh." >&2; exit 1 ;;
esac
printf "Installing and exercising the distribution CalculiX solver\n"
apt-get install -y -qq --no-install-recommends calculix-ccx gmsh
prepomax-linux-viewer --check-solver
prepomax-linux-viewer --check-gmsh
mkdir -p /tmp/prepomax-ccx
cat > /tmp/prepomax-ccx/cube.inp <<EOF
*HEADING
Distribution CalculiX package acceptance
*NODE
1,0,0,0
2,1,0,0
3,1,1,0
4,0,1,0
5,0,0,1
6,1,0,1
7,1,1,1
8,0,1,1
*ELEMENT,TYPE=C3D8,ELSET=SOLID
1,1,2,3,4,5,6,7,8
*NSET,NSET=LEFT
1,4,5,8
*NSET,NSET=RIGHT
2,3,6,7
*NSET,NSET=ALLNODES,GENERATE
1,8,1
*MATERIAL,NAME=ELASTIC
*ELASTIC
1000,0
*SOLID SECTION,ELSET=SOLID,MATERIAL=ELASTIC
*BOUNDARY
LEFT,1,1
ALLNODES,2,3
*STEP
*STATIC
*CLOAD
RIGHT,1,0.25
*NODE PRINT,NSET=RIGHT
U
*NODE FILE
U
*END STEP
EOF
(cd /tmp/prepomax-ccx && ccx -i cube >/dev/null)
test -s /tmp/prepomax-ccx/cube.dat
test -s /tmp/prepomax-ccx/cube.frd
cat > /tmp/prepomax-ccx/check.awk <<EOF
\$1 ~ /^(2|3|6|7)\$/ && NF == 4 {
    if (\$2 < 0.00099999 || \$2 > 0.00100001 || \$3 < -1e-10 || \$3 > 1e-10 || \$4 < -1e-10 || \$4 > 1e-10) exit 2
    count++
}
END { if (count != 4) exit 3 }
EOF
awk -f /tmp/prepomax-ccx/check.awk /tmp/prepomax-ccx/cube.dat
printf "Distribution CalculiX solved the analytical cube with U1=0.001\n"

printf "Exercising the distribution Gmsh CAD-to-project workflow\n"
mkdir -p /tmp/prepomax-gmsh
cat > /tmp/prepomax-gmsh/cube.geo <<EOF
SetFactory("OpenCASCADE");
Box(1) = {0, 0, 0, 1, 1, 1};
EOF
gmsh /tmp/prepomax-gmsh/cube.geo -0 -format step -o /tmp/prepomax-gmsh/cube.step -v 1
prepomax-linux-cli mesh-cad /tmp/prepomax-gmsh/cube.step /tmp/prepomax-gmsh/cube.pma 1000 0.25 0.35
test -s /tmp/prepomax-gmsh/cube.pma
prepomax-linux-cli inspect /tmp/prepomax-gmsh/cube.pma > /tmp/prepomax-gmsh/inspect.json
python3 - <<EOF
import json
data = json.load(open("/tmp/prepomax-gmsh/inspect.json", encoding="utf-8"))
assert data["Nodes"] > 0 and data["Elements"] > 0
assert data["Materials"] == ["ELASTIC"]
assert data["Parts"] == ["CAD_VOLUME_1"]
assert data["CadTopology"] == {"Points": 8, "Curves": 12, "Surfaces": 6, "Volumes": 1, "PhysicalGroups": 0}
assert "CAD_VOLUME_1" in data["ElementSets"]
assert len(data["Surfaces"]) == 6 and all(name.startswith("CAD_FACE_") for name in data["Surfaces"])
assert not data["HasFrd"] and not data["HasDat"]
source = open("/tmp/prepomax-gmsh/cube.step", "rb").read()
import hashlib
assert data["GeometrySource"] == {"FileName": "cube.step", "Bytes": len(source),
                                  "Sha256": hashlib.sha256(source).hexdigest()}
assert data["MeshingSetup"] == {"MaximumElementSize": 0.35, "MinimumElementSize": None,
                                 "Algorithm": "Delaunay", "Optimize": False, "SecondOrder": False}
EOF
prepomax-linux-cli extract-geometry /tmp/prepomax-gmsh/cube.pma /tmp/prepomax-gmsh/extracted.step
cmp /tmp/prepomax-gmsh/cube.step /tmp/prepomax-gmsh/extracted.step
prepomax-linux-cli cad-topology /tmp/prepomax-gmsh/cube.pma > /tmp/prepomax-gmsh/initial-topology.json
python3 - <<EOF
import json
topology = json.load(open("/tmp/prepomax-gmsh/initial-topology.json"))
face = next(entity["Tag"] for entity in topology["Entities"] if entity["Dimension"] == 2 and
            abs(entity["Bounds"][0]) < 1e-6 and abs(entity["Bounds"][3]) < 1e-6)
open("/tmp/prepomax-gmsh/face-tag", "w").write(str(face))
EOF
linux_face_tag="$(cat /tmp/prepomax-gmsh/face-tag)"
prepomax-linux-cli remesh-cad /tmp/prepomax-gmsh/cube.pma /tmp/prepomax-gmsh/remeshed.pma 0.45 \
    --min 0.12 --algorithm delaunay --optimize --second-order \
    --size-box 0.16 -0.01 -0.01 -0.01 0.35 1.01 1.01 \
    --size-face "$linux_face_tag" 0.16 0.4
prepomax-linux-cli inspect /tmp/prepomax-gmsh/remeshed.pma > /tmp/prepomax-gmsh/remeshed.json
python3 - <<EOF
import json
original = json.load(open("/tmp/prepomax-gmsh/inspect.json", encoding="utf-8"))
remeshed = json.load(open("/tmp/prepomax-gmsh/remeshed.json", encoding="utf-8"))
assert remeshed["Nodes"] > 0 and remeshed["Elements"] > 0
assert remeshed["Materials"] == ["ELASTIC"] and remeshed["GeometrySource"] == original["GeometrySource"]
assert len(remeshed["Surfaces"]) == 6 and all(name.startswith("CAD_FACE_") for name in remeshed["Surfaces"])
assert remeshed["MeshingSetup"] == {"MaximumElementSize": 0.45, "MinimumElementSize": 0.12,
                                     "Algorithm": "Delaunay", "Optimize": True, "SecondOrder": True,
                                     "SizeBox": {"MeshSize": 0.16, "XMin": -0.01, "YMin": -0.01, "ZMin": -0.01,
                                                 "XMax": 0.35, "YMax": 1.01, "ZMax": 1.01},
                                     "FaceSize": {"FaceTag": int(open("/tmp/prepomax-gmsh/face-tag").read()),
                                                  "MeshSize": 0.16, "TransitionDistance": 0.4}}
EOF
prepomax-linux-cli remesh-cad /tmp/prepomax-gmsh/remeshed.pma /tmp/prepomax-gmsh/stored-remesh.pma
prepomax-linux-cli inspect /tmp/prepomax-gmsh/stored-remesh.pma > /tmp/prepomax-gmsh/stored-remesh.json
prepomax-linux-cli export /tmp/prepomax-gmsh/stored-remesh.pma /tmp/prepomax-gmsh/stored-remesh.inp
prepomax-linux-cli cad-topology /tmp/prepomax-gmsh/stored-remesh.pma > /tmp/prepomax-gmsh/topology.json
python3 - <<EOF
import json
remeshed = json.load(open("/tmp/prepomax-gmsh/remeshed.json", encoding="utf-8"))
stored = json.load(open("/tmp/prepomax-gmsh/stored-remesh.json", encoding="utf-8"))
assert stored["Nodes"] > 0 and stored["Elements"] > 0
assert stored["GeometrySource"] == remeshed["GeometrySource"]
assert stored["MeshingSetup"] == remeshed["MeshingSetup"]
assert "C3D10" in open("/tmp/prepomax-gmsh/stored-remesh.inp").read()
topology = json.load(open("/tmp/prepomax-gmsh/topology.json"))
assert [sum(entity["Dimension"] == dim for entity in topology["Entities"]) for dim in range(4)] == [8, 12, 6, 1]
assert topology["SourceSha256"].lower() == stored["GeometrySource"]["Sha256"]
import zipfile
with zipfile.ZipFile("/tmp/prepomax-gmsh/stored-remesh.pma") as archive:
    assert json.loads(archive.read("manifest.json"))["Version"] == 6
    assert "geometry.topology.json" in archive.namelist()
EOF
printf "Distribution Gmsh generated and remeshed an editable PMA with persisted controls, six CAD-face regions, source-bound topology, guarded CAD-face refinement and exact retained STEP\n"

printf "Exercising static analysis step authoring through the installed CLI\n"
mkdir -p /tmp/prepomax-steps
prepomax-linux-cli add-step /tmp/prepomax-gmsh/stored-remesh.pma /tmp/prepomax-steps/added.pma load-2 \
    --mode automatic --period 2 --initial 0.5 --max-increment 1 --max-increments 200
prepomax-linux-cli duplicate-step /tmp/prepomax-steps/added.pma /tmp/prepomax-steps/duplicated.pma load-2 unused
prepomax-linux-cli step-active /tmp/prepomax-steps/duplicated.pma /tmp/prepomax-steps/inactive.pma unused false
prepomax-linux-cli edit-step /tmp/prepomax-steps/inactive.pma /tmp/prepomax-steps/edited.pma load-2 reload --mode direct
prepomax-linux-cli remove-step /tmp/prepomax-steps/edited.pma /tmp/prepomax-steps/final.pma unused
prepomax-linux-cli steps /tmp/prepomax-steps/final.pma > /tmp/prepomax-steps/steps.json
prepomax-linux-cli inspect /tmp/prepomax-steps/final.pma > /tmp/prepomax-steps/inspect.json
prepomax-linux-cli export /tmp/prepomax-steps/final.pma /tmp/prepomax-steps/analysis.inp
python3 - <<EOF
import json
steps = json.load(open("/tmp/prepomax-steps/steps.json"))
assert [step["Name"] for step in steps] == ["load-1", "reload"]
assert all(step["Active"] for step in steps)
assert steps[1]["IncrementationType"] == "Direct" and steps[1]["TimePeriod"] == 2
assert steps[1]["InitialTimeIncrement"] == 0.5 and steps[1]["MaxIncrements"] == 200
assert steps[1]["FieldOutputs"] == 2 and steps[1]["HistoryOutputs"] == 0
original = json.load(open("/tmp/prepomax-gmsh/stored-remesh.json"))
current = json.load(open("/tmp/prepomax-steps/inspect.json"))
assert current["ActiveAnalysisSteps"] == 2
assert current["GeometrySource"] == original["GeometrySource"] and current["MeshingSetup"] == original["MeshingSetup"]
assert current["CadTopology"] == original["CadTopology"]
assert "*Static, Direct" in open("/tmp/prepomax-steps/analysis.inp").read()
EOF
printf "PASS: installed static step add/edit/duplicate/deactivate/remove, production export and CAD/setup preservation\n"

printf "Exercising solid section authoring through the installed CLI\n"
mkdir -p /tmp/prepomax-sections
prepomax-linux-cli sections /tmp/prepomax-steps/final.pma > /tmp/prepomax-sections/original.json
python3 - <<EOF
import json
data = json.load(open("/tmp/prepomax-sections/original.json"))
open("/tmp/prepomax-sections/original-name", "w").write(data["Sections"][0]["Name"])
open("/tmp/prepomax-sections/part", "w").write(next(item["Name"] for item in data["Regions"] if item["RegionType"] == "PartName"))
open("/tmp/prepomax-sections/set", "w").write(next(item["Name"] for item in data["Regions"] if item["RegionType"] == "ElementSetName" and item["Elements"] == data["Coverage"]["ActiveElements"]))
EOF
linux_section_name="$(cat /tmp/prepomax-sections/original-name)"
linux_section_part="$(cat /tmp/prepomax-sections/part)"
linux_section_set="$(cat /tmp/prepomax-sections/set)"
prepomax-linux-cli remove-section /tmp/prepomax-steps/final.pma /tmp/prepomax-sections/empty.pma "$linux_section_name"
if prepomax-linux-cli solve /tmp/prepomax-sections/empty.pma /tmp/prepomax-sections/blocked-jobs ccx > /tmp/prepomax-sections/blocked.log 2>&1; then
    echo "Unassigned solid model unexpectedly started a solve." >&2
    exit 1
fi
test ! -d /tmp/prepomax-sections/blocked-jobs
prepomax-linux-cli add-material /tmp/prepomax-sections/empty.pma /tmp/prepomax-sections/material.pma section-elastic 5000 0
prepomax-linux-cli add-section /tmp/prepomax-sections/material.pma /tmp/prepomax-sections/assigned.pma all-solids section-elastic part "$linux_section_part"
prepomax-linux-cli duplicate-section /tmp/prepomax-sections/assigned.pma /tmp/prepomax-sections/duplicated.pma all-solids override
prepomax-linux-cli edit-section /tmp/prepomax-sections/duplicated.pma /tmp/prepomax-sections/edited.pma override replacement ELASTIC set "$linux_section_set"
prepomax-linux-cli sections /tmp/prepomax-sections/edited.pma > /tmp/prepomax-sections/overlap.json
prepomax-linux-cli section-active /tmp/prepomax-sections/edited.pma /tmp/prepomax-sections/inactive.pma replacement false
prepomax-linux-cli section-active /tmp/prepomax-sections/inactive.pma /tmp/prepomax-sections/active.pma replacement true
prepomax-linux-cli remove-section /tmp/prepomax-sections/active.pma /tmp/prepomax-sections/final.pma replacement
prepomax-linux-cli sections /tmp/prepomax-sections/final.pma > /tmp/prepomax-sections/final.json
prepomax-linux-cli inspect /tmp/prepomax-sections/final.pma > /tmp/prepomax-sections/inspect.json
prepomax-linux-cli export /tmp/prepomax-sections/final.pma /tmp/prepomax-sections/analysis.inp
python3 - <<EOF
import json
overlap = json.load(open("/tmp/prepomax-sections/overlap.json"))
final = json.load(open("/tmp/prepomax-sections/final.json"))
assert overlap["Coverage"]["OverlappingElements"] == overlap["Coverage"]["ActiveElements"]
assert overlap["Sections"][-1]["EffectiveElements"] == overlap["Coverage"]["ActiveElements"]
assert final["Coverage"]["CanSolve"] and final["Coverage"]["OverlappingElements"] == 0
assert final["Sections"][0]["Material"] == "section-elastic" and len(final["Sections"]) == 1
original = json.load(open("/tmp/prepomax-steps/inspect.json"))
current = json.load(open("/tmp/prepomax-sections/inspect.json"))
assert current["CanSolve"] and current["ActiveAnalysisSteps"] == 2
assert current["GeometrySource"] == original["GeometrySource"] and current["MeshingSetup"] == original["MeshingSetup"]
assert "Material=section-elastic" in open("/tmp/prepomax-sections/analysis.inp").read()
EOF
printf "PASS: installed solid section add/edit/duplicate/activation/remove, standalone material creation, coverage refusal/recovery and production export\n"

printf "Exercising plastic material property authoring through the installed CLI\n"
mkdir -p /tmp/prepomax-plasticity
prepomax-linux-cli plasticity /tmp/prepomax-sections/final.pma > /tmp/prepomax-plasticity/original.json
plastic_material="$(python3 -c "import json; print(json.load(open(\"/tmp/prepomax-plasticity/original.json\"))[0][\"Name\"])")"
prepomax-linux-cli set-plasticity /tmp/prepomax-sections/final.pma /tmp/prepomax-plasticity/set.pma \
    "$plastic_material" kinematic "100:0,120:0.01,150:0.05"
prepomax-linux-cli plasticity /tmp/prepomax-plasticity/set.pma > /tmp/prepomax-plasticity/set.json
prepomax-linux-cli export /tmp/prepomax-plasticity/set.pma /tmp/prepomax-plasticity/set.inp
prepomax-linux-cli set-plasticity /tmp/prepomax-plasticity/set.pma /tmp/prepomax-plasticity/edited.pma \
    "$plastic_material" combined "90:0,130:0.02"
prepomax-linux-cli plasticity /tmp/prepomax-plasticity/edited.pma > /tmp/prepomax-plasticity/edited.json
prepomax-linux-cli remove-plasticity /tmp/prepomax-plasticity/edited.pma /tmp/prepomax-plasticity/removed.pma "$plastic_material"
prepomax-linux-cli plasticity /tmp/prepomax-plasticity/removed.pma > /tmp/prepomax-plasticity/removed.json
prepomax-linux-cli export /tmp/prepomax-plasticity/removed.pma /tmp/prepomax-plasticity/removed.inp
python3 - <<EOF
import json
name = "$plastic_material"
set_property = next(item for item in json.load(open("/tmp/prepomax-plasticity/set.json")) if item["Name"] == name)
assert set_property["Hardening"] == "Kinematic" and set_property["Points"] == [
    {"YieldStress": 100, "PlasticStrain": 0}, {"YieldStress": 120, "PlasticStrain": .01},
    {"YieldStress": 150, "PlasticStrain": .05}]
edited = next(item for item in json.load(open("/tmp/prepomax-plasticity/edited.json")) if item["Name"] == name)
assert edited["Hardening"] == "Combined" and len(edited["Points"]) == 2
removed = next(item for item in json.load(open("/tmp/prepomax-plasticity/removed.json")) if item["Name"] == name)
assert removed["Hardening"] is None and removed["Points"] == []
assert "*Plastic, Hardening=Kinematic" in open("/tmp/prepomax-plasticity/set.inp").read()
assert "*Plastic" not in open("/tmp/prepomax-plasticity/removed.inp").read()
EOF
printf "PASS: installed multiline plasticity set/edit/remove, hardening selection and production export\n"

printf "Exercising field output authoring through the installed CLI\n"
mkdir -p /tmp/prepomax-field-outputs
prepomax-linux-cli field-outputs /tmp/prepomax-plasticity/set.pma > /tmp/prepomax-field-outputs/original.json
prepomax-linux-cli add-field-output /tmp/prepomax-plasticity/set.pma /tmp/prepomax-field-outputs/added.pma \
    load-1 response nodal U,RF
prepomax-linux-cli edit-field-output /tmp/prepomax-field-outputs/added.pma /tmp/prepomax-field-outputs/edited.pma \
    load-1 response response-all nodal U,RF --last-iterations true --global false
prepomax-linux-cli duplicate-field-output /tmp/prepomax-field-outputs/edited.pma /tmp/prepomax-field-outputs/duplicated.pma \
    load-1 response-all response-copy
prepomax-linux-cli field-output-active /tmp/prepomax-field-outputs/duplicated.pma /tmp/prepomax-field-outputs/inactive.pma \
    load-1 response-copy false
prepomax-linux-cli field-output-active /tmp/prepomax-field-outputs/inactive.pma /tmp/prepomax-field-outputs/active.pma \
    load-1 response-copy true
prepomax-linux-cli remove-field-output /tmp/prepomax-field-outputs/active.pma /tmp/prepomax-field-outputs/removed.pma \
    load-1 response-copy
prepomax-linux-cli propagate-field-output /tmp/prepomax-field-outputs/removed.pma /tmp/prepomax-field-outputs/final.pma \
    load-1 response-all
prepomax-linux-cli field-outputs /tmp/prepomax-field-outputs/final.pma > /tmp/prepomax-field-outputs/final.json
prepomax-linux-cli inspect /tmp/prepomax-field-outputs/final.pma > /tmp/prepomax-field-outputs/inspect.json
prepomax-linux-cli export /tmp/prepomax-field-outputs/final.pma /tmp/prepomax-field-outputs/analysis.inp
python3 - <<EOF
import json
data = json.load(open("/tmp/prepomax-field-outputs/final.json"))
requests = [item for item in data["Outputs"] if item["Name"] == "response-all"]
assert [item["Step"] for item in requests] == ["load-1", "reload"]
assert all(item["Kind"] == "Nodal" and item["Variables"] == ["RF", "U"] and
           item["LastIterations"] and not item["Global"] for item in requests)
assert not any(item["Name"] == "response-copy" for item in data["Outputs"])
deck = open("/tmp/prepomax-field-outputs/analysis.inp").read()
assert deck.count("*Node file, Last iterations, Global=No") == 2 and deck.count("RF, U") >= 2
before = json.load(open("/tmp/prepomax-sections/inspect.json"))
after = json.load(open("/tmp/prepomax-field-outputs/inspect.json"))
assert after["GeometrySource"] == before["GeometrySource"] and after["MeshingSetup"] == before["MeshingSetup"]
assert after["CadTopology"] == before["CadTopology"] and after["CanSolve"]
assert "*Plastic, Hardening=Kinematic" in deck
EOF
printf "PASS: installed field-output add/edit/duplicate/activation/remove/propagation, result invalidation and production export\n"

printf "Exercising history output authoring through the installed CLI\n"
mkdir -p /tmp/prepomax-history-outputs
prepomax-linux-cli history-outputs /tmp/prepomax-field-outputs/final.pma > /tmp/prepomax-history-outputs/original.json
history_region="$(python3 -c "import json; data=json.load(open(\"/tmp/prepomax-history-outputs/original.json\")); print(next(item[\"Name\"] for item in data[\"Regions\"] if item[\"RegionType\"] == \"NodeSetName\" and item[\"Entities\"] > 0))")"
prepomax-linux-cli add-history-output /tmp/prepomax-field-outputs/final.pma /tmp/prepomax-history-outputs/added.pma \
    load-1 response-history nodal U node-set "$history_region"
prepomax-linux-cli edit-history-output /tmp/prepomax-history-outputs/added.pma /tmp/prepomax-history-outputs/edited.pma \
    load-1 response-history response-history-all nodal U,RF node-set "$history_region" --totals yes --global false
prepomax-linux-cli duplicate-history-output /tmp/prepomax-history-outputs/edited.pma /tmp/prepomax-history-outputs/duplicated.pma \
    load-1 response-history-all response-history-copy
prepomax-linux-cli history-output-active /tmp/prepomax-history-outputs/duplicated.pma /tmp/prepomax-history-outputs/inactive.pma \
    load-1 response-history-copy false
prepomax-linux-cli history-output-active /tmp/prepomax-history-outputs/inactive.pma /tmp/prepomax-history-outputs/active.pma \
    load-1 response-history-copy true
prepomax-linux-cli remove-history-output /tmp/prepomax-history-outputs/active.pma /tmp/prepomax-history-outputs/removed.pma \
    load-1 response-history-copy
prepomax-linux-cli propagate-history-output /tmp/prepomax-history-outputs/removed.pma /tmp/prepomax-history-outputs/final.pma \
    load-1 response-history-all
prepomax-linux-cli history-outputs /tmp/prepomax-history-outputs/final.pma > /tmp/prepomax-history-outputs/final.json
prepomax-linux-cli inspect /tmp/prepomax-history-outputs/final.pma > /tmp/prepomax-history-outputs/inspect.json
prepomax-linux-cli export /tmp/prepomax-history-outputs/final.pma /tmp/prepomax-history-outputs/analysis.inp
python3 - <<EOF
import json
data = json.load(open("/tmp/prepomax-history-outputs/final.json"))
requests = [item for item in data["Outputs"] if item["Name"] == "response-history-all"]
assert [item["Step"] for item in requests] == ["load-1", "reload"]
assert all(item["Kind"] == "Nodal" and item["Variables"] == ["RF", "U"] and
           item["RegionType"] == "NodeSetName" and item["Region"] == "$history_region" and
           item["Totals"] == "Yes" and not item["Global"] for item in requests)
assert not any(item["Name"] == "response-history-copy" for item in data["Outputs"])
deck = open("/tmp/prepomax-history-outputs/analysis.inp").read()
assert deck.count("*Node print, Nset=$history_region, Totals=Yes") == 2 and deck.count("RF, U") >= 2
before = json.load(open("/tmp/prepomax-field-outputs/inspect.json"))
after = json.load(open("/tmp/prepomax-history-outputs/inspect.json"))
assert after["GeometrySource"] == before["GeometrySource"] and after["MeshingSetup"] == before["MeshingSetup"]
assert after["CadTopology"] == before["CadTopology"] and after["CanSolve"] and not after["HasFrd"] and not after["HasDat"]
EOF
printf "PASS: installed history-output add/edit/duplicate/activation/remove/propagation, result invalidation and production export\n"

printf "Exercising installed history-result catalog, aligned table and CSV export\n"
history_result="$(prepomax-linux-cli solve /tmp/prepomax-history-outputs/final.pma /tmp/prepomax-history-results/jobs /usr/bin/ccx)"
prepomax-linux-cli history-results "$history_result" > /tmp/prepomax-history-results/catalog.json
prepomax-linux-cli export-history "$history_result" /tmp/prepomax-history-results/history.csv --delimiter semicolon
python3 - "$history_result" <<EOF
import json, math, subprocess, sys
project = sys.argv[1]
catalog = json.load(open("/tmp/prepomax-history-results/catalog.json", encoding="utf-8"))
series = next(item for item in catalog["Series"] if item["Component"] == "U1" and item["Samples"] > 0)
table = json.loads(subprocess.run(["prepomax-linux-cli", "history-series", project, series["Set"],
                                   series["Field"], series["Component"]],
                                  check=True, capture_output=True, text=True).stdout)
assert len(table["Columns"]) >= 2 and table["Rows"]
values = [float(value) for row in table["Rows"] for value in row if value is not None]
assert values and all(math.isfinite(value) for value in values)
exported = open("/tmp/prepomax-history-results/history.csv", encoding="utf-8").read()
assert "History output component" in exported and ";" in exported and len(exported.splitlines()) >= 3
EOF
printf "PASS: installed native solve to DAT history catalog, aligned table and semicolon CSV export\n"

if [[ "$PREPOMAX_EXPECT_UPGRADE" == 1 ]]; then
    linux_initial_version="$(dpkg-query -W -f="\${Version}" prepomax-linux-viewer)"
    linux_upgrade_version="$(dpkg-deb -f /tmp/prepomax-linux-viewer-upgrade.deb Version)"
    dpkg --compare-versions "$linux_upgrade_version" gt "$linux_initial_version"
    mkdir -p /root/.local/state/prepomax/jobs
    printf "preserve across package upgrade\n" > /root/.local/state/prepomax/jobs/upgrade-sentinel
    cp /tmp/prepomax-history-outputs/final.pma /root/.local/state/prepomax/jobs/upgrade-project.pma
    sha256sum /root/.local/state/prepomax/jobs/upgrade-project.pma > /tmp/prepomax-upgrade-project.sha256
    printf "Upgrading package from %s to %s\n" "$linux_initial_version" "$linux_upgrade_version"
    apt-get install -y -qq --no-install-recommends /tmp/prepomax-linux-viewer-upgrade.deb
    test "$(dpkg-query -W -f="\${Version}" prepomax-linux-viewer)" = "$linux_upgrade_version"
    grep -Fxq "preserve across package upgrade" /root/.local/state/prepomax/jobs/upgrade-sentinel
    check_installed_package
    prepomax-linux-viewer --check-solver
    prepomax-linux-viewer --check-gmsh
    sha256sum --check /tmp/prepomax-upgrade-project.sha256
    prepomax-linux-cli inspect /root/.local/state/prepomax/jobs/upgrade-project.pma > /tmp/prepomax-gmsh/upgraded.json
    python3 - <<EOF
import json
before = json.load(open("/tmp/prepomax-history-outputs/inspect.json"))
after = json.load(open("/tmp/prepomax-gmsh/upgraded.json"))
assert after == before
EOF
fi

printf "Purging package and checking integration cleanup\n"
apt-get purge -y -qq prepomax-linux-viewer
! dpkg-query -W prepomax-linux-viewer >/dev/null 2>&1
test ! -e /usr/bin/prepomax-linux-viewer
test ! -e /usr/bin/prepomax-linux-cli
test ! -e /usr/lib/prepomax-linux-viewer
test ! -e /usr/share/applications/si.um.fs.prepomax.LinuxViewer.desktop
test ! -e /usr/share/metainfo/si.um.fs.prepomax.LinuxViewer.metainfo.xml
test ! -e /usr/share/mime/packages/prepomax-linux-viewer.xml
test ! -e /usr/share/pixmaps/prepomax-linux-viewer.ico
! grep -Fq "application/x-prepomax-pma:*.pma" /usr/share/mime/globs2
! grep -Fq "application/x-prepomax-pmx:*.pmx" /usr/share/mime/globs2
! grep -Fq "si.um.fs.prepomax.LinuxViewer.desktop" /usr/share/applications/mimeinfo.cache
if [[ "$PREPOMAX_EXPECT_UPGRADE" == 1 ]]; then
    grep -Fxq "preserve across package upgrade" /root/.local/state/prepomax/jobs/upgrade-sentinel
    sha256sum --check /tmp/prepomax-upgrade-project.sha256
    printf "PASS: clean Ubuntu 24.04 install, upgrade, private-runtime preflight and purge\n"
else
    printf "PASS: clean Ubuntu 24.04 install, private-runtime preflight and purge\n"
fi
'

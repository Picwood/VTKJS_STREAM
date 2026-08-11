#!/usr/bin/env python3
"""
Run a PrePoMax fatigue package with a Python array backend.

The runner uses CuPy when available and falls back to NumPy.  It writes:
  - prepomax_fatigue_results.json
  - prepomax_fatigue_results.csv
  - prepomax_fatigue_results.frd

The FRD contains a nodal scalar field named FATIGUE with components:
  DAMAGE, UR, LIFE, DSEQ
"""

from __future__ import annotations

import argparse
import csv
import json
import math
import os
import re
import sys
import time
from pathlib import Path

try:
    import cupy as xp  # type: ignore
    BACKEND = "CuPy/CUDA"
except Exception:
    import numpy as xp  # type: ignore
    BACKEND = "NumPy/CPU"

import numpy as np


FLOAT_RE = re.compile(r"[-+]?(?:\d+\.\d*|\d*\.\d+|\d+)(?:[Ee][-+]?\d+)?")


def emit_progress(percent: float, message: str) -> None:
    value = max(0.0, min(100.0, float(percent)))
    print(f"FATIGUE_PROGRESS:{value:.1f}:{message}", flush=True)


class Timer:
    def __init__(self) -> None:
        self.start = time.perf_counter()
        self.last = self.start

    def mark(self, label: str) -> None:
        now = time.perf_counter()
        print(f"Timing: {label}: {now - self.last:.3f} s (total {now - self.start:.3f} s)")
        self.last = now


def main() -> int:
    timer = Timer()
    emit_progress(0.0, "Starting fatigue calculation")
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("handoff", nargs="?", default="prepomax_fatigue_handoff.json")
    args = parser.parse_args()

    handoff = Path(args.handoff).resolve()
    if handoff.is_dir():
        handoff = handoff / "prepomax_fatigue_handoff.json"
    if not handoff.exists():
        raise FileNotFoundError(f"PrePoMax fatigue handoff not found: {handoff}")

    package_dir = handoff.parent
    with handoff.open("r", encoding="utf-8") as f:
        setup = json.load(f)
    emit_progress(2.0, "Read handoff")
    timer.mark("read handoff")

    print(f"Python fatigue backend: {BACKEND}")
    print(f"Package: {package_dir}")

    model_frd = resolve_package_path(package_dir, setup["model"]["frdFileName"])
    loads_file = resolve_package_path(package_dir, setup["loads"]["loadsFileName"])
    mesh = read_frd_mesh(model_frd)
    node_ids = mesh["node_ids"]
    if not node_ids:
        raise RuntimeError(f"No nodes were found in FRD mesh: {model_frd}")
    emit_progress(12.0, "Read model mesh")
    timer.mark("read FRD mesh")

    loads = read_loads(loads_file)
    components = [str(c.get("name", "")).strip() for c in setup["loads"].get("components", [])]
    if not components:
        components = loads["component_names"]
    emit_progress(18.0, "Read loads")
    timer.mark("read loads")

    stresses = []
    unit_load_cases = setup["model"].get("unitLoadCases", [])
    for index, component in enumerate(components):
        ulc = find_unit_load_case(unit_load_cases, component, index)
        frd_file = resolve_package_path(package_dir, ulc["frdFileName"])
        print(f"Reading stress field for {component}: {frd_file}")
        stress = read_frd_stress(frd_file, node_ids)
        stresses.append(stress)
        emit_progress(18.0 + 12.0 * (index + 1) / max(1, len(components)), "Read stress fields")
    timer.mark("read stress fields")

    if not stresses:
        raise RuntimeError("No unit-load stress fields were defined.")

    emit_progress(30.0, "Calculating fatigue")
    results = calculate_fatigue(setup, loads, components, node_ids, stresses, mesh)
    emit_progress(90.0, "Fatigue calculation complete")
    timer.mark("calculate fatigue")

    csv_file = package_dir / "prepomax_fatigue_results.csv"
    json_file = package_dir / "prepomax_fatigue_results.json"
    frd_file = package_dir / "prepomax_fatigue_results.frd"
    write_results_csv(csv_file, node_ids, results)
    emit_progress(94.0, "Wrote CSV results")
    timer.mark("write CSV")
    write_results_json(json_file, setup, results)
    emit_progress(97.0, "Wrote JSON results")
    timer.mark("write JSON")
    write_fatigue_frd(model_frd, frd_file, node_ids, results)
    emit_progress(100.0, "Wrote FRD results")
    timer.mark("write FRD")

    print(f"Fatigue CSV: {csv_file}")
    print(f"Fatigue summary: {json_file}")
    print(f"Fatigue FRD: {frd_file}")
    return 0


def resolve_package_path(package_dir: Path, value: str) -> Path:
    path = Path(value)
    if not path.is_absolute():
        path = package_dir / path
    return path.resolve()


def read_frd_node_ids(frd_file: Path) -> list[int]:
    node_ids: list[int] = []
    in_nodes = False
    with frd_file.open("r", encoding="ascii", errors="ignore") as f:
        for line in f:
            if line.startswith("    2C"):
                in_nodes = True
                continue
            if in_nodes and line.startswith(" -3"):
                break
            if in_nodes and line.startswith(" -1"):
                try:
                    node_ids.append(int(line[3:13]))
                except ValueError:
                    parts = line.split()
                    if len(parts) >= 2:
                        node_ids.append(int(parts[1]))
    return node_ids


def read_frd_mesh(frd_file: Path) -> dict:
    node_ids: list[int] = []
    node_coords: dict[int, np.ndarray] = {}
    elements: list[dict] = []
    in_nodes = False
    in_elements = False
    current_element: dict | None = None

    with frd_file.open("r", encoding="ascii", errors="ignore") as f:
        for line in f:
            if line.startswith("    2C"):
                in_nodes = True
                in_elements = False
                continue
            if line.startswith("    3C"):
                in_nodes = False
                in_elements = True
                continue
            if line.startswith(" -3"):
                if in_elements:
                    break
                in_nodes = False
                in_elements = False
                current_element = None
                continue

            if in_nodes and line.startswith(" -1"):
                try:
                    node_id = int(line[3:13])
                    coords = np.array([
                        float(line[13:25]),
                        float(line[25:37]),
                        float(line[37:49]),
                    ], dtype=np.float64)
                except ValueError:
                    parts = line.split()
                    if len(parts) < 5:
                        continue
                    node_id = int(parts[1])
                    coords = np.asarray([float(parts[2]), float(parts[3]), float(parts[4])], dtype=np.float64)
                node_ids.append(node_id)
                node_coords[node_id] = coords
                continue

            if in_elements and line.startswith(" -1"):
                try:
                    element_id = int(line[3:13])
                    descriptor = int(line[13:18])
                except ValueError:
                    parts = line.split()
                    if len(parts) < 3:
                        current_element = None
                        continue
                    element_id = int(parts[1])
                    descriptor = int(parts[2])
                current_element = {
                    "id": element_id,
                    "descriptor": descriptor,
                    "corner_count": frd_corner_node_count(descriptor),
                    "nodes": [],
                }
                continue

            if in_elements and line.startswith(" -2") and current_element is not None:
                numbers: list[int] = []
                for start in range(3, len(line), 10):
                    chunk = line[start:start + 10].strip()
                    if not chunk:
                        continue
                    try:
                        numbers.append(int(chunk))
                    except ValueError:
                        numbers = [int(v) for v in line.split()[1:]]
                        break
                current_element["nodes"].extend(numbers)
                if len(current_element["nodes"]) >= current_element["corner_count"] > 0:
                    current_element["nodes"] = current_element["nodes"][:current_element["corner_count"]]
                    elements.append(current_element)
                    current_element = None

    node_normals, node_axes, surface_mask = compute_surface_frames(node_ids, node_coords, elements)
    return {
        "node_ids": node_ids,
        "node_coords": node_coords,
        "elements": elements,
        "node_normals": node_normals,
        "node_axes": node_axes,
        "surface_mask": surface_mask,
    }


def frd_corner_node_count(descriptor: int) -> int:
    if descriptor in (1, 4):
        return 8
    if descriptor in (2, 5):
        return 6
    if descriptor in (3, 6):
        return 4
    if descriptor in (7, 8):
        return 3
    if descriptor in (9, 10):
        return 4
    return 0


def frd_face_maps(descriptor: int) -> tuple[list[tuple[int, int, int, int]], bool]:
    tet = [(0, 1, 2, 2), (0, 1, 3, 3), (0, 2, 3, 3), (1, 2, 3, 3)]
    hex8 = [(0, 1, 2, 3), (0, 1, 5, 4), (1, 2, 6, 5), (2, 6, 7, 3), (0, 3, 7, 4), (4, 5, 6, 7)]
    prism = [(0, 1, 2, 2), (0, 1, 4, 3), (0, 2, 5, 3), (1, 2, 5, 4), (3, 4, 5, 5)]
    tri = [(0, 1, 2, 2)]
    quad = [(0, 1, 2, 3)]
    if descriptor in (1, 4):
        return hex8, True
    if descriptor in (2, 5):
        return prism, True
    if descriptor in (3, 6):
        return tet, True
    if descriptor in (7, 8):
        return tri, False
    if descriptor in (9, 10):
        return quad, False
    return [], False


def compute_surface_frames(node_ids: list[int], node_coords: dict[int, np.ndarray],
                           elements: list[dict]) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    node_index = {node_id: i for i, node_id in enumerate(node_ids)}
    normals = np.zeros((len(node_ids), 3), dtype=np.float64)
    surface_mask = np.zeros(len(node_ids), dtype=bool)
    face_groups: dict[tuple[int, ...], list[tuple[list[int], np.ndarray, bool]]] = {}

    for element in elements:
        elem_nodes = [int(n) for n in element.get("nodes", []) if int(n) in node_coords]
        if len(elem_nodes) < 3:
            continue
        face_maps, is_solid = frd_face_maps(int(element.get("descriptor", 0)))
        if not face_maps:
            continue
        elem_centroid = np.mean([node_coords[n] for n in elem_nodes], axis=0)
        for face_map in face_maps:
            face_nodes = [elem_nodes[i] for i in face_map if i < len(elem_nodes)]
            unique_face_nodes = []
            for node_id in face_nodes:
                if node_id not in unique_face_nodes:
                    unique_face_nodes.append(node_id)
            if len(unique_face_nodes) < 3:
                continue
            key = tuple(sorted(unique_face_nodes))
            face_groups.setdefault(key, []).append((face_nodes, elem_centroid, is_solid))

    for grouped_faces in face_groups.values():
        for face_nodes, elem_centroid, is_solid in grouped_faces:
            if is_solid and len(grouped_faces) != 1:
                continue
            face_normal = surface_face_normal(face_nodes, node_coords, elem_centroid, is_solid)
            if face_normal is None:
                continue
            for node_id in set(face_nodes):
                idx = node_index.get(node_id)
                if idx is not None:
                    normals[idx, :] += face_normal

    for i in range(normals.shape[0]):
        surface_mask[i] = np.linalg.norm(normals[i, :]) > 1.0e-30
        normals[i, :] = normalize_vector(normals[i, :], np.array([0.0, 0.0, 1.0], dtype=np.float64))

    axes = np.zeros_like(normals)
    for i, normal in enumerate(normals):
        ref = np.array([1.0, 0.0, 0.0], dtype=np.float64)
        if abs(float(np.dot(ref, normal))) > 0.9:
            ref = np.array([0.0, 1.0, 0.0], dtype=np.float64)
        tangent = ref - float(np.dot(ref, normal)) * normal
        axes[i, :] = normalize_vector(tangent, np.array([0.0, 1.0, 0.0], dtype=np.float64))
    return normals, axes, surface_mask


def surface_face_normal(face_nodes: list[int], node_coords: dict[int, np.ndarray], elem_centroid: np.ndarray, is_solid: bool) -> np.ndarray | None:
    try:
        p = node_coords[face_nodes[0]]
        q = node_coords[face_nodes[1]]
        r = node_coords[face_nodes[2]]
    except KeyError:
        return None
    normal = np.cross(r - q, p - q)
    norm = np.linalg.norm(normal)
    if norm <= 1.0e-30:
        return None
    normal = normal / norm
    if is_solid:
        face_centroid = np.mean([node_coords[n] for n in set(face_nodes) if n in node_coords], axis=0)
        if np.linalg.norm(face_centroid + normal - elem_centroid) < np.linalg.norm(face_centroid - normal - elem_centroid):
            normal = -normal
    return normal


def normalize_vector(value: np.ndarray, fallback: np.ndarray) -> np.ndarray:
    norm = np.linalg.norm(value)
    if not math.isfinite(float(norm)) or norm <= 1.0e-30:
        return fallback.copy()
    return value / norm


def nanmin_update(current: float, value: float) -> float:
    if math.isnan(current):
        return value
    return min(current, value)


def nanmax_update(current: float, value: float) -> float:
    if math.isnan(current):
        return value
    return max(current, value)


def read_frd_stress(frd_file: Path, node_ids: list[int]) -> np.ndarray:
    values: dict[int, list[float]] = {}
    in_stress = False
    skip_description = 0
    with frd_file.open("r", encoding="ascii", errors="ignore") as f:
        for line in f:
            stripped = line.strip()
            if stripped.startswith("-4") and len(stripped.split()) >= 2 and stripped.split()[1].upper() == "STRESS":
                in_stress = True
                skip_description = 6
                continue
            if in_stress and skip_description > 0:
                skip_description -= 1
                continue
            if in_stress and stripped.startswith("-3"):
                break
            if in_stress and line.startswith(" -1"):
                try:
                    node_id = int(line[3:13])
                    nums = [float(line[start:start + 12]) for start in range(13, min(len(line), 85), 12)]
                except Exception:
                    parts = line.split()
                    if len(parts) < 8:
                        continue
                    node_id = int(parts[1])
                    nums = [float(v) for v in parts[2:8]]
                if len(nums) >= 6:
                    # CalculiX FRD tensor is SXX, SYY, SZZ, SXY, SYZ, SZX.
                    # FATLAB internal order is Sxx, Syy, Szz, Sxy, Sxz, Syz.
                    values[node_id] = [nums[0], nums[1], nums[2], nums[3], nums[5], nums[4]]

    stress = np.zeros((len(node_ids), 6), dtype=np.float64)
    for i, node_id in enumerate(node_ids):
        row = values.get(node_id)
        if row is not None:
            stress[i, :] = row
    return stress


def read_loads(loads_file: Path) -> dict:
    suffix = loads_file.suffix.lower()
    if suffix in (".xls", ".xlsx", ".xlsm"):
        try:
            import pandas as pd  # type: ignore
        except Exception as exc:
            raise RuntimeError("Reading Excel fatigue loads requires pandas/openpyxl.") from exc
        raw = pd.read_excel(loads_file, header=None)
        repetitions = float(raw.iloc[0, 1]) if raw.shape[1] > 1 else 1.0
        names = [str(v).strip() for v in raw.iloc[1, 1:].tolist() if str(v).strip() and str(v) != "nan"]
        units = [str(v).strip() for v in raw.iloc[2, 1:1 + len(names)].tolist()]
        numeric = raw.iloc[3:, 0:1 + len(names)].dropna(how="all").astype(float).to_numpy()
    else:
        rows = []
        with loads_file.open("r", encoding="utf-8-sig", newline="") as f:
            sample = f.read(2048)
            f.seek(0)
            dialect = csv.Sniffer().sniff(sample, delimiters=",;\t")
            rows = [row for row in csv.reader(f, dialect) if row]
        repetitions = float(rows[0][1]) if len(rows[0]) > 1 and rows[0][1] else 1.0
        names = [cell.strip() for cell in rows[1][1:] if cell.strip()]
        units = [cell.strip() for cell in rows[2][1:1 + len(names)]]
        numeric_rows = []
        for row in rows[3:]:
            if len(row) < 1 + len(names):
                continue
            try:
                numeric_rows.append([float(v) for v in row[:1 + len(names)]])
            except ValueError:
                continue
        numeric = np.asarray(numeric_rows, dtype=np.float64)

    if numeric.size == 0:
        raise RuntimeError(f"No numeric load samples were found in {loads_file}")
    return {
        "repetitions": repetitions,
        "component_names": names,
        "units": units,
        "time": numeric[:, 0],
        "values": numeric[:, 1:],
    }


def find_unit_load_case(unit_load_cases: list[dict], component: str, index: int) -> dict:
    for ulc in unit_load_cases:
        if str(ulc.get("loadName", "")).strip().lower() == component.strip().lower():
            return ulc
    if index < len(unit_load_cases):
        return unit_load_cases[index]
    if len(unit_load_cases) == 1:
        return unit_load_cases[0]
    raise RuntimeError(f"No unit-load stress field matches load component '{component}'.")


def calculate_fatigue(setup: dict, loads: dict, components: list[str], node_ids: list[int],
                      stresses: list[np.ndarray], mesh: dict | None = None) -> dict:
    sn = build_sn(setup["snCurve"])
    stress_mode = map_stress_mode(str(setup["snCurve"].get("fatigueStress", "Pnmax")))
    cycle_count = map_cycle_counter(str(setup["snCurve"].get("cycleCounter", "Reservoir")))
    is_critical_plane = stress_mode == "CP   "
    criterion = map_multiaxial_criterion(str(setup["snCurve"].get("multiaxialCriterion", "Normal stress")))
    if is_critical_plane and criterion not in supported_critical_plane_criteria():
        raise NotImplementedError(
            "Python CUDA critical-plane fatigue supports Normal stress, Modified shear, IIW equivalent, "
            f"Findley, and Dang Van. The selected multiaxial criterion is "
            f"'{setup['snCurve'].get('multiaxialCriterion', '')}'."
        )
    if is_critical_plane and mesh is None:
        raise RuntimeError("Critical-plane fatigue requires a model mesh to derive surface node normals and axes.")

    load_values = np.asarray(loads["values"], dtype=np.float64)
    if load_values.shape[1] < len(components):
        raise RuntimeError("The loads file has fewer load columns than configured load components.")
    load_matrix = load_values[:, :len(components)]
    lc_repetitions = total_repetitions(setup, loads)
    stress_fields = np.stack(stresses, axis=0).astype(np.float64)
    n_nodes = len(node_ids)
    n_lc = 1

    n_planes = 1
    if is_critical_plane:
        n_planes = critical_plane_count(setup["snCurve"])

    d_all = np.zeros((n_nodes, n_lc, n_planes), dtype=np.float64)
    n_load = np.zeros(n_nodes, dtype=np.float64)
    s_min = np.full((n_nodes, n_lc), np.nan, dtype=np.float64)
    s_max = np.full((n_nodes, n_lc), np.nan, dtype=np.float64)
    dseqi = np.zeros((n_nodes, n_planes), dtype=np.float64)

    print(f"Computing fatigue stress histories with {BACKEND}.")
    fast_results = try_single_linear_load_fast_path(
        sn, stress_mode, cycle_count, setup, loads, components, node_ids, stress_fields, n_lc, n_planes
    )
    if fast_results is not None:
        return fast_results

    batch_size = fatigue_node_batch_size(load_matrix.shape[0], stress_mode, n_nodes)
    print(f"Stress-history node batch size: {batch_size}.")
    if is_critical_plane:
        normals = np.asarray(mesh["node_normals"], dtype=np.float64)
        axes = np.asarray(mesh["node_axes"], dtype=np.float64)
        surface_mask = np.asarray(mesh.get("surface_mask", np.ones(n_nodes, dtype=bool)), dtype=bool)
        if not np.any(surface_mask):
            raise RuntimeError(
                "Critical-plane fatigue requires surface node normals and axes, but no exterior surface nodes "
                "could be reconstructed from the package FRD mesh."
            )
        skipped = int(np.count_nonzero(~surface_mask))
        if skipped:
            print(f"Skipping {skipped} non-surface nodes for critical-plane fatigue; no normal/axis is assigned to them.")
        print(f"Running FATLAB-compatible critical-plane node loop for {n_nodes} nodes and {n_planes} planes.")
        surface_nodes = np.flatnonzero(surface_mask)
        report_fatigue_progress(0, len(surface_nodes))
        for batch_start in range(0, len(surface_nodes), batch_size):
            batch_nodes = surface_nodes[batch_start:batch_start + batch_size]
            stress_time_batch = calc_stress_time_matrix(load_matrix, stress_fields[:, batch_nodes, :])
            for local_index, node_index in enumerate(batch_nodes):
                node_stress_time = stress_time_batch[:, local_index, :]
                sf_planes = stress_on_critical_planes(
                    node_stress_time,
                    normals[node_index, :],
                    axes[node_index, :],
                    setup["snCurve"],
                    criterion,
                )
                for plane_index in range(n_planes):
                    if is_shear_based_critical_plane(criterion):
                        plane_stress = sf_planes[:, plane_index, :]
                        s_min[node_index, 0] = nanmin_update(s_min[node_index, 0], float(np.min(plane_stress[:, 0])))
                        s_max[node_index, 0] = nanmax_update(s_max[node_index, 0], float(np.max(plane_stress[:, 0])))
                        rf = critical_plane_shear_cycle(plane_stress, lc_repetitions)
                        rf = equivalent_uniaxial(rf, setup["snCurve"], sn, criterion, node_stress_time)
                    else:
                        plane_stress = sf_planes[:, plane_index]
                        s_min[node_index, 0] = nanmin_update(s_min[node_index, 0], float(np.min(plane_stress)))
                        s_max[node_index, 0] = nanmax_update(s_max[node_index, 0], float(np.max(plane_stress)))
                        rf = cycle_counter(plane_stress, lc_repetitions, cycle_count, racetrack=0.05)
                    d_all[node_index, 0, plane_index] = damage_calc(rf, sn)
                    dseqi[node_index, plane_index] += equivalent_stress_bilin(rf, sn)
                    if plane_index == 0:
                        n_load[node_index] = float(np.sum(rf[3, :])) if rf.size else 0.0
            report_fatigue_progress(batch_start + len(batch_nodes), len(surface_nodes))
    else:
        print(f"Running FATLAB-compatible cycle-counting node loop for {n_nodes} nodes.")
        report_fatigue_progress(0, n_nodes)
        for batch_start in range(0, n_nodes, batch_size):
            batch_end = min(batch_start + batch_size, n_nodes)
            sf_batch = calc_fatigue_stress_matrix(load_matrix, stress_fields[:, batch_start:batch_end, :], stress_mode)
            for local_index, node_index in enumerate(range(batch_start, batch_end)):
                sf = sf_batch[:, local_index]
                s_min[node_index, 0] = float(np.min(sf))
                s_max[node_index, 0] = float(np.max(sf))
                rf = cycle_counter(sf, lc_repetitions, cycle_count, racetrack=0.05)
                d_all[node_index, 0, 0] = damage_calc(rf, sn)
                dseqi[node_index, 0] = equivalent_stress_bilin(rf, sn)
                n_load[node_index] = float(np.sum(rf[3, :])) if rf.size else 0.0
            report_fatigue_progress(batch_end, n_nodes)

    d_cp = np.sum(d_all, axis=1)
    cp_indices = np.argmax(d_cp, axis=1)
    d_tot = d_cp[np.arange(n_nodes), cp_indices]
    cp = cp_indices.astype(np.float64) + 1.0
    if is_critical_plane:
        cp = np.where(np.asarray(mesh.get("surface_mask", np.ones(n_nodes, dtype=bool)), dtype=bool), cp, 0.0)
    dseq = np.zeros(n_nodes, dtype=np.float64)
    dseq1 = np.zeros(n_nodes, dtype=np.float64)
    dseq2 = np.zeros(n_nodes, dtype=np.float64)
    ur = np.zeros(n_nodes, dtype=np.float64)
    active = n_load > 0
    dseqi_cp = dseqi[np.arange(n_nodes), cp_indices]
    dseq[active] = np.power((1.0 / sn["Dal"]) * dseqi_cp[active] / n_load[active], 1.0 / sn["m1"])
    dseq1 = np.power(np.maximum((1.0 / sn["Dal"]) * dseqi_cp / sn["N1"], 0.0), 1.0 / sn["m1"])
    dseq2 = np.power(np.maximum((1.0 / sn["Dal"]) * dseqi_cp / sn["N2"], 0.0), 1.0 / sn["m1"])
    ur = dseq2 / max(sn["ds2"], 1.0e-30)
    n_life = np.divide(n_load, d_tot, out=np.full_like(n_load, np.inf), where=d_tot > 0)
    valid_minmax = np.any(np.isfinite(s_min), axis=1)
    smin_out = np.zeros(n_nodes, dtype=np.float64)
    smax_out = np.zeros(n_nodes, dtype=np.float64)
    if np.any(valid_minmax):
        smin_out[valid_minmax] = np.nanmin(s_min[valid_minmax, :], axis=1)
        smax_out[valid_minmax] = np.nanmax(s_max[valid_minmax, :], axis=1)

    return {
        "backend": BACKEND,
        "nodeCount": len(node_ids),
        "cycles": float(np.nanmax(n_load)) if n_load.size else 0.0,
        "ds2": float(sn["ds2"]),
        "damage": d_tot,
        "utilization": ur,
        "life": n_life,
        "dseq": dseq,
        "dseq1": dseq1,
        "dseq2": dseq2,
        "nload": n_load,
        "smin": smin_out,
        "smax": smax_out,
        "dcp": d_cp[np.arange(n_nodes), cp_indices],
        "cp": cp,
    }


def try_single_linear_load_fast_path(sn: dict, stress_mode: str, cycle_count: int, setup: dict, loads: dict,
                                     components: list[str], node_ids: list[int], stress_fields: np.ndarray,
                                     n_lc: int, n_planes: int) -> dict | None:
    if os.environ.get("PREPOMAX_FATIGUE_DISABLE_FAST_PATH", "").strip().lower() in ("1", "true", "yes"):
        return None
    if len(components) != 1 or stress_fields.shape[0] != 1 or n_lc != 1 or n_planes != 1:
        return None
    if stress_mode not in linear_single_load_stress_modes():
        return None

    load_values = np.asarray(loads["values"], dtype=np.float64)
    if load_values.ndim != 2 or load_values.shape[1] < 1:
        return None
    load_signal = load_values[:, 0]
    lc_repetitions = total_repetitions(setup, loads)
    base_rf = cycle_counter(load_signal, lc_repetitions, cycle_count, racetrack=0.05)
    if base_rf.size == 0:
        return None

    print("Using single linear load fast path; cycle counting is evaluated once and scaled per node.")
    n_nodes = len(node_ids)
    unit_stress = stress_fields[0, :, :]
    node_scale = calc_fatigue_stress(unit_stress, stress_mode)

    d_tot = np.zeros(n_nodes, dtype=np.float64)
    dseqi = np.zeros(n_nodes, dtype=np.float64)
    n_load = np.zeros(n_nodes, dtype=np.float64)
    smin_out = np.zeros(n_nodes, dtype=np.float64)
    smax_out = np.zeros(n_nodes, dtype=np.float64)
    load_min = float(np.min(load_signal))
    load_max = float(np.max(load_signal))
    base_nload = float(np.sum(base_rf[3, :])) if base_rf.size else 0.0

    report_fatigue_progress(0, n_nodes)
    for node_index, scale in enumerate(node_scale):
        scale = float(scale)
        if abs(scale) > 1.0e-30:
            scaled_rf = scale_cycle_ranges(base_rf, scale)
            d_tot[node_index] = damage_calc(scaled_rf, sn)
            dseqi[node_index] = equivalent_stress_bilin(scaled_rf, sn)
            n_load[node_index] = base_nload
            if scale >= 0:
                smin_out[node_index] = scale * load_min
                smax_out[node_index] = scale * load_max
            else:
                smin_out[node_index] = scale * load_max
                smax_out[node_index] = scale * load_min
        report_fatigue_progress(node_index + 1, n_nodes)

    dseq = np.zeros(n_nodes, dtype=np.float64)
    active = n_load > 0
    dseq[active] = np.power((1.0 / sn["Dal"]) * dseqi[active] / n_load[active], 1.0 / sn["m1"])
    dseq1 = np.power(np.maximum((1.0 / sn["Dal"]) * dseqi / sn["N1"], 0.0), 1.0 / sn["m1"])
    dseq2 = np.power(np.maximum((1.0 / sn["Dal"]) * dseqi / sn["N2"], 0.0), 1.0 / sn["m1"])
    ur = dseq2 / max(sn["ds2"], 1.0e-30)
    n_life = np.divide(n_load, d_tot, out=np.full_like(n_load, np.inf), where=d_tot > 0)
    cp = np.ones(n_nodes, dtype=np.float64)

    return {
        "backend": BACKEND,
        "nodeCount": len(node_ids),
        "cycles": float(np.nanmax(n_load)) if n_load.size else 0.0,
        "ds2": float(sn["ds2"]),
        "damage": d_tot,
        "utilization": ur,
        "life": n_life,
        "dseq": dseq,
        "dseq1": dseq1,
        "dseq2": dseq2,
        "nload": n_load,
        "smin": smin_out,
        "smax": smax_out,
        "dcp": d_tot,
        "cp": cp,
    }


def linear_single_load_stress_modes() -> set[str]:
    return {
        "Sxx  ",
        "Syy  ",
        "Szz  ",
        "Txy  ",
        "Txz  ",
        "Tyz  ",
        "Ssvm ",
        "Pnmax",
    }


def scale_cycle_ranges(rf: np.ndarray, scale: float) -> np.ndarray:
    scaled = np.array(rf, dtype=np.float64, copy=True)
    scaled[0, :] = abs(scale) * scaled[0, :]
    scaled[1, :] = scale * scaled[1, :]
    if scaled.shape[0] > 2:
        scaled[2, :] = abs(scale) * scaled[2, :]
    if scaled.shape[0] > 4:
        scaled[4, :] = abs(scale) * scaled[4, :]
    return scaled


def build_sn(sn_setup: dict) -> dict:
    ds1 = float(sn_setup.get("ds1", 100.0))
    n1 = max(float(sn_setup.get("n1", 2.0e6)), 1.0e-30)
    n2 = max(float(sn_setup.get("n2", 1.0e7)), 1.0e-30)
    m1 = max(float(sn_setup.get("m1", 3.0)), 1.0e-30)
    gf = max(float(sn_setup.get("gf", 1.0)), 1.0e-30)
    return {
        "ds1": ds1,
        "N1": n1,
        "N2": n2,
        "m1": m1,
        "m2": max(float(sn_setup.get("m2", 5.0)), 1.0e-30),
        "gf": gf,
        "Dal": max(float(sn_setup.get("dal", sn_setup.get("allowableDamage", 1.0))), 1.0e-30),
        "minS": float(sn_setup.get("minS", 0.0)),
        "maxS": float(sn_setup.get("maxS", 1.0e30)),
        "ds2": ds1 / gf * (n1 / n2) ** (1.0 / m1),
        "mean_mode": map_mean_mode(str(sn_setup.get("meanMode", "None"))),
        "M": float(sn_setup.get("m", 0.2)),
        "Re": max(float(sn_setup.get("re", 235.0)), 1.0e-30),
        "Rm": max(float(sn_setup.get("rm", 370.0)), 1.0e-30),
        "mean_yield": bool(sn_setup.get("observeYieldLines", False)),
        "mean_comp": bool(sn_setup.get("extrapolateInCompression", False)),
    }


def map_stress_mode(mode: str) -> str:
    key = mode.strip().lower()
    if key == "sxx":
        return "Sxx  "
    if key == "syy":
        return "Syy  "
    if key == "szz":
        return "Szz  "
    if key in ("txy", "sxy"):
        return "Txy  "
    if key in ("txz", "sxz"):
        return "Txz  "
    if key in ("tyz", "syz"):
        return "Tyz  "
    if key in ("svm", "von mises", "vonmises"):
        return "Svm  "
    if key in ("ssvm", "signed von mises"):
        return "Ssvm "
    if key in ("p1", "principal stress"):
        return "P1   "
    if key == "p2":
        return "P2   "
    if key == "p3":
        return "P3   "
    if key in ("tmax", "tresca"):
        return "Tmax "
    if key in ("cp", "critical plane"):
        return "CP   "
    return "Pnmax"


def map_cycle_counter(counter: str) -> int:
    key = counter.strip().lower()
    if key in ("rainflow", "rainflow full"):
        return 2
    if key == "rainflow half":
        return 3
    if key == "single cycle":
        return 4
    return 1


def map_mean_mode(mode: str) -> int:
    modes = [
        "none",
        "linear",
        "bilinear",
        "soderberg",
        "modified goodman",
        "gerber parabola",
        "smith-watson-topper",
        "60% compression",
        "iiw low rs",
        "iiw medium rs",
        "fkm",
    ]
    key = mode.strip().lower()
    return modes.index(key) + 1 if key in modes else 1


def map_multiaxial_criterion(value: str) -> str:
    key = value.strip().lower()
    if key in ("normal stress", "normal stress  ", "normal"):
        return "Normal stress  "
    if key in ("modified shear", "gough-pollard"):
        return "Modified shear "
    if key in ("iiw equivalent", "iiw equivalent stress", "iiw"):
        return "IIW equivalent "
    if key == "findley":
        return "Findley        "
    if key in ("dang van", "dang-van"):
        return "Dang Van       "
    return value[:15].ljust(15)


def supported_critical_plane_criteria() -> set[str]:
    return {
        "Normal stress  ",
        "Modified shear ",
        "IIW equivalent ",
        "Findley        ",
        "Dang Van       ",
    }


def is_shear_based_critical_plane(criterion: str) -> bool:
    return criterion in {"IIW equivalent ", "Findley        ", "Dang Van       "}


def calc_stress_linear(load_matrix: np.ndarray, node_stresses: np.ndarray) -> np.ndarray:
    # Matches calc_stress_nonlinear for linear ULC superposition with ULC_load=1.
    return load_matrix @ node_stresses


def fatigue_node_batch_size(n_samples: int, stress_mode: str, n_nodes: int) -> int:
    requested_mb = float(os.environ.get("PREPOMAX_FATIGUE_BATCH_MB", "128"))
    target_bytes = max(16.0, requested_mb) * 1024.0 * 1024.0
    if BACKEND.startswith("CuPy"):
        try:
            free_bytes, _ = xp.cuda.runtime.memGetInfo()
            target_bytes = min(target_bytes, max(16.0 * 1024.0 * 1024.0, free_bytes * 0.25))
        except Exception:
            pass
    # Principal stress modes temporarily allocate the 3x3 tensor and eigenvalues.
    values_per_sample_node = 22 if stress_mode in ("P1   ", "P2   ", "P3   ", "Pnmax", "Tmax ") else 9
    bytes_per_node = max(1, n_samples) * values_per_sample_node * np.dtype(np.float64).itemsize
    return max(1, min(n_nodes, int(target_bytes // max(1, bytes_per_node))))


def report_fatigue_progress(completed: int, total: int) -> None:
    if total <= 0:
        return
    interval = max(1, total // 20)
    if completed == 0 or completed >= total or completed % interval < 1:
        local_percent = 100.0 * completed / total
        emit_progress(30.0 + 60.0 * completed / total, "Calculating fatigue")
        print(f"  {local_percent:5.1f}%")


def calc_stress_time_matrix(load_matrix: np.ndarray, stress_fields: np.ndarray) -> np.ndarray:
    load_backend = xp.asarray(load_matrix, dtype=xp.float64)
    stress_backend = xp.asarray(stress_fields, dtype=xp.float64)
    return to_numpy(xp.tensordot(load_backend, stress_backend, axes=(1, 0)))


def calc_fatigue_stress_matrix(load_matrix: np.ndarray, stress_fields: np.ndarray, stress_mode: str) -> np.ndarray:
    # samples x nodes x tensor(6)
    load_backend = xp.asarray(load_matrix, dtype=xp.float64)
    stress_backend = xp.asarray(stress_fields, dtype=xp.float64)
    stress_time = xp.tensordot(load_backend, stress_backend, axes=(1, 0))
    if stress_mode == "Sxx  ":
        sf = stress_time[:, :, 0]
    elif stress_mode == "Syy  ":
        sf = stress_time[:, :, 1]
    elif stress_mode == "Szz  ":
        sf = stress_time[:, :, 2]
    elif stress_mode == "Txy  ":
        sf = stress_time[:, :, 3]
    elif stress_mode == "Txz  ":
        sf = stress_time[:, :, 4]
    elif stress_mode == "Tyz  ":
        sf = stress_time[:, :, 5]
    elif stress_mode == "Svm  ":
        sf = von_mises_matrix(stress_time, signed=False)
    elif stress_mode == "Ssvm ":
        sf = von_mises_matrix(stress_time, signed=True)
    else:
        p = principal_stresses_matrix(stress_time)
        if stress_mode == "P1   ":
            sf = p[:, :, 0]
        elif stress_mode == "P2   ":
            sf = p[:, :, 1]
        elif stress_mode == "P3   ":
            sf = p[:, :, 2]
        elif stress_mode == "Tmax ":
            sf = (p[:, :, 0] - p[:, :, 2]) / 2.0
        else:
            idx = xp.argmax(xp.abs(p), axis=2)
            rows = xp.arange(p.shape[0])[:, None]
            cols = xp.arange(p.shape[1])[None, :]
            sf = p[rows, cols, idx]
    return to_numpy(sf)


def critical_plane_count(sn_curve: dict) -> int:
    n_theta = max(1, int(round(float(sn_curve.get("normalSearchPlanes", 36)))))
    n_phi = max(1, int(round(float(sn_curve.get("inclinedSearchPlanes", 1)))))
    return n_theta * n_phi


def search_plane_angles_from_setup(sn_curve: dict) -> tuple[np.ndarray, np.ndarray]:
    n_theta = max(1, int(round(float(sn_curve.get("normalSearchPlanes", 36)))))
    n_phi = max(1, int(round(float(sn_curve.get("inclinedSearchPlanes", 1)))))
    theta = np.arange(n_theta, dtype=np.float64) * math.pi / n_theta
    if n_phi == 1:
        phi = np.array([0.0], dtype=np.float64)
    elif n_phi == 2:
        phi = np.array([0.0, math.pi / 4.0], dtype=np.float64)
    else:
        phi = np.linspace(0.0, math.pi / 2.0, n_phi, dtype=np.float64)
    return theta, phi


def stress_on_critical_planes(stress_time: np.ndarray, node_normal: np.ndarray,
                              node_axis: np.ndarray, sn_curve: dict, criterion: str) -> np.ndarray:
    theta, phi = search_plane_angles_from_setup(sn_curve)
    n_planes = len(theta) * len(phi)
    if is_shear_based_critical_plane(criterion):
        out = np.zeros((stress_time.shape[0], n_planes, 3), dtype=np.float64)
    else:
        out = np.zeros((stress_time.shape[0], n_planes), dtype=np.float64)
    normal = normalize_vector(np.asarray(node_normal, dtype=np.float64), np.array([0.0, 0.0, 1.0], dtype=np.float64))
    axis = np.asarray(node_axis, dtype=np.float64)
    axis = axis - float(np.dot(axis, normal)) * normal
    axis = normalize_vector(axis, np.array([1.0, 0.0, 0.0], dtype=np.float64))
    y_axis = normalize_vector(np.cross(normal, axis), np.array([0.0, 1.0, 0.0], dtype=np.float64))
    initial_frame = np.column_stack((axis, y_axis, normal))

    sx = stress_time[:, 0]
    sy = stress_time[:, 1]
    sz = stress_time[:, 2]
    txy = stress_time[:, 3]
    txz = stress_time[:, 4]
    tyz = stress_time[:, 5]

    plane_index = 0
    for theta_i in theta:
        theta_frame = initial_frame @ rotation_matrix(np.array([0.0, 0.0, 1.0], dtype=np.float64), float(theta_i))
        for phi_i in phi:
            frame = theta_frame @ rotation_matrix(np.array([0.0, 1.0, 0.0], dtype=np.float64), float(phi_i))
            plane_normal = frame[:, 0]
            nx, ny, nz = plane_normal
            sn = (
                sx * nx * nx + sy * ny * ny + sz * nz * nz
                + 2.0 * (txy * nx * ny + tyz * ny * nz + txz * nx * nz)
            )
            if criterion == "Normal stress  ":
                out[:, plane_index] = sn
            else:
                u = frame[:, 1]
                v = frame[:, 2]
                tu = (
                    sx * nx * u[0] + sy * ny * u[1] + sz * nz * u[2]
                    + txy * (nx * u[1] + ny * u[0])
                    + tyz * (ny * u[2] + nz * u[1])
                    + txz * (nz * u[0] + nx * u[2])
                )
                tv = (
                    sx * nx * v[0] + sy * ny * v[1] + sz * nz * v[2]
                    + txy * (nx * v[1] + ny * v[0])
                    + tyz * (ny * v[2] + nz * v[1])
                    + txz * (nz * v[0] + nx * v[2])
                )
                if criterion == "Modified shear ":
                    i1 = sx + sy + sz
                    k1 = float(sn_curve.get("multiaxialK1", 0.3))
                    out[:, plane_index] = np.sign(i1) * np.sqrt(sn * sn + k1 * k1 * tu * tu)
                else:
                    out[:, plane_index, :] = np.column_stack((sn, tu, tv))
            plane_index += 1
    return out


def rotation_matrix(axis: np.ndarray, angle: float) -> np.ndarray:
    axis = normalize_vector(np.asarray(axis, dtype=np.float64), np.array([1.0, 0.0, 0.0], dtype=np.float64))
    x, y, z = axis
    c = math.cos(angle)
    s = math.sin(angle)
    c1 = 1.0 - c
    return np.array([
        [c + x * x * c1, x * y * c1 - z * s, x * z * c1 + y * s],
        [y * x * c1 + z * s, c + y * y * c1, y * z * c1 - x * s],
        [z * x * c1 - y * s, z * y * c1 + x * s, c + z * z * c1],
    ], dtype=np.float64)


def critical_plane_shear_cycle(plane_stress: np.ndarray, lc_reps: float) -> np.ndarray:
    sn = plane_stress[:, 0]
    tu = plane_stress[:, 1]
    tv = plane_stress[:, 2]
    _, shear_range = longest_chord(tu, tv)
    rf = np.array([[
        shear_range,
        np.ptp(sn) / 2.0,
        np.mean(sn),
        max(float(lc_reps), 1.0),
        np.max(sn),
    ]], dtype=np.float64).T
    return rf


def longest_chord(tu: np.ndarray, tv: np.ndarray) -> tuple[float, float]:
    points = np.column_stack((np.asarray(tu, dtype=np.float64), np.asarray(tv, dtype=np.float64)))
    if points.shape[0] < 2:
        return 0.0, 0.0
    delta = points[:, None, :] - points[None, :, :]
    distances = np.sqrt(np.sum(delta * delta, axis=2))
    i, j = np.unravel_index(int(np.argmax(distances)), distances.shape)
    mean_shear = float(np.linalg.norm(points[i, :] + points[j, :]) / 2.0)
    shear_range = float(distances[i, j])
    return mean_shear, shear_range


def equivalent_uniaxial(rf: np.ndarray, sn_curve: dict, sn: dict, criterion: str, stress_time: np.ndarray) -> np.ndarray:
    shear_amplitude = float(rf[0, 0]) / 2.0
    normal_amplitude = float(rf[1, 0])
    mean_normal = float(rf[2, 0])
    cycles = float(rf[3, 0])
    max_normal = float(rf[4, 0])
    k1 = float(sn_curve.get("multiaxialK1", 0.3))
    k2 = max(float(sn_curve.get("multiaxialK2", 1.0)), 1.0e-30)

    if criterion == "Findley        ":
        stress_range = (2.0 * shear_amplitude + 2.0 * k1 * max_normal) / (
            0.5 * (k1 + math.sqrt(1.0 + k1 * k1))
        )
    elif criterion == "IIW equivalent ":
        normal_range = 2.0 * normal_amplitude
        shear_range = 2.0 * shear_amplitude
        stress_range = (1.0 / math.sqrt(k2)) * math.sqrt(normal_range * normal_range + k1 * shear_range * shear_range)
    elif criterion == "Dang Van       ":
        hydrostatic_max = float(np.max((stress_time[:, 0] + stress_time[:, 1] + stress_time[:, 2]) / 3.0))
        a = k1
        b = 3.0 - 1.5 * k1
        stress_range = 2.0 * (a * shear_amplitude + b * hydrostatic_max)
    else:
        stress_range = 2.0 * normal_amplitude

    return np.array([[max(stress_range, 0.0)], [0.0], [0.0], [cycles], [0.0]], dtype=np.float64)


def von_mises_matrix(stress_time, signed: bool):
    sx = stress_time[:, :, 0]
    sy = stress_time[:, :, 1]
    sz = stress_time[:, :, 2]
    txy = stress_time[:, :, 3]
    txz = stress_time[:, :, 4]
    tyz = stress_time[:, :, 5]
    svm = xp.sqrt(0.5 * ((sx - sy) ** 2 + (sy - sz) ** 2 + (sz - sx) ** 2 + 6.0 * (txy ** 2 + txz ** 2 + tyz ** 2)))
    if signed:
        svm = xp.sign(sx + sy + sz) * svm
    return svm


def principal_stresses_matrix(stress_time):
    tensor = xp.empty((stress_time.shape[0], stress_time.shape[1], 3, 3), dtype=xp.float64)
    tensor[:, :, 0, 0] = stress_time[:, :, 0]
    tensor[:, :, 1, 1] = stress_time[:, :, 1]
    tensor[:, :, 2, 2] = stress_time[:, :, 2]
    tensor[:, :, 0, 1] = tensor[:, :, 1, 0] = stress_time[:, :, 3]
    tensor[:, :, 0, 2] = tensor[:, :, 2, 0] = stress_time[:, :, 4]
    tensor[:, :, 1, 2] = tensor[:, :, 2, 1] = stress_time[:, :, 5]
    return xp.flip(xp.linalg.eigvalsh(tensor), axis=2)


def calc_fatigue_stress(stress_time: np.ndarray, stress_mode: str) -> np.ndarray:
    if stress_mode == "Sxx  ":
        return stress_time[:, 0]
    if stress_mode == "Syy  ":
        return stress_time[:, 1]
    if stress_mode == "Szz  ":
        return stress_time[:, 2]
    if stress_mode == "Txy  ":
        return stress_time[:, 3]
    if stress_mode == "Txz  ":
        return stress_time[:, 4]
    if stress_mode == "Tyz  ":
        return stress_time[:, 5]
    if stress_mode == "Svm  ":
        return von_mises(stress_time, signed=False)
    if stress_mode == "Ssvm ":
        return von_mises(stress_time, signed=True)
    p = principal_stresses(stress_time)
    if stress_mode == "P1   ":
        return p[:, 0]
    if stress_mode == "P2   ":
        return p[:, 1]
    if stress_mode == "P3   ":
        return p[:, 2]
    if stress_mode == "Tmax ":
        return (p[:, 0] - p[:, 2]) / 2.0
    # FATLAB Pnmax: signed principal stress with maximum absolute magnitude.
    idx = np.argmax(np.abs(p), axis=1)
    return p[np.arange(p.shape[0]), idx]


def von_mises(stress_time: np.ndarray, signed: bool) -> np.ndarray:
    sx = stress_time[:, 0]
    sy = stress_time[:, 1]
    sz = stress_time[:, 2]
    txy = stress_time[:, 3]
    txz = stress_time[:, 4]
    tyz = stress_time[:, 5]
    svm = np.sqrt(0.5 * ((sx - sy) ** 2 + (sy - sz) ** 2 + (sz - sx) ** 2 + 6.0 * (txy ** 2 + txz ** 2 + tyz ** 2)))
    if signed:
        svm = np.sign(sx + sy + sz) * svm
    return svm


def principal_stresses(stress_time: np.ndarray) -> np.ndarray:
    tensor = np.empty((stress_time.shape[0], 3, 3), dtype=np.float64)
    tensor[:, 0, 0] = stress_time[:, 0]
    tensor[:, 1, 1] = stress_time[:, 1]
    tensor[:, 2, 2] = stress_time[:, 2]
    tensor[:, 0, 1] = tensor[:, 1, 0] = stress_time[:, 3]
    tensor[:, 0, 2] = tensor[:, 2, 0] = stress_time[:, 4]
    tensor[:, 1, 2] = tensor[:, 2, 1] = stress_time[:, 5]
    # eigvalsh returns ascending values; FATLAB sorts principal stresses descending.
    return np.flip(np.linalg.eigvalsh(tensor), axis=1)


def cycle_counter(signal: np.ndarray, lc_reps: float, cycle_count: int, racetrack: float) -> np.ndarray:
    signal = np.asarray(signal, dtype=np.float64).reshape(-1)
    if cycle_count == 4:
        rf = np.array([[np.ptp(signal)], [np.mean(signal)], [0.0], [1.0], [0.0]], dtype=np.float64)
        rf[3, :] *= lc_reps
        return rf
    extremes = sig2ext(signal)
    if racetrack and len(extremes) > 1:
        threshold = float(np.ptp(extremes) * racetrack)
        extremes = race_track_filter(extremes, threshold)
    if len(extremes) < 2 or np.max(extremes) == np.min(extremes):
        rf = np.array([[0.0], [0.0], [0.0], [0.0], [0.0]], dtype=np.float64)
    elif cycle_count == 1:
        rf = reservoir_counting(extremes)
    elif cycle_count == 2:
        rf = rainflow_full(extremes)
    elif cycle_count == 3:
        rf = rainflow_half(extremes)
    else:
        raise RuntimeError(f"Unknown cycle counting method: {cycle_count}")
    rf[3, :] *= lc_reps
    return rf


def sig2ext(signal: np.ndarray) -> np.ndarray:
    if len(signal) == 0:
        return np.asarray([], dtype=np.float64)
    out = [float(signal[0])]
    for i in range(1, len(signal) - 1):
        if (signal[i] > signal[i - 1] and signal[i] > signal[i + 1]) or (
            signal[i] < signal[i - 1] and signal[i] < signal[i + 1]
        ):
            out.append(float(signal[i]))
    out.append(float(signal[-1]))
    return remove_duplicated_values(np.asarray(out, dtype=np.float64))


def remove_duplicated_values(signal: np.ndarray) -> np.ndarray:
    if len(signal) < 3:
        return signal
    keep = np.ones(len(signal), dtype=bool)
    keep[1:] = signal[1:] != signal[:-1]
    return signal[keep]


def race_track_filter(peaks: np.ndarray, threshold: float) -> np.ndarray:
    if len(peaks) < 3:
        return peaks
    num_peaks = len(peaks)
    s1 = peaks[0]
    s2 = peaks[1]
    ind = 1
    kept = []
    while ind < num_peaks - 1:
        ind += 1
        s3 = peaks[ind]
        diff12 = abs(s1 - s2)
        diff23 = abs(s2 - s3)
        diff31 = abs(s3 - s1)
        if diff12 < threshold:
            if diff23 >= diff12 and diff23 >= diff31:
                s1, s2 = s2, s3
                if diff23 >= threshold:
                    break
            elif diff31 >= diff12 and diff31 >= diff23:
                s2 = s3
                if diff31 >= threshold:
                    break
        else:
            ind -= 1
            break
    while ind < num_peaks - 1:
        ind += 1
        s3 = peaks[ind]
        diff12 = abs(s1 - s2)
        diff23 = abs(s2 - s3)
        if diff23 >= threshold:
            kept.append(s1)
            s1, s2 = s2, s3
        else:
            ind += 1
            if ind >= num_peaks:
                break
            s3 = peaks[ind]
            if abs(s1 - s3) > diff12:
                s2 = s3
    kept.extend([s1, s2])
    return np.asarray(kept, dtype=np.float64)


def reservoir_counting(signal: np.ndarray) -> np.ndarray:
    if np.max(signal) == np.min(signal):
        return np.array([[0.0], [0.0], [0.0], [0.0], [0.0]], dtype=np.float64)
    imax = int(np.argmax(signal))
    shifted = np.concatenate([signal[imax:], signal[: imax + 1]])
    shifted = remove_duplicated_values(shifted)
    valley_indexes = [i for i in range(1, len(shifted) - 1) if shifted[i] < shifted[i - 1] and shifted[i] < shifted[i + 1]]
    valleys = shifted[valley_indexes]
    sorted_valleys = np.argsort(valleys)
    drained = np.zeros(len(shifted), dtype=bool)
    d_s, s_m, n_i = [], [], []
    for sorted_index in sorted_valleys:
        t_valley = valley_indexes[int(sorted_index)]
        t = t_valley
        smax_left = shifted[t]
        while t >= 0 and not drained[t]:
            smax_left = max(smax_left, shifted[t])
            t -= 1
        t = t_valley
        smax_right = shifted[t]
        while t < len(shifted) and not drained[t]:
            smax_right = max(smax_right, shifted[t])
            t += 1
        smax = min(smax_left, smax_right)
        smin = shifted[t_valley]
        d_s.append(smax - smin)
        s_m.append((smax + smin) / 2.0)
        n_i.append(1.0)
        drained[t_valley] = True
    if not d_s:
        return np.array([[0.0], [0.0], [0.0], [0.0], [0.0]], dtype=np.float64)
    d_s = np.asarray(d_s, dtype=np.float64)
    s_m = np.asarray(s_m, dtype=np.float64)
    n_i = np.asarray(n_i, dtype=np.float64)
    return np.vstack([d_s, s_m, d_s, n_i, d_s])


def rainflow_full(signal: np.ndarray) -> np.ndarray:
    if np.max(signal) == np.min(signal) or len(signal) < 3:
        return np.array([[0.0], [0.0], [0.0], [0.0], [0.0]], dtype=np.float64)
    s = sig2ext(remove_duplicated_values(signal))
    d_s, s_m, n_i = [], [], []
    for _ in range(2):
        i = 0
        j = 0
        while i + 3 < len(s):
            s1, s2, s3, s4 = s[i], s[i + 1], s[i + 2], s[i + 3]
            ds1 = abs(s2 - s1)
            ds2 = abs(s3 - s2)
            ds3 = abs(s4 - s3)
            if ds2 <= ds1 and ds2 <= ds3:
                n_i.append(1.0)
                d_s.append(ds2)
                s_m.append((s2 + s3) / 2.0)
                s = np.concatenate([s[:j], s[j:i + 1], s[i + 3:]])
                j += 2
                i = j
            else:
                i += 1
        res = s[j:]
        if len(res) < 3:
            break
        if res[0] == res[-1]:
            s = np.concatenate([res[:-1], res])
        else:
            s = sig2ext(np.concatenate([res, res]))
    if not d_s:
        return np.array([[0.0], [0.0], [0.0], [0.0], [0.0]], dtype=np.float64)
    d_s = np.asarray(d_s, dtype=np.float64)
    s_m = np.asarray(s_m, dtype=np.float64)
    n_i = np.asarray(n_i, dtype=np.float64)
    return np.vstack([d_s, s_m, d_s, n_i, d_s])


def rainflow_half(signal: np.ndarray) -> np.ndarray:
    # Standard four-point rainflow with half-cycle residue, matching FATLAB's half-cycle intent.
    s = sig2ext(remove_duplicated_values(signal))
    stack = []
    d_s, s_m, n_i = [], [], []
    for value in s:
        stack.append(float(value))
        while len(stack) >= 3:
            x = abs(stack[-2] - stack[-3])
            y = abs(stack[-1] - stack[-2])
            if y < x:
                break
            d_s.append(x)
            s_m.append((stack[-3] + stack[-2]) / 2.0)
            n_i.append(0.5 if len(stack) == 3 else 1.0)
            stack.pop(-2)
    for i in range(len(stack) - 1):
        ds = abs(stack[i + 1] - stack[i])
        d_s.append(ds)
        s_m.append((stack[i + 1] + stack[i]) / 2.0)
        n_i.append(0.5)
    if not d_s:
        return np.array([[0.0], [0.0], [0.0], [0.0], [0.0]], dtype=np.float64)
    d_s = np.asarray(d_s, dtype=np.float64)
    s_m = np.asarray(s_m, dtype=np.float64)
    n_i = np.asarray(n_i, dtype=np.float64)
    return np.vstack([d_s, s_m, d_s, n_i, d_s])


def mean_stress(sm: float, sn: dict) -> float:
    sa2 = sn["ds2"] / 2.0
    m = sn["M"]
    re = sn["Re"]
    rm = sn["Rm"]
    mode = sn["mean_mode"]
    if mode == 2:
        sa = sa2 - m * sm
    elif mode == 3:
        sa_r0 = sa2 / (1.0 + m)
        sa = sa2 - m * sm if sm <= sa_r0 else sa_r0 * (rm - sm) / max(rm - sa_r0, 1.0e-30)
    elif mode == 4:
        sa = sa2 * (1.0 - sm / re)
    elif mode == 5:
        sa = sa2 * (1.0 - sm / rm)
    elif mode == 6:
        sa = sa2 if sm < 0 else sa2 * (1.0 - sm * sm / (rm * rm))
    elif mode == 7:
        sa = sa2 if sm <= 0 else -0.5 * (sm - math.sqrt(sm * sm + 4.0 * sa2 * sa2))
    elif mode == 8:
        if sm < -sa2 / 0.6:
            sa = sa2 / 0.6
        elif sm <= sa2:
            sa = 1.25 * sa2 - 0.25 * sm
        else:
            sa = sa2
    elif mode == 9:
        if sm < 0:
            fr = 1.6
        elif sm <= 3.0 * sa2:
            fr = (-0.6 / max(3.0 * sa2, 1.0e-30)) * sm + 1.6
        else:
            fr = 1.0
        sa = fr * sa2
    elif mode == 10:
        if sm < 0:
            fr = 1.3
        elif sm <= 3.0 * sa2:
            fr = (-0.3 / max(3.0 * sa2, 1.0e-30)) * sm + 1.3
        else:
            fr = 1.0
        sa = fr * sa2
    elif mode == 11:
        sa_r0 = sa2 / (1.0 + m)
        sa_rni = sa2 / max(1.0 - m, 1.0e-30)
        sm_r05 = sa_r0 * (3.0 + m) / (1.0 + m)
        if sm <= -sa_rni:
            sa = sa_rni
        elif sm < sa_r0:
            sa = sa2 - m * sm
        elif sm < sm_r05:
            sa = sa_r0 * (1.0 + m / 3.0) - m / 3.0 * sm
        else:
            sa = sm_r05 / 3.0
    else:
        sa = sa2
    if not sn["mean_comp"] and sm < 0 and mode < 8:
        sa = sa2
    if sn["mean_yield"]:
        yield_line = re + sm if sm <= 0 else re - sm
        sa = min(yield_line, sa)
    return 2.0 * max(sa, 0.0)


def damage_calc(rf: np.ndarray, sn: dict) -> float:
    nr2 = sn["N2"]
    m1 = sn["m1"]
    m2 = sn["m2"]
    ds_min = sn["minS"] / sn["gf"]
    ds_max = sn["maxS"] / sn["gf"]
    damage = 0.0
    for ds_i, sm_i, n_i in zip(rf[0, :], rf[1, :], rf[3, :]):
        ds_r2 = mean_stress(float(sm_i), sn)
        c1 = nr2 * ds_r2 ** m1
        c2 = nr2 * ds_r2 ** m2
        if ds_i >= ds_max:
            ni = c1 / max(ds_i ** m1, 1.0e-30)
            di = max(1.0, n_i / ni)
        elif ds_i >= ds_r2:
            ni = c1 / max(ds_i ** m1, 1.0e-30)
            di = n_i / ni
        elif ds_i >= ds_min:
            ni = c2 / max(ds_i ** m2, 1.0e-30)
            di = n_i / ni
        else:
            di = 0.0
        damage += di
    return float(damage)


def equivalent_stress_bilin(rf: np.ndarray, sn: dict) -> float:
    m1 = sn["m1"]
    m2 = sn["m2"]
    ds_min = sn["minS"] / sn["gf"]
    ds_r2_zero = sn["ds2"]
    out = 0.0
    for ds_i, sm_i, n_i in zip(rf[0, :], rf[1, :], rf[3, :]):
        ds_r2_mean = mean_stress(float(sm_i), sn)
        if ds_r2_mean <= 0:
            continue
        ds_corrected = ds_i / (ds_r2_mean / ds_r2_zero)
        if ds_corrected >= ds_r2_zero:
            out += ds_corrected ** m1 * n_i
        elif ds_corrected >= ds_min:
            out += (ds_corrected ** m2) * (ds_r2_zero ** (m1 - m2)) * n_i
    return float(out)


def total_repetitions(setup: dict, loads: dict) -> float:
    load_cases = setup.get("loads", {}).get("loadCases", [])
    if load_cases:
        return max(float(load_cases[0].get("repetitions", 1.0)), 1.0)
    return max(float(loads.get("repetitions", 1.0)), 1.0)


def to_numpy(array) -> np.ndarray:
    if BACKEND.startswith("CuPy"):
        return xp.asnumpy(array)
    return np.asarray(array)


def write_results_csv(file_name: Path, node_ids: list[int], results: dict) -> None:
    with file_name.open("w", encoding="utf-8", newline="") as f:
        writer = csv.writer(f)
        writer.writerow(["Node", "Damage", "UR", "Life", "Dseq", "Dseq1", "Dseq2", "Nload", "Smin", "Smax", "CP"])
        for i, node_id in enumerate(node_ids):
            writer.writerow([
                node_id,
                float(results["damage"][i]),
                float(results["utilization"][i]),
                finite_float(results["life"][i]),
                float(results["dseq"][i]),
                float(results["dseq1"][i]),
                float(results["dseq2"][i]),
                float(results["nload"][i]),
                finite_float(results["smin"][i]),
                finite_float(results["smax"][i]),
                finite_float(results["cp"][i]),
            ])


def write_results_json(file_name: Path, setup: dict, results: dict) -> None:
    life = np.asarray(results["life"], dtype=np.float64)
    finite_life = life[np.isfinite(life)]
    summary = {
        "schema": "prepomax-fatigue-python-results-v1",
        "analysisName": setup.get("model", {}).get("analysisName", ""),
        "backend": results["backend"],
        "nodeCount": results["nodeCount"],
        "cycles": results["cycles"],
        "ds2": results["ds2"],
        "maxDamage": float(np.nanmax(results["damage"])),
        "maxUtilization": float(np.nanmax(results["utilization"])),
        "minLife": float(np.nanmin(finite_life)) if finite_life.size else math.inf,
        "resultsFrdFile": "prepomax_fatigue_results.frd",
    }
    with file_name.open("w", encoding="utf-8") as f:
        json.dump(summary, f, indent=2)


def write_fatigue_frd(source_frd: Path, output_frd: Path, node_ids: list[int], results: dict) -> None:
    mesh_lines = []
    with source_frd.open("r", encoding="ascii", errors="ignore") as f:
        for line in f:
            if line.startswith("    1PSTEP"):
                break
            mesh_lines.append(line.rstrip("\n"))

    with output_frd.open("w", encoding="ascii", newline="\n") as f:
        for line in mesh_lines:
            f.write(line + "\n")
        write_fatigue_field(f, node_ids, results)


def write_fatigue_field(f, node_ids: list[int], results: dict) -> None:
    components = [
        ("DAMAGE", results["damage"]),
        ("UR", results["utilization"]),
        ("LIFE", results["life"]),
        ("DSEQ", results["dseq"]),
        ("DSEQ1", results["dseq1"]),
        ("DSEQ2", results["dseq2"]),
        ("NLOAD", results["nload"]),
        ("SMIN", results["smin"]),
        ("SMAX", results["smax"]),
        ("CP", results["cp"]),
    ]
    block_id = 1
    increment_id = 1
    step_id = 1
    global_id = 101
    f.write("    1PSTEP" + str(block_id).rjust(26) + str(increment_id).rjust(12) + str(step_id).rjust(12) + "\n")
    f.write("  100CL" + str(global_id).rjust(5) + format_frd_float(1.0) +
            str(len(node_ids)).rjust(12) + str(0).rjust(22) + str(increment_id).rjust(12) + "\n")
    f.write(" -4  " + "FATIGUE".ljust(10) + str(len(components)).rjust(5) + "    1\n")
    for i, (name, _) in enumerate(components, start=1):
        f.write(" -5  " + name[:10].ljust(10) + "    1    2" + str(i).rjust(5) + "    0\n")
    for row, node_id in enumerate(node_ids):
        f.write(" -1" + str(node_id).rjust(10))
        for _, values in components:
            f.write(format_frd_float(finite_float(values[row])))
        f.write("\n")
    f.write(" -3\n")


def format_frd_float(value: float) -> str:
    if not math.isfinite(float(value)):
        value = 0.0
    return f"{float(value):12.5E}"


def finite_float(value) -> float:
    value = float(value)
    if math.isinf(value):
        return 1.0e30
    if math.isnan(value):
        return 0.0
    return value


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise

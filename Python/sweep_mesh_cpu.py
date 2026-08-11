"""
CPU-only swept mesh generation utilities.

The current workflow sweeps a 2D quad mesh around the global Y axis and produces
20-node hexahedral elements (C3D20R ordering).
"""

from __future__ import annotations

import math
from typing import Dict, Iterable, List, Optional, Sequence, Tuple


PointMap = Dict[int, List[float]]
QuadMap = Dict[int, List[int]]
BoundaryNodeMap = Dict[int, Dict[str, int]]


def _rotate_about_y(point: Sequence[float], center: Sequence[float], angle_rad: float) -> List[float]:
    """Rotate a point around the Y axis passing through center."""
    dx = float(point[0]) - float(center[0])
    dy = float(point[1]) - float(center[1])
    dz = float(point[2]) - float(center[2])
    cos_a = math.cos(angle_rad)
    sin_a = math.sin(angle_rad)
    return [
        float(center[0]) + dx * cos_a - dz * sin_a,
        float(center[1]) + dy,
        float(center[2]) + dx * sin_a + dz * cos_a,
    ]


def _canonical_edge(node_a: int, node_b: int) -> Tuple[int, int]:
    """Return an order-independent 2D edge key."""
    return (node_a, node_b) if node_a <= node_b else (node_b, node_a)


def _vector_sub(a: Sequence[float], b: Sequence[float]) -> Tuple[float, float, float]:
    return (float(a[0]) - float(b[0]), float(a[1]) - float(b[1]), float(a[2]) - float(b[2]))


def _cross(a: Sequence[float], b: Sequence[float]) -> Tuple[float, float, float]:
    return (
        float(a[1]) * float(b[2]) - float(a[2]) * float(b[1]),
        float(a[2]) * float(b[0]) - float(a[0]) * float(b[2]),
        float(a[0]) * float(b[1]) - float(a[1]) * float(b[0]),
    )


def _dot(a: Sequence[float], b: Sequence[float]) -> float:
    return float(a[0]) * float(b[0]) + float(a[1]) * float(b[1]) + float(a[2]) * float(b[2])


def _signed_local_volume(corners: Sequence[Sequence[float]]) -> float:
    """Return a quick orientation measure for the first local corner triad."""
    n1, n2, _n3, n4, n5 = corners[:5]
    e1 = _vector_sub(n2, n1)
    e2 = _vector_sub(n4, n1)
    e3 = _vector_sub(n5, n1)
    return _dot(e1, _cross(e2, e3))


def _build_quad_edges(quad_elements: QuadMap) -> Iterable[Tuple[int, int]]:
    """Yield unique 2D edges from all quads."""
    seen = set()
    for node_ids in quad_elements.values():
        if len(node_ids) != 4:
            continue
        for index in range(4):
            edge = _canonical_edge(node_ids[index], node_ids[(index + 1) % 4])
            if edge not in seen:
                seen.add(edge)
                yield edge


def get_profile_top_y(nodes_2d: PointMap) -> float:
    """Return the highest Y coordinate in the 2D profile."""
    if not nodes_2d:
        raise ValueError("The 2D mesh does not contain any nodes.")
    return max(float(coords[1]) for coords in nodes_2d.values())


def compute_stanchion_translation(nodes_2d: PointMap, target_top: Sequence[float]) -> List[float]:
    """
    Compute the translation that aligns the profile top with the target point.

    The target point supplies the final sweep axis in X/Z and the final top
    location in Y.
    """
    if target_top is None or len(target_top) < 3:
        raise ValueError("The stanchion target top point must contain three coordinates.")

    profile_top_y = get_profile_top_y(nodes_2d)
    return [
        float(target_top[0]),
        float(target_top[1]) - profile_top_y,
        float(target_top[2]),
    ]


def translate_profile_nodes(nodes_2d: PointMap, translation: Optional[Sequence[float]]) -> PointMap:
    """Return a translated copy of the 2D profile nodes."""
    offset = list(translation[:3]) if translation is not None and len(translation) >= 3 else [0.0, 0.0, 0.0]
    if math.isclose(offset[0], 0.0, abs_tol=1e-12) and math.isclose(offset[1], 0.0, abs_tol=1e-12) and math.isclose(offset[2], 0.0, abs_tol=1e-12):
        return {node_id: [float(coord) for coord in coords[:3]] for node_id, coords in nodes_2d.items()}

    translated_nodes: PointMap = {}
    for node_id, coords in nodes_2d.items():
        translated_nodes[node_id] = [
            float(coords[0]) + offset[0],
            float(coords[1]) + offset[1],
            float(coords[2]) + offset[2],
        ]
    return translated_nodes


def sweep_2d_mesh_cpu(
    nodes_2d: PointMap,
    quad_elements: QuadMap,
    repos: Optional[Sequence[float]] = None,
    start_node_id: int = 1,
    start_element_id: int = 1,
    num_angles: int = 50,
    total_angle: float = 360.0,
) -> Tuple[PointMap, Dict[int, List[int]], BoundaryNodeMap, set, set]:
    """
    Sweep a 2D quad mesh around the Y axis into 20-node hexahedral elements.

    Parameters match the legacy Python entry points so the GUI and CLI callers do
    not need to change.
    """
    if not nodes_2d:
        raise ValueError("The 2D mesh does not contain any nodes.")
    if not quad_elements:
        raise ValueError("The 2D mesh does not contain any quad elements.")
    if num_angles <= 0:
        raise ValueError("The number of sweep angles must be positive.")

    center = list(repos[:3]) if repos is not None and len(repos) >= 3 else [0.0, 0.0, 0.0]
    total_angle = float(total_angle)
    total_angle_rad = math.radians(total_angle)
    angle_step = total_angle_rad / float(num_angles)
    full_revolution = math.isclose(abs(total_angle), 360.0, abs_tol=1e-9)
    if full_revolution and num_angles < 2:
        raise ValueError("A stitched 360-degree sweep requires at least two angular divisions.")

    corner_layers = list(range(num_angles if full_revolution else num_angles + 1))

    nodes_3d: PointMap = {}
    elements_3d: Dict[int, List[int]] = {}
    boundary_nodes: BoundaryNodeMap = {}
    profile_elements: set = set()
    interface_elements: set = set()

    next_node_id = int(start_node_id)
    next_element_id = int(start_element_id)

    corner_node_ids: Dict[Tuple[int, int], int] = {}
    profile_mid_node_ids: Dict[Tuple[Tuple[int, int], int], int] = {}
    sweep_mid_node_ids: Dict[Tuple[int, int], int] = {}

    def normalize_layer_index(layer_index: int) -> int:
        if full_revolution and layer_index == num_angles:
            return 0
        return layer_index

    def get_corner_node(original_node_id: int, layer_index: int) -> int:
        normalized_layer_index = normalize_layer_index(layer_index)
        key = (original_node_id, normalized_layer_index)
        if key not in corner_node_ids:
            angle = angle_step * normalized_layer_index
            corner_node_ids[key] = next_node_id_map[0]
            nodes_3d[next_node_id_map[0]] = _rotate_about_y(nodes_2d[original_node_id], center, angle)
            next_node_id_map[0] += 1
        return corner_node_ids[key]

    def get_profile_mid_node(node_a: int, node_b: int, layer_index: int) -> int:
        normalized_layer_index = normalize_layer_index(layer_index)
        edge_key = _canonical_edge(node_a, node_b)
        key = (edge_key, normalized_layer_index)
        if key not in profile_mid_node_ids:
            midpoint = [
                0.5 * (float(nodes_2d[node_a][axis]) + float(nodes_2d[node_b][axis]))
                for axis in range(3)
            ]
            angle = angle_step * normalized_layer_index
            profile_mid_node_ids[key] = next_node_id_map[0]
            nodes_3d[next_node_id_map[0]] = _rotate_about_y(midpoint, center, angle)
            next_node_id_map[0] += 1
        return profile_mid_node_ids[key]

    def get_sweep_mid_node(original_node_id: int, segment_index: int) -> int:
        key = (original_node_id, segment_index)
        if key not in sweep_mid_node_ids:
            angle = angle_step * (segment_index + 0.5)
            sweep_mid_node_ids[key] = next_node_id_map[0]
            nodes_3d[next_node_id_map[0]] = _rotate_about_y(nodes_2d[original_node_id], center, angle)
            next_node_id_map[0] += 1
        return sweep_mid_node_ids[key]

    next_node_id_map = [next_node_id]

    for original_node_id in sorted(nodes_2d):
        boundary_nodes[original_node_id] = {
            "start": get_corner_node(original_node_id, 0),
            "end": get_corner_node(original_node_id, num_angles if not full_revolution else 0),
        }

    for edge_node_a, edge_node_b in _build_quad_edges(quad_elements):
        for layer_index in corner_layers:
            get_profile_mid_node(edge_node_a, edge_node_b, layer_index)

    for original_node_id in sorted(nodes_2d):
        for segment_index in range(num_angles):
            get_sweep_mid_node(original_node_id, segment_index)

    for quad_element_id in sorted(quad_elements):
        quad = list(quad_elements[quad_element_id])
        if len(quad) != 4:
            raise ValueError(f"Element {quad_element_id} is not a 4-node quad.")

        for segment_index in range(num_angles):
            corner_ids = [
                get_corner_node(quad[0], segment_index),
                get_corner_node(quad[1], segment_index),
                get_corner_node(quad[2], segment_index),
                get_corner_node(quad[3], segment_index),
                get_corner_node(quad[0], segment_index + 1),
                get_corner_node(quad[1], segment_index + 1),
                get_corner_node(quad[2], segment_index + 1),
                get_corner_node(quad[3], segment_index + 1),
            ]

            corner_coords = [nodes_3d[node_id] for node_id in corner_ids]
            if _signed_local_volume(corner_coords) < 0.0:
                quad = [quad[0], quad[3], quad[2], quad[1]]
                corner_ids = [
                    get_corner_node(quad[0], segment_index),
                    get_corner_node(quad[1], segment_index),
                    get_corner_node(quad[2], segment_index),
                    get_corner_node(quad[3], segment_index),
                    get_corner_node(quad[0], segment_index + 1),
                    get_corner_node(quad[1], segment_index + 1),
                    get_corner_node(quad[2], segment_index + 1),
                    get_corner_node(quad[3], segment_index + 1),
                ]

            element_node_ids = corner_ids + [
                get_profile_mid_node(quad[0], quad[1], segment_index),
                get_profile_mid_node(quad[1], quad[2], segment_index),
                get_profile_mid_node(quad[2], quad[3], segment_index),
                get_profile_mid_node(quad[3], quad[0], segment_index),
                get_profile_mid_node(quad[0], quad[1], segment_index + 1),
                get_profile_mid_node(quad[1], quad[2], segment_index + 1),
                get_profile_mid_node(quad[2], quad[3], segment_index + 1),
                get_profile_mid_node(quad[3], quad[0], segment_index + 1),
                get_sweep_mid_node(quad[0], segment_index),
                get_sweep_mid_node(quad[1], segment_index),
                get_sweep_mid_node(quad[2], segment_index),
                get_sweep_mid_node(quad[3], segment_index),
            ]

            elements_3d[next_element_id] = element_node_ids
            if segment_index == 0:
                profile_elements.add(next_element_id)
            next_element_id += 1

    return nodes_3d, elements_3d, boundary_nodes, profile_elements, interface_elements

import numpy as np
import math
import os
import sys
import json
import argparse
import base64
from concurrent.futures import ProcessPoolExecutor
from numba import cuda, jit
from collections import defaultdict
import matplotlib.pyplot as plt
from mpl_toolkits.mplot3d import Axes3D
import matplotlib.colors as mcolors
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg
from matplotlib.figure import Figure
import tkinter as tk
from tkinter import ttk, messagebox, filedialog
from sklearn.cluster import KMeans


# Add parent directory to path so we can import the other scripts
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from utilities_simulation import format_interface_surfaces, read_inp_file


_SURFACE_BUILD_CONTEXT = {}


def _init_surface_build_worker(all_nodes, part_elems_by_part, base_part_name, element_type_name, default_surface_tolerance):
    """Initialize immutable-ish context used by process workers for surface generation."""
    global _SURFACE_BUILD_CONTEXT
    _SURFACE_BUILD_CONTEXT = {
        "all_nodes": all_nodes,
        "part_elems_by_part": part_elems_by_part,
        "base_part_name": base_part_name,
        "element_type_name": element_type_name,
        "default_surface_tolerance": default_surface_tolerance,
    }


def _build_surface_results_for_cylinder(task):
    """
    Build surface face-element mappings for one cylinder.
    Returns a list of dicts that can be merged serially into model_data.
    """
    cyl_idx, cyl = task
    ctx = _SURFACE_BUILD_CONTEXT
    all_nodes = ctx["all_nodes"]
    part_elems_by_part = ctx["part_elems_by_part"]
    base_part_name = ctx["base_part_name"]
    element_type_name = ctx["element_type_name"]
    default_surface_tolerance = ctx["default_surface_tolerance"]

    actual_radius = cyl["actual_radius"]
    target_radius = cyl["target_radius"]
    surface_tolerance = cyl.get("tolerance", default_surface_tolerance)
    set_name = f"CYL_{cyl_idx + 1:02d}"

    results = []

    for part_order, (part_key, part_elems) in enumerate(part_elems_by_part.items()):
        if part_key == base_part_name:
            print(f"[INFO] Creating surfaces for base geometry with actual_radius={actual_radius}")
            face_elements_base = format_interface_surfaces(
                all_nodes, part_elems,
                actual_radius, 0.0,
                f"{set_name}_base",
                center=cyl["center"],
                direction=cyl["direction"],
                height=cyl["height"],
                chamfer=cyl["chamfer"],
                tolerance=surface_tolerance,
            )
            results.append({
                "merge_order": (cyl_idx, part_order, 0),
                "face_elements": face_elements_base,
                "set_name_prefix": f"{set_name}_base",
                "part_key": part_key,
            })
        elif "_EXTRUSION" in part_key:
            print(f"[INFO] Creating surfaces for extrusion inner with actual_radius={actual_radius}")
            face_elements_extrusion_inner = format_interface_surfaces(
                all_nodes, part_elems,
                actual_radius, 0.0,
                f"{set_name}_{element_type_name}_inner",
                center=cyl["center"],
                direction=cyl["direction"],
                height=cyl["height"],
                chamfer=cyl["chamfer"],
                tolerance=surface_tolerance,
            )
            results.append({
                "merge_order": (cyl_idx, part_order, 1),
                "face_elements": face_elements_extrusion_inner,
                "set_name_prefix": f"{set_name}_{element_type_name}_inner",
                "part_key": part_key,
            })

            print(f"[INFO] Creating surfaces for extrusion outer with target_radius={target_radius}")
            face_elements_extrusion_outer = format_interface_surfaces(
                all_nodes, part_elems,
                target_radius, 0.0,
                f"{set_name}_{element_type_name}_outer",
                center=cyl["center"],
                direction=cyl["direction"],
                height=cyl["height"],
                chamfer=cyl["chamfer"],
                tolerance=surface_tolerance,
            )
            results.append({
                "merge_order": (cyl_idx, part_order, 2),
                "face_elements": face_elements_extrusion_outer,
                "set_name_prefix": f"{set_name}_{element_type_name}_outer",
                "part_key": part_key,
            })

    return results


def find_nodes_on_cylinder(nodes, target_radius, tolerance=0.025, center=(0, 0), direction=None, height=None, chamfer=[0.0, 0.0]):
    """
    Find all nodes that lie on a cylinder with given radius, centered on an axis
    defined by direction vector and height.
    
    Parameters:
    - nodes: Dictionary of nodes {node_id: [x, y, z]}
    - target_radius: Target radius for the cylindrical surface
    - tolerance: Tolerance for radius comparison
    - center: (x, z) coordinates of the cylinder center point
    - direction: Direction vector of the cylinder axis [dx, dy, dz]
    - height: Height of the cylinder
    - chamfer: Chamfer parameters for the cylinder
    """
    # Validate chamfer parameters
    if chamfer is None or not isinstance(chamfer, list) or len(chamfer) < 2:
        chamfer = [0.0, 0.0]
        
    cylinder_nodes = {}
    
    # If no direction specified, default to Y-axis (old behavior)
    if direction is None or height is None:
        # Revert to original Y-parallel behavior
        x_center, z_center = center
        
        for node_id, coords in nodes.items():
            x, y, z = coords
            
            # Distance from the Y-parallel axis
            distance = math.sqrt((x - x_center)**2 + (z - z_center)**2)
            
            if abs(distance - target_radius) < tolerance:
                cylinder_nodes[node_id] = coords
    else:
        # Handle angled cylinder
        # Normalize direction vector
        dir_length = math.sqrt(sum(d*d for d in direction))
        if dir_length < 1e-10:  # Avoid division by zero
            direction = [0, 1, 0]  # Default to Y-axis if direction is too small
        else:
            direction = [d/dir_length for d in direction]
        
        # Calculate top and bottom points of cylinder axis
        # Normalize direction vector
        dir_length = math.sqrt(sum(d*d for d in direction))
        if dir_length < 1e-10:  # Avoid division by zero
            direction = [0, 1, 0]  # Default to Y-axis if direction is too small
        else:
            direction = [d/dir_length for d in direction]
        
        # Define cylinder endpoints based on center, direction and height
        # We define bottom_point at position b=0 and top_point at position b=1
        bottom_point = [
            center[0] + direction[0]*(chamfer[0]*1.05+height/2),  # Bottom = center + direction*height/2
            center[1] + direction[1]*(chamfer[0]*1.05+height/2) if len(center) > 2 else direction[1]*height/2,
            center[2] + direction[2]*(chamfer[0]*1.05+height/2) if len(center) > 2 else direction[2]*height/2
        ]
        
        top_point = [
            center[0] - direction[0]*height/1.98,  # Top = center - direction*height/2
            center[1] - direction[1]*height/1.98 if len(center) > 2 else -direction[1]*height/2,
            center[2] - direction[2]*height/1.98 if len(center) > 2 else -direction[2]*height/2
        ]
        
        print(f"[DEBUG] Find nodes - Bottom point (b=0): {bottom_point}")
        print(f"[DEBUG] Find nodes - Top point (b=1): {top_point}")
        print(f"[DEBUG] Find nodes - Axis length: {math.sqrt(sum((top_point[i]-bottom_point[i])**2 for i in range(3)))}")
        
        # For each node, check distance to cylinder axis
        for node_id, coords in nodes.items():
            # Calculate distance from point to cylinder axis
            # Using formula for distance from point to line
            p0 = coords  # Node coordinates
            p1 = bottom_point  # First point on axis
            p2 = top_point  # Second point on axis
            
            # Vector from p1 to p2 (axis direction)
            v = [p2[0]-p1[0], p2[1]-p1[1], p2[2]-p1[2]]
            # Vector from p1 to p0 (node)
            w = [p0[0]-p1[0], p0[1]-p1[1], p0[2]-p1[2]]
            
            # Calculate dot products
            c1 = sum(w[i]*v[i] for i in range(3))  # dot product of w and v
            c2 = sum(v[i]*v[i] for i in range(3))  # dot product of v and v
            
            # Parameter for closest point on line
            b = c1 / c2 if c2 > 0 else 0
            
            # Calculate closest point on axis
            pb = [p1[0] + b*v[0], p1[1] + b*v[1], p1[2] + b*v[2]]
            
            # Check if point is within cylinder height bounds
            if 0 <= b <= 1:
                # Calculate distance from node to closest point on axis
                distance = math.sqrt(sum((p0[i] - pb[i])**2 for i in range(3)))
                
                # Check if distance matches target radius
                if abs(distance - target_radius) < tolerance:
                    cylinder_nodes[node_id] = coords
    
    print(f"[DEBUG] Found {len(cylinder_nodes)} nodes on cylindrical surface")
    print(f"[DEBUG] Target radius: {target_radius}, Center: {center}")
    print(f"[DEBUG] Direction: {direction}, Height: {height}, Chamfer: {chamfer}")
    
    return cylinder_nodes

def identify_element_faces_on_cylinder(elements, cylinder_nodes, nodes):
    """Identify element faces that lie on the cylindrical surface"""
    # Element type to face node mapping
    face_definitions = {
        'C3D10': {  # Tetrahedral element with 10 nodes (4 vertices + 6 mid-edge nodes)
            'F1': {'vertices': [0, 1, 2], 'midnodes': [4, 5, 6]},  # Face 1: nodes 1-2-3 and mid-edge nodes
            'F2': {'vertices': [0, 3, 1], 'midnodes': [7, 8, 4]},  # Face 2: nodes 1-4-2 and mid-edge nodes
            'F3': {'vertices': [1, 3, 2], 'midnodes': [8, 9, 5]},  # Face 3: nodes 2-4-3 and mid-edge nodes
            'F4': {'vertices': [0, 2, 3], 'midnodes': [6, 9, 7]}   # Face 4: nodes 1-3-4 and mid-edge nodes
        },
        'C3D20': {  # Hexahedral element with 20 nodes
            'F1': {'vertices': [0, 1, 2, 3], 'midnodes': [8, 9, 10, 11]},  # Bottom face
            'F2': {'vertices': [4, 5, 6, 7], 'midnodes': [12, 13, 14, 15]},  # Top face
            'F3': {'vertices': [0, 1, 4, 5], 'midnodes': [8, 12, 16, 17]},  # Front face
            'F4': {'vertices': [1, 2, 5, 6], 'midnodes': [9, 13, 17, 18]},  # Right face
            'F5': {'vertices': [2, 3, 6, 7], 'midnodes': [10, 14, 18, 19]},  # Back face
            'F6': {'vertices': [3, 0, 7, 4], 'midnodes': [11, 15, 19, 16]}   # Left face
        },
        'C3D20R': {  # Hexahedral element with 20 nodes
            'F1': {'vertices': [0, 1, 2, 3], 'midnodes': [8, 9, 10, 11]},  # Bottom face
            'F2': {'vertices': [4, 5, 6, 7], 'midnodes': [12, 13, 14, 15]},  # Top face
            'F3': {'vertices': [0, 1, 4, 5], 'midnodes': [8, 12, 16, 17]},  # Front face
            'F4': {'vertices': [1, 2, 5, 6], 'midnodes': [9, 13, 17, 18]},  # Right face
            'F5': {'vertices': [2, 3, 6, 7], 'midnodes': [10, 14, 18, 19]},  # Back face
            'F6': {'vertices': [3, 0, 7, 4], 'midnodes': [11, 15, 19, 16]}   # Left face
        }
    }
    
    # Set of node IDs on the cylinder
    cylinder_node_ids = set(cylinder_nodes.keys())
    
    # Dictionary to store faces on the cylinder
    cylinder_faces = []
    
    print(f"[DEBUG] Identifying element faces on cylinder...")
    
    # For each element
    for elem_id, elem_data in elements.items():
        element_type = elem_data['type']
        node_list = elem_data['nodes']
        
        # Skip elements with unknown types
        if element_type not in face_definitions:
            continue
        
        # Check each face of the element
        for face_name, face_def in face_definitions[element_type].items():
            # Get vertex nodes for this face
            vertex_indices = face_def['vertices']
            vertex_nodes = [node_list[idx] for idx in vertex_indices]
            
            # Get mid-edge nodes for this face
            midnode_indices = face_def['midnodes']
            midnodes = [node_list[idx] for idx in midnode_indices]
            
            # Count how many vertices and midnodes are on the cylinder
            vertices_on_cylinder = sum(1 for node in vertex_nodes if node in cylinder_node_ids)
            midnodes_on_cylinder = sum(1 for node in midnodes if node in cylinder_node_ids)
            
            # If all vertices and midnodes are on the cylinder, it's a face on the cylinder
            total_nodes = len(vertex_indices) + len(midnode_indices)
            nodes_on_cylinder = vertices_on_cylinder + midnodes_on_cylinder
            
            # Consider it a face on the cylinder if most nodes are on the cylinder
            if nodes_on_cylinder >= 0.75 * total_nodes:
                # Debug: Check if this face contains node 894680
                debug_node_id = 894680
                all_face_nodes = vertex_nodes + midnodes
                if debug_node_id in all_face_nodes:
                    print(f"[DEBUG] Face with node {debug_node_id} being added: Element {elem_id} ({element_type}) face {face_name}")
                    print(f"[DEBUG]   Vertex nodes: {vertex_nodes}")
                    print(f"[DEBUG]   Midnodes: {midnodes}")
                    print(f"[DEBUG]   {nodes_on_cylinder}/{total_nodes} nodes on cylinder (threshold: {0.75 * total_nodes})")
                
                # Add face information
                cylinder_faces.append({
                    'element_id': elem_id,
                    'element_type': element_type,
                    'face_name': face_name,
                    'vertex_nodes': vertex_nodes,
                    'midnodes': midnodes,
                    'vertices_on_cylinder': vertices_on_cylinder,
                    'midnodes_on_cylinder': midnodes_on_cylinder
                })
    
    print(f"[DEBUG] Found {len(cylinder_faces)} faces on the cylindrical surface")
    return cylinder_faces

def create_wedge_elements_from_faces(cylinder_faces, nodes, elements, target_radius, extrusion_distance, direction, height, num_layers=1, center=(0, 0), chamfer=[0.0, 0.0], create_new_part=True):
    """
    Create wedge elements by extruding triangular faces on the cylinder
    
    Parameters:
    - cylinder_faces: List of faces on the cylindrical surface
    - nodes: Dictionary of nodes
    - elements: Dictionary of elements
    - target_radius: Target radius for the cylindrical surface
    - extrusion_distance: Distance to extrude
    - direction: Direction vector of the cylinder axis [dx, dy, dz]
    - height: Height of the cylinder
    - num_layers: Number of layers to create in the radial direction
    - center: Coordinates of the cylinder center
    - chamfer: Chamfer value for the cylindrical surface
    - create_new_part: Whether to create new nodes (True) or reuse existing ones (False)
    """
    # Dictionary to store new nodes
    new_nodes = {}
    next_node_id = max(nodes.keys()) + 1
    
    # Dictionary to map original node to extruded node for each layer
    node_to_extruded = {}
    
    # Dictionary to map original node to its duplicate
    node_to_duplicate = {}
    
    # List to store new wedge elements
    wedge_elements = []
    next_element_id = max(elements.keys()) + 1 if elements else 1
    
    # Dictionary to track midpoint nodes (to avoid duplicates)
    midpoint_nodes = {}
    
    # Extract center coordinates
    if len(center) == 2:
        x_center, z_center = center
        y_center = 0  # Default if not provided
    else:
        x_center, y_center, z_center = center
    
    print(f"[DEBUG] Creating wedge elements from faces...")
    print(f"[DEBUG] Extrusion center: {center}")
    print(f"[DEBUG] Direction: {direction}")
    print(f"[DEBUG] Height: {height}")
    print(f"[DEBUG] Creating {num_layers} layers of elements")
    print(f"[DEBUG] Create new part: {create_new_part}")
    
    # First pass: create extruded nodes for all nodes on cylinder for each layer
    nodes_on_cylinder = set()
    for face_info in cylinder_faces:
        # Only process triangular faces for wedge elements
        if len(face_info['vertex_nodes']) != 3:
            continue
        # Collect all nodes from this face
        vertex_nodes = face_info['vertex_nodes']
        midnodes = face_info['midnodes']
        all_face_nodes = vertex_nodes + midnodes
        nodes_on_cylinder.update(all_face_nodes)
    
    print(f"[DEBUG] Total unique nodes on cylinder: {len(nodes_on_cylinder)}")
    
    # For each layer, calculate distance increment
    layer_distances = []
    for i in range(num_layers):
        layer_fraction = (i + 1) / num_layers
        layer_distance = extrusion_distance * layer_fraction
        layer_distances.append(layer_distance)
    
    print(f"[DEBUG] Layer distances: {layer_distances}")
    
    # Normalize direction vector if provided
    if direction:
        dir_length = math.sqrt(sum(d*d for d in direction))
        if dir_length < 1e-10:  # Avoid division by zero
            direction = [0, 1, 0]  # Default to Y-axis if direction is too small
        else:
            direction = [d/dir_length for d in direction]
        
        # Calculate top and bottom points of cylinder axis
        # For angled cylinder calculations
        # The cylinder is defined by:
        # - Center point (center of gravity)
        # - Direction vector (normalized)
        # - Height (total length)
        # - Chamfer parameters
        
        # Important: We need to define the axis in a way that:
        # 1. Bottom point (where chamfer is) is where b=0
        # 2. Top point is where b=1
        # This ensures the parameter 'b' correctly represents position along axis
        
        # Define cylinder endpoints based on center, direction and height
        print(f"[DEBUG] Center: {center}, Direction: {direction}, Height: {height}, Chamfer: {chamfer}")
        
        # Calculate actual endpoints for proper b values (0 at bottom, 1 at top)
        bottom_point = [
            center[0] + direction[0]*(chamfer[0]*1.05+height/2),  # Bottom = center + direction*height/2
            center[1] + direction[1]*(chamfer[0]*1.05+height/2) if len(center) > 2 else direction[1]*height/2,
            center[2] + direction[2]*(chamfer[0]*1.05+height/2) if len(center) > 2 else direction[2]*height/2
        ]
        
        top_point = [
            center[0] - direction[0]*height/1.98,  # Top = center - direction*height/2
            center[1] - direction[1]*height/1.98 if len(center) > 2 else -direction[1]*height/2,
            center[2] - direction[2]*height/1.98 if len(center) > 2 else -direction[2]*height/2
        ]
        
        print(f"[DEBUG] Bottom point (b=0): {bottom_point}")
        print(f"[DEBUG] Top point (b=1): {top_point}")
        print(f"[DEBUG] Axis length: {math.sqrt(sum((top_point[i]-bottom_point[i])**2 for i in range(3)))}")
    
    # Now create extruded nodes for all nodes on cylinder for each layer
    for node_id in nodes_on_cylinder:
        if node_id not in node_to_extruded:
            node_to_extruded[node_id] = {}
            
            # Get original node coordinates
            if node_id not in nodes:
                print(f"[WARNING] Node {node_id} not found in nodes dictionary")
                continue
            
            x, y, z = nodes[node_id]
            
            # Always create duplicate of original node for consistent procedure
            duplicate_node_id = next_node_id
            next_node_id += 1
            new_nodes[duplicate_node_id] = [x, y, z]  # Exact copy of original
            node_to_duplicate[node_id] = duplicate_node_id
            
            # For each layer, create extruded nodes
            for layer_idx, layer_distance in enumerate(layer_distances):
                # Different extrusion logic based on whether we have direction/height or not
                if direction and height:
                    # Handle angled cylinder extrusion
                    # 1. Find closest point on cylinder axis
                    p0 = [x, y, z]  # Node coordinates
                    p1 = bottom_point  # First point on axis
                    p2 = top_point  # Second point on axis
                    
                    # Vector from p1 to p2 (axis direction)
                    v = [p2[0]-p1[0], p2[1]-p1[1], p2[2]-p1[2]]
                    # Vector from p1 to p0 (node)
                    w = [p0[0]-p1[0], p0[1]-p1[1], p0[2]-p1[2]]
                    
                    # Calculate dot products
                    c1 = sum(w[i]*v[i] for i in range(3))  # dot product of w and v
                    c2 = sum(v[i]*v[i] for i in range(3))  # dot product of v and v
                    
                    # Parameter for closest point on line
                    b = c1 / c2 if c2 > 0 else 0
                    
                    # Calculate closest point on axis
                    pb = [p1[0] + b*v[0], p1[1] + b*v[1], p1[2] + b*v[2]]
                    
                    # 2. Calculate vector from axis to node
                    radial_vector = [p0[0]-pb[0], p0[1]-pb[1], p0[2]-pb[2]]
                    
                    # 3. Calculate distance from node to axis
                    current_radius = math.sqrt(sum(r*r for r in radial_vector))
                    
                    # 4. Normalize radial vector
                    if current_radius > 0:
                        radial_vector = [r/current_radius for r in radial_vector]
                    else:
                        # If node is on axis, use arbitrary perpendicular direction
                        if abs(direction[0]) < abs(direction[1]) and abs(direction[0]) < abs(direction[2]):
                            # x is smallest, use vector in x direction
                            radial_vector = [1, 0, 0]
                        elif abs(direction[1]) < abs(direction[2]):
                            # y is smallest, use vector in y direction
                            radial_vector = [0, 1, 0]
                        else:
                            # z is smallest, use vector in z direction
                            radial_vector = [0, 0, 1]
                    
                    # 5. Apply chamfer if needed
                    # Apply chamfer based on position along cylinder axis
                    if chamfer[0] > 0 and height:
                        # Calculate position along cylinder axis (0 at bottom, 1 at top)
                        # The relative position along the cylinder axis, normalized between 0 and 1
                        axis_position = b
                        
                        # Define the chamfer region size relative to the cylinder height
                        chamfer_region_size = chamfer[0]/height if height > 0 else 0.0
                        
                        # For debugging - print the range of b values we're seeing
                        if node_id % 100 == 0:  # Only print for some nodes to avoid excessive output
                            print(f"[DEBUG] Node {node_id}: axis_position: {axis_position}, chamfer_region_size: {chamfer_region_size}")
                        
                        # If node is in the chamfer region (near bottom of cylinder, where b is close to 0)
                        # Note: We use a small tolerance to ensure we catch nodes that should be in the chamfer region
                        if axis_position <= chamfer_region_size + 0.01:
                            # Calculate chamfer factor (1 at bottom, 0 at end of chamfer region)
                            # Ensure we don't divide by zero if chamfer_region_size is very small
                            if chamfer_region_size > 1e-6:
                                chamfer_factor = 1.0 - (axis_position / chamfer_region_size)
                            else:
                                chamfer_factor = 1.0 if axis_position < 0.01 else 0.0
                                
                            # Clamp the factor between 0 and 1
                            chamfer_factor = max(0.0, min(1.0, chamfer_factor))
                            
                            # Adjust layer_distance based on chamfer factor
                            virtual_total_distance = extrusion_distance + (chamfer[1] * chamfer_factor)
                            virtual_layer_distance = virtual_total_distance*(layer_idx+1) / num_layers
                            new_radius = current_radius + virtual_layer_distance
                            
                            if node_id % 100 == 0:  # Only print for some nodes
                                print(f"[DEBUG] Applying chamfer - b: {axis_position}, factor: {chamfer_factor}, new_radius: {new_radius}")
                        else:
                            # No chamfer adjustment needed
                            new_radius = current_radius + layer_distance
                    else:
                        # No chamfer adjustment needed
                        new_radius = current_radius + layer_distance
                    
                    # 6. Calculate new position
                    new_x = pb[0] + radial_vector[0] * new_radius
                    new_y = pb[1] + radial_vector[1] * new_radius
                    new_z = pb[2] + radial_vector[2] * new_radius
                
                # Create new node for extruded position
                new_node_id = next_node_id
                next_node_id += 1
                new_nodes[new_node_id] = [new_x, new_y, new_z]
                
                # Map original node to extruded node for this layer
                node_to_extruded[node_id][layer_idx] = new_node_id
    
    print(f"[DEBUG] Created {len(node_to_duplicate)} duplicate nodes")
    print(f"[DEBUG] Created {sum(len(layers) for layers in node_to_extruded.values())} extruded nodes")
    
    # Second pass: create edge midpoint nodes for each layer
    for layer_idx in range(num_layers):
        edges_requiring_midpoints = set()
        
        # Identify all edges that need midpoints for this layer
        print(f"[DEBUG] Identifying edges requiring midpoints for layer {layer_idx+1}...")
        for face_info in cylinder_faces:
            # Only process triangular faces for wedge elements
            if len(face_info['vertex_nodes']) != 3:
                continue
            vertex_nodes = face_info['vertex_nodes']
            
            # For each vertex node, we need a midpoint to its extruded counterpart
            for node_id in vertex_nodes:
                if node_id in node_to_extruded and layer_idx in node_to_extruded[node_id]:
                    # For first layer, connect duplicate to first layer extruded
                    if layer_idx == 0:
                        duplicate_id = node_to_duplicate[node_id]
                        extruded_id = node_to_extruded[node_id][layer_idx]
                        edge = tuple(sorted([duplicate_id, extruded_id]))
                        edges_requiring_midpoints.add(edge)
                    # For subsequent layers, connect previous layer to current layer
                    else:
                        prev_extruded_id = node_to_extruded[node_id][layer_idx-1]
                        curr_extruded_id = node_to_extruded[node_id][layer_idx]
                        edge = tuple(sorted([prev_extruded_id, curr_extruded_id]))
                        edges_requiring_midpoints.add(edge)
        
        print(f"[DEBUG] Found {len(edges_requiring_midpoints)} edges requiring midpoints for layer {layer_idx+1}")
        
        # Create midpoint nodes for all identified edges in this layer
        for edge in edges_requiring_midpoints:
            node1, node2 = edge
            
            # Skip if we already have a midpoint for this edge
            if edge in midpoint_nodes:
                continue
            
            # Get coordinates of the two nodes - note all nodes should be in new_nodes now
            # as we're using duplicates of original nodes
            if node1 not in new_nodes and node1 not in nodes:
                print(f"[WARNING] Node {node1} not found in nodes dictionaries")
                continue
            if node2 not in new_nodes and node2 not in nodes:
                print(f"[WARNING] Node {node2} not found in nodes dictionaries")
                continue
            
            coords1 = new_nodes.get(node1, nodes.get(node1, [0, 0, 0]))
            coords2 = new_nodes.get(node2, nodes.get(node2, [0, 0, 0]))
            
            # Calculate midpoint
            mid_x = (coords1[0] + coords2[0]) / 2
            mid_y = (coords1[1] + coords2[1]) / 2
            mid_z = (coords1[2] + coords2[2]) / 2
            
            # Create new node
            new_node_id = next_node_id
            next_node_id += 1
            new_nodes[new_node_id] = [mid_x, mid_y, mid_z]
            
            # Store in midpoint cache
            midpoint_nodes[edge] = new_node_id
    
    print(f"[DEBUG] Created {len(midpoint_nodes)} midpoint nodes across all layers")
    
    # Third pass: create wedge elements for each layer
    print(f"[DEBUG] Creating wedge elements for all layers...")
    wedge_count = 0
    
    for face_info in cylinder_faces:
        elem_id = face_info['element_id']
        element_type = face_info['element_type']
        face_name = face_info['face_name']
        vertex_nodes = face_info['vertex_nodes']
        midnodes = face_info['midnodes']
        
        # Process triangular faces
        if len(vertex_nodes) == 3:  # It's a triangular face
            # Get the three vertices of the triangular face
            n1, n2, n3 = vertex_nodes
            
            # Create elements for each layer
            for layer_idx in range(num_layers):
                # Skip if any node doesn't have an extruded counterpart for this layer
                if (n1 not in node_to_extruded or layer_idx not in node_to_extruded[n1] or
                    n2 not in node_to_extruded or layer_idx not in node_to_extruded[n2] or
                    n3 not in node_to_extruded or layer_idx not in node_to_extruded[n3]):
                    print(f"[WARNING] Missing extruded node for face of element {elem_id}, layer {layer_idx+1}")
                    continue
                
                # For first layer
                if layer_idx == 0:
                    # Connect duplicate nodes to first layer extruded nodes
                    d1 = node_to_duplicate[n1]
                    d2 = node_to_duplicate[n2]
                    d3 = node_to_duplicate[n3]
                    
                    # Get first layer extruded nodes
                    n4 = node_to_extruded[n1][layer_idx]
                    n5 = node_to_extruded[n2][layer_idx]
                    n6 = node_to_extruded[n3][layer_idx]
                else:
                    # Connect previous layer to current layer
                    d1 = node_to_extruded[n1][layer_idx-1]
                    d2 = node_to_extruded[n2][layer_idx-1]
                    d3 = node_to_extruded[n3][layer_idx-1]
                    
                    # Get current layer extruded nodes
                    n4 = node_to_extruded[n1][layer_idx]
                    n5 = node_to_extruded[n2][layer_idx]
                    n6 = node_to_extruded[n3][layer_idx]
                
                # Get the three mid-edge nodes of the original face (if available)
                if len(midnodes) >= 3:
                    m1, m2, m3 = midnodes[:3]
                    
                    # Skip if any midnode doesn't have an extruded counterpart for this layer
                    if (m1 not in node_to_extruded or layer_idx not in node_to_extruded[m1] or
                        m2 not in node_to_extruded or layer_idx not in node_to_extruded[m2] or
                        m3 not in node_to_extruded or layer_idx not in node_to_extruded[m3]):
                        print(f"[WARNING] Missing extruded midnode for element {elem_id}, layer {layer_idx+1}")
                        continue
                    
                    # Get mid-edge nodes based on layer
                    if layer_idx == 0:
                        # For first layer, use duplicates of original midnodes
                        dm1 = node_to_duplicate[m1]
                        dm2 = node_to_duplicate[m2]
                        dm3 = node_to_duplicate[m3]
                    else:
                        # For subsequent layers, use previous layer midnodes
                        dm1 = node_to_extruded[m1][layer_idx-1]
                        dm2 = node_to_extruded[m2][layer_idx-1]
                        dm3 = node_to_extruded[m3][layer_idx-1]
                    
                    # Get extruded mid-edge nodes for current layer
                    m4 = node_to_extruded[m1][layer_idx]
                    m5 = node_to_extruded[m2][layer_idx]
                    m6 = node_to_extruded[m3][layer_idx]
                    
                    # Get or create mid-edge nodes between layer connections
                    edge1 = tuple(sorted([d1, n4]))
                    edge2 = tuple(sorted([d2, n5]))
                    edge3 = tuple(sorted([d3, n6]))
                    
                    # Ensure we have midpoints for these edges
                    if edge1 not in midpoint_nodes:
                        print(f"[WARNING] Missing midpoint for edge {edge1}")
                        continue
                    if edge2 not in midpoint_nodes:
                        print(f"[WARNING] Missing midpoint for edge {edge2}")
                        continue
                    if edge3 not in midpoint_nodes:
                        print(f"[WARNING] Missing midpoint for edge {edge3}")
                        continue
                    
                    e1 = midpoint_nodes[edge1]
                    e2 = midpoint_nodes[edge2]
                    e3 = midpoint_nodes[edge3]
                    
                    # Create the wedge element (C3D15)
                    wedge_element = [
                        next_element_id,  # Element ID
                        n4, n5, n6,       # Current layer triangle vertices (outer)
                        d1, d2, d3,       # Previous layer or duplicate triangle vertices (inner)
                        m4, m5, m6,       # Current layer triangle mid-edge nodes (outer)
                        dm1, dm2, dm3,    # Previous layer or duplicate triangle mid-edge nodes (inner)
                        e1, e2, e3        # Mid-edge nodes connecting inner and outer vertices
                    ]
                    next_element_id += 1
                    
                    wedge_elements.append(wedge_element)
                    wedge_count += 1
                else:
                    print(f"[WARNING] Insufficient midnodes for face of element {elem_id}")
    
    print(f"[DEBUG] Created {wedge_count} wedge elements across {num_layers} layers")
    print(f"[DEBUG] Created {len(new_nodes)} new nodes total")
    
    # Post-processing: If adding to existing part, replace duplicate nodes with originals
    if not create_new_part:
        print(f"[INFO] Post-processing: Finding and replacing duplicate nodes based on position")
        
        # Find duplicates by comparing node positions
        duplicate_to_original = {}
        nodes_to_remove = set()
        position_tolerance = 1e-9  # Very tight tolerance for exact position matching
        
        # For each new node, check if it matches an existing node position
        for new_node_id, new_coords in new_nodes.items():
            new_x, new_y, new_z = new_coords
            
            # Search through original nodes for matching position
            for orig_node_id, orig_coords in nodes.items():
                orig_x, orig_y, orig_z = orig_coords
                
                # Check if positions match within tolerance
                distance = ((new_x - orig_x)**2 + (new_y - orig_y)**2 + (new_z - orig_z)**2)**0.5
                if distance <= position_tolerance:
                    duplicate_to_original[new_node_id] = orig_node_id
                    nodes_to_remove.add(new_node_id)
                    print(f"[DEBUG] Found duplicate: new node {new_node_id} {new_coords} matches original {orig_node_id} {orig_coords} (distance={distance:.2e})")
                    break
        
        print(f"[INFO] Found {len(duplicate_to_original)} duplicate nodes based on position")
        
        # Update all wedge elements to use original nodes instead of duplicates
        # This ensures elements at cylinder interface use original surface nodes
        for wedge_element in wedge_elements:
            for i in range(1, len(wedge_element)):  # Skip element ID at index 0
                node_id = wedge_element[i]
                if node_id in duplicate_to_original:
                    original_node = duplicate_to_original[node_id]
                    wedge_element[i] = original_node
                    print(f"[DEBUG] Element {wedge_element[0]}: Replaced duplicate node {node_id} with original {original_node}")
        
        # Remove duplicate nodes from new_nodes (keep only extruded nodes)
        for node_id in nodes_to_remove:
            if node_id in new_nodes:
                del new_nodes[node_id]
        
        print(f"[INFO] Removed {len(nodes_to_remove)} duplicate nodes, kept {len(new_nodes)} extruded nodes")
    
    return new_nodes, wedge_elements

def validate_c3d20r_midedges(hex_element, original_nodes, added_nodes, abs_tol=1e-6, rel_tol=1e-3):
    """
    Validate that C3D20R mid-edge nodes (positions 9..20) are at the midpoints of the correct
    corner node pairs based on the standard C3D20R node ordering.

    hex_element: list of length 21 [elem_id, n1, n2, ..., n20]
    original_nodes: dict[int, [x,y,z]] for pre-existing nodes
    added_nodes: dict[int, [x,y,z]] for newly created nodes in this routine

    Returns: list of mismatches, each as a dict with keys:
      - mid_pos, end_pos_a, end_pos_b, mid_id, end_id_a, end_id_b, delta, tol
    """
    def get_coords(node_id):
        if node_id in added_nodes:
            return added_nodes[node_id]
        return original_nodes.get(node_id)

    # Map mid-edge positions to their corresponding end-node positions (1-based indices)
    checks = [
        (9, 1, 2), (10, 2, 3), (11, 3, 4), (12, 4, 1),
        (13, 5, 6), (14, 6, 7), (15, 7, 8), (16, 8, 5),
        (17, 1, 5), (18, 2, 6), (19, 3, 7), (20, 4, 8),
    ]

    mismatches = []
    # hex_element[0] is the element id; positions start at index 1
    for mid_pos, end_pos_a, end_pos_b in checks:
        mid_id = hex_element[mid_pos]
        end_id_a = hex_element[end_pos_a]
        end_id_b = hex_element[end_pos_b]

        p_mid = get_coords(mid_id)
        p_a = get_coords(end_id_a)
        p_b = get_coords(end_id_b)

        # Skip if any coordinate is missing
        if p_mid is None or p_a is None or p_b is None:
            continue

        # Compute midpoint and deviation
        midpoint = [0.5 * (p_a[i] + p_b[i]) for i in range(3)]
        delta_vec = [p_mid[i] - midpoint[i] for i in range(3)]
        delta = math.sqrt(sum(d*d for d in delta_vec))
        edge_len = math.sqrt(sum((p_a[i] - p_b[i])**2 for i in range(3)))
        tol = max(abs_tol, rel_tol * edge_len)

        if delta > tol:
            mismatches.append({
                'mid_pos': mid_pos,
                'end_pos_a': end_pos_a,
                'end_pos_b': end_pos_b,
                'mid_id': mid_id,
                'end_id_a': end_id_a,
                'end_id_b': end_id_b,
                'delta': delta,
                'tol': tol,
            })

    return mismatches

def auto_correct_c3d20r_midedges(hex_element, original_nodes, added_nodes, abs_tol=1e-6, rel_tol=1e-3):
    """
    Attempt to reorder the 12 mid-edge nodes (positions 9..20) so that each lies at the
    midpoint of the expected corner-node pair according to C3D20R ordering.

    Returns: (new_hex_element, changed, stats_dict)
    """
    def get_coords(node_id):
        if node_id in added_nodes:
            return added_nodes[node_id]
        return original_nodes.get(node_id)

    # Expected mapping: position -> (corner_pos_a, corner_pos_b)
    edge_specs = [
        (9, 1, 2), (10, 2, 3), (11, 3, 4), (12, 4, 1),
        (13, 5, 6), (14, 6, 7), (15, 7, 8), (16, 8, 5),
        (17, 1, 5), (18, 2, 6), (19, 3, 7), (20, 4, 8),
    ]

    # Compute target midpoints and tolerances per expected edge
    targets = {}
    for pos, a_pos, b_pos in edge_specs:
        a_id = hex_element[a_pos]
        b_id = hex_element[b_pos]
        p_a = get_coords(a_id)
        p_b = get_coords(b_id)
        if p_a is None or p_b is None:
            continue
        midpoint = [0.5 * (p_a[i] + p_b[i]) for i in range(3)]
        edge_len = math.sqrt(sum((p_a[i] - p_b[i])**2 for i in range(3)))
        tol = max(abs_tol, rel_tol * edge_len)
        targets[pos] = (midpoint, tol)

    # Candidates: current mid-edge nodes at positions 9..20
    candidates = []  # list of (pos, node_id, coords)
    for pos in range(9, 21):
        node_id = hex_element[pos]
        coords = get_coords(node_id)
        if coords is None:
            continue
        candidates.append((pos, node_id, coords))

    # Build all pairwise costs (distance to target midpoint)
    cost_list = []  # (distance, target_pos, candidate_index)
    for t_pos, (midpoint, _tol) in targets.items():
        for ci, (_cpos, _cid, ccoords) in enumerate(candidates):
            dvec = [ccoords[i] - midpoint[i] for i in range(3)]
            dist = math.sqrt(sum(d*d for d in dvec))
            cost_list.append((dist, t_pos, ci))

    # Greedy matching by ascending distance
    cost_list.sort(key=lambda x: x[0])
    assigned_targets = {}
    used_candidates = set()
    total_dist = 0.0
    for dist, t_pos, ci in cost_list:
        if t_pos in assigned_targets or ci in used_candidates:
            continue
        assigned_targets[t_pos] = ci
        used_candidates.add(ci)
        total_dist += dist
        if len(assigned_targets) == min(len(targets), len(candidates)):
            break

    # Build corrected element
    new_hex = list(hex_element)
    changed = False
    max_delta = 0.0
    for pos in range(9, 21):
        if pos not in targets:
            continue
        if pos not in assigned_targets:
            continue
        ci = assigned_targets[pos]
        _cpos, cid, ccoords = candidates[ci]
        # compute delta vs target for stats
        midpoint, tol = targets[pos]
        dvec = [ccoords[i] - midpoint[i] for i in range(3)]
        dist = math.sqrt(sum(d*d for d in dvec))
        max_delta = max(max_delta, dist)
        if new_hex[pos] != cid:
            changed = True
            new_hex[pos] = cid

    stats = {
        'changed': changed,
        'assigned': len(assigned_targets),
        'candidates': len(candidates),
        'targets': len(targets),
        'max_delta': max_delta,
        'avg_delta': (total_dist / max(1, len(assigned_targets))) if assigned_targets else 0.0,
        'unassigned': max(0, len(targets) - len(assigned_targets)),
    }

    return new_hex, changed, stats

def create_hex_elements_from_faces(cylinder_faces, nodes, elements, target_radius, extrusion_distance, direction, height, num_layers=1, center=(0, 0), chamfer=[0.0, 0.0], create_new_part=True):
    """
    Create hexahedral elements by extruding quadrilateral faces on the cylinder
    
    Parameters:
    - cylinder_faces: List of faces on the cylindrical surface
    - nodes: Dictionary of nodes
    - elements: Dictionary of elements
    - target_radius: Target radius for the cylindrical surface
    - extrusion_distance: Distance to extrude
    - direction: Direction vector of the cylinder axis [dx, dy, dz]
    - height: Height of the cylinder
    - num_layers: Number of layers to create in the radial direction
    - center: Coordinates of the cylinder center
    - chamfer: Chamfer value for the cylindrical surface
    - create_new_part: Whether to create new nodes (True) or reuse existing ones (False)
    """
    # Dictionary to store new nodes
    new_nodes = {}
    next_node_id = max(nodes.keys()) + 1 if nodes else 1
    
    # Dictionary to map original node to extruded node for each layer
    node_to_extruded = {}
    
    # Dictionary to map original node to its duplicate
    node_to_duplicate = {}
    
    # List to store new hexahedral elements
    hex_elements = []
    next_element_id = max(elements.keys()) + 1 if elements else 1
    
    # Dictionary to track midpoint nodes (to avoid duplicates)
    midpoint_nodes = {}
    
    # Extract center coordinates
    if len(center) == 2:
        x_center, z_center = center
        y_center = 0  # Default if not provided
    else:
        x_center, y_center, z_center = center
    
    print(f"[DEBUG] Creating hexahedral elements from faces...")
    print(f"[DEBUG] Extrusion center: {center}")
    print(f"[DEBUG] Direction: {direction}")
    print(f"[DEBUG] Height: {height}")
    print(f"[DEBUG] Creating {num_layers} layers of elements")
    print(f"[DEBUG] Create new part: {create_new_part}")
    
    # First pass: create extruded nodes for all nodes on cylinder for each layer
    nodes_on_cylinder = set()
    debug_node_id = 894680
    debug_faces_adding_node = []
    
    for face_info in cylinder_faces:
        # Only process quadrilateral faces for hex elements
        if len(face_info['vertex_nodes']) != 4:
            continue
        # Collect all nodes from this face
        vertex_nodes = face_info['vertex_nodes']
        midnodes = face_info['midnodes']
        all_face_nodes = vertex_nodes + midnodes
        nodes_on_cylinder.update(all_face_nodes)
        
        # Debug: Track which face adds the debug node
        if debug_node_id in all_face_nodes:
            debug_faces_adding_node.append(face_info)
    
    print(f"[DEBUG] Total unique nodes on cylinder: {len(nodes_on_cylinder)}")
    
    # Debug: Report if debug node gets added to nodes_on_cylinder
    if debug_node_id in nodes_on_cylinder:
        print(f"[DEBUG] Node {debug_node_id} WAS added to nodes_on_cylinder via {len(debug_faces_adding_node)} faces:")
        for face_info in debug_faces_adding_node:
            print(f"[DEBUG]   Face from element {face_info['element_id']} ({face_info['element_type']}) face {face_info['face_name']}")
    else:
        print(f"[DEBUG] Node {debug_node_id} was NOT added to nodes_on_cylinder")
    
    # For each layer, calculate distance increment
    layer_distances = []
    for i in range(num_layers):
        layer_fraction = (i + 1) / num_layers
        layer_distance = extrusion_distance * layer_fraction
        layer_distances.append(layer_distance)
    
    print(f"[DEBUG] Layer distances: {layer_distances}")
    
    # Normalize direction vector if provided
    if direction:
        dir_length = math.sqrt(sum(d*d for d in direction))
        if dir_length < 1e-10:  # Avoid division by zero
            direction = [0, 1, 0]  # Default to Y-axis if direction is too small
        else:
            direction = [d/dir_length for d in direction]
        
        # Calculate top and bottom points of cylinder axis
        print(f"[DEBUG] Center: {center}, Direction: {direction}, Height: {height}, Chamfer: {chamfer}")
        
        # Calculate actual endpoints for proper b values (0 at bottom, 1 at top)
        bottom_point = [
            center[0] + direction[0]*(chamfer[0]*1.05+height/2),  # Bottom = center + direction*height/2
            center[1] + direction[1]*(chamfer[0]*1.05+height/2) if len(center) > 2 else direction[1]*height/2,
            center[2] + direction[2]*(chamfer[0]*1.05+height/2) if len(center) > 2 else direction[2]*height/2
        ]
        
        top_point = [
            center[0] - direction[0]*height/1.98,  # Top = center - direction*height/2
            center[1] - direction[1]*height/1.98 if len(center) > 2 else -direction[1]*height/2,
            center[2] - direction[2]*height/1.98 if len(center) > 2 else -direction[2]*height/2
        ]
        
        print(f"[DEBUG] Bottom point (b=0): {bottom_point}")
        print(f"[DEBUG] Top point (b=1): {top_point}")
        print(f"[DEBUG] Axis length: {math.sqrt(sum((top_point[i]-bottom_point[i])**2 for i in range(3)))}")
    
    # Now create extruded nodes for all nodes on cylinder for each layer
    for node_id in nodes_on_cylinder:
        if node_id not in node_to_extruded:
            node_to_extruded[node_id] = {}
            
            # Get original node coordinates
            if node_id not in nodes:
                print(f"[WARNING] Node {node_id} not found in nodes dictionary")
                continue
            
            x, y, z = nodes[node_id]
            
            # Always create duplicate of original node for consistent procedure
            duplicate_node_id = next_node_id
            next_node_id += 1
            new_nodes[duplicate_node_id] = [x, y, z]  # Exact copy of original
            node_to_duplicate[node_id] = duplicate_node_id
            
            # For each layer, create extruded nodes
            for layer_idx, layer_distance in enumerate(layer_distances):
                # Different extrusion logic based on whether we have direction/height or not
                if direction and height:
                    # Handle angled cylinder extrusion
                    # 1. Find closest point on cylinder axis
                    p0 = [x, y, z]  # Node coordinates
                    p1 = bottom_point  # First point on axis
                    p2 = top_point  # Second point on axis
                    
                    # Vector from p1 to p2 (axis direction)
                    v = [p2[0]-p1[0], p2[1]-p1[1], p2[2]-p1[2]]
                    # Vector from p1 to p0 (node)
                    w = [p0[0]-p1[0], p0[1]-p1[1], p0[2]-p1[2]]
                    
                    # Calculate dot products
                    c1 = sum(w[i]*v[i] for i in range(3))  # dot product of w and v
                    c2 = sum(v[i]*v[i] for i in range(3))  # dot product of v and v
                    
                    # Parameter for closest point on line
                    b = c1 / c2 if c2 > 0 else 0
                    
                    # Calculate closest point on axis
                    pb = [p1[0] + b*v[0], p1[1] + b*v[1], p1[2] + b*v[2]]
                    
                    # 2. Calculate vector from axis to node
                    radial_vector = [p0[0]-pb[0], p0[1]-pb[1], p0[2]-pb[2]]
                    
                    # 3. Calculate distance from node to axis
                    current_radius = math.sqrt(sum(r*r for r in radial_vector))
                    
                    # 4. Normalize radial vector
                    if current_radius > 0:
                        radial_vector = [r/current_radius for r in radial_vector]
                    else:
                        # If node is on axis, use arbitrary perpendicular direction
                        if abs(direction[0]) < abs(direction[1]) and abs(direction[0]) < abs(direction[2]):
                            # x is smallest, use vector in x direction
                            radial_vector = [1, 0, 0]
                        elif abs(direction[1]) < abs(direction[2]):
                            # y is smallest, use vector in y direction
                            radial_vector = [0, 1, 0]
                        else:
                            # z is smallest, use vector in z direction
                            radial_vector = [0, 0, 1]
                    
                    # 5. Apply chamfer if needed
                    # Apply chamfer based on position along cylinder axis
                    if chamfer[0] > 0 and height:
                        # Calculate position along cylinder axis (0 at bottom, 1 at top)
                        # The relative position along the cylinder axis, normalized between 0 and 1
                        axis_position = b
                        
                        # Define the chamfer region size relative to the cylinder height
                        chamfer_region_size = chamfer[0]/height if height > 0 else 0.0
                        
                        # For debugging - print the range of b values we're seeing
                        if node_id % 100 == 0:  # Only print for some nodes to avoid excessive output
                            print(f"[DEBUG] Node {node_id}: axis_position: {axis_position}, chamfer_region_size: {chamfer_region_size}")
                        
                        # If node is in the chamfer region (near bottom of cylinder, where b is close to 0)
                        # Note: We use a small tolerance to ensure we catch nodes that should be in the chamfer region
                        if axis_position <= chamfer_region_size + 0.01:
                            # Calculate chamfer factor (1 at bottom, 0 at end of chamfer region)
                            # Ensure we don't divide by zero if chamfer_region_size is very small
                            if chamfer_region_size > 1e-6:
                                chamfer_factor = 1.0 - (axis_position / chamfer_region_size)
                            else:
                                chamfer_factor = 1.0 if axis_position < 0.01 else 0.0
                                
                            # Clamp the factor between 0 and 1
                            chamfer_factor = max(0.0, min(1.0, chamfer_factor))
                            
                            # Adjust layer_distance based on chamfer factor
                            virtual_total_distance = extrusion_distance + (chamfer[1] * chamfer_factor)
                            virtual_layer_distance = virtual_total_distance*(layer_idx+1) / num_layers
                            new_radius = current_radius + virtual_layer_distance
                            
                            if node_id % 100 == 0:  # Only print for some nodes
                                print(f"[DEBUG] Applying chamfer - b: {axis_position}, factor: {chamfer_factor}, new_radius: {new_radius}")
                        else:
                            # No chamfer adjustment needed
                            new_radius = current_radius + layer_distance
                    else:
                        # No chamfer adjustment needed
                        new_radius = current_radius + layer_distance
                    
                    # 6. Calculate new position
                    new_x = pb[0] + radial_vector[0] * new_radius
                    new_y = pb[1] + radial_vector[1] * new_radius
                    new_z = pb[2] + radial_vector[2] * new_radius
                
                # Create new node for extruded position
                new_node_id = next_node_id
                next_node_id += 1
                new_nodes[new_node_id] = [new_x, new_y, new_z]
                
                # Map original node to extruded node for this layer
                node_to_extruded[node_id][layer_idx] = new_node_id
    
    print(f"[DEBUG] Created {len(node_to_duplicate)} duplicate nodes")
    print(f"[DEBUG] Created {sum(len(layers) for layers in node_to_extruded.values())} extruded nodes")
    
    # Second pass: create edge midpoint nodes for each layer
    for layer_idx in range(num_layers):
        edges_requiring_midpoints = set()
        
        # Identify all edges that need midpoints for this layer
        print(f"[DEBUG] Identifying edges requiring midpoints for layer {layer_idx+1}...")
        for face_info in cylinder_faces:
            # Only process quadrilateral faces for hex elements
            if len(face_info['vertex_nodes']) != 4:
                continue
            vertex_nodes = face_info['vertex_nodes']
            
            # For each vertex node, we need a midpoint to its extruded counterpart
            for node_id in vertex_nodes:
                if node_id in node_to_extruded and layer_idx in node_to_extruded[node_id]:
                    # For first layer, connect duplicate to first layer extruded
                    if layer_idx == 0:
                        duplicate_id = node_to_duplicate[node_id]
                        extruded_id = node_to_extruded[node_id][layer_idx]
                        edge = tuple(sorted([duplicate_id, extruded_id]))
                        edges_requiring_midpoints.add(edge)
                    # For subsequent layers, connect previous layer to current layer
                    else:
                        prev_extruded_id = node_to_extruded[node_id][layer_idx-1]
                        curr_extruded_id = node_to_extruded[node_id][layer_idx]
                        edge = tuple(sorted([prev_extruded_id, curr_extruded_id]))
                        edges_requiring_midpoints.add(edge)
        
        print(f"[DEBUG] Found {len(edges_requiring_midpoints)} edges requiring midpoints for layer {layer_idx+1}")
        
        # Create midpoint nodes for all identified edges in this layer
        for edge in edges_requiring_midpoints:
            node1, node2 = edge
            
            # Skip if we already have a midpoint for this edge
            if edge in midpoint_nodes:
                continue
            
            # Get coordinates of the two nodes - note all nodes should be in new_nodes now
            # as we're using duplicates of original nodes
            if node1 not in new_nodes and node1 not in nodes:
                print(f"[WARNING] Node {node1} not found in nodes dictionaries")
                continue
            if node2 not in new_nodes and node2 not in nodes:
                print(f"[WARNING] Node {node2} not found in nodes dictionaries")
                continue
            
            coords1 = new_nodes.get(node1, nodes.get(node1, [0, 0, 0]))
            coords2 = new_nodes.get(node2, nodes.get(node2, [0, 0, 0]))
            
            # Calculate midpoint
            mid_x = (coords1[0] + coords2[0]) / 2
            mid_y = (coords1[1] + coords2[1]) / 2
            mid_z = (coords1[2] + coords2[2]) / 2
            
            # Create new node
            new_node_id = next_node_id
            next_node_id += 1
            new_nodes[new_node_id] = [mid_x, mid_y, mid_z]
            
            # Store in midpoint cache
            midpoint_nodes[edge] = new_node_id
    
    print(f"[DEBUG] Created {len(midpoint_nodes)} midpoint nodes across all layers")
    
    # Third pass: create hexahedral elements for each layer
    print(f"[DEBUG] Creating hexahedral elements for all layers...")
    hex_count = 0
    
    for face_info in cylinder_faces:
        elem_id = face_info['element_id']
        element_type = face_info['element_type']
        face_name = face_info['face_name']
        vertex_nodes = face_info['vertex_nodes']
        midnodes = face_info['midnodes']
        
        # Process quadrilateral faces
        if len(vertex_nodes) == 4:  # It's a quadrilateral face
            # Get the four vertices of the quadrilateral face
            n1, n2, n3, n4 = vertex_nodes
            
            # Create elements for each layer
            for layer_idx in range(num_layers):
                # Skip if any node doesn't have an extruded counterpart for this layer
                if (n1 not in node_to_extruded or layer_idx not in node_to_extruded[n1] or
                    n2 not in node_to_extruded or layer_idx not in node_to_extruded[n2] or
                    n3 not in node_to_extruded or layer_idx not in node_to_extruded[n3] or
                    n4 not in node_to_extruded or layer_idx not in node_to_extruded[n4]):
                    print(f"[WARNING] Missing extruded node for face of element {elem_id}, layer {layer_idx+1}")
                    continue
                
                # For first layer
                if layer_idx == 0:
                    # Connect duplicate nodes to first layer extruded nodes
                    d1 = node_to_duplicate[n1]
                    d2 = node_to_duplicate[n2]
                    d3 = node_to_duplicate[n3]
                    d4 = node_to_duplicate[n4]
                    
                    # Get first layer extruded nodes
                    n5 = node_to_extruded[n1][layer_idx]
                    n6 = node_to_extruded[n2][layer_idx]
                    n7 = node_to_extruded[n3][layer_idx]
                    n8 = node_to_extruded[n4][layer_idx]
                else:
                    # Connect previous layer to current layer
                    d1 = node_to_extruded[n1][layer_idx-1]
                    d2 = node_to_extruded[n2][layer_idx-1]
                    d3 = node_to_extruded[n3][layer_idx-1]
                    d4 = node_to_extruded[n4][layer_idx-1]
                    
                    # Get current layer extruded nodes
                    n5 = node_to_extruded[n1][layer_idx]
                    n6 = node_to_extruded[n2][layer_idx]
                    n7 = node_to_extruded[n3][layer_idx]
                    n8 = node_to_extruded[n4][layer_idx]
                
                # Get the four mid-edge nodes of the original face (if available)
                if len(midnodes) >= 4:
                    m1, m2, m3, m4 = midnodes[:4]
                    
                    # Skip if any midnode doesn't have an extruded counterpart for this layer
                    if (m1 not in node_to_extruded or layer_idx not in node_to_extruded[m1] or
                        m2 not in node_to_extruded or layer_idx not in node_to_extruded[m2] or
                        m3 not in node_to_extruded or layer_idx not in node_to_extruded[m3] or
                        m4 not in node_to_extruded or layer_idx not in node_to_extruded[m4]):
                        print(f"[WARNING] Missing extruded midnode for element {elem_id}, layer {layer_idx+1}")
                        continue
                    
                    # Get mid-edge nodes based on layer
                    if layer_idx == 0:
                        # For first layer, use duplicates of original midnodes
                        dm1 = node_to_duplicate[m1]
                        dm2 = node_to_duplicate[m2]
                        dm3 = node_to_duplicate[m3]
                        dm4 = node_to_duplicate[m4]
                    else:
                        # For subsequent layers, use previous layer midnodes
                        dm1 = node_to_extruded[m1][layer_idx-1]
                        dm2 = node_to_extruded[m2][layer_idx-1]
                        dm3 = node_to_extruded[m3][layer_idx-1]
                        dm4 = node_to_extruded[m4][layer_idx-1]
                    
                    # Get extruded mid-edge nodes for current layer
                    m5 = node_to_extruded[m1][layer_idx]
                    m6 = node_to_extruded[m2][layer_idx]
                    m7 = node_to_extruded[m3][layer_idx]
                    m8 = node_to_extruded[m4][layer_idx]
                    
                    # Get or create mid-edge nodes between layer connections
                    edge1 = tuple(sorted([d1, n5]))
                    edge2 = tuple(sorted([d2, n6]))
                    edge3 = tuple(sorted([d3, n7]))
                    edge4 = tuple(sorted([d4, n8]))
                    
                    # Ensure we have midpoints for these edges
                    if edge1 not in midpoint_nodes:
                        print(f"[WARNING] Missing midpoint for edge {edge1}")
                        continue
                    if edge2 not in midpoint_nodes:
                        print(f"[WARNING] Missing midpoint for edge {edge2}")
                        continue
                    if edge3 not in midpoint_nodes:
                        print(f"[WARNING] Missing midpoint for edge {edge3}")
                        continue
                    if edge4 not in midpoint_nodes:
                        print(f"[WARNING] Missing midpoint for edge {edge4}")
                        continue
                    
                    e1 = midpoint_nodes[edge1]
                    e2 = midpoint_nodes[edge2]
                    e3 = midpoint_nodes[edge3]
                    e4 = midpoint_nodes[edge4]
                    
                    # Create the hexahedral element (C3D20R)
                    # The node ordering for C3D20R follows the convention:
                    # - First 8 nodes: corner nodes in specific order
                    # - Next 12 nodes: mid-edge nodes
                    # Following the sweep_mesh_cuda.py convention where:
                    # Inner face (current layer): d1, d2, d3, d4
                    # Outer face (next layer): n5, n6, n7, n8
                    # The correct ordering maps the quadrilateral face to hex element
                    hex_element = [
                        next_element_id,  # Element ID
                        # Corner nodes - following C3D20R convention from sweep_mesh_cuda.py
                        d4, d3, d1, d2,   # Inner face vertices (nodes 1-4)
                        n8, n7, n5, n6,   # Outer face vertices (nodes 5-8)
                        # Mid-edge nodes of inner face (nodes 9-12)
                        dm3, dm2, dm1, dm4,
                        # Mid-edge nodes of outer face (nodes 13-16)
                        m7, m6, m5, m8,
                        # Mid-edge nodes connecting inner and outer vertices (nodes 17-20)
                        e3, e4, e1, e2
                    ]
                    next_element_id += 1
                    
                    # Validate mid-edge nodes positioning for this element
                    mismatches = validate_c3d20r_midedges(hex_element, nodes, new_nodes)
                    if mismatches:
                        # Attempt auto-correction of mid-edge ordering if mismatches found
                        corrected, changed, stats = auto_correct_c3d20r_midedges(hex_element, nodes, new_nodes)
                        if changed:
                            hex_element = corrected

                    hex_elements.append(hex_element)
                    hex_count += 1
                else:
                    print(f"[WARNING] Insufficient midnodes for face of element {elem_id}")
    
    print(f"[DEBUG] Created {hex_count} hexahedral elements across {num_layers} layers")
    print(f"[DEBUG] Created {len(new_nodes)} new nodes total")
    
    # Post-processing: If adding to existing part, replace duplicate nodes with originals
    if not create_new_part:
        print(f"[INFO] Post-processing: Finding and replacing duplicate nodes based on position")
        
        # Find duplicates by comparing node positions
        duplicate_to_original = {}
        nodes_to_remove = set()
        position_tolerance = 1e-9  # Very tight tolerance for exact position matching
        
        # For each new node, check if it matches an existing node position
        for new_node_id, new_coords in new_nodes.items():
            new_x, new_y, new_z = new_coords
            
            # Search through original nodes for matching position
            for orig_node_id, orig_coords in nodes.items():
                orig_x, orig_y, orig_z = orig_coords
                
                # Check if positions match within tolerance
                distance = ((new_x - orig_x)**2 + (new_y - orig_y)**2 + (new_z - orig_z)**2)**0.5
                if distance <= position_tolerance:
                    duplicate_to_original[new_node_id] = orig_node_id
                    nodes_to_remove.add(new_node_id)
                    #print(f"[DEBUG] Found duplicate: new node {new_node_id} {new_coords} matches original {orig_node_id} {orig_coords} (distance={distance:.2e})")
                    break
        
        print(f"[INFO] Found {len(duplicate_to_original)} duplicate nodes based on position")
        
        # Update all hex elements to use original nodes instead of duplicates
        # This ensures elements at cylinder interface use original surface nodes
        for hex_element in hex_elements:
            for i in range(1, len(hex_element)):  # Skip element ID at index 0
                node_id = hex_element[i]
                if node_id in duplicate_to_original:
                    original_node = duplicate_to_original[node_id]
                    hex_element[i] = original_node
                    print(f"[DEBUG] Element {hex_element[0]}: Replaced duplicate node {node_id} with original {original_node}")
        
        # Remove duplicate nodes from new_nodes (keep only extruded nodes)
        for node_id in nodes_to_remove:
            if node_id in new_nodes:
                del new_nodes[node_id]
        
        print(f"[INFO] Removed {len(nodes_to_remove)} duplicate nodes, kept {len(new_nodes)} extruded nodes")
    
    return new_nodes, hex_elements

def create_target_radius_gui(
    default_input_file=None,
    default_output_file=None,
    default_json_file=None,
    default_target_radius=17.89,
    default_tolerance=0.025,
):
    """
    Launch a GUI that lets the user pick files, tweak cylinder targets, and preview changes.
    
    Parameters:
    - default_input_file: Optional default INP file path
    - default_output_file: Optional default output INP path
    - default_json_file: Optional default JSON file containing cylinder data
    - default_target_radius: Default target radius when custom value is not provided
    - default_tolerance: Default tolerance for finding nodes on the cylinder
    
    Returns:
    - modified_cylinders: List of cylinder parameters with user overrides applied
    - create_new_part: Boolean indicating whether to create a new part
    - input_file: Selected input INP file path
    - output_file: Auto-generated (or edited) output INP file path
    - json_file: Selected JSON file path
    - tolerance: Tolerance value chosen by the user
    """
    root = tk.Tk()
    root.title("Cylindrical Extrusion Setup")
    root.geometry("1400x750")
    root.minsize(1150, 650)
    
    result = []
    create_new_part_result = [True]
    tolerance_result = [default_tolerance]
    
    selected_input = tk.StringVar(value=default_input_file or "")
    selected_output = tk.StringVar(value=default_output_file or "")
    selected_json = tk.StringVar(value=default_json_file or "")
    tolerance_var = tk.StringVar(value=f"{default_tolerance:.6f}")
    status_var = tk.StringVar(value="Select the input INP and JSON files to begin.")
    
    current_cylinders = []
    radius_entries = []
    layers_entries = []
    use_custom_entries = []
    
    submit_btn = None
    preview_btn = None
    rows_frame = None
    empty_state_label = None
    preview_ax = None
    preview_canvas = None
    
    def auto_set_output(*_):
        """Automatically derive the output file based on the input INP path"""
        if default_output_file:
            selected_output.set(default_output_file)
            return
        path = selected_input.get().strip()
        if not path:
            selected_output.set("")
            return
        base, ext = os.path.splitext(path)
        ext = ext if ext else ".inp"
        selected_output.set(f"{base}-extruded{ext}")
    
    auto_set_output()
    
    def set_status(message):
        status_var.set(message)
    
    def set_apply_state():
        """Enable/disable actions based on whether required data is present"""
        has_files = bool(selected_input.get().strip()) and bool(selected_json.get().strip())
        has_cylinders = bool(current_cylinders)
        try:
            tolerance_value = float(tolerance_var.get())
            tolerance_valid = tolerance_value > 0
        except ValueError:
            tolerance_valid = False
        
        enable_actions = has_files and has_cylinders and tolerance_valid
        
        if submit_btn is not None:
            if enable_actions:
                submit_btn.state(["!disabled"])
            else:
                submit_btn.state(["disabled"])
        if preview_btn is not None:
            if has_cylinders:
                preview_btn.state(["!disabled"])
            else:
                preview_btn.state(["disabled"])
    
    def update_entry_state(entry_widget, use_custom_var, cyl_index):
        """Allow only the first cylinder to be edited by default"""
        if cyl_index == 0:
            entry_widget.configure(state="normal")
            return
        
        if use_custom_var.get():
            entry_widget.configure(state="normal")
        else:
            entry_widget.configure(state="disabled")
            entry_widget.delete(0, "end")
            entry_widget.insert(0, f"{default_target_radius:.3f}")
    
    def populate_cylinder_rows():
        """Regenerate the cylinder rows based on the currently loaded JSON data"""
        nonlocal empty_state_label
        if rows_frame is None:
            return
        
        for child in rows_frame.winfo_children():
            child.destroy()
        
        radius_entries.clear()
        layers_entries.clear()
        use_custom_entries.clear()
        empty_state_label = None
        
        if not current_cylinders:
            empty_state_label = ttk.Label(rows_frame, text="Select a JSON file with cylinder data to edit targets.")
            empty_state_label.pack(anchor="w", padx=5, pady=5)
            return
        
        for i, cyl in enumerate(current_cylinders):
            frame = ttk.Frame(rows_frame)
            frame.pack(fill="x", pady=2)
            
            nominal_radius = float(cyl.get("radius", 0.0))
            extra_radius = float(cyl.get("extra_radius", 0.0))
            actual_radius = nominal_radius + extra_radius
            num_layers_default = int(cyl.get("num_layers", 1))
            
            ttk.Label(frame, text=f"{i+1}", width=8).pack(side="left", padx=3)
            ttk.Label(frame, text=f"{nominal_radius:.3f}", width=14).pack(side="left", padx=3)
            ttk.Label(frame, text=f"{extra_radius:.3f}", width=14).pack(side="left", padx=3)
            ttk.Label(frame, text=f"{actual_radius:.3f}", width=14).pack(side="left", padx=3)
            
            entry = ttk.Entry(frame, width=15)
            entry.pack(side="left", padx=3)
            default_value = actual_radius if i == 0 else cyl.get("target_radius", default_target_radius)
            entry.insert(0, f"{float(default_value):.3f}")
            radius_entries.append(entry)
            
            layers_entry = ttk.Entry(frame, width=10)
            layers_entry.pack(side="left", padx=3)
            layers_entry.insert(0, str(max(1, num_layers_default)))
            layers_entries.append(layers_entry)
            
            use_custom_var = tk.BooleanVar(value=(i == 0))
            use_custom_entries.append(use_custom_var)
            
            use_custom_check = ttk.Checkbutton(
                frame,
                text="Override default",
                variable=use_custom_var,
                command=lambda e=entry, v=use_custom_var, idx=i: update_entry_state(e, v, idx)
            )
            use_custom_check.pack(side="left", padx=3)
            
            update_entry_state(entry, use_custom_var, i)
    
    def reset_preview(message):
        """Display a placeholder message in the preview panel"""
        if preview_ax is None or preview_canvas is None:
            return
        preview_ax.clear()
        preview_ax.set_axis_off()
        preview_ax.text2D(0.5, 0.5, message, transform=preview_ax.transAxes,
                          horizontalalignment="center", verticalalignment="center")
        preview_canvas.draw_idle()
    
    def gather_inputs(validate=True):
        """Collect cylinder entries from the GUI"""
        if not current_cylinders:
            if validate:
                raise ValueError("No cylinders loaded from JSON.")
            return [], default_tolerance
        
        try:
            tolerance_value = float(tolerance_var.get())
        except ValueError:
            raise ValueError("Tolerance must be a number.")
        
        if tolerance_value <= 0:
            raise ValueError("Tolerance must be positive.")
        
        packed = []
        for i, (cyl, entry, layers_entry, use_custom_var) in enumerate(
            zip(current_cylinders, radius_entries, layers_entries, use_custom_entries)
        ):
            if i == 0 or use_custom_var.get():
                target_radius = float(entry.get())
            else:
                target_radius = float(default_target_radius)
            
            num_layers = int(layers_entry.get())
            if num_layers < 1:
                raise ValueError(f"Number of layers must be at least 1 for cylinder {i+1}.")
            
            nominal_radius = float(cyl.get("radius", 0.0))
            extra_radius = float(cyl.get("extra_radius", 0.0))
            actual_radius = nominal_radius + extra_radius
            extrusion_distance = target_radius - actual_radius
            
            center = cyl.get("center", (0, 0))
            direction = cyl.get("direction", [0, 1, 0])
            height = abs(cyl.get("height", 1.0))
            chamfer = cyl.get("chamfer") or [0.0, 0.0]
            
            packed.append({
                "original_radius": nominal_radius,
                "extra_radius": extra_radius,
                "actual_radius": actual_radius,
                "target_radius": target_radius,
                "extrusion_distance": extrusion_distance,
                "num_layers": num_layers,
                "center": tuple(center),
                "direction": direction,
                "height": height,
                "chamfer": chamfer,
                "tolerance": tolerance_value,
            })
        
        return packed, tolerance_value
    
    def update_visualization(show_errors=False):
        """Refresh the matplotlib preview inside the GUI"""
        if not current_cylinders:
            reset_preview("Load cylinder data to preview the geometry.")
            return
        
        try:
            cylinders_payload, _ = gather_inputs(validate=True)
        except ValueError as exc:
            reset_preview("Resolve the highlighted input issues to preview.")
            if show_errors:
                messagebox.showerror("Preview Error", str(exc))
            return
        
        if preview_ax is None or preview_canvas is None:
            return
        
        preview_ax.set_axis_on()
        visualize_cylinders(
            cylinders_payload,
            ax=preview_ax,
            canvas=preview_canvas,
            show=False
        )
    
    def load_cylinders_from_json(path=None):
        """Load cylinder definitions from a JSON file and refresh the UI"""
        nonlocal current_cylinders
        json_path = path if path is not None else selected_json.get().strip()
        if not json_path:
            messagebox.showwarning("JSON Required", "Please select a JSON file that contains cylinder data.")
            return
        
        try:
            with open(json_path, "r") as f:
                cylinder_payload = json.load(f)
            current_cylinders = list(cylinder_payload.get("cylinders", []))
            if not current_cylinders:
                set_status("The selected JSON file does not contain any cylinders.")
            else:
                set_status(f"Loaded {len(current_cylinders)} cylinders from {os.path.basename(json_path)}.")
        except Exception as exc:
            current_cylinders = []
            populate_cylinder_rows()
            reset_preview("Failed to load cylinder data.")
            set_status("Could not read the selected JSON file.")
            messagebox.showerror("JSON Error", f"Failed to load cylinder data:\n{exc}")
            set_apply_state()
            return
        
        populate_cylinder_rows()
        update_visualization()
        set_apply_state()
    
    def browse_input():
        file_path = filedialog.askopenfilename(
            title="Select input INP file",
            filetypes=[("INP files", "*.inp"), ("All files", "*.*")]
        )
        if file_path:
            selected_input.set(file_path)
            auto_set_output()
            set_apply_state()
    
    def browse_json():
        file_path = filedialog.askopenfilename(
            title="Select cylinder JSON file",
            filetypes=[("JSON files", "*.json"), ("All files", "*.*")]
        )
        if file_path:
            selected_json.set(file_path)
            load_cylinders_from_json(file_path)
        else:
            set_apply_state()
    
    def on_submit():
        try:
            cylinders_payload, tolerance_value = gather_inputs(validate=True)
        except ValueError as exc:
            messagebox.showerror("Input Error", f"Please correct the input values:\n{exc}")
            return
        
        if not cylinders_payload:
            messagebox.showwarning("No Cylinders", "No cylinder data was provided. Please load a JSON file.")
            return
        
        result.clear()
        result.extend(cylinders_payload)
        create_new_part_result[0] = part_option_var.get()
        tolerance_result[0] = tolerance_value
        root.destroy()
    
    # Files and tolerance selection
    files_frame = ttk.LabelFrame(root, text="File Selection", padding=10)
    files_frame.pack(fill="x", padx=10, pady=10)
    files_frame.columnconfigure(1, weight=1)
    
    ttk.Label(files_frame, text="Input INP file:").grid(row=0, column=0, sticky="w", padx=5, pady=5)
    input_entry = ttk.Entry(files_frame, textvariable=selected_input)
    input_entry.grid(row=0, column=1, sticky="ew", padx=5, pady=5)
    ttk.Button(files_frame, text="Browse…", command=browse_input).grid(row=0, column=2, padx=5, pady=5)
    
    ttk.Label(files_frame, text="Output INP file:").grid(row=1, column=0, sticky="w", padx=5, pady=5)
    output_entry = ttk.Entry(files_frame, textvariable=selected_output, state="readonly")
    output_entry.grid(row=1, column=1, sticky="ew", padx=5, pady=5)
    
    ttk.Label(files_frame, text="Cylinder JSON file:").grid(row=2, column=0, sticky="w", padx=5, pady=5)
    json_entry = ttk.Entry(files_frame, textvariable=selected_json)
    json_entry.grid(row=2, column=1, sticky="ew", padx=5, pady=5)
    ttk.Button(files_frame, text="Browse…", command=browse_json).grid(row=2, column=2, padx=5, pady=5)
    ttk.Button(files_frame, text="Load cylinders", command=load_cylinders_from_json).grid(row=2, column=3, padx=5, pady=5)
    
    ttk.Label(files_frame, text="Find nodes tolerance (mm):").grid(row=3, column=0, sticky="w", padx=5, pady=5)
    tol_entry = ttk.Entry(files_frame, textvariable=tolerance_var, width=12)
    tol_entry.grid(row=3, column=1, sticky="w", padx=5, pady=5)
    
    # Part creation options
    part_frame = ttk.LabelFrame(root, text="Part Creation Option", padding=10)
    part_frame.pack(fill="x", padx=10, pady=5)
    
    part_option_var = tk.BooleanVar(value=True)
    ttk.Radiobutton(
        part_frame,
        text="Create new part (independent nodes/elements)",
        variable=part_option_var,
        value=True
    ).pack(anchor="w", pady=2)
    ttk.Radiobutton(
        part_frame,
        text="Add to existing part (reuse existing nodes)",
        variable=part_option_var,
        value=False
    ).pack(anchor="w", pady=2)
    
    # Button bar pinned to bottom so it never scrolls out of view
    button_frame = ttk.Frame(root)
    button_frame.pack(side="bottom", fill="x", pady=10, padx=10)
    
    # Main content split (table + preview)
    content_frame = ttk.Frame(root)
    content_frame.pack(fill="both", expand=True, padx=10, pady=5)
    
    control_frame = ttk.Frame(content_frame)
    control_frame.pack(side="left", fill="both", expand=True, padx=(0, 10))
    
    header_frame = ttk.Frame(control_frame)
    header_frame.pack(fill="x", pady=(0, 5))
    ttk.Label(header_frame, text="Cylinder #", width=8).pack(side="left", padx=3)
    ttk.Label(header_frame, text="Nominal Radius", width=14).pack(side="left", padx=3)
    ttk.Label(header_frame, text="Extra Radius", width=14).pack(side="left", padx=3)
    ttk.Label(header_frame, text="Actual Radius", width=14).pack(side="left", padx=3)
    ttk.Label(header_frame, text="Target Radius", width=15).pack(side="left", padx=3)
    ttk.Label(header_frame, text="# Layers", width=10).pack(side="left", padx=3)
    ttk.Label(header_frame, text="Override", width=12).pack(side="left", padx=3)
    
    table_frame = ttk.Frame(control_frame)
    table_frame.pack(fill="both", expand=True)
    
    scroll_canvas = tk.Canvas(table_frame, highlightthickness=0)
    scrollbar = ttk.Scrollbar(table_frame, orient="vertical", command=scroll_canvas.yview)
    scroll_canvas.pack(side="left", fill="both", expand=True)
    scrollbar.pack(side="right", fill="y")
    
    rows_frame = ttk.Frame(scroll_canvas)
    scroll_canvas.create_window((0, 0), window=rows_frame, anchor="nw")
    scroll_canvas.configure(yscrollcommand=scrollbar.set)
    rows_frame.bind("<Configure>", lambda e: scroll_canvas.configure(scrollregion=scroll_canvas.bbox("all")))
    
    info_text = (
        f"Default target radius is {default_target_radius:.3f} mm. "
        "Uncheck \"Override\" to reuse this value on downstream cylinders."
    )
    ttk.Label(control_frame, textvariable=status_var, foreground="steelblue").pack(anchor="w", pady=(5, 0))
    ttk.Label(control_frame, text=info_text, foreground="gray20").pack(anchor="w", pady=(2, 5))
    
    # Preview panel
    preview_frame = ttk.LabelFrame(content_frame, text="Cylinder Preview", padding=5)
    preview_frame.pack(side="right", fill="both", expand=False)
    preview_frame.configure(width=400)
    
    preview_figure = Figure(figsize=(5.5, 4.5))
    preview_ax = preview_figure.add_subplot(111, projection='3d')
    preview_canvas = FigureCanvasTkAgg(preview_figure, master=preview_frame)
    preview_canvas.draw()
    preview_canvas.get_tk_widget().pack(fill="both", expand=True)
    reset_preview("Load cylinder data to preview the geometry.")
    
    preview_btn = ttk.Button(button_frame, text="Refresh Preview", command=lambda: update_visualization(show_errors=True))
    preview_btn.pack(side="left", padx=5)
    submit_btn = ttk.Button(button_frame, text="Apply", command=on_submit)
    submit_btn.pack(side="right", padx=5)
    
    # Variable traces for live validation
    def _on_input_change(*_):
        auto_set_output()
        set_apply_state()
    
    selected_input.trace_add("write", _on_input_change)
    selected_json.trace_add("write", lambda *_: set_apply_state())
    tolerance_var.trace_add("write", lambda *_: set_apply_state())
    
    # Load default JSON if available
    if default_json_file and os.path.exists(default_json_file):
        load_cylinders_from_json(default_json_file)
    else:
        populate_cylinder_rows()
        set_apply_state()
    
    root.mainloop()
    
    return (
        result,
        create_new_part_result[0],
        selected_input.get().strip(),
        selected_output.get().strip(),
        selected_json.get().strip(),
        tolerance_result[0],
    )

def process_cylinder_extrusion(
    nodes,
    elements,
    target_radius,
    extrusion_distance,
    num_layers=1,
    center=(0, 0),
    direction=None,
    height=None,
    chamfer=None,
    create_new_part=True,
    tolerance=0.025,
):
    """
    Process a single cylinder extrusion and return the new nodes and elements
    
    Parameters:
    - nodes: Dictionary of nodes
    - elements: Dictionary of elements
    - target_radius: Target radius for the cylindrical surface
    - extrusion_distance: Distance to extrude
    - num_layers: Number of layers to create in the radial direction
    - center: Coordinates of the cylinder center
    - direction: Direction vector of the cylinder axis [dx, dy, dz]
    - height: Height of the cylinder
    - chamfer: Chamfer value for the cylindrical surface
    - create_new_part: Whether to create new nodes (True) or reuse existing ones (False)
    - tolerance: Radial tolerance passed to find_nodes_on_cylinder
    
    Returns:
    - new_nodes: Dictionary of newly created nodes
    - new_elements: List of created elements (wedge or hex)
    - element_type: Type of elements created ('wedge' or 'hex')
    """
    
    # Ensure chamfer parameter is valid
    if chamfer is None or not isinstance(chamfer, list) or len(chamfer) < 2:
        chamfer = [0.0, 0.0]
        print(f"[INFO] Using default chamfer parameters: {chamfer}")
    
    # Find nodes on the cylindrical surface
    cylinder_nodes = find_nodes_on_cylinder(
        nodes,
        target_radius,
        tolerance=tolerance,
        center=center,
        direction=direction,
        height=height,
        chamfer=chamfer,
    )
    print(f"[INFO] Found {len(cylinder_nodes)} nodes on cylindrical surface")
    
    # Debug: Check if node 894680 is in cylinder_nodes
    debug_node_id = 894680
    if debug_node_id in cylinder_nodes:
        print(f"[DEBUG] Node {debug_node_id} IS in cylinder_nodes")
        if debug_node_id in nodes:
            coords = nodes[debug_node_id]
            print(f"[DEBUG] Node {debug_node_id} coordinates: {coords}")
    else:
        print(f"[DEBUG] Node {debug_node_id} is NOT in cylinder_nodes")
    
    # Identify element faces on the cylindrical surface
    cylinder_faces = identify_element_faces_on_cylinder(elements, cylinder_nodes, nodes)
    print(f"[INFO] Found {len(cylinder_faces)} faces on the cylindrical surface")
    
    # Debug: Check if node 894680 appears in any cylinder faces
    debug_node_id = 894680
    debug_faces_with_node = []
    for face_info in cylinder_faces:
        all_face_nodes = face_info['vertex_nodes'] + face_info['midnodes']
        if debug_node_id in all_face_nodes:
            debug_faces_with_node.append(face_info)
    
    if debug_faces_with_node:
        print(f"[DEBUG] Node {debug_node_id} appears in {len(debug_faces_with_node)} cylinder faces:")
        for face_info in debug_faces_with_node:
            print(f"[DEBUG]   Element {face_info['element_id']} ({face_info['element_type']}) face {face_info['face_name']}")
            print(f"[DEBUG]     Vertex nodes: {face_info['vertex_nodes']}")
            print(f"[DEBUG]     Mid nodes: {face_info['midnodes']}")
            print(f"[DEBUG]     Vertices on cylinder: {face_info['vertices_on_cylinder']}")
            print(f"[DEBUG]     Midnodes on cylinder: {face_info['midnodes_on_cylinder']}")
    else:
        print(f"[DEBUG] Node {debug_node_id} does NOT appear in any cylinder faces")
    
    # Determine the predominant element type
    element_types = {}
    for face in cylinder_faces:
        elem_type = face['element_type']
        element_types[elem_type] = element_types.get(elem_type, 0) + 1
    
    print(f"[INFO] Element type distribution on cylinder: {element_types}")
    
    # Decide whether to create wedge or hex elements based on predominant type
    if 'C3D10' in element_types and element_types.get('C3D10', 0) > element_types.get('C3D20', 0) + element_types.get('C3D20R', 0):
        # Create wedge elements
        print(f"[INFO] Creating wedge elements (C3D15) from tetrahedral faces")
        new_nodes, new_elements = create_wedge_elements_from_faces(
            cylinder_faces, nodes, elements, target_radius, extrusion_distance, direction, height,
            num_layers=num_layers, center=center, chamfer=chamfer, create_new_part=create_new_part)
        element_type = 'wedge'
    else:
        # Create hex elements
        print(f"[INFO] Creating hexahedral elements (C3D20R) from quadrilateral faces")
        new_nodes, new_elements = create_hex_elements_from_faces(
            cylinder_faces, nodes, elements, target_radius, extrusion_distance, direction, height,
            num_layers=num_layers, center=center, chamfer=chamfer, create_new_part=create_new_part)
        element_type = 'hex'
    
    print(f"[INFO] Created {len(new_elements)} {element_type} elements with {len(new_nodes)} new nodes")
    
    return new_nodes, new_elements, element_type

def write_modified_inp_file(input_filename, output_filename, model_data, part_names=None, create_new_part=True):
    """Write modified INP file with new nodes and elements
    
    Parameters:
    - input_filename: Original input file name (for reference only)
    - output_filename: Output file to write
    - model_data: Dictionary containing all model data
    - part_names: Optional list of part names to include (if None, include all parts)
    - create_new_part: Whether elements were created as new part or added to existing
    """
    with open(output_filename, 'w') as outfile:
        # Write header
        outfile.write("**\n")
        outfile.write("** Modified INP file with extruded elements\n")
        outfile.write("** Generated by cylindrical_extrusion.py\n")
        outfile.write("**\n")
        
        # Write node section
        outfile.write("*NODE\n")
        
        # Write all nodes
        for node_id, coords in model_data['nodes'].items():
            x, y, z = coords
            outfile.write(f"{node_id}, {x:.6f}, {y:.6f}, {z:.6f}\n")
        
        # Group elements by type
        element_types = {}
        for elem_id, elem_data in model_data['elements'].items():
            element_type = elem_data['type']
            # Skip C3D15 and C3D20R elements, they're handled separately
            if element_type in ['C3D15', 'C3D20R']:
                continue
                
            if element_type not in element_types:
                element_types[element_type] = []
            element_types[element_type].append(elem_id)
        
        # Write each element type section
        for element_type, element_ids in element_types.items():
            outfile.write(f"*ELEMENT, TYPE={element_type}\n")
            for elem_id in element_ids:
                nodes = model_data['elements'][elem_id]['nodes']
                # If we have more than 10 nodes, split across multiple lines
                if len(nodes) > 10:
                    outfile.write(f"{elem_id}, {', '.join(map(str, nodes[:10]))},\n")
                    outfile.write(f"    {', '.join(map(str, nodes[10:]))}\n")
                else:
                    outfile.write(f"{elem_id}, {', '.join(map(str, nodes))}\n")
        
        # Write wedge elements if any
        if 'wedge_elements' in model_data and model_data['wedge_elements']:
            if create_new_part:
                outfile.write("*ELEMENT, type=C3D15, elset=ALL_WEDGES\n")
            else:
                outfile.write("*ELEMENT, type=C3D15, elset=EXTRUDED_WEDGES\n")
            
            for element in model_data['wedge_elements']:
                elem_id = element[0]
                nodes = element[1:]
                
                # Write wedge element nodes across lines following Abaqus format
                outfile.write(f"{elem_id}, {', '.join(map(str, nodes[:8]))},\n")
                outfile.write(f"    {', '.join(map(str, nodes[8:]))}\n")
        
        # Write hex elements if any
        if 'hex_elements' in model_data and model_data['hex_elements']:
            if create_new_part:
                outfile.write("*ELEMENT, type=C3D20R, elset=ALL_HEXES\n")
            else:
                outfile.write("*ELEMENT, type=C3D20R, elset=EXTRUDED_HEXES\n")
            
            for element in model_data['hex_elements']:
                elem_id = element[0]
                nodes = element[1:]
                
                # Write hex element nodes across lines following Abaqus format
                outfile.write(f"{elem_id}, {', '.join(map(str, nodes[:10]))},\n")
                outfile.write(f"    {', '.join(map(str, nodes[10:]))}\n")

        # Write node sets
        if model_data['sets']['nsets']:
            outfile.write("\n** Node Sets\n")
            for nset_name, nset_data in model_data['sets']['nsets'].items():
                if nset_data["nodes"]:
                    outfile.write(f"{nset_data['declaration']}\n")
                    nodes_list = nset_data["nodes"]
                    # Write nodes with at most 8 per line
                    for i in range(0, len(nodes_list), 8):
                        chunk = nodes_list[i:i+8]
                        line = ", ".join(map(str, chunk))
                        if i + 8 < len(nodes_list):
                            line += ","
                        outfile.write(line + "\n")
        
        # Write element sets
        if model_data['sets']['elsets']:
            outfile.write("\n** Element Sets\n")
            for elset_name, elset_data in model_data['sets']['elsets'].items():
                if elset_data["elements"]:
                    outfile.write(f"{elset_data['declaration']}\n")
                    elements_list = elset_data["elements"]
                    # Write elements with at most 8 per line
                    for i in range(0, len(elements_list), 8):
                        chunk = elements_list[i:i+8]
                        line = ", ".join(map(str, chunk))
                        if i + 8 < len(elements_list):
                            line += ","
                        outfile.write(line + "\n")

        # Write surface definitions
        if model_data['sets']['surfaces']:
            outfile.write("\n** Surface Definitions\n")
            for surface_name, surface_data in model_data['sets']['surfaces'].items():
                # Write surface declaration
                outfile.write(f"{surface_data['declaration']}\n")
                
                # Write surface faces
                for elset_name, face_name in surface_data['faces']:
                    outfile.write(f"{elset_name}, {face_name}\n")
        
        # Write part definitions if parts are specified in the model_data
        if 'parts' in model_data and model_data['parts']:
            outfile.write("\n** Part Definitions\n")
            for part_name, part_data in model_data['parts'].items():
                if part_names and part_name not in part_names:
                    continue  # Skip parts not in the specified list
                
                outfile.write(f"*PART, NAME={part_name}\n")
                # Write part-specific elements and nodes if needed
                # Depending on your FEA solver, you might need to reference the previously defined
                # elements/nodes rather than redefining them here
                outfile.write(f"*END PART\n")
    
    # Print summary
    print(f"Modified INP file written to {output_filename}")
    print(f"Total nodes: {len(model_data['nodes'])}")
    print(f"Regular elements: {sum(1 for elem in model_data['elements'].values() if elem['type'] not in ['C3D15', 'C3D20R'])}")
    
    wedge_count = len(model_data.get('wedge_elements', []))
    hex_count = len(model_data.get('hex_elements', []))
    
    if wedge_count > 0:
        print(f"Wedge elements (C3D15): {wedge_count}")
    if hex_count > 0:
        print(f"Hexahedral elements (C3D20R): {hex_count}")
    
    if 'parts' in model_data:
        print(f"Parts: {', '.join(model_data['parts'].keys())}")

def visualize_cylinders(
    cylinder_data,
    nodes=None,
    elements=None,
    wedge_elements=None,
    hex_elements=None,
    show_mesh=False,
    ax=None,
    canvas=None,
    show=True,
):
    """
    Visualize the cylinders and optionally the mesh. When an axes instance is passed,
    the plot is rendered in-place so the caller can embed the preview inside a GUI.
    
    Parameters:
    - cylinder_data: List of cylinder parameters
    - nodes: Dictionary of node coordinates (optional)
    - elements: Dictionary of elements (optional)
    - wedge_elements: List of wedge elements (optional)
    - hex_elements: List of hex elements (optional)
    - show_mesh: Whether to show the mesh (default: False)
    - ax: Optional matplotlib axes to render into
    - canvas: Optional tkinter canvas wrapper for embedded plots
    - show: Whether to show the figure (used when creating a standalone figure)
    """
    created_figure = False
    if ax is None:
        fig = plt.figure(figsize=(12, 10))
        ax = fig.add_subplot(111, projection='3d')
        created_figure = True
    else:
        fig = ax.figure
        ax.clear()
    
    # Generate unique colors for each cylinder
    colors = list(mcolors.TABLEAU_COLORS.values())
    
    # Plot cylinders
    for i, cyl in enumerate(cylinder_data):
        color = colors[i % len(colors)]
        
        # Extract cylinder parameters
        center = cyl["center"]
        original_radius = cyl["original_radius"]
        extra_radius = cyl.get("extra_radius", 0)
        actual_radius = original_radius + extra_radius
        target_radius = cyl.get("target_radius", actual_radius)
        direction = cyl.get("direction", [0, 1, 0])
        height = cyl.get("height", 10)
        # Ensure we have a default value if chamfer is missing
        if "chamfer" not in cyl or cyl["chamfer"] is None:
            chamfer = [0, 0]
        else:
            chamfer = cyl.get("chamfer", [0, 0])
        
        # Normalize direction vector
        dir_length = math.sqrt(sum(d*d for d in direction))
        if dir_length < 1e-10:
            direction = [0, 1, 0]
        else:
            direction = [d/dir_length for d in direction]
        
        # Calculate cylinder endpoints
        if len(center) == 2:  # If only x,z provided
            center = [center[0], 0, center[1]]
            
        # Define cylinder endpoints consistently with other functions
        bottom_point = [
            center[0] + direction[0]*(chamfer[0]*1.05+height/2),
            center[1] + direction[1]*(chamfer[0]*1.05+height/2),
            center[2] + direction[2]*(chamfer[0]*1.05+height/2)
        ]
        
        top_point = [
            center[0] - direction[0]*height/1.95,
            center[1] - direction[1]*height/1.95,
            center[2] - direction[2]*height/1.95
        ]
        
        # Calculate orthogonal vectors for plotting
        v1 = direction
        if abs(v1[0]) < abs(v1[1]) and abs(v1[0]) < abs(v1[2]):
            v2 = [0, -v1[2], v1[1]]  # orthogonal to v1, choosing x=0
        else:
            v2 = [-v1[1], v1[0], 0]  # orthogonal to v1, choosing z=0
            
        # Normalize v2
        v2_length = math.sqrt(sum(d*d for d in v2))
        if v2_length < 1e-10:  # Avoid division by zero
            v2 = [1, 0, 0]  # Default if we can't normalize
        else:
            v2 = [d/v2_length for d in v2]
        
        # Cross product for third orthogonal vector
        v3 = [
            v1[1]*v2[2] - v1[2]*v2[1],
            v1[2]*v2[0] - v1[0]*v2[2],
            v1[0]*v2[1] - v1[1]*v2[0]
        ]
        
        # Plot cylinder axis
        ax.plot([bottom_point[0], top_point[0]],
                [bottom_point[1], top_point[1]],
                [bottom_point[2], top_point[2]], 'k-', linewidth=1)
        
        # Plot actual and target cylinders
        for radius, linestyle, alpha in [(actual_radius, '-', 0.3), (target_radius, '--', 0.8)]:
            # Create circle points
            theta = np.linspace(0, 2*np.pi, 50)
            
            # Plot circles at 10 positions along the cylinder
            steps = 10
            for t in np.linspace(0, 1, steps):
                # Interpolate between bottom and top
                center_point = [
                    bottom_point[0] + t*(top_point[0] - bottom_point[0]),
                    bottom_point[1] + t*(top_point[1] - bottom_point[1]),
                    bottom_point[2] + t*(top_point[2] - bottom_point[2])
                ]
                
                # Apply chamfer if needed
                actual_radius_at_height = radius
                if chamfer[0] > 0 and height > 0 and t < chamfer[0]/height:
                    # Avoid division by zero
                    if chamfer[0] > 1e-6:
                        chamfer_factor = 1.0 - (t * height / chamfer[0])
                    else:
                        chamfer_factor = 1.0 if t < 0.01 else 0.0
                    chamfer_factor = max(0.0, min(1.0, chamfer_factor))
                    actual_radius_at_height = radius + (chamfer[1] * chamfer_factor)
                
                # Calculate circle points
                x = center_point[0] + actual_radius_at_height * (v2[0] * np.cos(theta) + v3[0] * np.sin(theta))
                y = center_point[1] + actual_radius_at_height * (v2[1] * np.cos(theta) + v3[1] * np.sin(theta))
                z = center_point[2] + actual_radius_at_height * (v2[2] * np.cos(theta) + v3[2] * np.sin(theta))
                
                # Plot the circle
                ax.plot(x, y, z, color=color, linestyle=linestyle, alpha=alpha)
        
        # Add text label for the cylinder
        ax.text(center[0], center[1], center[2],
                f"Cyl {i+1}\nR={original_radius:.2f} -> {target_radius:.2f}",
                color=color, fontsize=8)
    
    # Plot mesh if requested
    if show_mesh and nodes and elements:
        # Plot a subset of elements (up to 300 to avoid overcrowding)
        element_sample = list(elements.items())[:300]
        for elem_id, elem_data in element_sample:
            # Get node coordinates
            node_list = elem_data['nodes'][:8]  # Take first 8 nodes for simplicity
            if len(node_list) < 4:  # Skip if less than tetrahedron
                continue
                
            # Get coordinates
            coords = []
            for node_id in node_list:
                if node_id in nodes:
                    coords.append(nodes[node_id])
            
            # Skip if any nodes are missing
            if len(coords) != len(node_list):
                continue
                
            # Plot element edges
            for i in range(len(coords)):
                for j in range(i+1, len(coords)):
                    ax.plot([coords[i][0], coords[j][0]],
                            [coords[i][1], coords[j][1]],
                            [coords[i][2], coords[j][2]], 'k-', linewidth=0.1, alpha=0.3)
    
    # Plot extruded elements if available
    if wedge_elements and nodes:
        # Plot a subset (up to 100 elements)
        wedges_sample = wedge_elements[:100]
        for wedge in wedges_sample:
            # Extract node IDs
            node_ids = wedge[1:7]  # Top and bottom triangles (6 vertices)
            
            # Get coordinates
            coords = []
            for node_id in node_ids:
                # Look in both original and new nodes
                if node_id in nodes:
                    coords.append(nodes[node_id])
                    
            # Skip if any nodes are missing
            if len(coords) != len(node_ids):
                continue
                
            # Plot wedge edges with different color
            for i in range(len(coords)):
                for j in range(i+1, len(coords)):
                    ax.plot([coords[i][0], coords[j][0]],
                            [coords[i][1], coords[j][1]],
                            [coords[i][2], coords[j][2]], 'g-', linewidth=0.4, alpha=0.5)
    
    if hex_elements and nodes:
        # Plot a subset (up to 100 elements)
        hex_sample = hex_elements[:100]
        for hex_elem in hex_sample:
            # Extract node IDs
            node_ids = hex_elem[1:9]  # First 8 vertices
            
            # Get coordinates
            coords = []
            for node_id in node_ids:
                # Look in both original and new nodes
                if node_id in nodes:
                    coords.append(nodes[node_id])
                    
            # Skip if any nodes are missing
            if len(coords) != len(node_ids):
                continue
                
            # Plot hex edges with different color
            for i in range(len(coords)):
                for j in range(i+1, len(coords)):
                    ax.plot([coords[i][0], coords[j][0]],
                            [coords[i][1], coords[j][1]],
                            [coords[i][2], coords[j][2]], 'b-', linewidth=0.4, alpha=0.5)
    
    # Set labels and title
    ax.set_xlabel('X')
    ax.set_ylabel('Y')
    ax.set_zlabel('Z')
    ax.set_title('Cylinder Visualization')
    
    # Try to make approximately equal scale
    all_points = []
    for cyl in cylinder_data:
        center = cyl["center"]
        if len(center) == 2:
            center = [center[0], 0, center[1]]
        all_points.append(center)
    
    if all_points:
        all_points = np.array(all_points)
        max_range = np.max([
            np.max(all_points[:,0]) - np.min(all_points[:,0]),
            np.max(all_points[:,1]) - np.min(all_points[:,1]),
            np.max(all_points[:,2]) - np.min(all_points[:,2])
        ])
        mid_x = (np.max(all_points[:,0]) + np.min(all_points[:,0])) / 2
        mid_y = (np.max(all_points[:,1]) + np.min(all_points[:,1])) / 2
        mid_z = (np.max(all_points[:,2]) + np.min(all_points[:,2])) / 2
        ax.set_xlim(mid_x - max_range/2, mid_x + max_range/2)
        ax.set_ylim(mid_y - max_range/2, mid_y + max_range/2)
        ax.set_zlim(mid_z - max_range/2, mid_z + max_range/2)
    
    # Add a legend
    legend_elements = [
        plt.Line2D([0], [0], color='k', linestyle='-', label='Cylinder Axis'),
        plt.Line2D([0], [0], color='gray', linestyle='-', alpha=0.3, label='Actual Radius'),
        plt.Line2D([0], [0], color='gray', linestyle='--', alpha=0.8, label='Target Radius')
    ]
    
    if show_mesh:
        legend_elements.append(plt.Line2D([0], [0], color='k', linestyle='-', linewidth=0.1, alpha=0.3, label='Mesh Elements'))
    if wedge_elements:
        legend_elements.append(plt.Line2D([0], [0], color='g', linestyle='-', linewidth=0.4, alpha=0.5, label='Extruded Wedges'))
    if hex_elements:
        legend_elements.append(plt.Line2D([0], [0], color='b', linestyle='-', linewidth=0.4, alpha=0.5, label='Extruded Hexes'))
        
    ax.legend(handles=legend_elements, loc='upper right')
    
    # Set the viewing angle
    ax.view_init(elev=30, azim=45)
    
    # Show or refresh the plot
    fig.tight_layout()
    if created_figure and show:
        plt.show()
    elif canvas is not None:
        canvas.draw_idle()
    elif created_figure:
        fig.canvas.draw_idle()

def process_face_elements(face_elements, set_name_prefix, part_name, model_data):
    """Process face elements and create elsets and surfaces
    
    Parameters:
    - face_elements: Dictionary from format_interface_surfaces
    - set_name_prefix: Prefix for the set names
    - part_name: Name of the part these surfaces belong to
    - model_data: Model data structure to update
    """
    if not face_elements or 'original' not in face_elements:
        print(f"[WARNING] No face elements found for {set_name_prefix}")
        return
        
    print(f"[INFO] Processing face elements for {set_name_prefix}")
    
    for face, elements_list in face_elements['original'].items():
        if not elements_list:
            continue  # Skip empty faces
            
        # Create element set names without part prefix to keep naming consistent
        # This ensures cylinder 1-2 and 3-4 use the same naming pattern
        elset_name = f"{set_name_prefix}_{face}"
        
        # Add the element set to model_data
        model_data['sets']['elsets'][elset_name] = {
            'declaration': f"*Elset, elset={elset_name}",
            'elements': elements_list
        }
        print(f"[INFO] Created element set {elset_name} with {len(elements_list)} elements")
        
        # Create the surface that references this element set
        surface_name = f"{set_name_prefix}"
        
        # Create or update the surface
        if surface_name not in model_data['sets']['surfaces']:
            model_data['sets']['surfaces'][surface_name] = {
                'declaration': f"*Surface, type=ELEMENT, name={surface_name}",
                'type': 'ELEMENT',
                'faces': []
            }
        
        # Add the face reference to the surface using the same element set name
        # without part prefixes to ensure consistent naming
        model_data['sets']['surfaces'][surface_name]['faces'].append((elset_name, face))
        print(f"[INFO] Added face {face} to surface {surface_name} referencing {elset_name}")


def write_delta_inp_file(output_filename, new_nodes, new_wedge_elements, new_hex_elements, part_name):
    """Write only newly created nodes/elements to an INP file for host-side import."""
    with open(output_filename, 'w') as outfile:
        outfile.write("**\n")
        outfile.write("** Cylindrical extrusion delta mesh\n")
        outfile.write("** Generated by cylindrical_extrusion.py\n")
        outfile.write("**\n")
        outfile.write(f"*PART, NAME={part_name}\n")
        outfile.write("*NODE\n")
        for node_id, coords in sorted(new_nodes.items()):
            outfile.write(f"{node_id}, {coords[0]:.6f}, {coords[1]:.6f}, {coords[2]:.6f}\n")

        if new_wedge_elements:
            outfile.write("*ELEMENT, type=C3D15\n")
            for element in new_wedge_elements:
                elem_id = element[0]
                nodes = element[1:]
                outfile.write(f"{elem_id}, {', '.join(map(str, nodes[:8]))},\n")
                outfile.write(f"    {', '.join(map(str, nodes[8:]))}\n")

        if new_hex_elements:
            outfile.write("*ELEMENT, type=C3D20R\n")
            for element in new_hex_elements:
                elem_id = element[0]
                nodes = element[1:]
                outfile.write(f"{elem_id}, {', '.join(map(str, nodes[:10]))},\n")
                outfile.write(f"    {', '.join(map(str, nodes[10:]))}\n")

    print(f"[INFO] Delta INP file written to {output_filename}")


def build_surface_payload(model_data, element_set_names, surface_names, max_existing_element_id):
    """Create a compact host payload split by import phase."""
    payload = {
        "existing_element_sets": [],
        "existing_surfaces": [],
        "generated_element_sets": [],
        "generated_surfaces": [],
    }

    element_set_phase = {}
    for elset_name in element_set_names:
        elset_data = model_data['sets']['elsets'][elset_name]
        element_ids = list(elset_data.get("elements", []))
        is_existing = len(element_ids) > 0 and all(element_id <= max_existing_element_id for element_id in element_ids)
        phase_name = "existing" if is_existing else "generated"
        element_set_phase[elset_name] = phase_name
        payload[f"{phase_name}_element_sets"].append({
            "name": elset_name,
            "elements": element_ids,
        })

    for surface_name in surface_names:
        surface_data = model_data['sets']['surfaces'][surface_name]
        faces = [
            {
                "element_set_name": elset_name,
                "face_name": face_name,
            }
            for elset_name, face_name in surface_data.get("faces", [])
        ]
        referenced_phases = {
            element_set_phase.get(face["element_set_name"], "generated")
            for face in faces
        }
        phase_name = "existing" if referenced_phases == {"existing"} else "generated"
        payload[f"{phase_name}_surfaces"].append({
            "name": surface_name,
            "faces": faces,
        })

    return payload


def write_surface_payload_file(output_filename, payload):
    """Write host merge payload as JSON."""
    with open(output_filename, 'w', encoding='utf-8') as handle:
        json.dump(payload, handle, indent=2)
    print(f"[INFO] Surface payload written to {output_filename}")


def _encode_report_value(value):
    if value is None:
        return ""
    return base64.b64encode(str(value).encode("utf-8")).decode("ascii")


def write_host_report(report_file, status, output_path=None, delta_inp_path=None, payload_path=None, error=None):
    """Write a small report consumed by the host application."""
    if not report_file:
        return

    with open(report_file, 'w', encoding='utf-8') as handle:
        handle.write(f"status={status}\n")
        handle.write(f"output_path_b64={_encode_report_value(output_path)}\n")
        handle.write(f"delta_inp_path_b64={_encode_report_value(delta_inp_path)}\n")
        handle.write(f"payload_path_b64={_encode_report_value(payload_path)}\n")
        handle.write(f"error_b64={_encode_report_value(error)}\n")

def main():
    # Parse command line arguments
    parser = argparse.ArgumentParser(description='Process cylindrical extrusion parameters.')
    parser.add_argument('input_file', nargs='?',
                      default="",
                      help='Input INP file path')
    parser.add_argument('output_file', nargs='?',
                      default="",
                      help='Output INP file path')
    parser.add_argument('--json', '-j',
                      default="cylinder_data.json",
                      help='JSON file containing cylinder data')
    parser.add_argument('--target_radius', '-r',
                      type=float,
                      default=17.89,
                      help='Default target radius for cylinders')
    parser.add_argument('--tolerance', '-t',
                      type=float,
                      default=0.025,
                      help='Default tolerance used when finding nodes on a cylinder')
    parser.add_argument('--surface-workers',
                      type=int,
                      default=1,
                      help='Number of worker processes for surface set/surface creation (1 disables parallelism)')
    parser.add_argument('--report-file',
                      default=None,
                      help='Write a completion report for a calling host application')
    parser.add_argument('--payload-file',
                      default=None,
                      help='Write the generated element-set/surface payload to this JSON file')
    parser.add_argument('--delta-inp-file',
                      default=None,
                      help='Write the newly generated nodes/elements to this delta INP file')

    args = parser.parse_args()
    
    input_file = args.input_file
    output_file = args.output_file
    json_file = args.json
    default_target_radius = args.target_radius

    default_tolerance = args.tolerance
    requested_surface_workers = max(1, args.surface_workers)
    
    # Launch GUI for file selection, tolerance, and cylinder parameters
    try:
        gui_result = create_target_radius_gui(
            default_input_file=input_file,
            default_output_file=output_file or None,
            default_json_file=json_file,
            default_target_radius=default_target_radius,
            default_tolerance=default_tolerance,
        )

        (cylinders,
         create_new_part,
         gui_input_file,
         gui_output_file,
         gui_json_file,
         selected_tolerance) = gui_result

        if not cylinders:
            print("[WARNING] No cylinders specified or GUI closed. Exiting without changes.")
            write_host_report(args.report_file, "cancelled")
            return

        input_file = gui_input_file or input_file
        output_file = gui_output_file or output_file
        json_file = gui_json_file or json_file
        tolerance_value = selected_tolerance or default_tolerance

        print(f"[INFO] Input INP file: {input_file}")
        print(f"[INFO] Output INP file: {output_file}")
        print(f"[INFO] Cylinder JSON: {json_file}")
        print(f"[INFO] Using default target radius: {default_target_radius} mm")
        print(f"[INFO] Node tolerance: {tolerance_value} mm")
        print(f"[INFO] Surface workers: {requested_surface_workers}")
        print(f"[INFO] Part creation option: {'Create new part' if create_new_part else 'Add to existing part'}")

        # Reload the JSON so we can persist the updated targets for reference
        try:
            with open(json_file, 'r') as f:
                cylinder_data = json.load(f)
        except Exception as e:
            print(f"[ERROR] Error loading cylinder data: {e}")
            raise

        for i, cyl in enumerate(cylinders):
            print(f"[INFO] Cylinder {i+1}:")
            print(f"  - Nominal radius: {cyl['original_radius']} mm")
            print(f"  - Extra radius: {cyl['extra_radius']} mm")
            print(f"  - Actual radius: {cyl['actual_radius']} mm")
            print(f"  - Target radius: {cyl['target_radius']} mm")
            print(f"  - Extrusion distance: {cyl['extrusion_distance']} mm")
            print(f"  - Number of layers: {cyl['num_layers']}")
            print(f"  - Center: {cyl['center']}")
            print(f"  - Direction: {cyl['direction']}")
            print(f"  - Height: {cyl['height']}")
            print(f"  - Chamfer: {cyl['chamfer']}")
            print(f"  - Tolerance: {cyl.get('tolerance', tolerance_value)} mm")

            if i < len(cylinder_data.get("cylinders", [])):
                cylinder_data["cylinders"][i]["target_radius"] = cyl["target_radius"]
                cylinder_data["cylinders"][i]["num_layers"] = cyl["num_layers"]
                cylinder_data["cylinders"][i]["tolerance"] = cyl.get("tolerance", tolerance_value)

        copy_json_file = json_file.replace(".json", "_copy.json")
        with open(copy_json_file, 'w') as f:
            json.dump(cylinder_data, f, indent=2)
        print(f"[INFO] Updated cylinder data in {copy_json_file} with new target radii")

        print(f"Reading INP file: {input_file}")

        # Initialize global dictionary for midpoint nodes
        global midpoint_nodes
        midpoint_nodes = {}
        max_node_id = 0
        max_elem_id = 0
        parts = {
            "MONO": {
                "file_path": input_file,
                "nodes": {},
                "elements": {},
                "node_sets": {},
                "element_sets": {},
                "existing_sets": {
                    'nsets': {},
                    'elsets': {},
                    'surfaces': {}
                }
            }
        }
        part_name = "MONOLITE_AION36X-pietro"

        # Read INP file. existing_sets is as follow existing_sets['surfaces'] = surfaces, existing_sets['nsets'] = nsets, existing_sets['elsets'] = elsets
        nodes, elements, existing_sets = read_inp_file(input_file, max_node_id, max_elem_id, parts["MONO"]["existing_sets"], part_name)

        # Initialize combined results
        all_new_nodes = {}
        all_wedge_elements = []
        all_hex_elements = []

        # Extract existing wedge and hex elements from the input file
        existing_wedge_ids = set()
        existing_hex_ids = set()
        for elem_id, elem_data in elements.items():
            if elem_data['type'] == 'C3D15':
                elem_list = [elem_id] + elem_data['nodes']
                all_wedge_elements.append(elem_list)
                existing_wedge_ids.add(elem_id)
            elif elem_data['type'] == 'C3D20R':
                elem_list = [elem_id] + elem_data['nodes']
                all_hex_elements.append(elem_list)
                existing_hex_ids.add(elem_id)

        print(f"[INFO] Found {len(all_wedge_elements)} existing wedge elements (C3D15) in the input file")
        print(f"[INFO] Found {len(all_hex_elements)} existing hex elements (C3D20R) in the input file")

        # Track max IDs for offsets
        max_node_id = max(nodes.keys())
        max_elem_id = max(elements.keys())
        node_id_offset = max_node_id + 1
        elem_id_offset = max_elem_id + 1

        # Process each cylinder
        for cyl_idx, cyl in enumerate(cylinders):
            print(f"\n[INFO] ===== PROCESSING CYLINDER {cyl_idx + 1} =====")

            actual_radius = cyl['actual_radius']
            target_radius = cyl['target_radius']

            print(f"Actual radius: {actual_radius} mm")
            print(f"Target radius: {target_radius} mm")
            print(f"Center: {cyl['center']}")
            print(f"Direction: {cyl['direction']}")
            print(f"Height: {cyl['height']}")
            print(f"Number of layers: {cyl['num_layers']}")

            if 'chamfer' not in cyl or cyl['chamfer'] is None or not isinstance(cyl['chamfer'], list) or len(cyl['chamfer']) < 2:
                cyl['chamfer'] = [0.0, 0.0]

            print(f"Chamfer: {cyl['chamfer']}")

            new_nodes, new_elements, element_type = process_cylinder_extrusion(
                nodes,
                elements,
                actual_radius,
                cyl["extrusion_distance"],
                num_layers=cyl["num_layers"],
                center=cyl["center"],
                direction=cyl["direction"],
                height=cyl["height"],
                chamfer=cyl["chamfer"],
                create_new_part=create_new_part,
                tolerance=cyl.get("tolerance", tolerance_value),
            )

            print(f"[INFO] Processing results from cylinder {cyl_idx + 1}")

            node_id_map = {}
            debug_node_id = 894680
            if create_new_part:
                for node_id, coords in new_nodes.items():
                    new_id = node_id_offset
                    node_id_map[node_id] = new_id
                    all_new_nodes[new_id] = coords
                    node_id_offset += 1
            else:
                all_new_nodes.update(new_nodes)
                for node_id in new_nodes:
                    node_id_map[node_id] = node_id

            for elem in new_elements:
                new_elem = [elem_id_offset]
                elem_id_offset += 1

                for node_id in elem[1:]:
                    mapped_node = node_id_map.get(node_id, node_id)
                    new_elem.append(mapped_node)

                    if node_id == debug_node_id or mapped_node == 929090:
                        print(f"[DEBUG] ELEMENT: Node {node_id} -> {mapped_node} used in element {new_elem[0]} (type: {element_type})")

                if element_type == 'wedge':
                    if new_elem[0] not in existing_wedge_ids:
                        all_wedge_elements.append(new_elem)
                    else:
                        print(f"[WARNING] Skipping duplicate wedge element ID: {new_elem[0]}")
                else:
                    if new_elem[0] not in existing_hex_ids:
                        all_hex_elements.append(new_elem)
                    else:
                        print(f"[WARNING] Skipping duplicate hex element ID: {new_elem[0]}")

            print(f"[INFO] Added {len(new_nodes)} nodes and {len(new_elements)} {element_type} elements from cylinder {cyl_idx + 1}")

        print(f"\n[INFO] ===== FINAL RESULTS =====")
        print(f"[INFO] Total new nodes: {len(all_new_nodes)}")
        print(f"[INFO] Total wedge elements: {len(all_wedge_elements)}")
        print(f"[INFO] Total hex elements: {len(all_hex_elements)}")

        model_data = {
            'nodes': {**nodes, **all_new_nodes},
            'elements': elements,
            'wedge_elements': all_wedge_elements,
            'hex_elements': all_hex_elements,
            'parts': {},
            'sets': existing_sets
        }

        if create_new_part:
            model_data['parts'] = {
                part_name: {
                    'elements': list(elements.keys()),
                    'nodes': list(nodes.keys())
                },
                f"{part_name}_EXTRUSION": {
                    'elements': [element[0] for element in all_wedge_elements + all_hex_elements if element[0] > max_elem_id],
                    'nodes': list(all_new_nodes.keys())
                }
            }
        else:
            model_data['parts'] = {
                part_name: {
                    'elements': list(elements.keys()) + [element[0] for element in all_wedge_elements + all_hex_elements if element[0] > max_elem_id],
                    'nodes': list(set(nodes.keys()) | set(all_new_nodes.keys()))
                }
            }

        for key, value in model_data.items():
            print(f"{key}")
            if key == "parts":
                for part_key, part_value in value.items():
                    print(f"  {part_key}")
            if key == "sets":
                for set_key, set_value in value.items():
                    print(f"  {set_key}")
                    if set_key == "surfaces":
                        for surface_key, surface_value in set_value.items():
                            print(f"    {surface_key}")

        existing_elset_names = set(model_data['sets']['elsets'].keys())
        existing_surface_names = set(model_data['sets']['surfaces'].keys())

        all_nodes = {**nodes, **all_new_nodes}

        generated_element_lookup = {}
        for wedge in all_wedge_elements:
            if wedge[0] > max_elem_id:
                generated_element_lookup[wedge[0]] = {'type': 'C3D15', 'nodes': wedge[1:]}
        for hex_elem in all_hex_elements:
            if hex_elem[0] > max_elem_id:
                generated_element_lookup[hex_elem[0]] = {'type': 'C3D20R', 'nodes': hex_elem[1:]}

        part_elems_by_part = {}
        for part_key, part_data in model_data['parts'].items():
            part_elems = {}
            for elem_id in part_data['elements']:
                if elem_id in elements:
                    part_elems[elem_id] = elements[elem_id]
                elif elem_id in generated_element_lookup:
                    part_elems[elem_id] = generated_element_lookup[elem_id]
            part_elems_by_part[part_key] = part_elems
            print(f"[INFO] Prepared {len(part_elems)} elements for surface extraction in part {part_key}")

        element_type_name = "hex" if all_hex_elements else "wedge"
        surface_workers = min(requested_surface_workers, max(1, len(cylinders)))

        surface_task_inputs = [(cyl_idx, cyl) for cyl_idx, cyl in enumerate(cylinders)]
        surface_results = []

        if surface_workers > 1 and len(surface_task_inputs) > 1:
            print(f"[INFO] Parallel surface generation enabled with {surface_workers} worker processes")
            with ProcessPoolExecutor(
                max_workers=surface_workers,
                initializer=_init_surface_build_worker,
                initargs=(all_nodes, part_elems_by_part, part_name, element_type_name, tolerance_value),
            ) as executor:
                for cylinder_result in executor.map(_build_surface_results_for_cylinder, surface_task_inputs):
                    surface_results.extend(cylinder_result)
        else:
            print("[INFO] Surface generation running in single process")
            _init_surface_build_worker(all_nodes, part_elems_by_part, part_name, element_type_name, tolerance_value)
            for task in surface_task_inputs:
                surface_results.extend(_build_surface_results_for_cylinder(task))

        surface_results.sort(key=lambda item: item["merge_order"])
        for item in surface_results:
            process_face_elements(
                item["face_elements"],
                item["set_name_prefix"],
                item["part_key"],
                model_data,
            )

        generated_elset_names = sorted(set(model_data['sets']['elsets'].keys()) - existing_elset_names)
        generated_surface_names = sorted(set(model_data['sets']['surfaces'].keys()) - existing_surface_names)

        write_modified_inp_file(input_file, output_file, model_data, part_names=list(model_data['parts'].keys()), create_new_part=create_new_part)

        delta_inp_path = None
        payload_path = None
        if args.report_file:
            if not create_new_part:
                raise RuntimeError("PrePoMax host integration currently supports only 'Create new part'.")

            delta_inp_path = args.delta_inp_file or output_file.replace(".inp", "_delta.inp")
            payload_path = args.payload_file or output_file.replace(".inp", "_payload.json")
            new_wedge_elements = [element for element in all_wedge_elements if element[0] > max_elem_id]
            new_hex_elements = [element for element in all_hex_elements if element[0] > max_elem_id]
            delta_part_name = f"{part_name}_EXTRUSION"
            write_delta_inp_file(delta_inp_path, all_new_nodes, new_wedge_elements, new_hex_elements, delta_part_name)
            write_surface_payload_file(
                payload_path,
                build_surface_payload(model_data, generated_elset_names, generated_surface_names, max_elem_id),
            )

        print(f"[INFO] Processing complete. Updated cylinder data in {json_file}")
        print(f"[INFO] Modified INP file written to {output_file}")
        write_host_report(args.report_file, "completed", output_path=output_file,
                          delta_inp_path=delta_inp_path, payload_path=payload_path)
    except Exception as exc:
        error_message = f"{exc}"
        print(f"[ERROR] {error_message}")
        write_host_report(args.report_file, "failed", error=error_message)
        raise

if __name__ == "__main__":
    main()

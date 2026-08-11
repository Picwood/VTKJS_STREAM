import math
import os
import sys
import json
import re
import logging
import subprocess
import numpy as np
import tkinter as tk
from tkinter import ttk, filedialog, messagebox
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg
from matplotlib.figure import Figure
import matplotlib.cm as cm
import matplotlib.colors as mcolors
from collections import defaultdict
import concurrent.futures
import multiprocessing

# Note: Using projection-based surface reconstruction instead of external repair functions

# Set up logging configuration
def setup_logging(log_level=logging.INFO, log_file=None):
    """Configure logging for the mesh_tools module
    
    Parameters:
    - log_level: Logging level (default: logging.INFO)
    - log_file: Optional path to a log file
    
    Returns:
    - Logger instance
    """
    # Create logger
    logger = logging.getLogger('mesh_tools')
    logger.setLevel(log_level)
    
    # Remove existing handlers to avoid duplicates
    if logger.hasHandlers():
        logger.handlers.clear()
    
    # Create console handler with formatting
    console_handler = logging.StreamHandler()
    console_handler.setLevel(log_level)
    
    # Create formatter
    formatter = logging.Formatter('%(asctime)s - %(name)s - %(levelname)s - %(message)s')
    console_handler.setFormatter(formatter)
    
    # Add console handler to logger
    logger.addHandler(console_handler)
    
    # Add file handler if a log file is specified
    if log_file:
        file_handler = logging.FileHandler(log_file)
        file_handler.setLevel(log_level)
        file_handler.setFormatter(formatter)
        logger.addHandler(file_handler)
    
    return logger

# Create a default logger
logger = setup_logging()


def _replace_last(text: str, target: str, replacement: str):
    """Replace only the last occurrence of target in text."""
    idx = text.rfind(target)
    if idx == -1:
        return text, False
    return text[:idx] + replacement + text[idx + len(target):], True


def fallback_extract_from_inp(script_path: str, repos):
    """Run the original script to produce an INP and parse its 2D mesh."""
    script_dir = os.path.dirname(os.path.abspath(script_path))
    with open(script_path, "r") as f:
        script_content = f.read()
    write_matches = re.findall(r'gmsh\.write\(["\'](.+?)["\']\)', script_content)
    expected_out = os.path.join(script_dir, write_matches[0]) if write_matches else None

    subprocess.run([sys.executable, script_path], check=True)

    inp_file = expected_out
    if not inp_file or not os.path.exists(inp_file):
        inp_files = [f for f in os.listdir(script_dir) if f.lower().endswith(".inp")]
        if not inp_files:
            raise FileNotFoundError("No INP file produced by the script.")
        inp_files.sort(key=lambda x: os.path.getmtime(os.path.join(script_dir, x)), reverse=True)
        inp_file = os.path.join(script_dir, inp_files[0])

    read_result = read_inp_file(inp_file)
    if isinstance(read_result, tuple):
        if len(read_result) < 2:
            raise ValueError(f"read_inp_file returned unexpected tuple length: {len(read_result)}")
        nodes, elements = read_result[0], read_result[1]
    else:
        raise TypeError(f"read_inp_file returned unexpected type: {type(read_result)}")
    quads = {}
    for eid, ed in elements.items():
        nlist = ed.get("nodes", [])
        if len(nlist) >= 4:
            quads[eid] = nlist[:4]
        elif len(nlist) == 3:
            quads[eid] = nlist + [nlist[0]]
    return nodes, quads, repos


def extract_2d_mesh_from_gmsh_script(script_path: str, return_metadata: bool = False):
    """
    Extract 2D nodes/quads by executing a Gmsh Python script.

    Extraction code is injected at the LAST gmsh.finalize() if present; otherwise
    it is appended to the end of the file, ensuring we grab the mesh after the
    final mesh generation call.
    """
    print(f"\n=== Processing {script_path} ===\n")

    try:
        script_dir = os.path.dirname(os.path.abspath(script_path))
        script_name = os.path.basename(script_path)
        temp_script_path = os.path.join(script_dir, f"temp_{script_name}")

        with open(script_path, "r") as f:
            script_content = f.read()

        # Parse translation parameters
        repos = [0.0, 0.0, 0.0]
        try:
            with open(script_path, "r") as f:
                script_lines = f.readlines()
            if len(script_lines) >= 4 and "# next lines describe the center position" in script_lines[0]:
                for idx, line in enumerate(script_lines[1:4]):
                    if line.startswith("#"):
                        try:
                            repos[idx] = float(line.strip()[1:].strip())
                        except ValueError:
                            pass
        except Exception as e:  # pylint: disable=broad-except
            print(f"[ERROR] Failed to parse translation parameters: {e}")

        extraction_code = """
# Mesh extraction code added by utilities_simulation.extract_2d_mesh_from_gmsh_script
import json, os, re as _re, numpy as _np, gmsh
nodes = {}
quad_elements = {}
node_tags, node_coords, _ = gmsh.model.mesh.getNodes()
coords = _np.array(node_coords).reshape(-1, 3)
for i, tag in enumerate(node_tags):
    nodes[int(tag)] = coords[i].tolist()
element_types, element_tags, node_tags = gmsh.model.mesh.getElements(2)
for etype, etags, enodes in zip(element_types, element_tags, node_tags):
    nodes_per_elem = gmsh.model.mesh.getElementProperties(etype)[3]
    enodes = _np.array(enodes).reshape(-1, nodes_per_elem)
    for i, etag in enumerate(etags):
        elem_nodes = [int(n) for n in enodes[i]]
        if nodes_per_elem >= 4:
            quad_elements[int(etag)] = elem_nodes[:4]
        elif nodes_per_elem == 3:
            quad_elements[int(etag)] = elem_nodes[:3] + [elem_nodes[0]]
# Curve metadata (for patch boundary selection by line names/tags)
curve_nodes = {}
curve_entities = gmsh.model.getEntities(1)
curve_tag_set = set(int(tag) for (_dim, tag) in curve_entities)
for (_dim, ctag) in curve_entities:
    ctag = int(ctag)
    cnode_set = set()
    c_types, c_tags, c_node_groups = gmsh.model.mesh.getElements(1, ctag)
    for c_etype, _c_etags, c_enodes in zip(c_types, c_tags, c_node_groups):
        c_npe = gmsh.model.mesh.getElementProperties(c_etype)[3]
        if c_npe <= 0:
            continue
        c_arr = _np.array(c_enodes).reshape(-1, c_npe)
        for crow in c_arr:
            for n in crow:
                cnode_set.add(int(n))
    if not cnode_set:
        try:
            c_ntags, _c_coords, _ = gmsh.model.mesh.getNodes(1, ctag)
            for n in c_ntags:
                cnode_set.add(int(n))
        except Exception:
            pass
    curve_nodes[str(ctag)] = sorted(cnode_set)

named_curve_tags = {}
for _name, _val in list(globals().items()):
    if not isinstance(_name, str):
        continue
    if not _re.match(r'^[A-Za-z_]\\w*$', _name):
        continue
    try:
        _ival = int(_val)
    except Exception:
        continue
    if _ival in curve_tag_set and _name.lower().startswith('l'):
        named_curve_tags[_name] = _ival

mesh_data = {
    'nodes': {str(k): v for k, v in nodes.items()},
    'elements': {str(k): v for k, v in quad_elements.items()},
    'metadata': {
        'curve_nodes': curve_nodes,
        'named_curve_tags': named_curve_tags,
    }
}
output_json = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'mesh_data.json')
with open(output_json, 'w') as f:
    json.dump(mesh_data, f)
print(f\"Extracted {len(nodes)} nodes and {len(quad_elements)} elements to {output_json}\")
gmsh.finalize()
"""

        modified, replaced = _replace_last(script_content, "gmsh.finalize()", extraction_code)
        if not replaced:
            modified = script_content + "\n" + extraction_code

        with open(temp_script_path, "w") as f:
            f.write(modified)

        subprocess.run([sys.executable, temp_script_path], check=True)

        mesh_data_path = os.path.join(script_dir, "mesh_data.json")
        if not os.path.exists(mesh_data_path):
            raise FileNotFoundError(f"Mesh data file not found: {mesh_data_path}")

        with open(mesh_data_path, "r") as f:
            mesh_data = json.load(f)
        nodes = {int(k): v for k, v in mesh_data["nodes"].items()}
        quad_elements = {int(k): v for k, v in mesh_data["elements"].items()}

        metadata = mesh_data.get("metadata", {}) if isinstance(mesh_data, dict) else {}
        if return_metadata:
            return nodes, quad_elements, repos, metadata
        return nodes, quad_elements, repos

    except Exception as e:  # pylint: disable=broad-except
        print(f"Error extracting mesh directly from GMSH script: {e}")
        fallback = fallback_extract_from_inp(script_path, repos)
        if return_metadata:
            n, q, r = fallback
            return n, q, r, {}
        return fallback
    finally:
        for path in (locals().get("temp_script_path"), os.path.join(os.path.dirname(os.path.abspath(script_path)), "mesh_data.json")):
            try:
                if path and os.path.exists(path):
                    os.remove(path)
            except Exception:
                pass

def determine_interface_faces(element_nodes, nodes_3d, searching_radius, target_radius, element_type, center=(0, 0), direction=None, height=None, chamfer=[0.0, 0.0], tolerance=0.015):
    """Determine which faces of an element are at the interface radius
    
    Parameters:
    - element_nodes: List of node IDs for the element
    - nodes_3d: Dictionary of node coordinates {node_id: [x, y, z]}
    - searching_radius: The real radius where elements are located (original_radius + extra_radius)
    - target_radius: The target radius up to which we want to extrude
    - element_type: Type of element (C3D20, C3D15, C3D10)
    - center: Coordinates of the cylinder center
    - direction: Direction vector of the cylinder axis [dx, dy, dz]
    - height: Height of the cylinder
    - chamfer: Chamfer parameters [chamfer_x, chamfer_z]
    - tolerance: Tolerance for radius comparison
    
    Returns:
    - List of face identifiers ('S1', 'S2', etc.) that are at the interface radius
    """
    # Validate chamfer parameters
    if chamfer is None or not isinstance(chamfer, list) or len(chamfer) < 2:
        chamfer = [0.0, 0.0]
        
    # Face node mappings for different element types
    face_nodes = {
        'C3D20': {  # Hexahedral element with 20 nodes
            'S1': [0,1,2,3, 8,9,10,11],  # Bottom face
            'S2': [4,5,6,7, 12,13,14,15],  # Top face
            'S3': [0,1,4,5, 8,12,16,17],  # Front face
            'S4': [1,2,5,6, 9,13,17,18],  # Right face
            'S5': [2,3,6,7, 10,14,18,19],  # Back face
            'S6': [3,0,7,4, 11,15,19,16]   # Left face
        },
        'C3D20R': {  # Hexahedral element with 20 nodes
            'S1': [0,1,2,3, 8,9,10,11],  # Bottom face
            'S2': [4,5,6,7, 12,13,14,15],  # Top face
            'S3': [0,1,4,5, 8,12,16,17],  # Front face
            'S4': [1,2,5,6, 9,13,17,18],  # Right face
            'S5': [2,3,6,7, 10,14,18,19],  # Back face
            'S6': [3,0,7,4, 11,15,19,16]   # Left face
        },
        'C3D15': {  # Wedge element with 15 nodes
            'S1': [0,1,2, 6,7,8],      # Bottom triangular face
            'S2': [3,4,5, 9,10,11],    # Top triangular face
            'S3': [0,1,4,3, 6,13,9,12], # Front quadrilateral face
            'S4': [1,2,5,4, 7,14,10,13], # Right quadrilateral face
            'S5': [0,3,5,2, 12,11,14,8]  # Back quadrilateral face
        },
        'C3D10': {  # Tetrahedral element with 10 nodes
            'S1': [0, 1, 2, 4, 5, 6],      # Face 1: nodes 1-2-3 and mid-edge nodes
            'S2': [0, 1, 3, 4, 8, 7],      # Face 2: nodes 1-2-4 and mid-edge nodes
            'S3': [1, 2, 3, 5, 9, 8],      # Face 3: nodes 2-3-4 and mid-edge nodes
            'S4': [0, 2, 3, 6, 9, 7]       # Face 4: nodes 1-3-4 and mid-edge nodes
        },
        'C3D4': {  # Tetrahedral element with 4 nodes
            'S1': [0,1,2],
            'S2': [0,3,1],
            'S3': [1,3,2],
            'S4': [0,2,3]
        }
    }
    
    # Get face definitions for this element type
    if element_type not in face_nodes:
        print(f"[WARNING] Unknown element type: {element_type}")
        return []
    
    face_definitions = face_nodes[element_type]
    interface_faces = []
    
    # Calculate required number of nodes at interface based on element type
    required_nodes = {
        'C3D20': 6,  # At least 6 out of 8 nodes for hexahedral faces
        'C3D20R': 6,  # At least 6 out of 8 nodes for hexahedral faces
        'C3D15': 6,  # At least 4 out of 6 nodes for triangular faces or 6 out of 8 for quadrilateral faces
        'C3D10': 4,  # At least 4 out of 6 nodes for tetrahedral faces
        'C3D4': 3    # At least 3 out of 3 nodes for tetrahedral faces
    }
    
    # Calculate cylinder axis points if direction and height are provided
    if direction and height:
                
        # Define cylinder endpoints consistently with other functions
        # We define bottom_point at position b=0 and top_point at position b=1
        bottom_point = [
            center[0] + direction[0]*((chamfer[0]*1.05)+(height/2)),  # Bottom = center + direction*height/2
            center[1] + direction[1]*((chamfer[0]*1.05)+(height/2)) if len(center) > 2 else direction[1]*height/2,
            center[2] + direction[2]*((chamfer[0]*1.05)+(height/2)) if len(center) > 2 else direction[2]*height/2
        ]
        
        top_point = [
            center[0] - direction[0]*height/1.96,  # Top = center - direction*height/2
            center[1] - direction[1]*height/1.96 if len(center) > 2 else -direction[1]*height/2,
            center[2] - direction[2]*height/1.96 if len(center) > 2 else -direction[2]*height/2
        ]
    
    # For each face in the element
    for face, node_indices in face_definitions.items():
        # Get coordinates for nodes in this face
        face_coords = [nodes_3d[element_nodes[i]] for i in node_indices]
        
        # Calculate radii for all nodes in this face
        face_radii = []
        face_y_values = []
        face_positions = []  # For position along cylinder axis
        
        for coords in face_coords:
            x, y, z = coords
            if target_radius == 0:
                # Different handling based on whether we have direction/height or y_range
                if direction and height:
                    # Calculate distance to cylinder axis using point-to-line distance
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

                    # Skip if point is outside cylinder height bounds
                    if not (0 <= b <= 1):
                        continue

                    # Calculate distance from node to axis
                    r = math.sqrt(sum((p0[i] - pb[i])**2 for i in range(3)))

                    face_radii.append(r)
                    face_positions.append(b)  # Store position along axis (0=bottom, 1=top)
                    face_y_values.append(y)  # Still store y for compatibility
            # Check for interface with target radius
            else:
                # Calculate distance to cylinder axis using point-to-line distance
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

                # Calculate distance from node to axis
                r = math.sqrt(sum((p0[i] - pb[i])**2 for i in range(3)))

                face_radii.append(r)
                face_positions.append(b)  # Store position along axis (0=bottom, 1=top)
                face_y_values.append(y)  # Still store y for compatibility

                # Check for nodes between searching_radius and target_radius
                min_r = min(searching_radius, target_radius)
                max_r = max(searching_radius, target_radius)

                if direction and height:
                    # For angled cylinder, check bottom interface (position near 0.0)
                    # Explicitly exclude positions near 1.0 (top) to prevent wrong selection
                    nodes_at_interface = sum(
                        1 for r, pos, yf in zip(face_radii, face_positions, face_y_values)
                        if ((min_r + 0.5 <= r <= max_r + tolerance) and (1e-6 <= pos <= tolerance) and (pos < 0.9))
                    )
                    
                    required_nodes_at_interface = required_nodes[element_type] if element_type == "C3D10" else 8
                    if nodes_at_interface >= required_nodes_at_interface:
                        interface_faces.append(face)
                        continue
        
        # Count nodes at interface radius
        nodes_at_interface = 0
        
        # Original behavior - just check searching_radius
        nodes_at_interface = sum(1 for r in face_radii if (abs(r - searching_radius) < tolerance))
        
        # Check if enough nodes are at interface
        if nodes_at_interface >= required_nodes[element_type] and target_radius == 0:
            interface_faces.append(face)
    
    return interface_faces

def format_interface_surfaces(nodes_3d, elements, searching_radius, target_radius, set_name, center=(0, 0), direction=None, height=None, y_range=None, chamfer=[0.0, 0.0], tolerance=0.015):
    """Write interface surface definitions with specific face designations
    
    Parameters:
    - nodes_3d: Dictionary of node coordinates {node_id: [x, y, z]}
    - elements: Dictionary of original elements {elem_id: {'type': element_type, 'nodes': [node_ids]}}
    - searching_radius: The real radius where elements are located (original_radius + extra_radius)
    - target_radius: The target radius up to which we want to extrude
    - set_name: Base name for the surface sets
    - center: Coordinates of the cylinder center
    - direction: Direction vector of the cylinder axis [dx, dy, dz]
    - height: Height of the cylinder
    - y_range: Optional y-range for the cylinder
    - chamfer: Chamfer parameters [chamfer_x, chamfer_z]
    - tolerance: Tolerance for radius comparison
    
    Returns:
    - Dictionary of face elements organized by face designation
    """
    # Validate chamfer parameters
    if chamfer is None or not isinstance(chamfer, list) or len(chamfer) < 2:
        chamfer = [0.0, 0.0]
    # Initialize face elements dictionary for each element type
    face_elements = {
        'original': {'S1': [], 'S2': [], 'S3': [], 'S4': [], 'S5': [], 'S6': []},
    }

    # Normalize direction vector
    dir_length = math.sqrt(sum(d*d for d in direction))
    if dir_length < 1e-10:  # Avoid division by zero
        direction = [0, -1, 0]  # Default to Y-axis if direction is too small
    else:
        direction = [d/dir_length for d in direction]

    # Defensive type check
    if not isinstance(elements, dict):
        raise TypeError(f"elements must be a dictionary, got {type(elements)}")
    # Process original elements
    for elem_id, element_data in elements.items():
        element_type = element_data['type']
        element_nodes = element_data['nodes']
        # Determine interface faces for this element
        interface_faces = determine_interface_faces(
            element_nodes, nodes_3d, searching_radius, target_radius, 
            element_type, center=center, direction=direction, height=height, chamfer=chamfer, tolerance=tolerance)
        # Add element to appropriate face lists
        for face in interface_faces:
            face_elements['original'][face].append(elem_id)


    print(f"[INFO] Created surfaces for {set_name}:")
    print(f"[INFO] - Original elements: {sum(len(faces) for faces in face_elements['original'].values())} faces")
    return face_elements 

def parse_gmsh_script(script_path):
    """Parse a GMSH script to extract points and lines."""
    points = []
    lines = []
    arc_data = {}  # Maps line index to (center_idx, is_arc) tuple
    point_count = 0
    line_count = 0
    point_index = 0
    point_map = {}
    variables = {}
    # Dictionary to store variable values for expression evaluation
    var_values = {}
    
    # Create a safe math environment for evaluating expressions
    safe_math = {
        'sin': math.sin,
        'cos': math.cos,
        'tan': math.tan,
        'asin': math.asin,
        'acos': math.acos,
        'atan': math.atan,
        'atan2': math.atan2,
        'pi': math.pi,
        'sqrt': math.sqrt,
        'pow': math.pow,
        'abs': abs,
        'max': max,
        'min': min,
        'round': round,
        'floor': math.floor,
        'ceil': math.ceil,
        'exp': math.exp,
        'log': math.log,
        'log10': math.log10
    }
    
    # Variables to track model transformations
    model_translation = [0, 0, 0]
    is_quarter_rotation = False
    translation_comment_found = False
    reading_translation_values = False
    translation_value_index = 0
    
    if not script_path or not os.path.exists(script_path):
        print(f"Error: Script path '{script_path}' does not exist")
        return [], [], {}, model_translation, is_quarter_rotation
    
    try:
        print(f"Parsing GMSH script: {script_path}")
        
        # Read the script file
        with open(script_path, 'r') as f:
            script_lines = f.readlines()
            
        # First look for translation values in comments at the beginning
        for i, line in enumerate(script_lines):
            line = line.strip()
            
            # Look for the translation comment marker
            if "# next lines describe the center position" in line:
                translation_comment_found = True
                reading_translation_values = True
                continue
                
            # If we're reading translation values from comments
            if reading_translation_values and line.startswith('#'):
                try:
                    # Extract the numerical value from the comment
                    value_str = line[1:].strip()
                    if value_str:
                        value = float(value_str)
                        model_translation[translation_value_index] = value
                        translation_value_index += 1
                        if translation_value_index >= 3:
                            reading_translation_values = False
                except ValueError as e:
                    print(f"Warning: Could not parse translation value from comment: {line}, Error: {str(e)}")
                    # If we can't parse this line, stop reading translation values
                    if translation_value_index < 3:
                        print(f"Only read {translation_value_index} translation values")
                    reading_translation_values = False
                continue
                
            # Look for rotation operations that indicate a quarter rotation
            if "rotate" in line and "-math.pi/2" in line:
                is_quarter_rotation = True
                
            # First pass: collect variable assignments
            if '=' in line and not line.startswith('#') and not line.startswith('gmsh.') and not 'addPoint' in line and not 'addLine' in line and not 'addCircleArc' in line:
                try:
                    var_name, var_expr = line.split('=', 1)
                    var_name = var_name.strip()
                    var_expr = var_expr.strip()
                    
                    # Remove trailing comments if any
                    if '#' in var_expr:
                        var_expr = var_expr.split('#', 1)[0].strip()
                    
                    # Try to evaluate the expression
                    try:
                        # Use existing variables in the evaluation context
                        eval_context = {**safe_math, **var_values}
                        var_value = eval(var_expr, {"__builtins__": {}}, eval_context)
                        var_values[var_name] = var_value
                        print(f"Parsed variable: {var_name} = {var_value}")
                    except Exception as e:
                        print(f"Warning: Could not evaluate variable expression: {var_expr}, Error: {str(e)}")
                except Exception as e:
                    print(f"Warning: Could not parse variable assignment: {line}, Error: {str(e)}")
                
        # Process the script lines (for points and lines)
        for i, line in enumerate(script_lines):
            line = line.strip()
            
            # Skip comments and empty lines
            if not line or line.startswith('#'):
                continue
            
            # Process point definitions
            if 'addPoint' in line:
                try:
                    # Extract point coordinates from addPoint call
                    if '(' in line and ')' in line:
                        # Extract variable name and coordinates
                        var_name = line.split('=')[0].strip()
                        params_part = line.split('(', 1)[1].split(')', 1)[0]
                        params = [p.strip() for p in params_part.split(',')]
                        
                        if len(params) >= 3:  # Need at least x, y, z
                            try:
                                # Try to evaluate each parameter as an expression
                                x_expr, y_expr, z_expr = params[0], params[1], params[2]
                                
                                # Create evaluation context with math functions and variables
                                eval_context = {**safe_math, **var_values}
                                
                                # Evaluate expressions using the collected variables
                                try:
                                    x = float(eval(x_expr, {"__builtins__": {}}, eval_context))
                                except Exception as e:
                                    print(f"Warning: Could not evaluate x coordinate expression: {x_expr}, Error: {str(e)}")
                                    x = 0.0
                                    
                                try:
                                    y = float(eval(y_expr, {"__builtins__": {}}, eval_context))
                                except Exception as e:
                                    print(f"Warning: Could not evaluate y coordinate expression: {y_expr}, Error: {str(e)}")
                                    y = 0.0
                                    
                                try:
                                    z = float(eval(z_expr, {"__builtins__": {}}, eval_context))
                                except Exception as e:
                                    print(f"Warning: Could not evaluate z coordinate expression: {z_expr}, Error: {str(e)}")
                                    z = 0.0
                                
                                points.append((x, y, z))
                                point_index = len(points) - 1
                                
                                # Store variable mapping
                                variables[var_name] = point_index
                                point_count += 1
                            except Exception as e:
                                print(f"Warning: Could not parse point coordinates: {params[:3]}, Error: {str(e)}")
                except Exception as e:
                    print(f"Warning: Could not parse point definition: {line}, Error: {str(e)}")
                
            # Process line definitions
            elif 'addLine' in line:
                try:
                    # Extract line endpoints from addLine call
                    if '(' in line and ')' in line:
                        # Extract variable name and endpoints
                        var_name = line.split('=')[0].strip()
                        params_part = line.split('(', 1)[1].split(')', 1)[0]
                        params = [p.strip() for p in params_part.split(',')]
                        
                        if len(params) >= 2:  # Need start and end points
                            start_var = params[0]
                            end_var = params[1]
                            
                            # Resolve variable references
                            if start_var in variables and end_var in variables:
                                start_idx = variables[start_var]
                                end_idx = variables[end_var]
                                
                                lines.append((start_idx, end_idx))
                                line_index = len(lines) - 1
                                
                                # Store variable mapping
                                variables[var_name] = line_index
                                line_count += 1
                            else:
                                print(f"Warning: Could not resolve line endpoints: {start_var}, {end_var}")
                except Exception as e:
                    print(f"Warning: Could not parse line definition: {line}, Error: {str(e)}")
            
            # Process circle arc definitions
            elif 'addCircleArc' in line:
                try:
                    # Extract arc points from addCircleArc call
                    if '(' in line and ')' in line:
                        # Extract variable name and points
                        var_name = line.split('=')[0].strip()
                        params_part = line.split('(', 1)[1].split(')', 1)[0]
                        params = [p.strip() for p in params_part.split(',')]
                        
                        if len(params) >= 3:  # Need start, center, and end points
                            start_var = params[0]
                            center_var = params[1]
                            end_var = params[2]
                            
                            # Resolve variable references
                            if start_var in variables and center_var in variables and end_var in variables:
                                start_idx = variables[start_var]
                                center_idx = variables[center_var]
                                end_idx = variables[end_var]

                                # Add the line with start and end points
                                lines.append((start_idx, end_idx))
                                line_index = len(lines) - 1
                                
                                # Store the arc data
                                arc_data[line_index] = (start_idx, center_idx, end_idx)
                                
                                # Store variable mapping
                                variables[var_name] = line_index
                                line_count += 1
                            else:
                                print(f"Warning: Could not resolve arc points: {start_var}, {center_var}, {end_var}")
                except Exception as e:
                    print(f"Warning: Could not parse arc definition: {line}, Error: {str(e)}")
        
        print(f"Parsed {point_count} points and {line_count} lines and {len(arc_data)} arcs from GMSH script")
        print(f"Model translation from GMSH comments: {model_translation}")
        print(f"Is quarter rotation: {is_quarter_rotation}")
        return points, lines, arc_data, model_translation, is_quarter_rotation
    except Exception as e:
        print(f"Error parsing GMSH script: {str(e)}")
        import traceback
        traceback.print_exc()
        return [], [], {}, [0, 0, 0], False
    
def parse_gmsh_script_extrapol(script_path):
    """Parse a GMSH script to extract points and lines."""
    points = []
    lines = []
    arc_data = {}  # Maps line index to (start_idx, center_idx, end_idx) tuple
    point_count = 0
    line_count = 0
    point_index = 0
    point_map = {}
    variables = {}
    # Dictionary to store variable values for expression evaluation
    var_values = {}
    
    # Create a safe math environment for evaluating expressions
    safe_math = {
        'sin': math.sin,
        'cos': math.cos,
        'tan': math.tan,
        'asin': math.asin,
        'acos': math.acos,
        'atan': math.atan,
        'atan2': math.atan2,
        'pi': math.pi,
        'sqrt': math.sqrt,
        'pow': math.pow,
        'abs': abs,
        'max': max,
        'min': min,
        'round': round,
        'floor': math.floor,
        'ceil': math.ceil,
        'exp': math.exp,
        'log': math.log,
        'log10': math.log10,
        'math': math,
        'np': np
    }
    
    # Variables to track model transformations
    model_translation = [0, 0, 0]
    is_quarter_rotation = False
    translation_comment_found = False
    reading_translation_values = False
    translation_value_index = 0
    
    if not script_path or not os.path.exists(script_path):
        print(f"Error: Script path '{script_path}' does not exist")
        return [], [], {}, model_translation, is_quarter_rotation
    
    try:
        print(f"Parsing GMSH script: {script_path}")
        
        # Read the script file
        with open(script_path, 'r') as f:
            script_lines = f.readlines()
            
        # First look for translation values in comments at the beginning
        for i, line in enumerate(script_lines):
            line = line.strip()
            
            # Look for the translation comment marker
            if "# next lines describe the center position" in line:
                translation_comment_found = True
                reading_translation_values = True
                continue
                
            # If we're reading translation values from comments
            if reading_translation_values and line.startswith('#'):
                try:
                    # Extract the numerical value from the comment
                    value_str = line[1:].strip()
                    if value_str:
                        value = float(value_str)
                        model_translation[translation_value_index] = value
                        translation_value_index += 1
                        if translation_value_index >= 3:
                            reading_translation_values = False
                except ValueError as e:
                    print(f"Warning: Could not parse translation value from comment: {line}, Error: {str(e)}")
                    # If we can't parse this line, stop reading translation values
                    if translation_value_index < 3:
                        print(f"Only read {translation_value_index} translation values")
                    reading_translation_values = False
                continue
                
            # Look for rotation operations that indicate a quarter rotation
            if "rotate" in line and ("-math.pi/2" in line or "-np.pi/2" in line):
                is_quarter_rotation = True
                
        # First pass: collect variable assignments including dictionary definitions
        for i, line in enumerate(script_lines):
            line = line.strip()
            
            # Skip comments and empty lines
            if not line or line.startswith('#'):
                continue
                
            # Handle dictionary assignments (like p_orig = {...})
            if '=' in line and '{' in line and '}' in line and not line.startswith('gmsh.'):
                try:
                    var_name = line.split('=')[0].strip()
                    if var_name.endswith(']'):  # Array indexing like points_calc['p1']
                        dict_name = var_name.split('[')[0].strip()
                        if "'" in var_name or '"' in var_name:
                            # Extract key from quotes
                            key_start = var_name.find("'") if "'" in var_name else var_name.find('"')
                            key_end = var_name.find("'", key_start + 1) if "'" in var_name else var_name.find('"', key_start + 1)
                            dict_key = var_name[key_start + 1:key_end]
                        else:
                            # Handle np.array assignments
                            continue
                    else:
                        dict_name = var_name
                        
                    # Extract dictionary content
                    if '{' in line and '}' in line:
                        dict_content = line[line.find('{'):line.rfind('}') + 1]
                        # Try to evaluate the dictionary
                        eval_context = {**safe_math, 'array': np.array, 'np': np}
                        dict_value = eval(dict_content, {"__builtins__": {}}, eval_context)
                        var_values[dict_name] = dict_value
                        print(f"Parsed dictionary: {dict_name} = {dict_value}")
                except Exception as e:
                    print(f"Warning: Could not parse dictionary assignment: {line}, Error: {str(e)}")
                    continue
                    
            # Handle regular variable assignments
            elif '=' in line and not line.startswith('#') and not line.startswith('gmsh.') and not 'addPoint' in line and not 'addLine' in line and not 'addCircleArc' in line:
                try:
                    var_name, var_expr = line.split('=', 1)
                    var_name = var_name.strip()
                    var_expr = var_expr.strip()
                    
                    # Remove trailing comments if any
                    if '#' in var_expr:
                        var_expr = var_expr.split('#', 1)[0].strip()
                    
                    # Try to evaluate the expression
                    try:
                        # Use existing variables in the evaluation context
                        eval_context = {**safe_math, **var_values}
                        var_value = eval(var_expr, {"__builtins__": {}}, eval_context)
                        var_values[var_name] = var_value
                        print(f"Parsed variable: {var_name} = {var_value}")
                    except Exception as e:
                        print(f"Warning: Could not evaluate variable expression: {var_expr}, Error: {str(e)}")
                except Exception as e:
                    print(f"Warning: Could not parse variable assignment: {line}, Error: {str(e)}")
        
        # Second pass: collect point definitions
        for i, line in enumerate(script_lines):
            line = line.strip()
            
            # Skip comments and empty lines
            if not line or line.startswith('#'):
                continue
            
            # Process point definitions (handle both direct and dictionary-based)
            if 'addPoint' in line:
                try:
                    # Extract point coordinates from addPoint call
                    if '(' in line and ')' in line:
                        # Extract variable name
                        var_name = line.split('=')[0].strip()
                        
                        # Extract parameters
                        params_part = line.split('(', 1)[1].split(')', 1)[0]
                        params = [p.strip() for p in params_part.split(',')]
                        
                        if len(params) >= 3:  # Need at least x, y, z
                            try:
                                # Try to evaluate each parameter as an expression
                                x_expr, y_expr, z_expr = params[0], params[1], params[2]
                                
                                # Create evaluation context with math functions and variables
                                eval_context = {**safe_math, **var_values}
                                
                                # Handle dictionary lookups like p_orig['p1']
                                if '[' in x_expr and ']' in x_expr:
                                    dict_name = x_expr.split('[')[0].strip()
                                    if "'" in x_expr or '"' in x_expr:
                                        key_start = x_expr.find("'") if "'" in x_expr else x_expr.find('"')
                                        key_end = x_expr.find("'", key_start + 1) if "'" in x_expr else x_expr.find('"', key_start + 1)
                                        dict_key = x_expr[key_start + 1:key_end]
                                        if dict_name in var_values and dict_key in var_values[dict_name]:
                                            x = float(var_values[dict_name][dict_key][0])
                                        else:
                                            x = 0.0
                                    else:
                                        x = 0.0
                                else:
                                    x = float(eval(x_expr, {"__builtins__": {}}, eval_context))
                                    
                                if '[' in y_expr and ']' in y_expr:
                                    dict_name = y_expr.split('[')[0].strip()
                                    if "'" in y_expr or '"' in y_expr:
                                        key_start = y_expr.find("'") if "'" in y_expr else y_expr.find('"')
                                        key_end = y_expr.find("'", key_start + 1) if "'" in y_expr else y_expr.find('"', key_start + 1)
                                        dict_key = y_expr[key_start + 1:key_end]
                                        if dict_name in var_values and dict_key in var_values[dict_name]:
                                            y = float(var_values[dict_name][dict_key][1])
                                        else:
                                            y = 0.0
                                    else:
                                        y = 0.0
                                else:
                                    y = float(eval(y_expr, {"__builtins__": {}}, eval_context))
                                    
                                if '[' in z_expr and ']' in z_expr:
                                    dict_name = z_expr.split('[')[0].strip()
                                    if "'" in z_expr or '"' in z_expr:
                                        key_start = z_expr.find("'") if "'" in z_expr else z_expr.find('"')
                                        key_end = z_expr.find("'", key_start + 1) if "'" in z_expr else z_expr.find('"', key_start + 1)
                                        dict_key = z_expr[key_start + 1:key_end]
                                        if dict_name in var_values and dict_key in var_values[dict_name]:
                                            z = float(var_values[dict_name][dict_key][2])
                                        else:
                                            z = 0.0
                                    else:
                                        z = 0.0
                                else:
                                    z = float(eval(z_expr, {"__builtins__": {}}, eval_context))
                                
                                points.append((x, y, z))
                                point_index = len(points) - 1
                                
                                # Store variable mapping
                                variables[var_name] = point_index
                                point_count += 1
                                print(f"Added point {var_name}: ({x}, {y}, {z})")
                            except Exception as e:
                                print(f"Warning: Could not parse point coordinates: {params[:3]}, Error: {str(e)}")
                except Exception as e:
                    print(f"Warning: Could not parse point definition: {line}, Error: {str(e)}")
                
            # Process line definitions
            elif 'addLine' in line:
                try:
                    # Extract line endpoints from addLine call
                    if '(' in line and ')' in line:
                        # Extract variable name
                        var_name = line.split('=')[0].strip()
                        params_part = line.split('(', 1)[1].split(')', 1)[0]
                        params = [p.strip() for p in params_part.split(',')]
                        
                        if len(params) >= 2:  # Need start and end points
                            start_var = params[0]
                            end_var = params[1]
                            
                            # Resolve variable references
                            if start_var in variables and end_var in variables:
                                start_idx = variables[start_var]
                                end_idx = variables[end_var]
                                
                                lines.append((start_idx, end_idx))
                                line_index = len(lines) - 1
                                
                                # Store variable mapping
                                variables[var_name] = line_index
                                line_count += 1
                                print(f"Added line {var_name}: {start_idx} -> {end_idx}")
                            else:
                                print(f"Warning: Could not resolve line endpoints: {start_var}, {end_var}")
                except Exception as e:
                    print(f"Warning: Could not parse line definition: {line}, Error: {str(e)}")
            
            # Process circle arc definitions
            elif 'addCircleArc' in line:
                try:
                    # Extract arc points from addCircleArc call
                    if '(' in line and ')' in line:
                        # Extract variable name
                        var_name = line.split('=')[0].strip()
                        params_part = line.split('(', 1)[1].split(')', 1)[0]
                        params = [p.strip() for p in params_part.split(',')]
                        
                        if len(params) >= 3:  # Need start, center, and end points
                            start_var = params[0]
                            center_var = params[1]
                            end_var = params[2]
                            
                            # Resolve variable references
                            if start_var in variables and center_var in variables and end_var in variables:
                                start_idx = variables[start_var]
                                center_idx = variables[center_var]
                                end_idx = variables[end_var]

                                # Add the line with start and end points
                                lines.append((start_idx, end_idx))
                                line_index = len(lines) - 1
                                
                                # Store the arc data (start, center, end indices)
                                arc_data[line_index] = (start_idx, center_idx, end_idx)
                                
                                # Store variable mapping
                                variables[var_name] = line_index
                                line_count += 1
                                print(f"Added arc {var_name}: {start_idx} -> {center_idx} -> {end_idx}")
                            else:
                                print(f"Warning: Could not resolve arc points: {start_var}, {center_var}, {end_var}")
                except Exception as e:
                    print(f"Warning: Could not parse arc definition: {line}, Error: {str(e)}")
        
        print(f"Parsed {point_count} points and {line_count} lines and {len(arc_data)} arcs from GMSH script")
        print(f"Model translation from GMSH comments: {model_translation}")
        print(f"Is quarter rotation: {is_quarter_rotation}")
        return points, lines, arc_data, model_translation, is_quarter_rotation
    except Exception as e:
        print(f"Error parsing GMSH script: {str(e)}")
        import traceback
        traceback.print_exc()
        return [], [], {}, [0, 0, 0], False
    
def read_inp_file(filename, max_node_id=0, max_elem_id=0, existing_sets=None, part_name="", fullfile=False):
    """Read nodes, elements, and sets from an INP file with case-insensitive section detection
    
    Parameters:
    - filename: Path to the INP file
    - max_node_id: Maximum node ID to offset imported nodes (default: 0)
    - max_elem_id: Maximum element ID to offset imported elements (default: 0)
    - existing_sets: Dictionary containing existing sets to append to (default: None)
    - part_name: Prefix for set names (default: "")
    - fullfile: If True, return more comprehensive data needed for mirroring (default: False)
    
    Returns:
    - If fullfile=False: nodes, elements, existing_sets
    - If fullfile=True: nodes, elements, existing_sets, node_sets, element_sets, element_set_references, other_sections, model_parts
      where other_sections is a dictionary grouping additional INP sections
      (springs, orientations, materials, constraints, amplitudes, etc.)
    """
    # Define expected node counts for different element types
    ELEMENT_NODE_COUNTS = {
        'C3D4': 4,      # 4-node tetrahedral
        'C3D6': 6,      # 6-node triangular prism
        'C3D8': 8,      # 8-node hexahedral
        'C3D10': 10,    # 10-node tetrahedral (with mid-side nodes)
        'C3D15': 15,    # 15-node triangular prism (with mid-side nodes)
        'C3D20': 20,    # 20-node hexahedral (with mid-side nodes)
        'C3D20R': 20,   # 20-node reduced integration hexahedral
        'C3D27': 27,    # 27-node hexahedral (with all mid-side and center nodes)
        # Add other common element types as needed
        'S3': 3,        # 3-node triangular shell
        'S4': 4,        # 4-node quadrilateral shell
        'S6': 6,        # 6-node triangular shell
        'S8': 8,        # 8-node quadrilateral shell
        'M3D3': 3,      # 3-node membrane
        'M3D4': 4,      # 4-node membrane
    }
    nodes = {}
    elements = {}  # Changed to dictionary with element_id as key
    surfaces = {}  # Dictionary to store surface definitions
    
    # Initialize existing_sets if None
    if existing_sets is None:
        existing_sets = {'nsets': {}, 'elsets': {}, 'surfaces': {}}
    
    print(f"\n[DEBUG] Opening file: {filename}")
    try:
        with open(filename, 'r') as f:
            lines = f.readlines()
        print(f"[DEBUG] Successfully read {len(lines)} lines from file")
    except Exception as e:
        print(f"[ERROR] Failed to open file: {e}")
        if fullfile:
            return {}, {}, {}, {}, {}, {}, [], {}
        else:
            return {}, {}, {}
    
    in_node_section = False
    in_element_section = False
    in_nset_section = False
    in_elset_section = False
    in_surface_section = False
    current_set_name = ""
    current_set = []
    current_element_type = ""
    current_elset = ""
    current_surface_name = ""
    current_surface_faces = []
    
    # Dictionary to track elements by type
    elements_by_type = {}
    
    # Additional data structures for fullfile mode
    node_sets = {}
    element_sets = {}
    element_set_references = {}
    # Store additional, non-mesh sections verbatim when fullfile=True
    other_sections = {
        'spring_elements': [],
        'spring_definitions': [],
        'orientations': [],
        'materials': [],
        'solid_sections': [],
        'constraints': [],
        'surface_interactions': [],
        'amplitudes': [],
        'contact_pairs': [],
        'raw': []
    }
    current_section = None
    section_header = None
    section_lines = []
    
    # Track parts and their associated elements and nodes
    model_parts = {}  # {part_name: {'type': element_type, 'elements': [elem_ids], 'nodes': set(node_ids)}}
    current_part_name = ""
    
    node_count = 0
    element_count = 0
    error_count = 0
    
    # Element validation counters
    valid_elements = 0
    invalid_elements = 0
    unknown_element_types = set()
    validated_elements = set()  # Track which elements have been validated
    
    def validate_element_nodes(elem_type, node_list):
        """Validate that an element has the correct number of nodes for its type"""
        if elem_type not in ELEMENT_NODE_COUNTS:
            unknown_element_types.add(elem_type)
            return True  # Allow unknown types to pass for now
        
        expected_nodes = ELEMENT_NODE_COUNTS[elem_type]
        actual_nodes = len(node_list)
        
        if actual_nodes != expected_nodes:
            print(f"[WARNING] Element type {elem_type} has {actual_nodes} nodes, expected {expected_nodes}")
            return False
        return True
    
    # Helpers to capture non-mesh sections (only when fullfile=True)
    active_other_section = None
    active_other_header = ""
    active_other_lines = []

    def detect_other_section(line_upper):
        """Identify non-mesh section types we want to preserve verbatim."""
        if line_upper.startswith('*ORIENTATION'):
            return 'orientations'
        if line_upper.startswith('*SPRING'):
            return 'spring_definitions'
        if line_upper.startswith('*ELEMENT') and 'SPRING' in line_upper:
            return 'spring_elements'
        if line_upper.startswith('*MATERIAL'):
            return 'materials'
        if line_upper.startswith('*SOLID SECTION'):
            return 'solid_sections'
        if line_upper.startswith('*TIE') or line_upper.startswith('*RIGID BODY'):
            return 'constraints'
        if line_upper.startswith('*SURFACE INTERACTION'):
            return 'surface_interactions'
        if line_upper.startswith('*AMPLITUDE'):
            return 'amplitudes'
        if line_upper.startswith('*CONTACT PAIR'):
            return 'contact_pairs'
        return None

    def finalize_other_section():
        nonlocal active_other_section, active_other_header, active_other_lines
        if active_other_section:
            other_sections[active_other_section].append({
                'header': active_other_header,
                'lines': active_other_lines.copy()
            })
            active_other_section = None
            active_other_header = ""
            active_other_lines = []

    def start_other_section(section_type, header_line):
        nonlocal active_other_section, active_other_header, active_other_lines
        finalize_other_section()
        active_other_section = section_type
        active_other_header = header_line.strip()
        active_other_lines = []
    
    print(f"[DEBUG] Starting to parse file sections")
    
    i = 0
    while i < len(lines):
        line = lines[i].strip()
        if not line:
            i += 1
            continue
            
        # Use case-insensitive comparisons for all section markers
        line_upper = line.lstrip().upper()

        # If we are already capturing a non-mesh section, keep collecting lines until a new section starts
        if fullfile and active_other_section:
            new_section_type = detect_other_section(line_upper) if line_upper.startswith('*') else None
            is_mesh_section = (
                (line_upper.startswith('*NODE') and 'NSET' not in line_upper) or
                line_upper.startswith('*ELEMENT') or
                '*NSET' in line_upper or
                '*ELSET' in line_upper or
                '*SURFACE' in line_upper
            )

            if is_mesh_section or new_section_type:
                finalize_other_section()
                # If the new section is also a captured type, start capturing it and skip normal parsing
                if new_section_type:
                    start_other_section(new_section_type, line)
                    # Only spring elements should still be parsed as elements
                    if new_section_type != 'spring_elements':
                        i += 1
                        continue
                # Fall through for mesh sections so normal parsing can occur
            else:
                active_other_lines.append(line)
                if active_other_section != 'spring_elements':
                    i += 1
                    continue

        # Start capturing recognised non-mesh sections (fullfile only)
        detected_section = detect_other_section(line_upper) if fullfile else None
        if detected_section:
            start_other_section(detected_section, line)
            # Let spring elements continue through normal element parsing too
            if detected_section != 'spring_elements':
                i += 1
                continue
            
        # Check for node section with case-insensitive matching
        if line_upper.startswith('*NODE') and 'NSET' not in line_upper:
            # Save any current set before moving to new section
            if in_nset_section and current_set:
                existing_sets['nsets'][current_set_name]['nodes'] = current_set
                if fullfile:
                    node_sets[current_set_name] = current_set.copy()
                current_set = []
                
            # For fullfile mode, save previous section if any
            if fullfile and current_section and current_section != 'NODE' and section_lines:
                other_sections['raw'].append((section_header, section_lines))
                
            in_node_section = True
            in_element_section = False
            in_nset_section = False
            in_elset_section = False
            in_surface_section = False
            
            # For fullfile mode
            if fullfile:
                current_section = 'NODE'
                section_header = line
                section_lines = []
                
            i += 1
            continue
            
        elif line_upper.startswith('*ELEMENT'):
            # Save any current set before moving to new section
            if in_nset_section and current_set:
                existing_sets['nsets'][current_set_name]['nodes'] = current_set
                if fullfile:
                    node_sets[current_set_name] = current_set.copy()
                    
            # For fullfile mode, save previous section if any
            if fullfile and current_section and current_section != 'ELEMENT' and section_lines:
                other_sections['raw'].append((section_header, section_lines))
                
            in_node_section = False
            in_element_section = True
            in_nset_section = False
            in_elset_section = False
            in_surface_section = False
            
            # For fullfile mode
            if fullfile:
                current_section = 'ELEMENT'
                section_header = line
                section_lines = []
            
            # Extract element type from declaration (case-insensitive)
            if "TYPE=" in line_upper:
                # Extract the element type, preserving original case for compatibility
                type_start = line_upper.find("TYPE=") + 5
                # Find the end of the type (either comma or end of line)
                if "," in line[type_start:]:
                    type_end = line[type_start:].find(",") + type_start
                else:
                    type_end = len(line)
                
                current_element_type = line[type_start:type_end].strip()
                
                # Extract ELSET if present (part name)
                current_elset = ""
                current_part_name = ""
                if "ELSET=" in line_upper:
                    elset_start = line_upper.find("ELSET=") + 6
                    if "," in line[elset_start:]:
                        elset_end = line[elset_start:].find(",") + elset_start
                    else:
                        elset_end = len(line)
                    
                    # Get the part name without adding prefix
                    current_part_name = line[elset_start:elset_end].strip()
                    
                    # Create the element set name with prefix
                    current_elset = f"ELSET_{part_name}_{current_part_name}"
                    
                    # Initialize the part in our tracking dictionary if not exists
                    if fullfile and current_part_name:
                        if current_part_name not in model_parts:
                            model_parts[current_part_name] = {
                                'type': current_element_type,
                                'elements': [],
                                'nodes': set(),
                                'elsets': set(),
                                'nsets': set()
                            }
                    
                    # Check if this elset name already exists in existing_sets
                    if current_elset in existing_sets['elsets']:
                        # Append a suffix to make the name unique
                        original_elset = current_elset
                        current_elset = f"{current_elset}_imp"
                        print(f"[DEBUG] Renamed existing elset '{original_elset}' to '{current_elset}'")
                        existing_sets['elsets'][current_elset] = {
                            'declaration': line.strip(),
                            'elements': []
                        }
                    else:
                        existing_sets['elsets'][current_elset] = {
                            'declaration': line.strip(),
                            'elements': []
                        }
            else:
                print(f"[WARNING] Element section at line {i+1} has no TYPE specified")
            
            i += 1
            continue
            
        elif '*NSET' in line_upper:
            # Save any current set before moving to new section
            if in_nset_section and current_set:
                existing_sets['nsets'][current_set_name]['nodes'] = current_set
                if fullfile:
                    node_sets[current_set_name] = current_set.copy()
                    
            # For fullfile mode, save previous section if any
            if fullfile and current_section and current_section != 'NSET' and section_lines:
                other_sections['raw'].append((section_header, section_lines))
                
            in_node_section = False
            in_element_section = False
            in_nset_section = True
            in_elset_section = False
            in_surface_section = False
            
            # For fullfile mode
            if fullfile:
                current_section = 'NSET'
                section_header = line
                section_lines = []
            
            # Extract set name and declaration (case-insensitive)
            if "=" in line:
                # Find the name after "nset=" or "NSET="
                line_parts = line.split("=")
                if len(line_parts) > 1:
                    nset_name = line_parts[1].strip().split(',')[0].strip()
                    current_set_name = f"NSET_{part_name}_{nset_name}"
                    
                    # Check if this node set is associated with a part
                    if fullfile:
                        # Look for part names in the set name
                        for part_key in model_parts.keys():
                            if part_key in nset_name:
                                model_parts[part_key]['nsets'].add(current_set_name)
                                break
                    
                    # Add to existing_sets if not already present
                    if current_set_name not in existing_sets['nsets']:
                        existing_sets['nsets'][current_set_name] = {
                            'declaration': f"*Nset, nset={current_set_name}",
                            'nodes': []
                        }
                else:
                    print(f"[WARNING] Nset with no name at line {i+1}")
                    current_set_name = f"NSET_{part_name}_UNNAMED_{i+1}"
            else:
                print(f"[WARNING] Nset with no name at line {i+1}")
                current_set_name = f"NSET_{part_name}_UNNAMED_{i+1}"
                
            current_set = []
            i += 1
            continue
            
        elif '*ELSET' in line_upper:
            # Save any current set before moving to new section
            if in_nset_section and current_set:
                existing_sets['nsets'][current_set_name]['nodes'] = current_set
                if fullfile:
                    node_sets[current_set_name] = current_set.copy()
                current_set = []
                
            # For fullfile mode, save previous section if any
            if fullfile and current_section and current_section != 'ELSET' and section_lines:
                other_sections['raw'].append((section_header, section_lines))
                
            in_node_section = False
            in_element_section = False
            in_nset_section = False
            in_elset_section = True
            in_surface_section = False
            
            # For fullfile mode
            if fullfile:
                current_section = 'ELSET'
                section_header = line
                section_lines = []
            
            # Extract set name and full declaration (case-insensitive)
            if "=" in line:
                line_parts = line.split("=")
                if len(line_parts) > 1:
                    elset_name = line_parts[1].strip().split(',')[0].strip()
                    current_set_name = f"ELSET_{part_name}_{elset_name}"
                    
                    # Check if this element set is associated with a part
                    if fullfile:
                        # Look for part names in the set name
                        for part_key in model_parts.keys():
                            if part_key in elset_name:
                                model_parts[part_key]['elsets'].add(current_set_name)
                                break
                    
                    # Add to existing_sets if not already present
                    if current_set_name not in existing_sets['elsets']:
                        existing_sets['elsets'][current_set_name] = {
                            'declaration': line.strip(),
                            'elements': []
                        }
                else:
                    print(f"[WARNING] Elset with no name at line {i+1}")
                    current_set_name = f"ELSET_{part_name}_UNNAMED_{i+1}"
            else:
                print(f"[WARNING] Elset with no name at line {i+1}")
                current_set_name = f"ELSET_{part_name}_UNNAMED_{i+1}"
            
            # Read all elements in this set until we hit another * or end of file
            set_elements = []
            i += 1
            
            # Collect lines for fullfile mode
            if fullfile:
                elset_lines = []
            
            while i < len(lines) and not lines[i].strip().startswith('*'):
                elements_line = lines[i].strip()
                if elements_line:  # Skip empty lines
                    # Add to section_lines for fullfile mode
                    if fullfile:
                        section_lines.append(elements_line)
                    
                    # Remove trailing comma if present
                    if elements_line.endswith(','):
                        elements_line = elements_line[:-1]
                    
                    # Check if this line contains element IDs or references to other element sets
                    try:
                        # Try to parse as element IDs
                        set_elements.extend([int(x.strip()) + max_elem_id for x in elements_line.split(',') if x.strip()])
                        if fullfile:
                            element_sets[current_set_name] = set_elements.copy()
                    except ValueError:
                        # This contains references to other element sets
                        if fullfile:
                            if current_set_name not in element_set_references:
                                element_set_references[current_set_name] = []
                            element_set_references[current_set_name].extend([x.strip() for x in elements_line.split(',') if x.strip()])
                        print(f"[INFO] Element set {current_set_name} references other sets: {elements_line}")
                        
                i += 1
            
            # Store both declaration and elements
            existing_sets['elsets'][current_set_name]['elements'] = set_elements
            
            continue
            
        elif '*SURFACE' in line_upper:
            # For fullfile mode, save previous section if any
            if fullfile and current_section and current_section != 'SURFACE' and section_lines:
                other_sections['raw'].append((section_header, section_lines))
                
            # Moving to a surface section
            in_node_section = False
            in_element_section = False
            in_nset_section = False
            in_elset_section = False
            in_surface_section = True
            
            # For fullfile mode
            if fullfile:
                current_section = 'SURFACE'
                section_header = line
                section_lines = []
            
            # Extract surface name and type
            current_surface_faces = []
            surface_type = "ELEMENT"  # Default type
            surface_name = ""
            
            if "TYPE=" in line_upper:
                type_start = line_upper.find("TYPE=") + 5
                if "," in line[type_start:]:
                    type_end = line[type_start:].find(",") + type_start
                else:
                    type_end = len(line)
                surface_type = line[type_start:type_end].strip()
            
            if "NAME=" in line_upper:
                name_start = line_upper.find("NAME=") + 5
                if "," in line[name_start:]:
                    name_end = line[name_start:].find(",") + name_start
                else:
                    name_end = len(line)
                surface_name = f"SURFACE_{part_name}_{line[name_start:name_end].strip()}"
            
            if not surface_name:
                print(f"[WARNING] Surface with no name at line {i+1}")
                surface_name = f"SURFACE_{part_name}_UNNAMED_{i+1}"
                
            current_surface_name = surface_name
            
            # Create new surface entry
            surfaces[current_surface_name] = {
                'declaration': line.strip(),
                'type': surface_type,
                'faces': []
            }
            
            i += 1
            continue
            
        elif line.startswith('*'):
            # Save any current set before moving to new section
            if in_nset_section and current_set:
                existing_sets['nsets'][current_set_name]['nodes'] = current_set
                if fullfile:
                    node_sets[current_set_name] = current_set.copy()
                current_set = []
                
            # End of a surface section
            if in_surface_section and current_surface_name:
                surfaces[current_surface_name]['faces'] = current_surface_faces
                current_surface_faces = []
                
            # For fullfile mode, save previous section if any
            if fullfile and current_section and section_lines:
                other_sections['raw'].append((section_header, section_lines))
                
            in_node_section = False
            in_element_section = False
            in_nset_section = False
            in_elset_section = False
            in_surface_section = False
            
            # For fullfile mode - starting a new "other" section
            if fullfile:
                current_section = 'OTHER'
                section_header = line
                section_lines = []
                
            i += 1
            continue
            
        # Process nodes        
        if in_node_section:
            # Process node line: ID, X, Y, Z
            try:
                parts = line.split(',')
                if len(parts) < 4:
                    print(f"[ERROR] Node line {i+1} has insufficient data: {line}")
                    error_count += 1
                    i += 1
                    continue
                    
                node_id = int(parts[0]) + max_node_id
                coords = []
                for j, coord in enumerate(parts[1:4]):
                    try:
                        coords.append(float(coord.strip()))
                    except ValueError:
                        print(f"[ERROR] Invalid coordinate at line {i+1}, position {j+1}: '{coord}'")
                        error_count += 1
                
                if len(coords) == 3:  # Only add if we have all 3 coordinates
                    nodes[node_id] = coords
                    node_count += 1
                
                # For fullfile mode
                if fullfile:
                    section_lines.append(line)
                    
            except Exception as e:
                print(f"[ERROR] Failed to parse node at line {i+1}: {e}")
                print(f"[ERROR] Line content: {line}")
                error_count += 1
            
        # Process elements
        elif in_element_section:
            try:
                # Handle multi-line element definitions
                full_element_line = line
                
                # If the line ends with a comma, it continues on the next line
                while full_element_line.endswith(',') and i + 1 < len(lines):
                    i += 1
                    next_line = lines[i].strip()
                    if next_line:
                        full_element_line += ' ' + next_line
                
                # For fullfile mode
                if fullfile:
                    section_lines.append(full_element_line)
                
                # Now process the complete element definition
                parts = full_element_line.split(',')
                if len(parts) < 2:
                    print(f"[ERROR] Element line {i+1} has insufficient data: {full_element_line}")
                    error_count += 1
                    i += 1
                    continue
                
                # Get element ID and node IDs (clean up whitespace and empty strings)
                elem_id = int(parts[0].strip()) + max_elem_id
                
                # Clean up node IDs - handle both comma-separated and space-separated values
                node_ids = []
                for part in parts[1:]:
                    # Split by whitespace if any and process each token
                    tokens = [token.strip() for token in part.split() if token.strip()]
                    for token in tokens:
                        if token:  # Skip empty strings
                            try:
                                node_ids.append(int(token) + max_node_id)
                            except ValueError:
                                print(f"[ERROR] Invalid node ID in element at line {i+1}: '{token}'")
                                error_count += 1
                
                # If this is a continuation line, append to existing element
                if elem_id in elements:
                    elements[elem_id]['nodes'].extend(node_ids)
                    # Validate the complete element after all nodes are added
                    # (Note: This validation will happen when we finish reading all node lines)
                else:
                    # Create new element
                    elements[elem_id] = {
                        'type': current_element_type,
                        'nodes': node_ids,
                        'elset': current_elset,
                        'part': current_part_name
                    }
                    element_count += 1
                    
                    # Validate element node count
                    is_valid = validate_element_nodes(current_element_type, node_ids)
                    validated_elements.add(elem_id)  # Mark as validated
                    
                    if is_valid:
                        valid_elements += 1
                        # Add element to its type-specific set only if valid
                        if current_element_type not in elements_by_type:
                            elements_by_type[current_element_type] = []
                        elements_by_type[current_element_type].append(elem_id)
                        
                        # Track this element in the part dictionary if we're in fullfile mode
                        if fullfile and current_part_name and current_part_name in model_parts:
                            model_parts[current_part_name]['elements'].append(elem_id)
                            # Add all nodes used by this element to the part's node set
                            model_parts[current_part_name]['nodes'].update(node_ids)
                    else:
                        invalid_elements += 1
                        print(f"[WARNING] Element {elem_id} of type {current_element_type} has invalid node count - excluding from type-specific sets")
                
            except Exception as e:
                print(f"[ERROR] Failed to parse element at line {i+1}: {e}")
                print(f"[ERROR] Line content: {line}")
                error_count += 1
                
        # Process node sets
        elif in_nset_section:
            # Process set members, handling trailing commas
            try:
                line = line.strip()
                if line:  # Skip empty lines
                    # For fullfile mode
                    if fullfile:
                        section_lines.append(line)
                        
                    # Remove trailing comma if present
                    if line.endswith(','):
                        line = line[:-1]
                    # Split and convert to integers, ignoring empty strings
                    node_ids = [int(x.strip()) + max_node_id for x in line.split(',') if x.strip()]
                    existing_sets['nsets'][current_set_name]['nodes'].extend(node_ids)
                    current_set.extend(node_ids)
                    
            except Exception as e:
                print(f"[ERROR] Failed to parse node set at line {i+1}: {e}")
                print(f"[ERROR] Line content: {line}")
                error_count += 1
        
        # Process surface definitions
        elif in_surface_section:
            # Process surface members which are elset/face pairs
            try:
                line = line.strip()
                if line:  # Skip empty lines
                    # For fullfile mode
                    if fullfile:
                        section_lines.append(line)
                        
                    # Format is typically "elset_name, face_name"
                    parts = [part.strip() for part in line.split(',') if part.strip()]
                    if len(parts) >= 2:
                        elset_name = parts[0]
                        face_name = parts[1]
                        
                        # Use the consistent element set naming format
                        # Store the full element set name (with ELSET_ prefix)
                        full_elset_name = elset_name
                        if not elset_name.startswith("ELSET_"):
                            full_elset_name = f"ELSET_{part_name}_{elset_name}"
                            
                        current_surface_faces.append((full_elset_name, face_name))
                        surfaces[current_surface_name]['faces'].append((full_elset_name, face_name))
                    else:
                        print(f"[WARNING] Invalid surface definition at line {i+1}: needs elset and face")
            except Exception as e:
                print(f"[ERROR] Failed to parse surface at line {i+1}: {e}")
                print(f"[ERROR] Line content: {line}")
                error_count += 1
        
        # Handle other sections for fullfile mode
        elif fullfile and current_section == 'OTHER':
            section_lines.append(line)
                
        i += 1
    
    # Save any remaining node set
    if in_nset_section and current_set:
        existing_sets['nsets'][current_set_name]['nodes'] = current_set
        if fullfile:
            node_sets[current_set_name] = current_set.copy()
    
    # Save any remaining surface definition
    if in_surface_section and current_surface_name and current_surface_faces:
        surfaces[current_surface_name]['faces'] = current_surface_faces
    
    # For fullfile mode, save the last section if any
    if fullfile and current_section and section_lines:
        other_sections['raw'].append((section_header, section_lines))

    # Finalize any captured non-mesh section
    if fullfile:
        finalize_other_section()
    
    # Create type-specific element sets
    for elem_type, elem_ids in elements_by_type.items():
        type_set_name = f"ELSET_{part_name}_{elem_type}_ALL"
        if type_set_name not in existing_sets['elsets']:
            existing_sets['elsets'][type_set_name] = {
                'declaration': f"*Elset, elset={type_set_name}",
                'elements': elem_ids
            }
            # Also track in element_sets for fullfile mode
            if fullfile:
                element_sets[type_set_name] = elem_ids.copy()
            print(f"[DEBUG] Created new element set '{type_set_name}' with {len(elem_ids)} elements")
    
    # Final validation pass for any multi-line elements that might have been missed
    final_validation_invalid = 0
    for elem_id, elem_data in elements.items():
        elem_type = elem_data['type']
        node_list = elem_data['nodes']
        
        # Skip elements that were already validated during parsing
        if elem_id in validated_elements:
            continue
            
        # Validate elements that weren't validated yet (likely multi-line elements)
        is_valid = validate_element_nodes(elem_type, node_list)
        validated_elements.add(elem_id)  # Mark as validated
        
        if is_valid:
            valid_elements += 1
            # Add to type-specific set
            if elem_type not in elements_by_type:
                elements_by_type[elem_type] = []
            elements_by_type[elem_type].append(elem_id)
            
            # Track this element in the part dictionary if we're in fullfile mode
            if fullfile and elem_data['part'] and elem_data['part'] in model_parts:
                model_parts[elem_data['part']]['elements'].append(elem_id)
                # Add all nodes used by this element to the part's node set
                model_parts[elem_data['part']]['nodes'].update(node_list)
        else:
            final_validation_invalid += 1
            invalid_elements += 1
            print(f"[WARNING] Multi-line element {elem_id} of type {elem_type} has invalid node count - excluding from type-specific sets")
    
    print(f"\n[DEBUG] Parsing complete:")
    print(f"[DEBUG] Found {len(nodes)} nodes")
    print(f"[DEBUG] Found {len(elements)} elements")
    print(f"[DEBUG] Valid elements: {valid_elements}")
    print(f"[DEBUG] Invalid elements: {invalid_elements}")
    if final_validation_invalid > 0:
        print(f"[DEBUG] Multi-line elements with invalid node count: {final_validation_invalid}")
    if unknown_element_types:
        print(f"[DEBUG] Unknown element types encountered: {sorted(unknown_element_types)}")
    print(f"[DEBUG] Found {len(existing_sets['nsets'])} node sets")
    print(f"[DEBUG] Found {len(existing_sets['elsets'])} element sets")
    print(f"[DEBUG] Found {len(surfaces)} surfaces")
    print(f"[DEBUG] Encountered {error_count} errors during parsing")
    
    # Print a few sample nodes to verify data
    if nodes:
        print("\n[DEBUG] Sample node data:")
        for i, (node_id, coords) in enumerate(nodes.items()):
            if i >= 5:
                break
            print(f"[DEBUG] Node {node_id}: {coords}")
    
    # Print a few sample elements to verify data
    if elements:
        print("\n[DEBUG] Sample element data:")
        for i, (elem_id, elem_data) in enumerate(elements.items()):
            if i >= 5:
                break
            print(f"[DEBUG] Element {elem_id} of type {elem_data['type']}, nodes: {elem_data['nodes'][:5]}...")
    
    # Print a few sample surfaces to verify
    if surfaces:
        print("\n[DEBUG] Sample surface data:")
        for i, (surface_name, surface_data) in enumerate(surfaces.items()):
            if i >= 3:
                break
            print(f"[DEBUG] Surface {surface_name}, type: {surface_data['type']}, faces: {surface_data['faces'][:3]}...")
    
    # Add surfaces to existing_sets
    existing_sets['surfaces'] = surfaces
    
    # Return the appropriate data based on fullfile flag
    if fullfile:
        return nodes, elements, existing_sets, node_sets, element_sets, element_set_references, other_sections, model_parts
    else:
        return nodes, elements, existing_sets

def identify_unshared_faces(elements, node_to_elements):
    """Identify element faces that are not shared with other elements
    
    Parameters:
    - elements: Dictionary of elements {elem_id: {'type': element_type, 'nodes': [node_ids]}}
    - node_to_elements: Dictionary mapping each node ID to a list of element IDs that contain it
    
    Returns:
    - Dictionary mapping element IDs to lists of unshared face IDs
    """
    # Define face nodes for different element types
    face_nodes = {
        'C3D4': {  # Tetrahedral element with 4 nodes
            'S1': [0, 1, 2],      # Face 1: nodes 1-2-3
            'S2': [0, 1, 3],      # Face 2: nodes 1-2-4
            'S3': [1, 2, 3],      # Face 3: nodes 2-3-4
            'S4': [0, 2, 3]       # Face 4: nodes 1-3-4
        },
        'C3D10': {  # Tetrahedral element with 10 nodes
            'S1': [0, 1, 2, 4, 5, 6],      # Face 1: nodes 1-2-3 and mid-edge nodes
            'S2': [0, 1, 3, 4, 8, 7],      # Face 2: nodes 1-2-4 and mid-edge nodes
            'S3': [1, 2, 3, 5, 9, 8],      # Face 3: nodes 2-3-4 and mid-edge nodes
            'S4': [0, 2, 3, 6, 9, 7]       # Face 4: nodes 1-3-4 and mid-edge nodes
        },
        # Can add other element types as needed
    }
    
    unshared_faces = {}  # {elem_id: ['S1', 'S3', ...]}
    
    # Process each element
    for elem_id, elem_data in elements.items():
        element_type = elem_data['type'].upper()
        
        # Skip if not a supported element type
        if element_type not in face_nodes:
            continue
        
        element_nodes = elem_data['nodes']
        faces = face_nodes[element_type]
        
        # Check each face to see if it's shared
        for face_id, face_local_nodes in faces.items():
            # Get actual node IDs for this face
            face_node_ids = [element_nodes[i] for i in face_local_nodes]
            
            # Count elements sharing each node
            sharing_elements = set()
            for node_id in face_node_ids:
                if node_id in node_to_elements:
                    for e_id in node_to_elements[node_id]:
                        if e_id != elem_id:  # Don't count self
                            sharing_elements.add(e_id)
            
            # If no elements share all these nodes, then this is an unshared face
            if len(sharing_elements) == 0:
                if elem_id not in unshared_faces:
                    unshared_faces[elem_id] = []
                unshared_faces[elem_id].append(face_id)
    
    return unshared_faces

def identify_parts_with_symmetry_nodes(model_parts, nodes, symmetry_plane_nodes, local_logger=None):
    """Identify parts that have at least one node on the symmetry plane (x=0)
    
    Parameters:
    - model_parts: Dictionary of model parts {part_name: {'type': type, 'elements': [elem_ids], 'nodes': set(node_ids), ...}}
    - nodes: Dictionary of node coordinates {node_id: [x, y, z]}
    - symmetry_plane_nodes: Dictionary of nodes on the symmetry plane {node_id: [0, y, z]}
    - local_logger: Logger instance for debug output
    
    Returns:
    - Set of part names that have at least one node on the symmetry plane
    """
    # Use standard logger if none provided
    if local_logger is None:
        local_logger = logging
    
    parts_on_symmetry_plane = set()
    
    # Check each part
    for part_name, part_data in model_parts.items():
        part_nodes = part_data.get('nodes', set())
        
        # Check if any node in this part is on the symmetry plane
        for node_id in part_nodes:
            if node_id in symmetry_plane_nodes:
                parts_on_symmetry_plane.add(part_name)
                local_logger.info(f"Part '{part_name}' has nodes on symmetry plane (x=0)")
                break
    
    local_logger.info(f"Found {len(parts_on_symmetry_plane)} parts with nodes on the symmetry plane")
    return parts_on_symmetry_plane

def assign_surfaces_to_parts(surfaces, model_parts, local_logger=None):
    """Assign surfaces to their corresponding parts based on the element sets they reference.
    
    Parameters:
    - surfaces: Dictionary of surfaces {surface_name: {'type': type, 'faces': [(elset_name, face_id), ...], ...}}
    - model_parts: Dictionary of model parts {part_name: {'type': type, 'elements': [elem_ids], 'elsets': set(), ...}}
    - local_logger: Logger instance for debug output
    
    Returns:
    - Updated model_parts dictionary with surfaces assigned to their respective parts
    """
    # Use standard logger if none provided
    if local_logger is None:
        local_logger = logging
    
    local_logger.info(f"Assigning {len(surfaces)} surfaces to parts")
    
    # Track statistics
    assigned_count = 0
    unassigned_count = 0
    
    # Ensure each part has a 'surfaces' set
    for part_name in model_parts:
        if 'surfaces' not in model_parts[part_name]:
            model_parts[part_name]['surfaces'] = set()
    
    # Process each surface
    for surface_name, surface_data in surfaces.items():
        assigned = False
        
        # Skip empty surfaces
        if not surface_data.get('faces', []):
            local_logger.debug(f"Skipping empty surface: {surface_name}")
            unassigned_count += 1
            continue
        
        # Get the first element set referenced by this surface
        for elset_name, face_id in surface_data['faces']:
            # Check if this element set belongs to a part
            for part_name, part_data in model_parts.items():
                if 'elsets' in part_data and elset_name in part_data['elsets']:
                    # Add this surface to the part
                    model_parts[part_name]['surfaces'].add(surface_name)
                    local_logger.debug(f"Assigned surface '{surface_name}' to part '{part_name}' based on element set '{elset_name}'")
                    assigned = True
                    assigned_count += 1
                    break
            
            if assigned:
                break
        
        if not assigned:
            local_logger.debug(f"Could not find part for surface '{surface_name}'")
            unassigned_count += 1
    
    local_logger.info(f"Surface assignment complete: {assigned_count} assigned, {unassigned_count} unassigned")
    
    return model_parts

def get_element_face_node_ids(element_type, element_nodes, face_id):
    """Return the node IDs that form a given face for supported solid element types."""
    if not element_nodes or face_id is None:
        return []
    
    elem_type = element_type.upper()
    face_key = face_id.upper()
    
    face_nodes = {
        'C3D4': {
            'S1': [0, 1, 2],
            'S2': [0, 3, 1],
            'S3': [1, 3, 2],
            'S4': [0, 2, 3],
        },
        'C3D8': {
            'S1': [0, 1, 2, 3],
            'S2': [4, 5, 6, 7],
            'S3': [0, 1, 5, 4],
            'S4': [1, 2, 6, 5],
            'S5': [2, 3, 7, 6],
            'S6': [3, 0, 4, 7],
        },
        'C3D10': {
            'S1': [0, 1, 2, 4, 5, 6],
            'S2': [0, 1, 3, 4, 8, 7],
            'S3': [1, 2, 3, 5, 9, 8],
            'S4': [0, 2, 3, 6, 9, 7],
        },
        'C3D15': {
            'S1': [0, 1, 2, 6, 7, 8],
            'S2': [3, 4, 5, 9, 10, 11],
            'S3': [0, 1, 4, 3, 6, 13, 9, 12],
            'S4': [1, 2, 5, 4, 7, 14, 10, 13],
            'S5': [0, 3, 5, 2, 12, 11, 14, 8],
        },
        'C3D20': {
            'S1': [0, 1, 2, 3, 8, 9, 10, 11],
            'S2': [4, 5, 6, 7, 12, 13, 14, 15],
            'S3': [0, 1, 5, 4, 8, 17, 12, 16],
            'S4': [1, 2, 6, 5, 9, 18, 13, 17],
            'S5': [2, 3, 7, 6, 10, 19, 14, 18],
            'S6': [3, 0, 4, 7, 11, 16, 15, 19],
        },
        'C3D20R': {
            'S1': [0, 1, 2, 3, 8, 9, 10, 11],
            'S2': [4, 5, 6, 7, 12, 13, 14, 15],
            'S3': [0, 1, 5, 4, 8, 17, 12, 16],
            'S4': [1, 2, 6, 5, 9, 18, 13, 17],
            'S5': [2, 3, 7, 6, 10, 19, 14, 18],
            'S6': [3, 0, 4, 7, 11, 16, 15, 19],
        },
        'C3D6': {
            'S1': [0, 1, 2],
            'S2': [3, 4, 5],
            'S3': [0, 1, 4, 3],
            'S4': [1, 2, 5, 4],
            'S5': [2, 0, 3, 5],
        },
    }
    
    node_indices = face_nodes.get(elem_type, {}).get(face_key, [])
    
    if not node_indices:
        return []
    
    # Guard against malformed elements
    return [element_nodes[i] for i in node_indices if i < len(element_nodes)]

def surface_touches_symmetry_plane(surface_data, element_sets, elements, symmetry_plane_node_ids, local_logger=None):
    """Return True if every node on at least one referenced face lies on the symmetry plane."""
    if local_logger is None:
        local_logger = logging
    
    for elset_name, face_id in surface_data.get('faces', []):
        element_ids = element_sets.get(elset_name, [])
        if not element_ids:
            continue
        
        for elem_id in element_ids:
            elem_data = elements.get(elem_id)
            if not elem_data:
                continue
            
            face_node_ids = get_element_face_node_ids(elem_data['type'], elem_data['nodes'], face_id)
            nodes_to_check = face_node_ids or elem_data['nodes']
            
            if nodes_to_check and all(node_id in symmetry_plane_node_ids for node_id in nodes_to_check):
                local_logger.debug(f"Surface face '{elset_name}/{face_id}' lies on symmetry plane via element {elem_id}")
                return True
    
    return False

def mirror_inp_file(input_filename, output_filename, mirror_prefix="MIRRORED", verbose=False, log_level=logging.INFO, log_file=None, symmetry_plane_point=None, symmetry_plane_normal=None):
    local_logger = setup_logging(log_level, log_file)
    
    local_logger.info(f"Reading INP file: {input_filename}")
    nodes, elements, existing_sets, nsets, element_sets, element_set_references, other_sections, model_parts = read_inp_file(input_filename, fullfile=True)

    # Extract surfaces from the existing_sets dictionary
    surfaces = existing_sets.get('surfaces', {})
    
    # Calculate max node and element IDs
    max_node_id = max(nodes.keys()) if nodes else 0
    max_elem_id = max(elements.keys()) if elements else 0
    
    local_logger.info("Assigning element sets to parts...")
    model_parts = assign_elsets_to_parts(element_sets, elements, model_parts, local_logger)
    
    local_logger.info("Assigning surfaces to parts...")
    model_parts = assign_surfaces_to_parts(surfaces, model_parts, local_logger)

    # Prepare symmetry plane definition
    plane_tol = 1e-3
    # Defaults: X=0 plane (point at origin, normal along +X)
    default_point = (0.0, 0.0, 0.0)
    default_normal = (1.0, 0.0, 0.0)
    
    if symmetry_plane_point is None or symmetry_plane_normal is None:
        plane_point = default_point
        plane_normal = default_normal
    else:
        try:
            plane_point = tuple(float(v) for v in symmetry_plane_point)
            plane_normal = tuple(float(v) for v in symmetry_plane_normal)
        except Exception:
            # Fallback to default if parsing fails
            plane_point = default_point
            plane_normal = default_normal
            local_logger.warning("Invalid symmetry plane inputs; defaulting to X=0 plane")

    # Normalize the plane normal; fallback to default if degenerate
    nx, ny, nz = plane_normal
    norm = math.sqrt(nx*nx + ny*ny + nz*nz)
    if norm <= 0.0:
        nx, ny, nz = default_normal
        norm = 1.0
        plane_point = default_point
        local_logger.warning("Zero-length normal provided; defaulting to X=0 plane")
    nx /= norm
    ny /= norm
    nz /= norm

    def dot(a, b):
        return a[0]*b[0] + a[1]*b[1] + a[2]*b[2]

    def sub(a, b):
        return (a[0]-b[0], a[1]-b[1], a[2]-b[2])

    def reflect_point(p):
        # Reflect point p across plane defined by (plane_point, n_hat)
        v = sub(p, plane_point)
        d = v[0]*nx + v[1]*ny + v[2]*nz
        return (p[0] - 2.0*d*nx, p[1] - 2.0*d*ny, p[2] - 2.0*d*nz)

    def on_plane(p):
        # Signed distance along normal; compare with tolerance
        v = sub(p, plane_point)
        d = abs(v[0]*nx + v[1]*ny + v[2]*nz)
        return d < plane_tol

    # Identify nodes on the symmetry plane
    symmetry_plane_nodes = {nid: coords for nid, coords in nodes.items() if on_plane(coords)}
    local_logger.info(
        f"Found {len(symmetry_plane_nodes)} nodes within {plane_tol} of plane (point={plane_point}, normal=({nx:.6f},{ny:.6f},{nz:.6f}))"
    )

    parts_on_symmetry_plane = identify_parts_with_symmetry_nodes(model_parts, nodes, symmetry_plane_nodes, local_logger)
    
    mirrored_nodes = {}
    mirrored_node_map = {}
    mirrored_node_map_inverse = {}

    for node_id, coords in nodes.items():
        if node_id not in symmetry_plane_nodes:
            mirrored_node_id = node_id + max_node_id
            mirrored_coords = reflect_point(coords)
            mirrored_nodes[mirrored_node_id] = (mirrored_coords[0], mirrored_coords[1], mirrored_coords[2])
            mirrored_node_map[node_id] = mirrored_node_id
            mirrored_node_map_inverse[mirrored_node_id] = node_id
        else:
            mirrored_node_map[node_id] = node_id
            mirrored_node_map_inverse[node_id] = node_id

    mirrored_elements = {}
    for elem_id, elem_data in elements.items():
        mirrored_elem_id = elem_id + max_elem_id
        elem_type = elem_data['type']
        part_name = elem_data.get('part', '')
        
        # Get the node IDs for the mirrored element before reordering
        new_nodes = [mirrored_node_map[nid] for nid in elem_data['nodes']]

        if elem_type.upper() == 'C3D10':  # Quadratic tetrahedral elements
            # For C3D10 (quadratic tet), need to reorder nodes to maintain proper orientation
            if len(new_nodes) == 10:
                new_nodes[0], new_nodes[1], new_nodes[2], new_nodes[3], new_nodes[4], new_nodes[5], new_nodes[6], new_nodes[7], new_nodes[8], new_nodes[9] = new_nodes[1], new_nodes[0], new_nodes[2], new_nodes[3], new_nodes[4], new_nodes[6], new_nodes[5], new_nodes[8], new_nodes[7], new_nodes[9]
        
        elif elem_type.upper() in ['C3D8', 'C3D20', 'C3D20R']:  # Hexahedral elements
            # For hexahedral elements, swap first four nodes and last four nodes
            # This is an odd permutation that restores positive orientation
            # after a geometric reflection, independent of the plane used.
            if len(new_nodes) >= 8:
                new_nodes[0], new_nodes[1], new_nodes[2], new_nodes[3], new_nodes[4], new_nodes[5], new_nodes[6], new_nodes[7] = (
                    new_nodes[4], new_nodes[5], new_nodes[6], new_nodes[7],
                    new_nodes[0], new_nodes[1], new_nodes[2], new_nodes[3]
                )
                
                # For C3D20 (quadratic hex), need additional swaps for edge and face nodes
                if len(new_nodes) == 20:
                    # Swap edge nodes (8,9) (10,11) (12,13) (14,15)
                    new_nodes[9], new_nodes[13] = new_nodes[13], new_nodes[9]
                    new_nodes[11], new_nodes[15] = new_nodes[15], new_nodes[11]
                    new_nodes[8], new_nodes[12] = new_nodes[12], new_nodes[8]
                    new_nodes[10], new_nodes[14] = new_nodes[14], new_nodes[10]
        
        elif elem_type.upper() == 'C3D15':  # Wedge/prism elements 
            # For wedge elements, need to swap nodes to maintain proper orientation
            if len(new_nodes) >= 6:
                # Check if this element is on the symmetry plane (just for logging)
                is_on_symmetry_plane = part_name in parts_on_symmetry_plane
                
                # Log the node swapping for C3D15 elements, especially on symmetry plane
                if is_on_symmetry_plane:
                    local_logger.debug(f"Mirroring C3D15 element {elem_id} on symmetry plane in part {part_name}, applying standard node swapping")
                
                # Swap the triangular faces (nodes 0,1,2 and 3,4,5)
                # For the first triangular face
                new_nodes[0], new_nodes[1], new_nodes[2],new_nodes[3], new_nodes[4], new_nodes[5] = new_nodes[3], new_nodes[4], new_nodes[5], new_nodes[0], new_nodes[1], new_nodes[2]
                
                # For quadratic wedge (C3D15), need additional swaps for mid-edge and face nodes
                if len(new_nodes) == 15:
                    # Swap mid-edge nodes on triangular faces
                    new_nodes[6], new_nodes[9] = new_nodes[9], new_nodes[6]  # Edges on bottom triangle
                    new_nodes[7], new_nodes[10] = new_nodes[10], new_nodes[7]  # Vertical edges
                    new_nodes[8], new_nodes[11] = new_nodes[11], new_nodes[8]  # Edges on top triangle
        
        mirrored_elements[mirrored_elem_id] = {'type': elem_type, 'nodes': new_nodes, 'part': part_name}

    mirrored_element_sets = {}
    for elset_name, elset_data in element_sets.items():
        mirrored_elset_name = elset_name.replace("LEFT", "RIGHT") if "LEFT" in elset_name else f"{mirror_prefix}_{elset_name}"
        mirrored_element_sets[mirrored_elset_name] = [eid + max_elem_id for eid in elset_data]

    mirrored_surfaces = {}
    symmetry_plane_node_ids = set(symmetry_plane_nodes.keys())
    surfaces_on_symmetry_plane = set()
    for surface_name, surface_data in surfaces.items():
        if surface_touches_symmetry_plane(surface_data, element_sets, elements, symmetry_plane_node_ids, local_logger):
            surfaces_on_symmetry_plane.add(surface_name)
    local_logger.info(f"Identified {len(surfaces_on_symmetry_plane)} surfaces on the symmetry plane")

    for surface_name, surface_data in surfaces.items():
        if surface_name in surfaces_on_symmetry_plane:
            # Complete this surface by merging mirrored elements into existing element sets
            local_logger.info(f"Completing symmetry plane surface: {surface_name}")
            
            original_faces = surface_data.get('faces', [])
            merged_count = 0
            
            for elset_name, face_id in original_faces:
                mirrored_elset_name = elset_name.replace("LEFT", "RIGHT") if "LEFT" in elset_name else f"{mirror_prefix}_{elset_name}"
                if mirrored_elset_name in mirrored_element_sets:
                    # Get the element type from the first element in the mirrored element set
                    first_elem_id = mirrored_element_sets[mirrored_elset_name][0]
                    element_type = mirrored_elements[first_elem_id]['type']
                    
                    # Get the correct face ID for the mirrored element
                    mirrored_face_id = get_mirrored_face_id(face_id, element_type)
                    
                    # Find existing element set that matches the mirrored face pattern
                    import re
                    # Extract base name and create pattern for the mirrored face
                    base_match = re.match(r'(.+)_S(\d+)$', elset_name)
                    if base_match:
                        base_name = base_match.group(1)
                        target_set_name = f"{base_name}_S{mirrored_face_id[1:]}"  # Remove 'S' from face_id and add back
                        
                        # Check if target element set exists
                        if target_set_name in element_sets:
                            # Merge mirrored elements into existing set
                            element_sets[target_set_name].extend(mirrored_element_sets[mirrored_elset_name])
                            merged_count += 1
                            local_logger.debug(f"Merged {len(mirrored_element_sets[mirrored_elset_name])} mirrored elements from {mirrored_elset_name} into existing {target_set_name}")
                            # Remove the mirrored set since its elements are now merged
                            del mirrored_element_sets[mirrored_elset_name]
                        else:
                            # Keep original logic as fallback - add as separate mirrored set
                            element_sets[target_set_name] = mirrored_element_sets[mirrored_elset_name]
                            surfaces[surface_name]['faces'].append((target_set_name, mirrored_face_id))
                    else:
                        local_logger.warning(f"Could not parse element set name pattern: {elset_name}")
            
            if merged_count > 0:
                local_logger.info(f"Completed '{surface_name}' by merging {merged_count} mirrored element sets into existing sets.")
            else:
                local_logger.warning(f"Could not merge any mirrored elements for '{surface_name}' - keeping original only.")

        else:
            # This is a non-symmetry surface, so create its mirrored counterpart
            mirrored_faces = []
            for elset_name, face_id in surface_data.get('faces', []):
                mirrored_elset_name = elset_name.replace("LEFT", "RIGHT") if "LEFT" in elset_name else f"{mirror_prefix}_{elset_name}"
                if mirrored_elset_name in mirrored_element_sets:
                    # Get the element type from the first element in the mirrored element set
                    first_elem_id = mirrored_element_sets[mirrored_elset_name][0]
                    element_type = mirrored_elements[first_elem_id]['type']
                    
                    # Get the correct face ID for the mirrored element
                    mirrored_face_id = get_mirrored_face_id(face_id, element_type)
                    mirrored_faces.append((mirrored_elset_name, mirrored_face_id))
            
            if mirrored_faces:
                mirrored_surface_name = surface_name.replace("LEFT", "RIGHT") if "LEFT" in surface_name else f"{mirror_prefix}_{surface_name}"
                mirrored_surfaces[mirrored_surface_name] = {**surface_data, 'faces': mirrored_faces}
                
    # Writing the output file
    with open(output_filename, 'w') as f:
        f.write("*HEADING\nMirrored INP File\n")
        
        # Write all nodes (original, mirrored, symmetry)
        f.write("*NODE\n")
        for node_id, coords in nodes.items():
            f.write(f"{node_id}, {coords[0]}, {coords[1]}, {coords[2]}\n")
        for node_id, coords in mirrored_nodes.items():
            f.write(f"{node_id}, {coords[0]}, {coords[1]}, {coords[2]}\n")
            
        # Write all elements
        all_elements = {**elements, **mirrored_elements}
        
        # Group elements by type to write them in blocks
        elements_by_type = {}
        for elem_id, elem_data in all_elements.items():
            elem_type = elem_data['type']
            if elem_type not in elements_by_type:
                elements_by_type[elem_type] = {}
            elements_by_type[elem_type][elem_id] = elem_data['nodes']
            
        for elem_type, elems in elements_by_type.items():
            f.write(f"*ELEMENT, TYPE={elem_type}\n")
            for elem_id, node_ids in elems.items():
                f.write(f"{elem_id}, {', '.join(map(str, node_ids))}\n")

        # Write all element sets
        all_elsets = {**element_sets, **mirrored_element_sets}
        for elset_name, elset_data in all_elsets.items():
            f.write(f"*ELSET, ELSET={elset_name}\n")
            for i in range(0, len(elset_data), 16):
                f.write(", ".join(map(str, elset_data[i:i+16])) + "\n")

        # Merge surfaces that share face nodes after mirroring
        all_surfaces = {**surfaces, **mirrored_surfaces}
        all_elements = {**elements, **mirrored_elements}
        all_nodes = {**nodes, **mirrored_nodes}
        
        local_logger.info(f"Merging shared surfaces from {len(all_surfaces)} total surfaces")
        #merged_surfaces = merge_shared_surfaces(all_surfaces, all_elements, all_nodes)
        #local_logger.info(f"After merging: {len(merged_surfaces)} surfaces remain")
        
        # Write all surfaces
        for surf_name, surf_data in all_surfaces.items():
            f.write(f"*SURFACE, NAME={surf_name}, TYPE=ELEMENT\n")
            for elset_name, face_id in surf_data['faces']:
                f.write(f"{elset_name}, {face_id}\n")

        # Write other sections
        #for header, section_lines in other_sections:
        #    f.write(f"{header}\n")
        #    for line in section_lines:
        #        f.write(f"{line}\n")

    local_logger.info(f"Mirrored file written to: {output_filename}")
    stats = {
        "original_nodes": len(nodes),
        "mirrored_nodes": len(mirrored_nodes),
        "symmetry_nodes": len(symmetry_plane_nodes),
        "original_elements": len(elements),
        "mirrored_elements": len(mirrored_elements)
    }
    return stats

def find_part_for_elset(elset_name, element_ids, elements, model_parts, local_logger=None):
    """Find which part an element set belongs to by checking its elements.
    
    Parameters:
    - elset_name: Name of the element set
    - element_ids: List of element IDs in the set
    - elements: Dictionary of all elements
    - model_parts: Dictionary of model parts
    - local_logger: Logger instance
    
    Returns:
    - The name of the part, or None if not found.
    """
    if local_logger is None:
        local_logger = logging

    # First, try to find the part based on the element's 'part' attribute
    if element_ids:
        first_elem_id = element_ids[0]
        if first_elem_id in elements and 'part' in elements[first_elem_id]:
            part_name = elements[first_elem_id]['part']
            if part_name:
                return part_name

    # If we couldn't determine the part directly, try to match with elements in model_parts
    for part_name, part_data in model_parts.items():
        part_elements = part_data.get('elements', [])
        
        # Check if any element in the set is in this part
        for elem_id in element_ids:
            if elem_id in part_elements:
                return part_name
    
    return None

def assign_elsets_to_parts(element_sets, elements, model_parts, local_logger=None):
    """Assign element sets to their corresponding parts using find_part_for_elset.
    
    Parameters:
    - element_sets: Dictionary of element sets {set_name: [element_ids]}
    - elements: Dictionary of elements {elem_id: {'type': type, 'nodes': [node_ids], 'part': part_name}}
    - model_parts: Dictionary of model parts {part_name: {'type': type, 'elements': [elem_ids], 'elsets': set(), ...}}
    - local_logger: Logger instance for debug output
    
    Returns:
    - Updated model_parts dictionary with element sets assigned to their respective parts
    """
    # Use standard logger if none provided
    if local_logger is None:
        local_logger = logging
    
    local_logger.info(f"Assigning {len(element_sets)} element sets to parts")
    
    # Track statistics
    assigned_count = 0
    unassigned_count = 0
    
    # Process each element set
    for elset_name, element_ids in element_sets.items():
        # Skip empty element sets
        if not element_ids:
            local_logger.debug(f"Skipping empty element set: {elset_name}")
            unassigned_count += 1
            continue
            
        # Find which part this element set belongs to
        part_name = find_part_for_elset(elset_name, element_ids, elements, model_parts, local_logger)
        
        if part_name and part_name in model_parts:
            # Ensure the part has an 'elsets' set
            if 'elsets' not in model_parts[part_name]:
                model_parts[part_name]['elsets'] = set()
                
            # Add this element set to the part
            model_parts[part_name]['elsets'].add(elset_name)
            #local_logger.debug(f"Assigned element set '{elset_name}' to part '{part_name}'")
            assigned_count += 1
        else:
            #local_logger.debug(f"Could not find part for element set '{elset_name}'")
            unassigned_count += 1
    
    local_logger.info(f"Element set assignment complete: {assigned_count} assigned, {unassigned_count} unassigned")
    
    return model_parts

def get_mirrored_face_id(face_id, element_type, is_on_symmetry_plane=False):
    """
    Get the correct face ID for mirrored elements, accounting for node reordering.
    
    For C3D10 elements, the mirroring involves node reordering:
    [0, 1, 2, 3, 4, 5, 6, 7, 8, 9] -> [1, 0, 2, 3, 4, 6, 5, 8, 7, 9]
    
    This means face definitions need to be updated accordingly.
    """
    if element_type.upper() == 'C3D10':
        # C3D10 face mapping after node reordering due to mirroring
        # Original faces:
                # S1: [0, 1, 2, 4, 5, 6] -> [1, 0, 2, 4, 6, 5] -> S1
        # S2: [0, 1, 3, 4, 8, 7] -> [1, 0, 3, 4, 7, 8] -> S2
        # S3: [1, 2, 3, 5, 9, 8] -> [0, 2, 3, 6, 9, 7] -> S4
        # S4: [0, 2, 3, 6, 9, 7] -> [1, 2, 3, 5, 9, 8] -> S3
        
        face_mapping = {
            'S1': 'S1',  # Stays the same (but node order changes within face)
            'S2': 'S2',  # Stays the same (but node order changes within face)
            'S3': 'S4',  # Maps to S4
            'S4': 'S3'   # Maps to S3
        }
        
        return face_mapping.get(face_id, face_id)
    
    elif element_type.upper() == 'C3D15':
        # C3D15 face mapping after node reordering due to mirroring
        # Node reordering: [0,1,2,3,4,5,6,7,8,9,10,11,12,13,14] -> [3,4,5,0,1,2,9,10,11,6,7,8,12,13,14]
        # Original faces:
                # S1: [0,1,2, 6,7,8] -> [3,4,5, 9,10,11] -> S2 (bottom becomes top)
        # S2: [3,4,5, 9,10,11] -> [0,1,2, 6,7,8] -> S1 (top becomes bottom)
        # S3: [0,1,4,3, 6,13,9,12] -> [3,4,1,0, 9,13,6,12] -> rearranged S3
        # S4: [1,2,5,4, 7,14,10,13] -> [4,5,2,1, 10,14,7,13] -> rearranged S4
        # S5: [0,3,5,2, 12,11,14,8] -> [3,0,2,5, 12,6,14,11] -> rearranged S5
        
        face_mapping = {
            'S1': 'S2',  # Bottom triangular face becomes top
            'S2': 'S1',  # Top triangular face becomes bottom
            'S3': 'S3',  # Front quadrilateral face (nodes reordered but same face)
            'S4': 'S4',  # Right quadrilateral face (nodes reordered but same face)
            'S5': 'S5'   # Back quadrilateral face (nodes reordered but same face)
        }
        
        return face_mapping.get(face_id, face_id)
    
    elif element_type.upper() == 'C3D4':
        # For C3D4 elements, face IDs do not change upon mirroring
        return face_id

    # For other elements like C3D15, C3D20, etc., swapping might be needed
    # (This part of logic for other element types remains)
    return face_id

def find_symmetry_plane_face(element_nodes, symmetry_plane_nodes_set, element_type):
    """
    Find which face of an element lies on the symmetry plane.
    
    Parameters:
    - element_nodes: List of node IDs for this element
    - symmetry_plane_nodes_set: Set of node IDs that are on the symmetry plane
    - element_type: Type of element (e.g., 'C3D10', 'C3D4')
    
    Returns:
    - Face ID (e.g., 'S1', 'S2', etc.) if a face lies on the symmetry plane, None otherwise
    """
    # Define face node mappings for different element types
    face_nodes = {
        'C3D15': {  # Wedge element with 15 nodes
            'S1': [0, 1, 2, 6, 7, 8],      # Bottom triangular face
            'S2': [3, 4, 5, 9, 10, 11],    # Top triangular face
            'S3': [0, 1, 4, 3, 6, 13, 9, 12], # Front quadrilateral face
            'S4': [1, 2, 5, 4, 7, 14, 10, 13], # Right quadrilateral face
            'S5': [0, 3, 5, 2, 12, 11, 14, 8]  # Back quadrilateral face
        },
        'C3D10': {  # Tetrahedral element with 10 nodes
            'S1': [0, 1, 2, 4, 5, 6],      # Face 1: nodes 1-2-3 and mid-edge nodes
            'S2': [0, 1, 3, 4, 8, 7],      # Face 2: nodes 1-2-4 and mid-edge nodes
            'S3': [1, 2, 3, 5, 9, 8],      # Face 3: nodes 2-3-4 and mid-edge nodes
            'S4': [0, 2, 3, 6, 9, 7]       # Face 4: nodes 1-3-4 and mid-edge nodes
        },
        'C3D4': {  # Tetrahedral element with 4 nodes
            'S1': [0, 1, 2],
            'S2': [0, 3, 1],
            'S3': [1, 3, 2],
            'S4': [0, 2, 3]
        }
    }
    
    if element_type.upper() not in face_nodes:
        return None
    
    face_definitions = face_nodes[element_type.upper()]
    
    # For each face, check if all nodes are on the symmetry plane
    for face_id, node_indices in face_definitions.items():
        face_node_ids = [element_nodes[i] for i in node_indices]
        
        # Check if all nodes in this face are on the symmetry plane
        if all(node_id in symmetry_plane_nodes_set for node_id in face_node_ids):
            return face_id
    
    return None

def merge_shared_surfaces(surfaces, elements, nodes):
    """
    Merge surfaces that form original/mirrored pairs with MIRRORED_ prefix.
    
    Only merges surfaces if:
    1. One has the MIRRORED_ prefix and the other doesn't
    2. They have the same base name (ignoring face suffixes like _S1)
    3. They share face nodes
    
    Parameters:
    - surfaces: Dict {surface_name: {'elements': set(element_ids), 'face_id': str, 'faces': [(elset, face_id)], ...}}
    - elements: Dict {element_id: {'type': str, 'nodes': [node_ids], ...}}
    - nodes: Dict {node_id: (x, y, z)}
    
    Returns:
    - Dict {surface_name: {'elements': set(element_ids), 'face_id': str, 'faces': [(elset, face_id)], ...}}
    """
    # Define face node mappings for different element types
    face_node_definitions = {
        'C3D15': {  # Wedge element with 15 nodes
            'S1': [0, 1, 2, 6, 7, 8],      # Bottom triangular face
            'S2': [3, 4, 5, 9, 10, 11],    # Top triangular face
            'S3': [0, 1, 4, 3, 6, 13, 9, 12], # Front quadrilateral face
            'S4': [1, 2, 5, 4, 7, 14, 10, 13], # Right quadrilateral face
            'S5': [0, 3, 5, 2, 12, 11, 14, 8]  # Back quadrilateral face
        },
        'C3D10': {  # Tetrahedral element with 10 nodes
            'S1': [0, 1, 2, 4, 5, 6],      # Face 1: nodes 1-2-3 and mid-edge nodes
            'S2': [0, 1, 3, 4, 8, 7],      # Face 2: nodes 1-2-4 and mid-edge nodes
            'S3': [1, 2, 3, 5, 9, 8],      # Face 3: nodes 2-3-4 and mid-edge nodes
            'S4': [0, 2, 3, 6, 9, 7]       # Face 4: nodes 1-3-4 and mid-edge nodes
        },
        'C3D4': {  # Tetrahedral element with 4 nodes
            'S1': [0, 1, 2],
            'S2': [0, 3, 1],
            'S3': [1, 3, 2],
            'S4': [0, 2, 3]
        },
        'C3D20': {  # Hexahedral element with 20 nodes
            'S1': [0, 1, 2, 3, 8, 9, 10, 11],      # Face 1
            'S2': [4, 5, 6, 7, 12, 13, 14, 15],    # Face 2
            'S3': [0, 1, 5, 4, 8, 17, 12, 16],     # Face 3
            'S4': [1, 2, 6, 5, 9, 18, 13, 17],     # Face 4
            'S5': [2, 3, 7, 6, 10, 19, 14, 18],    # Face 5
            'S6': [0, 4, 7, 3, 16, 15, 19, 11]     # Face 6
        },
        'C3D20R': {  # Reduced integration hexahedral element with 20 nodes (same as C3D20)
            'S1': [0, 1, 2, 3, 8, 9, 10, 11],      # Face 1
            'S2': [4, 5, 6, 7, 12, 13, 14, 15],    # Face 2
            'S3': [0, 1, 5, 4, 8, 17, 12, 16],     # Face 3
            'S4': [1, 2, 6, 5, 9, 18, 13, 17],     # Face 4
            'S5': [2, 3, 7, 6, 10, 19, 14, 18],    # Face 5
            'S6': [0, 4, 7, 3, 16, 15, 19, 11]     # Face 6
        }
    }
    
    def get_surface_face_nodes(surface_name, surface_data):
        """Get all unique face nodes for a surface."""
        face_nodes = set()
        
        # Handle surfaces that have 'faces' list (new format)
        if 'faces' in surface_data:
            for elset_name, face_id in surface_data['faces']:
                # Get elements from the elset_name if it exists in elements
                for elem_id, elem_data in elements.items():
                    elem_type = elem_data.get('type', '').upper()
                    elem_nodes = elem_data.get('nodes', [])
                    
                    if elem_type not in face_node_definitions:
                        continue
                        
                    if face_id not in face_node_definitions[elem_type]:
                        continue
                        
                    # Get the node indices for this face
                    face_node_indices = face_node_definitions[elem_type][face_id]
                    
                    # Add the actual node IDs to the set
                    for node_index in face_node_indices:
                        if node_index < len(elem_nodes):
                            face_nodes.add(elem_nodes[node_index])
        
        # Handle surfaces that have direct element sets (legacy format)
        elif 'elements' in surface_data:
            face_id = surface_data.get('face_id', 'S1')
            element_set = surface_data.get('elements', set())
            
            for elem_id in element_set:
                if elem_id not in elements:
                    continue
                    
                elem_data = elements[elem_id]
                elem_type = elem_data.get('type', '').upper()
                elem_nodes = elem_data.get('nodes', [])
                
                if elem_type not in face_node_definitions:
                    continue
                    
                if face_id not in face_node_definitions[elem_type]:
                    continue
                    
                # Get the node indices for this face
                face_node_indices = face_node_definitions[elem_type][face_id]
                
                # Add the actual node IDs to the set
                for node_index in face_node_indices:
                    if node_index < len(elem_nodes):
                        face_nodes.add(elem_nodes[node_index])
        
        return face_nodes
    
    def get_base_surface_name(surface_name):
        """Extract base name without MIRRORED_ prefix and face suffixes like _S1, _S2, etc."""
        # Remove MIRRORED_ prefix if present
        if surface_name.startswith('MIRRORED_'):
            base_name = surface_name[9:]  # Remove 'MIRRORED_' prefix
        else:
            base_name = surface_name
        
        # Remove face suffixes like _S1, _S2, etc.
        import re
        # Pattern to match face suffixes: _S followed by digits
        base_name = re.sub(r'_S\d+$', '', base_name)
        
        return base_name
    
    def find_surface_pairs():
        """Find original/mirrored surface pairs that should be merged."""
        pairs = []
        processed = set()
        
        for surf_name in surfaces.keys():
            if surf_name in processed:
                continue
                
            # Check if this is a mirrored surface
            if surf_name.startswith('MIRRORED_'):
                # Find corresponding original surface
                base_name = get_base_surface_name(surf_name)
                
                # Look for original surface with same base name
                for other_surf_name in surfaces.keys():
                    if (not other_surf_name.startswith('MIRRORED_') and 
                        get_base_surface_name(other_surf_name) == base_name):
                        
                        pairs.append((other_surf_name, surf_name))
                        processed.add(surf_name)
                        processed.add(other_surf_name)
                        break
            else:
                # Check if there's a corresponding mirrored surface
                base_name = get_base_surface_name(surf_name)
                mirrored_name = None
                
                # Look for mirrored surface with same base name
                for other_surf_name in surfaces.keys():
                    if (other_surf_name.startswith('MIRRORED_') and 
                        get_base_surface_name(other_surf_name) == base_name):
                        
                        mirrored_name = other_surf_name
                        break
                
                if mirrored_name:
                    pairs.append((surf_name, mirrored_name))
                    processed.add(surf_name)
                    processed.add(mirrored_name)
        
        return pairs
    
    # Find all original/mirrored pairs
    surface_pairs = find_surface_pairs()
    
    merged_surfaces = {}
    processed_surfaces = set()
    
    # Process each pair
    for original_name, mirrored_name in surface_pairs:
        original_data = surfaces[original_name]
        mirrored_data = surfaces[mirrored_name]
        
        # Get face nodes for both surfaces
        original_nodes = get_surface_face_nodes(original_name, original_data)
        mirrored_nodes = get_surface_face_nodes(mirrored_name, mirrored_data)
        
        # Check if they share any face nodes
        if original_nodes & mirrored_nodes:  # Intersection of sets
            # Merge the surfaces
            merged_faces = []
            
            # Combine faces from both surfaces
            if 'faces' in original_data:
                merged_faces.extend(original_data['faces'])
            if 'faces' in mirrored_data:
                merged_faces.extend(mirrored_data['faces'])
            
            # Create merged surface using original name (without MIRRORED_ prefix)
            base_name = get_base_surface_name(original_name)
            merged_name = base_name
            
            # Create merged surface data based on original surface
            merged_surface_data = original_data.copy()
            if merged_faces:
                merged_surface_data['faces'] = merged_faces
            
            merged_surfaces[merged_name] = merged_surface_data
            processed_surfaces.add(original_name)
            processed_surfaces.add(mirrored_name)
        else:
            # No shared nodes, keep both surfaces separate
            merged_surfaces[original_name] = original_data
            merged_surfaces[mirrored_name] = mirrored_data
            processed_surfaces.add(original_name)
            processed_surfaces.add(mirrored_name)
    
    # Add any surfaces that weren't part of pairs
    for surf_name, surf_data in surfaces.items():
        if surf_name not in processed_surfaces:
            merged_surfaces[surf_name] = surf_data
    
    return merged_surfaces

def find_nodes_on_cylinder(nodes, target_radius, tolerance=0.015, center=(0, 0), direction=None, height=None, chamfer=[0.0, 0.0]):
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
            center[0] - direction[0]*height/1.95,  # Top = center - direction*height/2
            center[1] - direction[1]*height/1.95 if len(center) > 2 else -direction[1]*height/2,
            center[2] - direction[2]*height/1.95 if len(center) > 2 else -direction[2]*height/2
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

def translate_nodes_radially(nodes, radius_shift, center=(0, 0), direction=None, height=None, chamfer=[0.0, 0.0]):
    """
    Translate nodes on a cylindrical surface radially outward by a specified distance.
    
    Parameters:
    - nodes: Dictionary of nodes to translate {node_id: [x, y, z]}
    - radius_shift: Distance to shift nodes radially (positive for outward, negative for inward)
    - center: (x, z) or (x, y, z) coordinates of the cylinder center point
    - direction: Direction vector of the cylinder axis [dx, dy, dz]
    - height: Height of the cylinder
    - chamfer: Chamfer parameters for the cylinder
    
    Returns:
    - Dictionary of translated nodes {node_id: [new_x, new_y, new_z]}
    """
    # Validate chamfer parameters
    if chamfer is None or not isinstance(chamfer, list) or len(chamfer) < 2:
        chamfer = [0.0, 0.0]
        
    translated_nodes = {}
    
    # If no direction specified, default to Y-axis (simple cylinder along Y)
    if direction is None or height is None:
        # Simple Y-parallel cylinder
        x_center, z_center = center
        
        for node_id, coords in nodes.items():
            x, y, z = coords
            
            # Calculate vector from axis to node (in XZ plane)
            dx = x - x_center
            dz = z - z_center
            
            # Calculate current distance from axis
            current_radius = math.sqrt(dx**2 + dz**2)
            
            if current_radius < 1e-10:  # Handle nodes on the axis
                print(f"[WARNING] Node {node_id} is very close to axis, cannot determine radial direction")
                translated_nodes[node_id] = coords  # Keep original coordinates
                continue
                
            # Calculate unit vector in radial direction
            unit_radial_x = dx / current_radius
            unit_radial_z = dz / current_radius
            
            # Translate node along radial vector
            new_x = x + unit_radial_x * radius_shift
            new_z = z + unit_radial_z * radius_shift
            
            # Store new coordinates
            translated_nodes[node_id] = [new_x, y, new_z]
    else:
        # Handle angled cylinder
        # Normalize direction vector
        dir_length = math.sqrt(sum(d*d for d in direction))
        if dir_length < 1e-10:  # Avoid division by zero
            direction = [0, 1, 0]  # Default to Y-axis if direction is too small
        else:
            direction = [d/dir_length for d in direction]
        
        # Define cylinder endpoints based on center, direction and height
        bottom_point = [
            center[0] + direction[0]*(chamfer[0]*1.05+height/2),
            center[1] + direction[1]*(chamfer[0]*1.05+height/2) if len(center) > 2 else direction[1]*height/2,
            center[2] + direction[2]*(chamfer[0]*1.05+height/2) if len(center) > 2 else direction[2]*height/2
        ]
        
        top_point = [
            center[0] - direction[0]*height/1.95,
            center[1] - direction[1]*height/1.95 if len(center) > 2 else -direction[1]*height/2,
            center[2] - direction[2]*height/1.95 if len(center) > 2 else -direction[2]*height/2
        ]
        
        # Vector along cylinder axis
        axis_vector = [top_point[i] - bottom_point[i] for i in range(3)]
        
        # Process each node
        for node_id, coords in nodes.items():
            p0 = coords  # Node coordinates
            p1 = bottom_point  # First point on axis
            p2 = top_point  # Second point on axis
            
            # Vector from p1 to p2 (axis direction)
            v = [p2[i] - p1[i] for i in range(3)]
            # Vector from p1 to p0 (node)
            w = [p0[i] - p1[i] for i in range(3)]
            
            # Calculate dot products
            c1 = sum(w[i]*v[i] for i in range(3))  # dot product of w and v
            c2 = sum(v[i]*v[i] for i in range(3))  # dot product of v and v
            
            # Parameter for closest point on line
            b = c1 / c2 if c2 > 0 else 0
            
            # Calculate closest point on axis
            pb = [p1[i] + b*v[i] for i in range(3)]
            
            # Vector from axis to node (radial direction)
            radial_vector = [p0[i] - pb[i] for i in range(3)]
            
            # Calculate current distance from axis
            current_radius = math.sqrt(sum(r**2 for r in radial_vector))
            
            if current_radius < 1e-10:  # Handle nodes on the axis
                print(f"[WARNING] Node {node_id} is very close to axis, cannot determine radial direction")
                translated_nodes[node_id] = coords  # Keep original coordinates
                continue
                
            # Calculate unit vector in radial direction
            unit_radial = [r / current_radius for r in radial_vector]
            
            # Translate node along radial vector
            new_coords = [p0[i] + unit_radial[i] * radius_shift for i in range(3)]
            
            # Store new coordinates
            translated_nodes[node_id] = new_coords
    
    print(f"[INFO] Translated {len(translated_nodes)} nodes radially by {radius_shift} units")
    
    return translated_nodes

def clean_inp_file(inp_file, replace_file=False):
    """
    Clean the INP file by removing unnecessary elements like line elements (T3D3) and surface elements (CPS6).
    Creates a new file with '_cleaned' added to the original filename.
    
    Args:
        inp_file: Path to the original INP file
        
    Returns:
        Path to the cleaned INP file
    """
    # Create output filename
    file_base, file_ext = os.path.splitext(inp_file)
    
    output_file = f"{file_base}_cleaned{file_ext}"
    
    print(f"Cleaning INP file: {inp_file}")
    print(f"Removing line elements and surface elements")
    
    # Elements to remove (can be extended if needed)
    element_types_to_remove = ["T3D2", "T3D3", "CPS3", "CPS4", "CPS6", "CPS8"]
    
    with open(inp_file, 'r') as infile, open(output_file, 'w') as outfile:
        # State tracking
        skip_section = False
        current_element_type = None
        elset_name = None
        
        # Process line by line
        line_count = 0
        skipped_lines = 0
        
        for line in infile:
            line_count += 1
            
            # Check if this is an element definition line
            if line.strip().startswith("*ELEMENT"):
                # Parse the element type
                parts = line.strip().split(",")
                for part in parts:
                    if part.strip().startswith("type="):
                        current_element_type = part.strip()[5:]  # Remove "type="
                    if part.strip().startswith("ELSET="):
                        elset_name = part.strip()[6:]  # Remove "ELSET="
                
                # Decide whether to skip this section
                if current_element_type in element_types_to_remove:
                    skip_section = True
                    #print(f"Skipping element type {current_element_type} (ELSET={elset_name})")
                    skipped_lines += 1
                    continue
                else:
                    skip_section = False
            
            # Check if we're starting a new section
            elif line.strip().startswith("*"):
                skip_section = False
                current_element_type = None
                elset_name = None
            
            # Skip lines if we're in a section to be removed
            if skip_section:
                skipped_lines += 1
                continue
            
            # Write line to output file
            outfile.write(line)
    if replace_file:
        os.remove(inp_file)
        os.rename(output_file, inp_file)
    print(f"Cleaning complete. Processed {line_count} lines, removed {skipped_lines} lines.")
    print(f"Cleaned file saved to: {output_file}")
    
    return output_file

def determine_plane_faces(element_nodes, nodes_3d, plane_point, plane_normal, element_type, tolerance=0.015, max_radius=0):
    """Determine which faces of an element are on a specified plane
    
    Parameters:
    - element_nodes: List of node IDs for the element
    - nodes_3d: Dictionary of node coordinates {node_id: [x, y, z]}
    - plane_point: Point on the plane [x, y, z]
    - plane_normal: Normal vector of the plane [nx, ny, nz]
    - element_type: Type of element (C3D20, C3D15, C3D10)
    - tolerance: Tolerance for plane distance comparison
    - max_radius: If > 0, only include nodes within this distance from plane_point (default: 0, no restriction)
    
    Returns:
    - List of face identifiers ('S1', 'S2', etc.) that are on the specified plane
    """
    # Face node mappings for different element types
    face_nodes = {
        'C3D20': {  # Hexahedral element with 20 nodes
            'S1': [0,1,2,3, 8,9,10,11],  # Bottom face
            'S2': [4,5,6,7, 12,13,14,15],  # Top face
            'S3': [0,1,4,5, 8,12,16,17],  # Front face
            'S4': [1,2,5,6, 9,13,17,18],  # Right face
            'S5': [2,3,6,7, 10,14,18,19],  # Back face
            'S6': [3,0,7,4, 11,15,19,16]   # Left face
        },
        'C3D20R': {  # Hexahedral element with 20 nodes
            'S1': [0,1,2,3, 8,9,10,11],  # Bottom face
            'S2': [4,5,6,7, 12,13,14,15],  # Top face
            'S3': [0,1,4,5, 8,12,16,17],  # Front face
            'S4': [1,2,5,6, 9,13,17,18],  # Right face
            'S5': [2,3,6,7, 10,14,18,19],  # Back face
            'S6': [3,0,7,4, 11,15,19,16]   # Left face
        },
        'C3D15': {  # Wedge element with 15 nodes
            'S1': [0,1,2, 6,7,8],      # Bottom triangular face
            'S2': [3,4,5, 9,10,11],    # Top triangular face
            'S3': [0,1,4,3, 6,13,9,12], # Front quadrilateral face
            'S4': [1,2,5,4, 7,14,10,13], # Right quadrilateral face
            'S5': [0,3,5,2, 12,11,14,8]  # Back quadrilateral face
        },
        'C3D10': {  # Tetrahedral element with 10 nodes
            'S1': [0, 1, 2, 4, 5, 6],      # Face 1: nodes 1-2-3 and mid-edge nodes
            'S2': [0, 1, 3, 4, 8, 7],      # Face 2: nodes 1-2-4 and mid-edge nodes
            'S3': [1, 2, 3, 5, 9, 8],      # Face 3: nodes 2-3-4 and mid-edge nodes
            'S4': [0, 2, 3, 6, 9, 7]       # Face 4: nodes 1-3-4 and mid-edge nodes
        },
        'C3D4': {  # Tetrahedral element with 4 nodes
            'S1': [0,1,2],
            'S2': [0,3,1],
            'S3': [1,3,2],
            'S4': [0,2,3]
        }
    }
    
    # Get face definitions for this element type
    if element_type not in face_nodes:
        print(f"[WARNING] Unknown element type: {element_type}")
        return []
    
    face_definitions = face_nodes[element_type]
    plane_faces = []
    
    # Calculate required number of nodes on plane based on element type
    required_nodes = {
        'C3D20': 6,  # At least 6 out of 8 nodes for hexahedral faces
        'C3D20R': 6,  # At least 6 out of 8 nodes for hexahedral faces
        'C3D15': 6,  # At least 4 out of 6 nodes for triangular faces or 6 out of 8 for quadrilateral faces
        'C3D10': 4,  # At least 4 out of 6 nodes for tetrahedral faces
        'C3D4': 3    # At least 3 out of 3 nodes for tetrahedral faces
    }
    
    # Normalize plane normal vector
    normal_length = math.sqrt(sum(n*n for n in plane_normal))
    if normal_length < 1e-10:  # Avoid division by zero
        print(f"[WARNING] Invalid plane normal vector: {plane_normal}")
        return []
    
    plane_normal = [n/normal_length for n in plane_normal]
    
    # For each face in the element
    for face, node_indices in face_definitions.items():
        # Get coordinates for nodes in this face
        face_coords = [nodes_3d[element_nodes[i]] for i in node_indices]
        
        # Calculate distances to plane for all nodes in this face
        # Using point-plane distance formula: d = |ax₀ + by₀ + cz₀ + d| / √(a² + b² + c²)
        # Where (a,b,c) is the normal vector and d = -(ax₁ + by₁ + cz₁) for a point (x₁,y₁,z₁) on the plane
        # Since normal is already normalized, we can simplify to d = |n·(p - p₀)|
        
        plane_distances = []
        nodes_within_radius = []
        
        for coords in face_coords:
            # Vector from plane point to node
            vec_to_node = [coords[i] - plane_point[i] for i in range(3)]
            
            # Calculate dot product: n·(p - p₀) for plane distance
            distance = abs(sum(plane_normal[i] * vec_to_node[i] for i in range(3)))
            plane_distances.append(distance)
            
            # Check if we need to apply radius constraint
            if max_radius > 0:
                # Calculate Euclidean distance from plane_point to the node
                distance_from_point = math.sqrt(sum(vec_to_node[i]**2 for i in range(3)))
                nodes_within_radius.append(distance_from_point <= max_radius)
            else:
                # No radius constraint
                nodes_within_radius.append(True)
        
        # Count nodes that are (1) close to the plane and (2) within the specified radius
        nodes_on_plane = sum(1 for d, within_radius in zip(plane_distances, nodes_within_radius) 
                           if d < tolerance and within_radius)
        
        # Check if enough nodes are on the plane and within radius
        if nodes_on_plane >= required_nodes[element_type]:
            plane_faces.append(face)
    
    return plane_faces

def format_plane_surfaces(nodes_3d, elements, plane_point, plane_normal, set_name, tolerance=0.015, max_radius=0):
    """Write plane surface definitions with specific face designations
    
    Parameters:
    - nodes_3d: Dictionary of node coordinates {node_id: [x, y, z]}
    - elements: Dictionary of original elements {elem_id: {'type': element_type, 'nodes': [node_ids]}}
    - plane_point: Point on the plane [x, y, z]
    - plane_normal: Normal vector of the plane [nx, ny, nz]
    - set_name: Base name for the surface sets
    - tolerance: Tolerance for plane distance comparison
    - max_radius: If > 0, only include nodes within this distance from plane_point (default: 0, no restriction)
    
    Returns:
    - Dictionary of face elements organized by face designation
    """
    # Initialize face elements dictionary for each element type
    face_elements = {
        'original': {'S1': [], 'S2': [], 'S3': [], 'S4': [], 'S5': [], 'S6': []},
    }

    # Normalize plane normal vector
    normal_length = math.sqrt(sum(n*n for n in plane_normal))
    if normal_length < 1e-10:  # Avoid division by zero
        print(f"[WARNING] Invalid plane normal vector: {plane_normal}")
        return face_elements
    
    plane_normal = [n/normal_length for n in plane_normal]

    # Defensive type check
    if not isinstance(elements, dict):
        raise TypeError(f"elements must be a dictionary, got {type(elements)}")
        
    # Process original elements
    for elem_id, element_data in elements.items():
        element_type = element_data['type']
        element_nodes = element_data['nodes']
        
        # Determine plane faces for this element
        plane_faces = determine_plane_faces(
            element_nodes, nodes_3d, plane_point, plane_normal, 
            element_type, tolerance=tolerance, max_radius=max_radius)
            
        # Add element to appropriate face lists
        for face in plane_faces:
            face_elements['original'][face].append(elem_id)

    # Count total faces found
    total_faces = sum(len(faces) for faces in face_elements['original'].values())
    print(f"[INFO] Created plane surfaces for {set_name}:")
    print(f"[INFO] - Original elements: {total_faces} faces")
    if max_radius > 0:
        print(f"[INFO] - Applied radius constraint: {max_radius} units from reference point")
    
    return face_elements

def get_surface_nodes(surface_name, nodes, elements, existing_sets):
    """Extract all node IDs that form a specific surface
    
    Parameters:
    - surface_name: Name of the surface to extract nodes from
    - nodes: Dictionary of node coordinates {node_id: [x, y, z]}
    - elements: Dictionary of elements {elem_id: {'type': element_type, 'nodes': [node_ids]}}
    - existing_sets: Dictionary of sets {'nsets': {}, 'elsets': {}, 'surfaces': {}}
    
    Returns:
    - Set of node IDs that form the specified surface
    """
    # Check if the surface exists
    if 'surfaces' not in existing_sets or surface_name not in existing_sets['surfaces']:
        #print(f"[WARNING] Surface {surface_name} not found in existing sets")
        return set()
    
    # Get surface definition
    surface = existing_sets['surfaces'][surface_name]
    surface_faces = surface.get('faces', [])
    
    # Face node mappings for different element types
    face_nodes = {
        'C3D20': {  # Hexahedral element with 20 nodes
            'S1': [0,1,2,3, 8,9,10,11],  # Bottom face
            'S2': [4,5,6,7, 12,13,14,15],  # Top face
            'S3': [0,1,4,5, 8,12,16,17],  # Front face
            'S4': [1,2,5,6, 9,13,17,18],  # Right face
            'S5': [2,3,6,7, 10,14,18,19],  # Back face
            'S6': [3,0,7,4, 11,15,19,16]   # Left face
        },
        'C3D20R': {  # Hexahedral element with 20 nodes
            'S1': [0,1,2,3, 8,9,10,11],  # Bottom face
            'S2': [4,5,6,7, 12,13,14,15],  # Top face
            'S3': [0,1,4,5, 8,12,16,17],  # Front face
            'S4': [1,2,5,6, 9,13,17,18],  # Right face
            'S5': [2,3,6,7, 10,14,18,19],  # Back face
            'S6': [3,0,7,4, 11,15,19,16]   # Left face
        },
        'C3D15': {  # Wedge element with 15 nodes
            'S1': [0,1,2, 6,7,8],      # Bottom triangular face
            'S2': [3,4,5, 9,10,11],    # Top triangular face
            'S3': [0,1,4,3, 6,13,9,12], # Front quadrilateral face
            'S4': [1,2,5,4, 7,14,10,13], # Right quadrilateral face
            'S5': [0,3,5,2, 12,11,14,8]  # Back quadrilateral face
        },
        'C3D10': {  # Tetrahedral element with 10 nodes
            'S1': [0, 1, 2, 4, 5, 6],      # Face 1: nodes 1-2-3 and mid-edge nodes
            'S2': [0, 1, 3, 4, 8, 7],      # Face 2: nodes 1-2-4 and mid-edge nodes
            'S3': [1, 2, 3, 5, 9, 8],      # Face 3: nodes 2-3-4 and mid-edge nodes
            'S4': [0, 2, 3, 6, 9, 7]       # Face 4: nodes 1-3-4 and mid-edge nodes
        }
    }
    
    # Collect all node IDs
    surface_nodes = set()
    
    # Process each face in the surface
    for elset_name, face_id in surface_faces:
        # Get the element set
        if elset_name not in existing_sets['elsets']:
            print(f"[WARNING] Element set {elset_name} referenced in surface {surface_name} not found")
            continue
            
        # Get element IDs in this set
        element_ids = existing_sets['elsets'][elset_name].get('elements', [])
        
        # Process each element
        for elem_id in element_ids:
            if elem_id not in elements:
                print(f"[WARNING] Element {elem_id} referenced in set {elset_name} not found")
                continue
                
            # Get element data
            element_data = elements[elem_id]
            element_type = element_data['type']
            element_nodes = element_data['nodes']
            
            # Check if we have face definition for this element type
            if element_type not in face_nodes:
                print(f"[WARNING] Unknown element type: {element_type} for element {elem_id}")
                continue
                
            # Check if we have this face definition
            if face_id not in face_nodes[element_type]:
                print(f"[WARNING] Unknown face ID: {face_id} for element type {element_type}")
                continue
                
            # Get local node indices for this face
            face_local_indices = face_nodes[element_type][face_id]
            
            # Convert local indices to global node IDs
            for local_idx in face_local_indices:
                if local_idx < len(element_nodes):
                    node_id = element_nodes[local_idx]
                    surface_nodes.add(node_id)
                else:
                    print(f"[WARNING] Local node index {local_idx} out of range for element {elem_id}")
    
    print(f"[INFO] Found {len(surface_nodes)} nodes in surface {surface_name}")
    return surface_nodes

def create_wedge_elements_from_faces(cylinder_faces, nodes, elements, target_radius, extrusion_distance, direction, height, num_layers=1, center=(0, 0), chamfer=[0.0, 0.0]):
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
    
    # First pass: create extruded nodes for all nodes on cylinder for each layer
    nodes_on_cylinder = set()
    for face_info in cylinder_faces:
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
                #print(f"[WARNING] Node {node_id} not found in nodes dictionary")
                continue
            
            x, y, z = nodes[node_id]
            
            # Create duplicate of original node (first layer inner node)
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
                #print(f"[WARNING] Node {node1} not found in nodes dictionaries")
                continue
            if node2 not in new_nodes and node2 not in nodes:
                #print(f"[WARNING] Node {node2} not found in nodes dictionaries")
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
        
        # Process quadrilateral faces by splitting into two triangles
        elif len(vertex_nodes) == 4:  # It's a quadrilateral face
            v1, v2, v3, v4 = vertex_nodes
            
            # Split into two triangles: (v1, v2, v3) and (v1, v3, v4)
            # Process each triangle similar to above...
            
            # This part would be implemented for quadrilateral faces
            # For brevity, I'm omitting this implementation
    
    print(f"[DEBUG] Created {wedge_count} wedge elements across {num_layers} layers")
    print(f"[DEBUG] Created {len(new_nodes)} new nodes total")
    return new_nodes, wedge_elements

def get_radial_vectors(surface_nodes, nodes, center, direction):
    """Calculate radial vectors for nodes on a cylindrical surface
    
    Parameters:
    - surface_nodes: Set or list of node IDs on the surface
    - nodes: Dictionary of node coordinates {node_id: [x, y, z]}
    - center: Center point of the cylinder [x, y, z]
    - direction: Direction vector of cylinder axis [dx, dy, dz]
    
    Returns:
    - Dictionary mapping node IDs to their radial vectors [rx, ry, rz]
    - Dictionary of node groups with similar radial directions
    """
    # Ensure center and direction are numeric
    try:
        center = [float(c) for c in center]
        direction = [float(d) for d in direction]
    except (ValueError, TypeError):
        print("[WARNING] Invalid center or direction values. Converting to default values.")
        center = [0.0, 0.0, 0.0]
        direction = [0.0, -1.0, 0.0]
    
    # Ensure center has 3 dimensions
    if len(center) == 2:
        center = [center[0], 0, center[1]]
        
    # Normalize direction vector
    dir_length = math.sqrt(sum(d*d for d in direction))
    if dir_length < 1e-10:  # Avoid division by zero
        direction = [0, -1, 0]  # Default to Y-axis
    else:
        direction = [d/dir_length for d in direction]
        
    # Dictionary to store radial vectors
    radial_vectors = {}
    
    # Dictionary to group nodes by similar radial directions
    # We'll use a string representation of the rounded vector as the key
    node_groups = {}
    
    for node_id in surface_nodes:
        # Get node coordinates
        node_coords = nodes.get(node_id)
        if not node_coords:
            continue
            
        # Project node onto cylinder axis
        # Calculate the vector from center to node
        vector = [node_coords[0] - center[0], node_coords[1] - center[1], node_coords[2] - center[2]]
        
        # Calculate the dot product
        dot_product = sum(vector[i] * direction[i] for i in range(3))
        
        # Calculate the projection onto the axis
        projected_point = [
            center[0] + dot_product * direction[0],
            center[1] + dot_product * direction[1],
            center[2] + dot_product * direction[2]
        ]
        
        # Calculate the radial vector from projection to node
        radial_vector = [
            node_coords[0] - projected_point[0],
            node_coords[1] - projected_point[1],
            node_coords[2] - projected_point[2]
        ]
        
        # Normalize the radial vector
        radial_length = math.sqrt(sum(r*r for r in radial_vector))
        if radial_length > 1e-10:  # Avoid division by zero
            radial_vector = [r/radial_length for r in radial_vector]
            
            # Store the radial vector
            radial_vectors[node_id] = radial_vector
            
            # Group nodes by similar radial directions
            # Round to 2 decimal places to group similar directions
            rounded_vector = [round(r, 2) for r in radial_vector]
            group_key = str(rounded_vector)
            
            if group_key not in node_groups:
                node_groups[group_key] = {
                    'nodes': [],
                    'vector': radial_vector
                }
            node_groups[group_key]['nodes'].append(node_id)
    
    return radial_vectors, node_groups

def get_surface_nodes_with_vectors(surface_name, nodes, elements, existing_sets, center, direction):
    """Extract all node IDs that form specific surfaces and calculate their radial vectors
    
    Parameters:
    - surface_name: Name of the surface or list of surface names to extract nodes from
    - nodes: Dictionary of node coordinates {node_id: [x, y, z]}
    - elements: Dictionary of elements {elem_id: {'type': element_type, 'nodes': [node_ids]}}
    - existing_sets: Dictionary of sets {'nsets': {}, 'elsets': {}, 'surfaces': {}}
    - center: Center point of the cylinder [x, y, z]
    - direction: Direction vector of cylinder axis [dx, dy, dz]
    
    Returns:
    - Set of node IDs that form the specified surface(s)
    - Dictionary mapping node IDs to their radial vectors
    - Dictionary of node groups with similar radial directions
    """
    # Validate inputs
    if center is None or direction is None:
        print(f"[WARNING] Missing center or direction for surface(s). Using defaults.")
        center = [0.0, 0.0, 0.0]
        direction = [0.0, -1.0, 0.0]
    
    # Check if surface_name is a single string or a list of strings
    if isinstance(surface_name, str):
        # Single surface - use original behavior
        surface_nodes = get_surface_nodes(surface_name, nodes, elements, existing_sets)
        print(f"[INFO] Processing single surface: {surface_name} with {len(surface_nodes)} nodes")
    else:
        # List of surfaces - collect nodes from all surfaces
        surface_nodes = set()
        for name in surface_name:
            nodes_for_surface = get_surface_nodes(name, nodes, elements, existing_sets)
            surface_nodes.update(nodes_for_surface)
            print(f"[INFO] Added {len(nodes_for_surface)} nodes from surface: {name}")
        print(f"[INFO] Total unique nodes from {len(surface_name)} surfaces: {len(surface_nodes)}")
    
    # Calculate radial vectors and group nodes
    radial_vectors, node_groups = get_radial_vectors(surface_nodes, nodes, center, direction)
    
    return surface_nodes, radial_vectors, node_groups

def _parse_orientation_definitions(lines):
    """Extract orientation definitions and return a mapping keyed by name."""
    orientation_map = {}
    i = 0
    while i < len(lines):
        line = lines[i].strip()
        if line.lstrip().upper().startswith('*ORIENTATION'):
            name_match = re.search(r'NAME=([^,]+)', line, re.IGNORECASE)
            orientation_name = name_match.group(1).strip() if name_match else None
            values = []
            j = i + 1
            while j < len(lines):
                next_line = lines[j].strip()
                if next_line.startswith('*'):
                    break
                if next_line and not next_line.startswith('**'):
                    for val in next_line.replace(',', ' ').split():
                        try:
                            values.append(float(val))
                        except ValueError:
                            continue
                j += 1
            key = orientation_name.upper() if orientation_name else None
            if key:
                vector1 = values[:3] if len(values) >= 3 else []
                vector2 = values[3:6] if len(values) >= 6 else []
                orientation_map[key] = {
                    'name': orientation_name,
                    'vector1': vector1,
                    'vector2': vector2
                }
            i = j - 1
        i += 1
    return orientation_map

def _extract_node_coordinates_from_lines(lines):
    """Extract node coordinates from INP file lines."""
    node_coords = {}
    in_node_block = False
    
    for raw_line in lines:
        line = raw_line.strip()
        if not line:
            continue
        
        if line.startswith('*'):
            in_node_block = line.upper().startswith('*NODE')
            continue
        
        if in_node_block:
            parts = [p.strip() for p in line.split(',') if p.strip()]
            if len(parts) >= 4:
                try:
                    node_id = int(parts[0])
                    x, y, z = map(float, parts[1:4])
                    node_coords[node_id] = [x, y, z]
                except ValueError:
                    continue
    
    return node_coords

def _extract_spring_elements_from_lines(lines):
    """Extract SPRING1 element definitions (element ID, node ID, elset)."""
    spring_elements = []
    current_elset = None
    current_type = None
    
    for idx, raw_line in enumerate(lines):
        line = raw_line.strip()
        if not line:
            continue
        
        if line.startswith('*'):
            current_elset = None
            current_type = None
            
            if line.upper().startswith('*ELEMENT'):
                # Parse element type
                type_match = re.search(r'TYPE=([^,]+)', line, re.IGNORECASE)
                elset_match = re.search(r'ELSET=([^,]+)', line, re.IGNORECASE)
                
                if type_match and type_match.group(1).strip().upper() == 'SPRING1':
                    current_type = 'SPRING1'
                    current_elset = elset_match.group(1).strip() if elset_match else None
            continue
        
        if current_type == 'SPRING1':
            parts = [p.strip() for p in line.split(',') if p.strip()]
            if len(parts) >= 2:
                try:
                    elem_id = int(parts[0])
                    node_id = int(parts[1])
                except ValueError:
                    continue
                spring_elements.append({
                    'elem_id': elem_id,
                    'node_id': node_id,
                    'elset': current_elset,
                    'line_index': idx
                })
    
    return spring_elements

def _extract_spring_elsets(lines):
    """Extract spring elset definitions mapping elset name -> element IDs."""
    elset_map = defaultdict(list)
    current_elset = None
    
    for raw_line in lines:
        line = raw_line.strip()
        if not line:
            continue
        
        if line.startswith('*'):
            current_elset = None
            if line.upper().startswith('*ELSET'):
                match = re.search(r'ELSET=([^,]+)', line, re.IGNORECASE)
                if match:
                    name = match.group(1).strip()
                    # Only capture spring elsets
                    if 'SPRING' in name.upper():
                        current_elset = name
                        elset_map.setdefault(current_elset, [])
            continue
        
        if current_elset:
            parts = [p.strip() for p in line.split(',') if p.strip()]
            for p in parts:
                try:
                    elem_id = int(p)
                    elset_map[current_elset].append(elem_id)
                except ValueError:
                    continue
    
    return dict(elset_map)

def build_spring_position_lookup(lines):
    """Return mapping from spring elset name to node Y coordinate."""
    node_coords = _extract_node_coordinates_from_lines(lines)
    spring_elements = _extract_spring_elements_from_lines(lines)
    spring_elsets = _extract_spring_elsets(lines)
    elset_to_positions = defaultdict(list)
    elem_to_line = {spring['elem_id']: spring.get('line_index') for spring in spring_elements}
    
    # Map element id -> node id (from SPRING1 element definitions)
    elem_to_node = {spring['elem_id']: spring['node_id'] for spring in spring_elements}
    
    # First, fill from explicit spring elsets that point to element ids
    for elset_name, elem_ids in spring_elsets.items():
        for elem_id in elem_ids:
            node_id = elem_to_node.get(elem_id)
            if node_id is None:
                continue
            coords = node_coords.get(node_id)
            if coords and len(coords) >= 2:
                elset_to_positions[elset_name].append({
                    'y': coords[1],
                    'coords': coords,
                    'node_id': node_id,
                    'elem_id': elem_id,
                    'element_line_index': elem_to_line.get(elem_id)
                })
    
    # Also add the block elset used in the *Element definition (Surface_Spring-X_All)
    for spring in spring_elements:
        node_id = spring.get('node_id')
        elset = spring.get('elset')
        elem_id = spring.get('elem_id')
        if node_id is None or elset is None:
            continue
        coords = node_coords.get(node_id)
        if coords and len(coords) >= 2:
            elset_to_positions[elset].append({
                'y': coords[1],
                'coords': coords,
                'node_id': node_id,
                'elem_id': elem_id,
                'element_line_index': elem_to_line.get(elem_id)
            })
    
    return dict(elset_to_positions)

def parse_spring_sections(inp_filename, verbose=True):
    """Parse spring sections from an INP file
    
    Parameters:
    - inp_filename: Path to the INP file
    - verbose: If False, suppress per-spring debug logging
    
    Returns:
    - Dictionary with spring data organized by spring name and direction
      (each spring entry includes its orientation name and Z component when available)
    - List of all lines from the file for reconstruction
    """
    spring_data = defaultdict(lambda: defaultdict(list))  # {spring_name: {direction: [(elset, value, line_index)]}}
    all_lines = []
    current_spring_name = None
    current_direction = None
    current_elset = None
    spring_line_index = None
    debug = print if verbose else (lambda *args, **kwargs: None)
    
    print(f"[INFO] Parsing spring sections from: {inp_filename}")
    
    try:
        with open(inp_filename, 'r') as f:
            lines = f.readlines()

        orientation_map = _parse_orientation_definitions(lines)
        
        i = 0
        while i < len(lines):
            line = lines[i].strip()
            all_lines.append(lines[i])  # Keep original line with newline
            
            # Look for spring definition lines
            if line.upper().startswith('*SPRING'):
                spring_line_index = i
                current_orientation = None
                current_orientation_component = None
                current_spring_name = None
                current_direction = None
                current_elset = None

                orient_match = re.search(r'ORIENTATION=([^,]+)', line, re.IGNORECASE)
                if orient_match:
                    current_orientation = orient_match.group(1).strip()
                    orientation_info = orientation_map.get(current_orientation.upper())
                    if orientation_info:
                        vector1 = orientation_info.get('vector1', [])
                        if len(vector1) >= 3:
                            current_orientation_component = vector1[2]
                
                # Parse the elset parameter
                elset_match = re.search(r'ELSET=([^,]+)', line, re.IGNORECASE)
                if elset_match:
                    raw_elset_part = elset_match.group(1).strip()
                    elset_part_upper = raw_elset_part.upper()
                    current_elset = raw_elset_part
                    
                    # Extract spring name and direction from elset
                    # Expected format: Surface_Spring-1_15001_DOF_1 (legacy)
                    if '_DOF_' in elset_part_upper:
                        parts = elset_part_upper.split('_DOF_')
                        if len(parts) == 2:
                            # Extract spring name (everything between "Spring-" and the node number)
                            base_part = parts[0]
                            direction_num = parts[1]
                            
                            # Find the spring name pattern (Spring-X)
                            if 'SPRING-' in base_part:
                                spring_start = base_part.find('SPRING-')
                                spring_part = base_part[spring_start:]
                                # Extract just the Spring-X part
                                spring_name_parts = spring_part.split('_')
                                if spring_name_parts:
                                    current_spring_name = spring_name_parts[0].upper()  # normalize name
                                    current_direction = f"DOF_{direction_num}"
                                    debug(f"[DEBUG] Found spring: {current_spring_name}, direction: {current_direction}, elset: {current_elset}")
                    else:
                        # Newer pattern without explicit DOF, e.g., Surface_SPRING-1_143
                        name_match = re.search(r'(SPRING-[^,_\s]+)', raw_elset_part, re.IGNORECASE)
                        if name_match:
                            current_spring_name = name_match.group(1).upper()
                            debug(f"[DEBUG] Found spring name from elset: {current_spring_name}, elset: {current_elset}")
                
                # Read direction line (next line)
                if i + 1 < len(lines):
                    i += 1
                    direction_line = lines[i].strip()
                    all_lines.append(lines[i])

                    # If direction still unknown, derive from direction line tokens
                    if current_direction is None:
                        tokens = [t for t in re.split(r'[,\s]+', direction_line) if t]
                        if tokens:
                            current_direction = f"DOF_{tokens[0]}"
                            debug(f"[DEBUG] Derived direction from line: {current_direction} (line {i})")
                    
                    # Read spring constant value (next line)
                    if i + 1 < len(lines):
                        i += 1
                        value_line = lines[i].strip()
                        all_lines.append(lines[i])
                        
                        try:
                            spring_value = float(value_line)
                            
                            if current_spring_name and current_direction:
                                spring_data[current_spring_name][current_direction].append({
                                    'elset': current_elset,
                                    'value': spring_value,
                                    'line_index': i,  # Index of the value line
                                    'direction_line_index': i - 1,  # Index of direction line
                                    'spring_line_index': spring_line_index,  # Index of spring definition line
                                    'orientation': current_orientation,
                                    'orientation_component': current_orientation_component
                                })
                                debug(f"[DEBUG] Added spring value: {spring_value} for {current_spring_name} {current_direction}")
                        except ValueError:
                            print(f"[WARNING] Could not parse spring value: {value_line}")
            
            i += 1
    
    except Exception as e:
        print(f"[ERROR] Failed to parse spring sections: {e}")
        return {}, []
    
    # Convert to regular dict for easier handling
    spring_data = {name: dict(directions) for name, directions in spring_data.items()}
    
    print(f"[INFO] Found {len(spring_data)} spring groups")
    for spring_name, directions in spring_data.items():
        for direction, springs in directions.items():
            total_value = sum(s['value'] for s in springs)
            print(f"[INFO] {spring_name} {direction}: {len(springs)} nodes, total stiffness: {total_value:.6e}")
    
    return spring_data, all_lines

def calculate_spring_totals(spring_data):
    """Calculate total spring constants for each spring name and direction
    
    Parameters:
    - spring_data: Dictionary from parse_spring_sections
    
    Returns:
    - Dictionary with totals {spring_name: {direction: total_value}}
    """
    totals = {}
    
    for spring_name, directions in spring_data.items():
        totals[spring_name] = {}
        for direction, springs in directions.items():
            total_value = sum(s['value'] for s in springs)
            totals[spring_name][direction] = total_value
    
    return totals

class SpringConstantGUI:
    """GUI for modifying spring constants"""
    
    def __init__(self, spring_totals, spring_data, all_lines, inp_filename, spring_positions=None):
        self.spring_totals = spring_totals
        self.spring_data = spring_data
        self.all_lines = all_lines
        self.inp_filename = inp_filename
        self.modified_totals = {}
        self.result = None
        self.orientation_offset_vars = {}  # Per-spring offset (%)
        self.orientation_offsets_pct = {}   # Per-spring offset (decimal)
        self.spring_orientation_z = {}  # Per-spring stored orientation Z component
        self.preview_labels = {}  # Per-spring preview labels
        self.spring_positions = spring_positions or {}
        self.per_spring_values = {}
        self.linear_vars = {}  # spring_name -> {'a': tk.StringVar, 'b': tk.StringVar}
        self.plot_window = None
        self.plot_canvas = None
        self.plot_fig = None
        self.plot_ax = None
        self.plot_frame = None
        
        # Create main window
        self.root = tk.Tk()
        self.root.title("Spring Constant Editor")
        self.root.geometry("1100x750")

        # Control variables
        self.global_multiplier_var = tk.StringVar(self.root, value="1.0")
        
        # Create GUI elements
        self.create_widgets()
        
        # Initialize modified totals with original values
        for spring_name, directions in spring_totals.items():
            self.modified_totals[spring_name] = directions.copy()
    
    def create_widgets(self):
        """Create the GUI widgets"""
        # Main frame
        main_frame = ttk.Frame(self.root, padding="10")
        main_frame.grid(row=0, column=0, sticky=(tk.W, tk.E, tk.N, tk.S))
        
        # Configure grid weights
        self.root.columnconfigure(0, weight=1)
        self.root.rowconfigure(0, weight=1)
        for col in range(12):
            main_frame.columnconfigure(col, weight=1)
        
        # Title
        title_label = ttk.Label(main_frame, text="Spring Constant Editor", font=("Arial", 16, "bold"))
        title_label.grid(row=0, column=0, columnspan=12, pady=(0, 20))
        
        # File info
        file_label = ttk.Label(main_frame, text=f"File: {os.path.basename(self.inp_filename)}")
        file_label.grid(row=1, column=0, columnspan=12, sticky=tk.W, pady=(0, 10))

        # Bulk controls
        bulk_frame = ttk.LabelFrame(main_frame, text="Bulk updates", padding="8")
        bulk_frame.grid(row=2, column=0, columnspan=12, sticky=(tk.W, tk.E), pady=(0, 12))
        bulk_frame.columnconfigure(4, weight=1)

        ttk.Label(bulk_frame, text="Multiply all totals by").grid(row=0, column=0, sticky=tk.W, padx=(0, 6))
        multiplier_entry = ttk.Entry(bulk_frame, width=10, textvariable=self.global_multiplier_var)
        multiplier_entry.grid(row=0, column=1, sticky=tk.W)
        apply_all_btn = ttk.Button(bulk_frame, text="Apply", command=self.apply_global_multiplier)
        apply_all_btn.grid(row=0, column=2, padx=(6, 0))

        ttk.Label(bulk_frame, text="Per-spring offsets set below. Offset applies (1 + pct * orientation_z) to each spring.", foreground="gray").grid(
            row=1, column=0, columnspan=6, sticky=tk.W, pady=(6, 0))

        # Plot controls - single spring group at a time
        plot_frame = ttk.LabelFrame(main_frame, text="Spring visualization", padding="8")
        plot_frame.grid(row=3, column=0, columnspan=12, sticky=(tk.W, tk.E), pady=(0, 12))
        plot_frame.columnconfigure(2, weight=1)
        plot_frame.rowconfigure(1, weight=1)
        self.plot_frame = plot_frame

        ttk.Label(plot_frame, text="Spring group").grid(row=0, column=0, sticky=tk.W, padx=(0, 6))
        self.plot_group_var = tk.StringVar(self.root)
        group_options = sorted(self.spring_totals.keys())
        if group_options:
            self.plot_group_var.set(group_options[0])
        group_combo = ttk.Combobox(plot_frame, textvariable=self.plot_group_var, values=group_options, state="readonly", width=25)
        group_combo.grid(row=0, column=1, sticky=tk.W, padx=(0, 10))
        group_combo.bind("<<ComboboxSelected>>", lambda e: self.show_spring_plot())
        plot_btn = ttk.Button(plot_frame, text="Show plot", command=self.show_spring_plot)
        plot_btn.grid(row=0, column=2, sticky=tk.W)

        # Embedded plot canvas
        self.plot_fig = Figure(figsize=(7, 5))
        self.plot_ax = self.plot_fig.add_subplot(111, projection='3d')
        self.plot_canvas = FigureCanvasTkAgg(self.plot_fig, master=plot_frame)
        self.plot_canvas.get_tk_widget().grid(row=1, column=0, columnspan=3, sticky=(tk.W, tk.E, tk.N, tk.S), pady=(10, 0))
        self.plot_ax.set_xlabel("X position")
        self.plot_ax.set_ylabel("Y position")
        self.plot_ax.set_zlabel("Z position")
        
        # Headers
        header_row = 4
        ttk.Label(main_frame, text="Spring Name", font=("Arial", 12, "bold")).grid(row=header_row, column=0, sticky=tk.W, padx=(0, 10))
        ttk.Label(main_frame, text="Direction", font=("Arial", 12, "bold")).grid(row=header_row, column=1, sticky=tk.W, padx=(0, 10))
        ttk.Label(main_frame, text="Current Total", font=("Arial", 12, "bold")).grid(row=header_row, column=2, sticky=tk.W, padx=(0, 10))
        ttk.Label(main_frame, text="New Total", font=("Arial", 12, "bold")).grid(row=header_row, column=3, sticky=tk.W, padx=(0, 10))
        ttk.Label(main_frame, text="Ratio", font=("Arial", 12, "bold")).grid(row=header_row, column=4, sticky=tk.W, padx=(0, 10))
        ttk.Label(main_frame, text="Offset (%)", font=("Arial", 12, "bold")).grid(row=header_row, column=5, sticky=tk.W, padx=(0, 10))
        ttk.Label(main_frame, text="Preview factor", font=("Arial", 12, "bold")).grid(row=header_row, column=6, sticky=tk.W, padx=(0, 10))
        ttk.Label(main_frame, text="a (x)", font=("Arial", 12, "bold")).grid(row=header_row, column=7, sticky=tk.W, padx=(0, 10))
        ttk.Label(main_frame, text="b (y)", font=("Arial", 12, "bold")).grid(row=header_row, column=8, sticky=tk.W, padx=(0, 10))
        ttk.Label(main_frame, text="c (z)", font=("Arial", 12, "bold")).grid(row=header_row, column=9, sticky=tk.W, padx=(0, 10))
        ttk.Label(main_frame, text="d", font=("Arial", 12, "bold")).grid(row=header_row, column=10, sticky=tk.W, padx=(0, 10))
        ttk.Label(main_frame, text="Apply k=ax+by+cz+d", font=("Arial", 12, "bold")).grid(row=header_row, column=11, sticky=tk.W, padx=(0, 10))
        
        # Separator
        separator = ttk.Separator(main_frame, orient='horizontal')
        separator.grid(row=header_row + 1, column=0, columnspan=12, sticky=(tk.W, tk.E), pady=5)
        
        # Spring entries
        self.entries = {}
        row = header_row + 2
        spring_seen = set()
        
        for spring_name, directions in self.spring_totals.items():
            for direction, total_value in directions.items():
                # Spring name
                ttk.Label(main_frame, text=spring_name).grid(row=row, column=0, sticky=tk.W, padx=(0, 10), pady=2)
                
                # Direction
                ttk.Label(main_frame, text=direction).grid(row=row, column=1, sticky=tk.W, padx=(0, 10), pady=2)
                
                # Current total
                ttk.Label(main_frame, text=f"{total_value:.6e}").grid(row=row, column=2, sticky=tk.W, padx=(0, 10), pady=2)
                
                # New total entry
                entry = ttk.Entry(main_frame, width=15)
                entry.insert(0, f"{total_value:.6e}")
                entry.grid(row=row, column=3, sticky=tk.W, padx=(0, 10), pady=2)
                
                # Store entry reference
                self.entries[(spring_name, direction)] = {
                    'entry': entry,
                    'original': total_value,
                    'ratio_label': ttk.Label(main_frame, text="1.000")
                }
                
                # Ratio label
                self.entries[(spring_name, direction)]['ratio_label'].grid(row=row, column=4, sticky=tk.W, padx=(0, 10), pady=2)
                
                # Bind entry change event
                entry.bind('<KeyRelease>', lambda e, sn=spring_name, d=direction: self.update_ratio(sn, d))
                
                # Per-spring controls (only place one set per spring)
                if spring_name not in spring_seen:
                    # Offset entry
                    offset_var = tk.StringVar(self.root, value="0.0")
                    self.orientation_offset_vars[spring_name] = offset_var
                    offset_entry = ttk.Entry(main_frame, width=10, textvariable=offset_var)
                    offset_entry.grid(row=row, column=5, sticky=tk.W, padx=(0, 10), pady=2)
                    offset_entry.bind('<KeyRelease>', lambda e, sn=spring_name: self.update_offset_preview(sn))
                    # Capture orientation Z component for this spring (from first available spring entry)
                    orientation_z = self._get_orientation_component(spring_name)
                    if orientation_z is not None:
                        self.spring_orientation_z[spring_name] = orientation_z
                    preview_label = ttk.Label(main_frame, text=self._format_preview(spring_name))
                    preview_label.grid(row=row, column=6, sticky=tk.W, padx=(0, 10), pady=2)
                    self.preview_labels[spring_name] = preview_label
                    
                    # Linear k=ax+by+cz+d inputs per spring
                    a_var = tk.StringVar(self.root, value="0.0")
                    b_var = tk.StringVar(self.root, value="0.0")
                    c_var = tk.StringVar(self.root, value="0.0")
                    d_var = tk.StringVar(self.root, value="0.0")
                    self.linear_vars[spring_name] = {'a': a_var, 'b': b_var, 'c': c_var, 'd': d_var}
                    a_entry = ttk.Entry(main_frame, width=8, textvariable=a_var)
                    b_entry = ttk.Entry(main_frame, width=8, textvariable=b_var)
                    c_entry = ttk.Entry(main_frame, width=8, textvariable=c_var)
                    d_entry = ttk.Entry(main_frame, width=8, textvariable=d_var)
                    a_entry.grid(row=row, column=7, sticky=tk.W, padx=(0, 10), pady=2)
                    b_entry.grid(row=row, column=8, sticky=tk.W, padx=(0, 10), pady=2)
                    c_entry.grid(row=row, column=9, sticky=tk.W, padx=(0, 10), pady=2)
                    d_entry.grid(row=row, column=10, sticky=tk.W, padx=(0, 10), pady=2)
                    apply_btn = ttk.Button(main_frame, text="Apply", command=lambda sn=spring_name: self.apply_linear_for_spring(sn))
                    apply_btn.grid(row=row, column=11, sticky=tk.W, padx=(0, 10), pady=2)
                    
                    spring_seen.add(spring_name)
                else:
                    # placeholders for alignment
                    for col in [5, 6, 7, 8, 9, 10, 11]:
                        ttk.Label(main_frame, text="").grid(row=row, column=col, sticky=tk.W, padx=(0, 10), pady=2)

                row += 1

        if group_options:
            self.show_spring_plot()
        
        # Buttons frame
        button_frame = ttk.Frame(main_frame)
        button_frame.grid(row=row + 1, column=0, columnspan=12, pady=20)
        
        # Reset button
        reset_btn = ttk.Button(button_frame, text="Reset All", command=self.reset_values)
        reset_btn.pack(side=tk.LEFT, padx=(0, 10))
        
        # Apply button
        apply_btn = ttk.Button(button_frame, text="Apply Changes", command=self.apply_changes)
        apply_btn.pack(side=tk.LEFT, padx=(0, 10))
        
        # Cancel button
        cancel_btn = ttk.Button(button_frame, text="Cancel", command=self.cancel)
        cancel_btn.pack(side=tk.LEFT)
        
        # Status label
        self.status_label = ttk.Label(main_frame, text="Ready", foreground="blue")
        self.status_label.grid(row=row + 2, column=0, columnspan=12, pady=10)
    
    def update_ratio(self, spring_name, direction):
        """Update the ratio label when entry value changes"""
        try:
            entry = self.entries[(spring_name, direction)]['entry']
            original = self.entries[(spring_name, direction)]['original']
            ratio_label = self.entries[(spring_name, direction)]['ratio_label']
            
            new_value = float(entry.get())
            ratio = new_value / original if original != 0 else 0
            
            ratio_label.config(text=f"{ratio:.3f}")
            
            # Update color based on ratio
            if abs(ratio - 1.0) < 0.001:
                ratio_label.config(foreground="black")
            elif ratio > 1.0:
                ratio_label.config(foreground="red")
            else:
                ratio_label.config(foreground="blue")

            self.show_spring_plot()
                
        except ValueError:
            # Invalid number entered
            ratio_label = self.entries[(spring_name, direction)]['ratio_label']
            ratio_label.config(text="Invalid", foreground="red")

    def _get_orientation_component(self, spring_name):
        """Fetch the first available orientation Z component for a spring name."""
        spring_entry = self.spring_data.get(spring_name, {})
        for direction_data in spring_entry.values():
            if direction_data:
                component = direction_data[0].get('orientation_component')
                if component is not None:
                    return component
        return None

    def _format_preview(self, spring_name):
        """Generate preview text for orientation offset effect."""
        offset_pct = 0.0
        try:
            offset_pct = float(self.orientation_offset_vars[spring_name].get()) / 100.0
        except Exception:
            offset_pct = 0.0
        orient_z = self.spring_orientation_z.get(spring_name)
        if orient_z is None:
            return "n/a"
        factor = 1.0 + offset_pct * orient_z
        return f"Z={orient_z:.3f} → ×{factor:.3f}"

    def update_offset_preview(self, spring_name):
        """Update the preview label when offset changes."""
        if spring_name not in self.preview_labels:
            return
        text = self._format_preview(spring_name)
        self.preview_labels[spring_name].config(text=text)
        self.show_spring_plot()

    def apply_global_multiplier(self):
        """Apply a multiplier to all spring totals at once"""
        try:
            multiplier = float(self.global_multiplier_var.get())
        except ValueError:
            messagebox.showerror("Error", "Invalid global multiplier")
            return

        for (spring_name, direction), entry_data in self.entries.items():
            entry = entry_data['entry']
            new_value = entry_data['original'] * multiplier
            entry.delete(0, tk.END)
            entry.insert(0, f"{new_value:.6e}")
            self.update_ratio(spring_name, direction)

        self.status_label.config(text=f"Applied multiplier {multiplier:.3f} to all springs", foreground="green")
        self.show_spring_plot()
    
    def reset_values(self):
        """Reset all entries to original values"""
        for (spring_name, direction), entry_data in self.entries.items():
            entry = entry_data['entry']
            original = entry_data['original']
            
            entry.delete(0, tk.END)
            entry.insert(0, f"{original:.6e}")
            self.update_ratio(spring_name, direction)
        
        self.global_multiplier_var.set("1.0")
        for offset_var in self.orientation_offset_vars.values():
            offset_var.set("0.0")
        self.orientation_offsets_pct = {}
        for spring_name in self.preview_labels:
            self.update_offset_preview(spring_name)
        self.status_label.config(text="Values reset to original", foreground="blue")
        self.show_spring_plot()
    
    def _get_next_y_for_elset(self, elset_name):
        """Return the Y value for a given spring elset."""
        if not elset_name:
            return None
        values = self.spring_positions.get(elset_name)
        if not values:
            return None
        entry = values[0] if isinstance(values, list) else values
        if isinstance(entry, dict):
            return entry.get('y')
        return entry

    def _get_position_entry(self, elset_name):
        """Return the first stored position entry for an elset."""
        if not elset_name:
            return None
        values = self.spring_positions.get(elset_name)
        if not values:
            return None
        entry = values[0] if isinstance(values, list) else values
        return entry if isinstance(entry, dict) else {'y': entry, 'coords': [None, entry, None]}

    def _get_orientation_offset_pct(self, spring_name):
        """Current orientation offset as a decimal for a spring group."""
        offset_var = self.orientation_offset_vars.get(spring_name)
        if not offset_var:
            return 0.0
        try:
            return float(offset_var.get()) / 100.0
        except Exception:
            return 0.0

    def _get_current_ratio(self, spring_name, direction):
        """Return current ratio based on edited totals."""
        entry_data = self.entries.get((spring_name, direction))
        if not entry_data:
            return None
        try:
            current_total = float(entry_data['entry'].get())
            original = entry_data['original']
            return (current_total / original) if original else None
        except ValueError:
            return None

    def _gather_plot_data(self, spring_name):
        """Collect per-spring values and positions for plotting a single group."""
        points = []
        directions = self.spring_data.get(spring_name, {})
        for direction, springs in directions.items():
            ratio = self._get_current_ratio(spring_name, direction)
            orient_pct = self._get_orientation_offset_pct(spring_name)
            for spring in springs:
                elset = spring.get('elset')
                pos_entry = self._get_position_entry(elset)
                if not pos_entry:
                    continue
                coords = pos_entry.get('coords') or []
                if len(coords) < 3 or any(c is None for c in coords[:3]):
                    continue
                x_pos, y_pos, z_pos = coords[:3]
                value_override = self.per_spring_values.get(elset)
                base_value = None
                if value_override is not None:
                    base_value = value_override
                elif ratio is not None:
                    base_value = spring['value'] * ratio
                else:
                    base_value = spring['value']

                if orient_pct and spring.get('orientation_component') is not None:
                    base_value *= (1 + orient_pct * spring['orientation_component'])

                if base_value is None or base_value < 0:
                    continue

                points.append({
                    'coords': (x_pos, y_pos, z_pos),
                    'value': base_value,
                    'direction': direction,
                    'elset': elset
                })
        return points

    def show_spring_plot(self):
        """Display a scatter plot for the selected spring group inside the UI."""
        spring_name = self.plot_group_var.get()
        if not spring_name:
            if hasattr(self, "status_label"):
                self.status_label.config(text="No spring group selected for plotting", foreground="orange")
            return

        points = self._gather_plot_data(spring_name)
        if not points:
            if self.plot_ax:
                self.plot_ax.clear()
                self.plot_ax.set_title(f"{spring_name}: no data")
                self.plot_ax.set_xlabel("X position")
                self.plot_ax.set_ylabel("Y position")
                self.plot_ax.set_zlabel("Z position")
            if self.plot_canvas:
                self.plot_canvas.draw_idle()
            return

        by_dir = defaultdict(list)
        for pt in points:
            by_dir[pt['direction']].append(pt)

        values = [pt['value'] for pt in points]
        vmin, vmax = min(values), max(values)
        if vmax <= vmin:
            vmax = vmin + 1e-9

        if not self.plot_fig or not self.plot_ax:
            return

        ax = self.plot_ax
        fig = self.plot_fig
        ax.clear()
        norm = mcolors.Normalize(vmin=vmin, vmax=vmax)
        cmap = cm.get_cmap('viridis')
        scatter_obj = None
        markers = ['o', 's', '^', 'v', 'D', 'x', '+']

        for idx, (direction, dir_points) in enumerate(sorted(by_dir.items())):
            xs = [p['coords'][0] for p in dir_points]
            ys = [p['coords'][1] for p in dir_points]
            zs = [p['coords'][2] for p in dir_points]
            vals = [p['value'] for p in dir_points]
            scatter_obj = ax.scatter(
                xs,
                ys,
                zs,
                c=vals,
                cmap=cmap,
                norm=norm,
                marker=markers[idx % len(markers)],
                label=direction,
                edgecolors='black',
                linewidths=0.5
            )

        ax.set_xlabel("X position")
        ax.set_ylabel("Y position")
        ax.set_zlabel("Z position")
        ax.set_title(f"{spring_name} springs ({len(points)} points)")
        ax.legend()
        if scatter_obj:
            # Clear previous colorbars
            if fig.axes and len(fig.axes) > 1:
                for extra_ax in fig.axes[1:]:
                    fig.delaxes(extra_ax)
            fig.colorbar(scatter_obj, ax=ax, label="Spring value")

        self.plot_canvas.draw_idle()

    def _close_plot_window(self):
        """Close plot window cleanly."""
        if self.plot_window:
            try:
                self.plot_window.destroy()
            finally:
                self.plot_window = None
    
    def apply_linear_for_spring(self, spring_name):
        """Apply k = a*x + b*y + c*z + d for a spring group using node positions."""
        if spring_name not in self.linear_vars:
            return
        try:
            a_val = float(self.linear_vars[spring_name]['a'].get())
            b_val = float(self.linear_vars[spring_name]['b'].get())
            c_val = float(self.linear_vars[spring_name]['c'].get())
            d_val = float(self.linear_vars[spring_name]['d'].get())
        except ValueError:
            messagebox.showerror("Error", f"Invalid linear coefficients for {spring_name}")
            return
        
        if not self.spring_positions:
            messagebox.showwarning("Warning", "No spring node positions found in the INP file")
            return
        
        updated = 0
        totals_by_dir = defaultdict(float)
        missing = []
        
        directions = self.spring_data.get(spring_name, {})
        for direction, springs in directions.items():
            for spring in springs:
                elset = spring.get('elset')
                pos_entry = self._get_position_entry(elset)
                if not pos_entry:
                    missing.append(elset or f"{spring_name}_{direction}")
                    continue
                coords = pos_entry.get('coords') or []
                if len(coords) < 3 or any(c is None for c in coords[:3]):
                    missing.append(elset or f"{spring_name}_{direction}")
                    continue
                x_val, y_val, z_val = coords[:3]
                k_val = a_val * x_val + b_val * y_val + c_val * z_val + d_val
                self.per_spring_values[elset] = k_val
                totals_by_dir[direction] += k_val
                updated += 1
        
        if not updated:
            messagebox.showwarning("Warning", f"No springs updated for {spring_name} (missing positions)")
            return
        
        for direction, total_k in totals_by_dir.items():
            entry_data = self.entries.get((spring_name, direction))
            if not entry_data:
                continue
            entry = entry_data['entry']
            entry.delete(0, tk.END)
            entry.insert(0, f"{total_k:.6e}")
            self.update_ratio(spring_name, direction)
        
        status = f"Applied k=ax+by+cz+d to {updated} springs in {spring_name}"
        if missing:
            status += f"; missing positions for {len(missing)} springs"
        self.status_label.config(text=status, foreground="green")
        self.show_spring_plot()
    
    def apply_changes(self):
        """Apply the changes and close the GUI"""
        try:
            # Validate all entries
            for (spring_name, direction), entry_data in self.entries.items():
                entry = entry_data['entry']
                try:
                    new_value = float(entry.get())
                    self.modified_totals[spring_name][direction] = new_value
                except ValueError:
                    messagebox.showerror("Error", f"Invalid value for {spring_name} {direction}")
                    return

            # Per-spring orientation offsets
            self.orientation_offsets_pct = {}
            for spring_name, offset_var in self.orientation_offset_vars.items():
                try:
                    self.orientation_offsets_pct[spring_name] = float(offset_var.get()) / 100.0
                except ValueError:
                    messagebox.showerror("Error", f"Invalid orientation offset percentage for {spring_name}")
                    return
            # Update previews to reflect any last edits before closing
            for spring_name in self.orientation_offset_vars:
                self.update_offset_preview(spring_name)
            
            self.result = "apply"
            self.root.quit()
            
        except Exception as e:
            messagebox.showerror("Error", f"Failed to apply changes: {e}")
    
    def cancel(self):
        """Cancel the changes and close the GUI"""
        self.result = "cancel"
        self.root.quit()
    
    def show(self):
        """Show the GUI and return the result"""
        self.root.mainloop()
        self.root.destroy()
        return self.result

def apply_spring_modifications(spring_data, original_totals, modified_totals, all_lines, orientation_offset=0.0, per_spring_values=None, return_final_totals=False, verbose=True):
    """Apply proportional modifications to individual spring constants
    
    Parameters:
    - spring_data: Original spring data from parse_spring_sections
    - original_totals: Original total values
    - modified_totals: New total values from GUI
    - all_lines: All lines from the original file
    - orientation_offset: Either a single decimal multiplier to apply to all springs (e.g. 0.1 for 10%)
                         or a dict mapping spring_name -> decimal multiplier
    - per_spring_values: Optional dict mapping elset name -> new spring value (overrides ratio logic)
    - return_final_totals: If True, also return the resulting totals after applying orientation offset
    - verbose: If False, suppress per-spring debug logging
    
    Returns:
    - Modified lines list (and optionally the final totals dict)
    """
    modified_lines = all_lines.copy()
    final_totals = defaultdict(dict)
    orientation_offset = orientation_offset or 0.0
    if isinstance(orientation_offset, dict):
        orientation_offset = { (k or "").upper(): v for k, v in orientation_offset.items() }
    orientation_warnings = []
    debug = print if verbose else (lambda *args, **kwargs: None)
    per_spring_values = per_spring_values or {}
    spring_elset_map = _extract_spring_elsets(all_lines)
    spring_elements = _extract_spring_elements_from_lines(all_lines)
    element_line_lookup = {elem['elem_id']: elem.get('line_index') for elem in spring_elements if elem.get('line_index') is not None}
    lines_to_remove = set()
    elements_to_remove = set()
    removed_springs = 0
    
    print(f"[INFO] Applying spring modifications...")
    
    for spring_name, directions in spring_data.items():
        for direction, springs in directions.items():
            if spring_name in original_totals and direction in original_totals[spring_name]:
                original_total = original_totals[spring_name][direction]
                modified_total = modified_totals[spring_name][direction]
                
                ratio = None
                if original_total != 0:
                    ratio = modified_total / original_total
                    print(f"[INFO] {spring_name} {direction}: ratio = {ratio:.6f}")
                else:
                    print(f"[WARNING] Original total is zero for {spring_name} {direction}")
                
                # Apply per-spring overrides or ratio scaling
                for spring in springs:
                    line_index = spring['line_index']
                    original_value = spring['value']
                    elset_name = spring.get('elset')
                    base_value = None
                    
                    if elset_name in per_spring_values:
                        base_value = per_spring_values[elset_name]
                    elif ratio is not None:
                        base_value = original_value * ratio
                    else:
                        continue  # Skip if no ratio and no override available
                    
                    # Remove springs with negative stiffness
                    if base_value is not None and base_value < 0:
                        removed_springs += 1
                        lines_to_remove.update(filter(lambda x: x is not None, [
                            line_index,
                            spring.get('direction_line_index'),
                            spring.get('spring_line_index')
                        ]))
                        if elset_name:
                            for elem_id in spring_elset_map.get(elset_name, []):
                                elements_to_remove.add(elem_id)
                        debug(f"[DEBUG] Removing spring {elset_name or spring_name} with negative value {base_value:.6e}")
                        continue

                    # Get per-spring offset if dict, else global
                    offset_pct = 0.0
                    if isinstance(orientation_offset, dict):
                        offset_pct = orientation_offset.get(spring_name, 0.0) or 0.0
                    else:
                        offset_pct = orientation_offset or 0.0

                    if offset_pct:
                        component = spring.get('orientation_component')
                        if component is not None:
                            base_value *= (1 + offset_pct * component)
                        else:
                            orientation_warnings.append((spring_name, spring.get('orientation')))

                    if base_value is not None and base_value < 0:
                        removed_springs += 1
                        lines_to_remove.update(filter(lambda x: x is not None, [
                            line_index,
                            spring.get('direction_line_index'),
                            spring.get('spring_line_index')
                        ]))
                        if elset_name:
                            for elem_id in spring_elset_map.get(elset_name, []):
                                elements_to_remove.add(elem_id)
                        debug(f"[DEBUG] Removing spring {elset_name or spring_name} after offsets with value {base_value:.6e}")
                        continue
                    
                    # Update the line
                    modified_lines[line_index] = f"{base_value:.6e}\n"
                    final_totals.setdefault(spring_name, {}).setdefault(direction, 0.0)
                    final_totals[spring_name][direction] += base_value
                    
                    debug(f"[DEBUG] Updated line {line_index}: {original_value:.6e} -> {base_value:.6e}")
    
    if orientation_warnings:
        missing_by_spring = defaultdict(set)
        for spring_name, orient in orientation_warnings:
            if spring_name:
                missing_by_spring[spring_name].add(orient or "unspecified")
        for spring_name, missing_orients in missing_by_spring.items():
            missing_list = ", ".join(sorted(missing_orients))
            print(f"[WARNING] Orientation offset requested for {spring_name} but missing orientation data for: {missing_list}")

    for elem_id in elements_to_remove:
        line_idx = element_line_lookup.get(elem_id)
        if line_idx is not None:
            lines_to_remove.add(line_idx)

    if lines_to_remove or elements_to_remove:
        cleaned_lines = []
        target_elsets = set(spring_elset_map.keys())
        current_elset = None
        for idx, line in enumerate(modified_lines):
            if idx in lines_to_remove:
                continue

            stripped = line.strip()
            if stripped.startswith('*'):
                current_elset = None
                if stripped.upper().startswith('*ELSET'):
                    match = re.search(r'ELSET=([^,]+)', stripped, re.IGNORECASE)
                    if match:
                        name = match.group(1).strip()
                        if name in target_elsets:
                            current_elset = name
                cleaned_lines.append(line)
                continue

            if current_elset and elements_to_remove:
                parts = [p.strip() for p in line.split(',') if p.strip()]
                remaining = []
                for p in parts:
                    try:
                        if int(p) in elements_to_remove:
                            continue
                    except ValueError:
                        pass
                    remaining.append(p)
                if not remaining:
                    continue
                cleaned_lines.append(", ".join(remaining) + "\n")
            else:
                cleaned_lines.append(line)
        modified_lines = cleaned_lines

    if removed_springs:
        print(f"[INFO] Removed {removed_springs} springs with negative values")

    if return_final_totals:
        return modified_lines, final_totals
    return modified_lines

def write_cylinder_edge_data(edge_data, filename="cylinder_edge_data.json"):
    """
    Write cylinder edge data to a JSON file
    """
    with open(filename, 'w') as outfile:
        json.dump(edge_data, outfile, indent=2)
    
    print(f"[INFO] Wrote cylinder edge data to {filename}")

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
            'F3': {'vertices': [0, 1, 5, 4], 'midnodes': [8, 16, 12, 17]},  # Front face
            'F4': {'vertices': [1, 2, 6, 5], 'midnodes': [9, 18, 13, 16]},  # Right face
            'F5': {'vertices': [2, 3, 7, 6], 'midnodes': [10, 19, 14, 18]},  # Back face
            'F6': {'vertices': [3, 0, 4, 7], 'midnodes': [11, 17, 15, 19]}   # Left face
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

def process_cylinder_extrusion(nodes, elements, target_radius, extrusion_distance, num_layers=1, center=(0, 0), direction=None, height=None, chamfer=None):
    """
    Process a single cylinder extrusion and return the new nodes and wedge elements
    
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
    
    Returns:
    - new_nodes: Dictionary of newly created nodes
    - wedge_elements: List of created wedge elements
    """
    
    # Ensure chamfer parameter is valid
    if chamfer is None or not isinstance(chamfer, list) or len(chamfer) < 2:
        chamfer = [0.0, 0.0]
        print(f"[INFO] Using default chamfer parameters: {chamfer}")
    
    # Find nodes on the cylindrical surface
    cylinder_nodes = find_nodes_on_cylinder(nodes, target_radius, center=center, direction=direction, height=height, chamfer=chamfer)
    print(f"[INFO] Found {len(cylinder_nodes)} nodes on cylindrical surface")
    
    # Identify element faces on the cylindrical surface
    cylinder_faces = identify_element_faces_on_cylinder(elements, cylinder_nodes, nodes)
    print(f"[INFO] Found {len(cylinder_faces)} faces on the cylindrical surface")
    
    # Create wedge elements by extruding triangular faces on the cylinder
    new_nodes, wedge_elements = create_wedge_elements_from_faces(
        cylinder_faces, nodes, elements, target_radius, extrusion_distance, direction, height, 
        num_layers=num_layers, center=center, chamfer=chamfer)
    print(f"[INFO] Created {len(wedge_elements)} wedge elements with {len(new_nodes)} new nodes")
    
    return new_nodes, wedge_elements

def modify_spring_constants(inp_filename, output_filename=None):
    """Main function to modify spring constants in an INP file
    
    Parameters:
    - inp_filename: Path to the input INP file (optional, file dialog appears if None/invalid)
    - output_filename: Path to the output INP file (optional, defaults to input + '_modified')
    
    Returns:
    - Path to the output file if successful, None if cancelled
    """
    if not inp_filename or not os.path.exists(inp_filename):
        chooser_root = tk.Tk()
        chooser_root.withdraw()
        selected = filedialog.askopenfilename(
            title="Select INP file",
            filetypes=[("Abaqus INP files", "*.inp"), ("All files", "*.*")]
        )
        chooser_root.destroy()
        if not selected:
            print("[INFO] No file selected; operation cancelled")
            return None
        inp_filename = selected

    print(f"[INFO] Starting spring constant modification for: {inp_filename}")
    
    # Parse spring sections
    spring_data, all_lines = parse_spring_sections(inp_filename)
    
    if not spring_data:
        print("[WARNING] No spring sections found in the file")
        return None
    
    # Calculate totals
    spring_totals = calculate_spring_totals(spring_data)
    spring_positions = build_spring_position_lookup(all_lines)
    
    # Show GUI for user input
    gui = SpringConstantGUI(spring_totals, spring_data, all_lines, inp_filename, spring_positions=spring_positions)
    result = gui.show()
    
    if result == "cancel":
        print("[INFO] User cancelled the operation")
        return None
    
    # Apply modifications
    modified_lines, final_totals = apply_spring_modifications(
        spring_data,
        spring_totals,
        gui.modified_totals,
        all_lines,
        orientation_offset=gui.orientation_offsets_pct,
        per_spring_values=gui.per_spring_values,
        return_final_totals=True
    )
    
    # Determine output filename
    if output_filename is None:
        base_name, ext = os.path.splitext(inp_filename)
        output_filename = f"{base_name}_modified{ext}"
    
    # Write modified file
    try:
        with open(output_filename, 'w') as f:
            f.writelines(modified_lines)
        
        print(f"[INFO] Successfully wrote modified file to: {output_filename}")
        
        # Print summary of changes
        print(f"\n[INFO] Summary of changes:")
        if gui.orientation_offsets_pct:
            for spring_name, pct in gui.orientation_offsets_pct.items():
                print(f"[INFO] Orientation offset for {spring_name}: {pct * 100:.2f}% of orientation Z component applied to its springs")
        for spring_name, directions in gui.modified_totals.items():
            for direction, new_total in directions.items():
                original_total = spring_totals[spring_name][direction]
                ratio = new_total / original_total if original_total != 0 else 0
                final_total = final_totals.get(spring_name, {}).get(direction, new_total)
                if gui.orientation_offsets_pct:
                    print(f"[INFO] {spring_name} {direction}: {original_total:.6e} -> {new_total:.6e} (ratio: {ratio:.3f}), after orientation offset: {final_total:.6e}")
                else:
                    print(f"[INFO] {spring_name} {direction}: {original_total:.6e} -> {new_total:.6e} (ratio: {ratio:.3f})")
        
        return output_filename
        
    except Exception as e:
        print(f"[ERROR] Failed to write output file: {e}")
        return None

def build_node_to_elements_mapping(elements):
    """Build a mapping from node IDs to element IDs that contain them
    
    Parameters:
    - elements: Dictionary of elements {elem_id: {'type': type, 'nodes': [node_ids]}}
    
    Returns:
    - Dictionary mapping node IDs to sets of element IDs
    """
    node_to_elements = defaultdict(set)
    
    for elem_id, elem_data in elements.items():
        node_ids = elem_data.get('nodes', [])
        for node_id in node_ids:
            node_to_elements[node_id].add(elem_id)
    
    return node_to_elements

def check_boundary_face_chunk(element_chunk, all_elements, node_to_elements):
    """Worker function to check boundary faces for a chunk of elements
    
    Parameters:
    - element_chunk: Dictionary of elements to process
    - all_elements: Dictionary of all elements (for reference)
    - node_to_elements: Dictionary mapping nodes to elements containing them
    
    Returns:
    - Set of tuples: {(element_id, face_name), ...}
    """
    boundary_faces = set()
    
    # C3D20R face definitions
    face_nodes_c3d20r = {
        'S1': [0,1,2,3, 8,9,10,11],    # Bottom face
        'S2': [4,5,6,7, 12,13,14,15],  # Top face
        'S3': [0,1,4,5, 8,12,16,17],   # Front face
        'S4': [1,2,5,6, 9,13,17,18],   # Right face
        'S5': [2,3,6,7, 10,14,18,19],  # Back face
        'S6': [3,0,7,4, 11,15,19,16]   # Left face
    }
    
    # C3D20R corner and mid-edge node indices for each face
    face_corner_nodes = {
        'S1': [0,1,2,3],     # Corner nodes
        'S2': [4,5,6,7],
        'S3': [0,1,4,5],
        'S4': [1,2,5,6],
        'S5': [2,3,6,7],
        'S6': [3,0,7,4]
    }
    
    face_mid_edge_nodes = {
        'S1': [8,9,10,11],   # Mid-edge nodes
        'S2': [12,13,14,15],
        'S3': [8,12,16,17],
        'S4': [9,13,17,18],
        'S5': [10,14,18,19],
        'S6': [11,15,19,16]
    }
    
    for elem_id, elem_data in element_chunk.items():
        elem_type = elem_data.get('type', '')
        
        # Only process C3D20R elements
        if elem_type != 'C3D20R':
            continue
            
        node_ids = elem_data.get('nodes', [])
        
        # Skip if we don't have the expected number of nodes
        if len(node_ids) != 20:
            continue
        
        # Check each face of the element
        for face_name in face_nodes_c3d20r.keys():
            corner_indices = face_corner_nodes[face_name]
            mid_edge_indices = face_mid_edge_nodes[face_name]
            
            # Get actual node IDs for this face
            corner_node_ids = [node_ids[i] for i in corner_indices]
            mid_edge_node_ids = [node_ids[i] for i in mid_edge_indices]
            
            # Check boundary conditions
            is_boundary_face = True
            
            # Check corner nodes (can be shared with max 4 elements)
            for node_id in corner_node_ids:
                if node_id in node_to_elements:
                    sharing_count = len(node_to_elements[node_id])
                    if sharing_count > 4:
                        is_boundary_face = False
                        break
            
            # Check mid-edge nodes (can be shared with max 1 other element = 2 total)
            if is_boundary_face:
                for node_id in mid_edge_node_ids:
                    if node_id in node_to_elements:
                        sharing_count = len(node_to_elements[node_id])
                        if sharing_count > 2:
                            is_boundary_face = False
                            break
            
            # If this face meets boundary criteria, add it to results
            if is_boundary_face:
                boundary_faces.add((elem_id, face_name))
    
    return boundary_faces

def find_boundary_faces_c3d20r(elements, nodes=None, max_workers=None):
    """Find all element faces that are at the volume boundary for C3D20R elements
    
    For a face to be considered at the boundary:
    - Corner nodes can be shared with maximum 4 other elements
    - Mid-edge nodes can be shared with maximum 1 other element (2 total including current)
    
    Parameters:
    - elements: Dictionary of elements {elem_id: {'type': type, 'nodes': [node_ids]}}
    - nodes: Dictionary of node coordinates (optional, for future enhancements)
    - max_workers: Maximum number of worker processes (default: CPU count)
    
    Returns:
    - Set of tuples: {(element_id, face_name), ...} representing boundary faces
    """
    print(f"[INFO] Finding boundary faces for C3D20R elements...")
    
    # Filter to only C3D20R elements
    c3d20r_elements = {
        elem_id: elem_data for elem_id, elem_data in elements.items()
        if elem_data.get('type', '') == 'C3D20R'
    }
    
    if not c3d20r_elements:
        print(f"[WARNING] No C3D20R elements found in the mesh")
        return set()
    
    print(f"[INFO] Processing {len(c3d20r_elements)} C3D20R elements")
    
    # Build node-to-elements mapping
    print(f"[INFO] Building node-to-elements mapping...")
    node_to_elements = build_node_to_elements_mapping(elements)
    print(f"[INFO] Mapped {len(node_to_elements)} nodes to their containing elements")
    
    # Determine number of workers
    if max_workers is None:
        max_workers = min(multiprocessing.cpu_count(), len(c3d20r_elements))
    
    # Split elements into chunks for parallel processing
    element_items = list(c3d20r_elements.items())
    chunk_size = max(1, len(element_items) // max_workers)
    element_chunks = []
    
    for i in range(0, len(element_items), chunk_size):
        chunk = dict(element_items[i:i + chunk_size])
        element_chunks.append(chunk)
    
    print(f"[INFO] Processing {len(element_chunks)} chunks using {max_workers} workers")
    
    # Process chunks in parallel
    boundary_faces = set()
    
    with concurrent.futures.ProcessPoolExecutor(max_workers=max_workers) as executor:
        # Submit all chunks
        future_to_chunk = {
            executor.submit(check_boundary_face_chunk, chunk, elements, node_to_elements): i
            for i, chunk in enumerate(element_chunks)
        }
        
        # Collect results
        processed_chunks = 0
        for future in concurrent.futures.as_completed(future_to_chunk):
            chunk_idx = future_to_chunk[future]
            try:
                chunk_boundary_faces = future.result()
                boundary_faces.update(chunk_boundary_faces)
                processed_chunks += 1
                print(f"[INFO] Completed chunk {processed_chunks}/{len(element_chunks)}, found {len(chunk_boundary_faces)} boundary faces")
            except Exception as exc:
                print(f"[ERROR] Chunk {chunk_idx} generated an exception: {exc}")
    
    print(f"[INFO] Found {len(boundary_faces)} total boundary faces")
    return boundary_faces

def get_boundary_face_elements_dict(boundary_faces):
    """Convert boundary faces set to dictionary organized by element ID
    
    Parameters:
    - boundary_faces: Set of tuples {(element_id, face_name), ...}
    
    Returns:
    - Dictionary {element_id: [face_names], ...}
    """
    boundary_dict = defaultdict(list)
    
    for elem_id, face_name in boundary_faces:
        boundary_dict[elem_id].append(face_name)
    
    return dict(boundary_dict)

def get_boundary_face_statistics(boundary_faces, elements):
    """Generate statistics about boundary faces
    
    Parameters:
    - boundary_faces: Set of tuples {(element_id, face_name), ...}
    - elements: Dictionary of all elements
    
    Returns:
    - Dictionary with statistics
    """
    boundary_dict = get_boundary_face_elements_dict(boundary_faces)
    
    # Count elements by number of boundary faces
    face_count_distribution = defaultdict(int)
    for elem_id, face_list in boundary_dict.items():
        face_count_distribution[len(face_list)] += 1
    
    # Count total C3D20R elements
    total_c3d20r = sum(1 for elem_data in elements.values() if elem_data.get('type', '') == 'C3D20R')
    
    stats = {
        'total_boundary_faces': len(boundary_faces),
        'elements_with_boundary_faces': len(boundary_dict),
        'total_c3d20r_elements': total_c3d20r,
        'percentage_boundary_elements': (len(boundary_dict) / total_c3d20r * 100) if total_c3d20r > 0 else 0,
        'face_count_distribution': dict(face_count_distribution)
    }
    
    return stats

def compute_face_normal(face_nodes, nodes):
    """Compute the normal vector for a face given its nodes
    
    Parameters:
    - face_nodes: List of node IDs forming the face
    - nodes: Dictionary of node coordinates {node_id: [x, y, z]}
    
    Returns:
    - Normalized normal vector [nx, ny, nz]
    """
    # Get coordinates of the first three nodes
    if len(face_nodes) < 3:
        return [0, 0, 0]
    
    # Get node coordinates
    try:
        p1 = nodes[face_nodes[0]]
        p2 = nodes[face_nodes[1]]
        p3 = nodes[face_nodes[2]]
    except KeyError:
        return [0, 0, 0]
    
    # Calculate vectors
    v1 = [p2[0] - p1[0], p2[1] - p1[1], p2[2] - p1[2]]
    v2 = [p3[0] - p1[0], p3[1] - p1[1], p3[2] - p1[2]]
    
    # Calculate cross product
    normal = [
        v1[1] * v2[2] - v1[2] * v2[1],
        v1[2] * v2[0] - v1[0] * v2[2],
        v1[0] * v2[1] - v1[1] * v2[0]
    ]
    
    # Normalize the vector
    length = math.sqrt(normal[0]**2 + normal[1]**2 + normal[2]**2)
    if length > 1e-10:
        normal = [n / length for n in normal]
    else:
        normal = [0, 0, 0]
    
    return normal

def compute_angle_between_normals(normal1, normal2):
    """Compute the angle between two normal vectors in radians
    
    Parameters:
    - normal1: First normal vector [nx, ny, nz]
    - normal2: Second normal vector [nx, ny, nz]
    
    Returns:
    - Angle in radians
    """
    # Calculate dot product
    dot_product = sum(n1 * n2 for n1, n2 in zip(normal1, normal2))
    
    # Clamp to avoid numerical errors
    dot_product = max(-1.0, min(1.0, dot_product))
    
    # Calculate angle
    angle = math.acos(abs(dot_product))  # Use absolute value to get acute angle
    
    return angle

def build_face_neighbor_map(elements, nodes):
    """Build a mapping of face neighbors and compute edge angles
    
    Parameters:
    - elements: Dictionary of elements {elem_id: {'type': type, 'nodes': [node_ids]}}
    - nodes: Dictionary of node coordinates {node_id: [x, y, z]}
    
    Returns:
    - face_neighbors: Dictionary mapping face IDs to neighboring face IDs
    - edge_angles: Dictionary mapping edge tuples to minimum angles
    - face_id_to_element: Dictionary mapping face IDs to (element_id, face_name)
    """
    import time
    
    # Face node mappings for different element types
    face_node_definitions = {
        'C3D20': {  # Hexahedral element with 20 nodes
            'S1': [0,1,2,3, 8,9,10,11],  # Bottom face
            'S2': [4,5,6,7, 12,13,14,15],  # Top face
            'S3': [0,1,4,5, 8,12,16,17],  # Front face
            'S4': [1,2,5,6, 9,13,17,18],  # Right face
            'S5': [2,3,6,7, 10,14,18,19],  # Back face
            'S6': [3,0,7,4, 11,15,19,16]   # Left face
        },
        'C3D20R': {  # Hexahedral element with 20 nodes
            'S1': [0,1,2,3, 8,9,10,11],  # Bottom face
            'S2': [4,5,6,7, 12,13,14,15],  # Top face
            'S3': [0,1,4,5, 8,12,16,17],  # Front face
            'S4': [1,2,5,6, 9,13,17,18],  # Right face
            'S5': [2,3,6,7, 10,14,18,19],  # Back face
            'S6': [3,0,7,4, 11,15,19,16]   # Left face
        },
        'C3D15': {  # Wedge element with 15 nodes
            'S1': [0,1,2, 6,7,8],      # Bottom triangular face
            'S2': [3,4,5, 9,10,11],    # Top triangular face
            'S3': [0,1,4,3, 6,13,9,12], # Front quadrilateral face
            'S4': [1,2,5,4, 7,14,10,13], # Right quadrilateral face
            'S5': [0,3,5,2, 12,11,14,8]  # Back quadrilateral face
        },
        'C3D10': {  # Tetrahedral element with 10 nodes
            'S1': [0, 1, 2, 4, 5, 6],      # Face 1
            'S2': [0, 1, 3, 4, 8, 7],      # Face 2
            'S3': [1, 2, 3, 5, 9, 8],      # Face 3
            'S4': [0, 2, 3, 6, 9, 7]       # Face 4
        },
        'C3D4': {  # Tetrahedral element with 4 nodes
            'S1': [0,1,2],
            'S2': [0,3,1],
            'S3': [1,3,2],
            'S4': [0,2,3]
        }
    }
    
    print(f"[INFO] Building face neighbor map for {len(elements):,} elements...")
    print(f"[INFO] This is the most computationally intensive step...")
    
    # Dictionary to store face information
    face_info = {}  # {face_id: {'element_id': elem_id, 'face_name': face_name, 'nodes': [node_ids], 'normal': [nx, ny, nz]}}
    face_id_counter = 0
    face_id_to_element = {}
    
    # First pass: Build node-to-elements mapping to find element connectivity
    print(f"[INFO] Step 1/3: Building node-to-elements mapping...")
    start_time = time.time()
    node_to_elements = build_node_to_elements_mapping(elements)
    step1_time = time.time() - start_time
    print(f"[INFO] [OK] Mapped {len(node_to_elements):,} nodes to elements in {step1_time:.2f}s")
    
    # Second pass: Build face information for all elements
    print(f"[INFO] Step 2/3: Creating face information for all elements...")
    start_time = time.time()
    processed_elements = 0
    progress_interval = max(1, len(elements) // 20)  # Report progress every 5%
    
    for elem_id, elem_data in elements.items():
        element_type = elem_data.get('type', '')
        element_nodes = elem_data.get('nodes', [])
        
        if element_type not in face_node_definitions:
            continue
            
        # Process each face of this element
        for face_name, node_indices in face_node_definitions[element_type].items():
            # Get actual node IDs for this face
            face_nodes = [element_nodes[i] for i in node_indices if i < len(element_nodes)]
            
            if len(face_nodes) < 3:
                continue
                
            # Compute face normal
            face_normal = compute_face_normal(face_nodes, nodes)
            
            # Create unique face ID
            face_id = face_id_counter
            face_id_counter += 1
            
            # Store face information
            face_info[face_id] = {
                'element_id': elem_id,
                'face_name': face_name,
                'nodes': face_nodes,
                'normal': face_normal
            }
            
            face_id_to_element[face_id] = (elem_id, face_name)
        
        processed_elements += 1
        if processed_elements % progress_interval == 0 or processed_elements == len(elements):
            progress = (processed_elements / len(elements)) * 100
            elapsed = time.time() - start_time
            eta = (elapsed / processed_elements) * (len(elements) - processed_elements)
            print(f"[INFO]   Progress: {progress:.1f}% ({processed_elements:,}/{len(elements):,}) - ETA: {eta:.1f}s")
    
    step2_time = time.time() - start_time
    print(f"[INFO] [OK] Created {len(face_info):,} faces from elements in {step2_time:.2f}s")
    
    # Third pass: Build neighbor relationships using optimized edge-based matching
    print(f"[INFO] Step 3/3: Finding face neighbors and computing angles...")
    print(f"[INFO] Using optimized edge-based algorithm for better performance...")
    start_time = time.time()
    
    face_neighbors = defaultdict(set)
    edge_angles = {}
    
    # Create edge-to-faces mapping for faster neighbor finding
    print(f"[INFO]   Building edge-to-faces mapping...")
    edge_to_faces = defaultdict(list)
    
    for face_id, face_info_data in face_info.items():
        face_nodes = face_info_data['nodes']
        # Get corner nodes only (first 3 or 4 nodes)
        corner_nodes = face_nodes[:4] if len(face_nodes) >= 4 else face_nodes[:3]
        
        # Generate all edges for this face
        edges = []
        for i in range(len(corner_nodes)):
            for j in range(i + 1, len(corner_nodes)):
                edge = tuple(sorted([corner_nodes[i], corner_nodes[j]]))
                edges.append(edge)
        
        # Map each edge to this face
        for edge in edges:
            edge_to_faces[edge].append(face_id)
    
    print(f"[INFO]   Created {len(edge_to_faces):,} edges from faces")
    
    # Find neighbors through shared edges
    print(f"[INFO]   Finding neighbors through shared edges...")
    processed_edges = 0
    progress_interval = max(1, len(edge_to_faces) // 20)
    
    for edge, faces_on_edge in edge_to_faces.items():
        # If more than one face shares this edge, they are neighbors
        if len(faces_on_edge) >= 2:
            # Create neighbor relationships between all faces sharing this edge
            for i in range(len(faces_on_edge)):
                for j in range(i + 1, len(faces_on_edge)):
                    face_id1 = faces_on_edge[i]
                    face_id2 = faces_on_edge[j]
                    
                    # Add bidirectional neighbor relationship
                    face_neighbors[face_id1].add(face_id2)
                    face_neighbors[face_id2].add(face_id1)
                    
                    # Compute angle between face normals
                    face1_normal = face_info[face_id1]['normal']
                    face2_normal = face_info[face_id2]['normal']
                    angle = compute_angle_between_normals(face1_normal, face2_normal)
                    
                    # Store the minimum angle for this edge
                    edge_key = tuple(sorted([face_id1, face_id2]))
                    if edge_key not in edge_angles or angle < edge_angles[edge_key]:
                        edge_angles[edge_key] = angle
        
        processed_edges += 1
        if processed_edges % progress_interval == 0 or processed_edges == len(edge_to_faces):
            progress = (processed_edges / len(edge_to_faces)) * 100
            elapsed = time.time() - start_time
            eta = (elapsed / processed_edges) * (len(edge_to_faces) - processed_edges) if processed_edges > 0 else 0
            print(f"[INFO]   Progress: {progress:.1f}% ({processed_edges:,}/{len(edge_to_faces):,}) - ETA: {eta:.1f}s")
    
    step3_time = time.time() - start_time
    print(f"[INFO] [OK] Found {len(edge_angles):,} face neighbor relationships in {step3_time:.2f}s")
    
    total_time = step1_time + step2_time + step3_time
    print(f"[INFO] Face neighbor map complete! Total time: {total_time:.2f}s")
    print(f"[INFO]   - Node mapping: {step1_time:.2f}s")
    print(f"[INFO]   - Face creation: {step2_time:.2f}s") 
    print(f"[INFO]   - Neighbor finding: {step3_time:.2f}s")
    
    # Convert to regular dict
    face_neighbors = dict(face_neighbors)
    
    return face_neighbors, edge_angles, face_id_to_element, face_info

def expand_surface_by_angle(elements, nodes, initial_element_id, initial_face_name, angle_threshold_degrees, surface_name="EXPANDED_SURFACE"):
    """Expand a surface by including all connected faces within an angle threshold
    
    This function mirrors the C# GetGeometrySurfaceIdsByAngle functionality.
    Starting from an initial element face, it finds all connected faces that are
    within the specified angle threshold and creates a surface set.
    
    Parameters:
    - elements: Dictionary of elements {elem_id: {'type': type, 'nodes': [node_ids]}}
    - nodes: Dictionary of node coordinates {node_id: [x, y, z]}
    - initial_element_id: ID of the starting element
    - initial_face_name: Name of the starting face (e.g., 'S1', 'S2', etc.)
    - angle_threshold_degrees: Maximum angle in degrees between faces to include
    - surface_name: Name for the resulting surface set
    
    Returns:
    - Dictionary with surface definition compatible with INP format:
      {
          'surface_name': {
              'declaration': '*SURFACE, NAME=surface_name, TYPE=ELEMENT',
              'type': 'ELEMENT',
              'faces': [(elset_name, face_name), ...]
          }
      }
    - Dictionary with element sets for each face:
      {
          'elset_name': {
              'declaration': '*ELSET, ELSET=elset_name',
              'elements': [elem_id, ...]
          }
      }
    """
    print(f"[INFO] Expanding surface from element {initial_element_id}, face {initial_face_name}")
    print(f"[INFO] Angle threshold: {angle_threshold_degrees} degrees")
    
    # Convert angle to radians
    angle_threshold_radians = math.radians(angle_threshold_degrees)
    
    # Build face neighbor map and edge angles
    face_neighbors, edge_angles, face_id_to_element, face_info = build_face_neighbor_map(elements, nodes)
    
    # Find the initial face ID
    initial_face_id = None
    for face_id, (elem_id, face_name) in face_id_to_element.items():
        if elem_id == initial_element_id and face_name == initial_face_name:
            initial_face_id = face_id
            break
    
    if initial_face_id is None:
        print(f"[ERROR] Initial face not found: element {initial_element_id}, face {initial_face_name}")
        return {}, {}
    
    print(f"[INFO] Starting from face ID {initial_face_id}")
    
    # Initialize surface expansion
    surface_face_ids = set()
    not_visited_face_ids = set()
    
    # Add initial face
    surface_face_ids.add(initial_face_id)
    not_visited_face_ids.add(initial_face_id)
    
    # Expand surface using the spreading algorithm from C#
    iteration = 0
    while not_visited_face_ids:
        iteration += 1
        print(f"[INFO] Iteration {iteration}: processing {len(not_visited_face_ids)} faces")
        
        # Find new face candidates
        new_face_candidates = set()
        
        for current_face_id in not_visited_face_ids:
            # Get neighbors of current face
            neighbors = face_neighbors.get(current_face_id, set())
            
            for neighbor_id in neighbors:
                # Skip if already in surface
                if neighbor_id in surface_face_ids or neighbor_id in new_face_candidates:
                    continue
                
                # Check angle threshold
                edge_key = tuple(sorted([current_face_id, neighbor_id]))
                if edge_key in edge_angles:
                    edge_angle = edge_angles[edge_key]
                    if edge_angle <= angle_threshold_radians:
                        new_face_candidates.add(neighbor_id)
        
        # Add new candidates to surface and mark them for visiting
        not_visited_face_ids.clear()
        for candidate_id in new_face_candidates:
            surface_face_ids.add(candidate_id)
            not_visited_face_ids.add(candidate_id)
        
        print(f"[INFO] Added {len(new_face_candidates)} new faces to surface")
        
        # Safety check to prevent infinite loops
        if iteration > 1000:
            print(f"[WARNING] Maximum iterations reached, stopping expansion")
            break
    
    print(f"[INFO] Surface expansion complete. Total faces: {len(surface_face_ids)}")
    
    # Group elements by face name (S1, S2, S3, etc.)
    face_name_to_elements = defaultdict(set)  # {face_name: set_of_element_ids}
    
    for face_id in surface_face_ids:
        elem_id, face_name = face_id_to_element[face_id]
        face_name_to_elements[face_name].add(elem_id)
    
    # Create element sets grouped by face name
    element_sets = {}
    surface_faces = []
    
    for face_name, element_ids in face_name_to_elements.items():
        if element_ids:  # Only create sets for faces that have elements
            # Create element set name grouped by face
            elset_name = f"{surface_name}_{face_name}"
            
            # Create element set with all elements for this face
            element_sets[elset_name] = {
                'declaration': f"*ELSET, ELSET={elset_name}",
                'elements': sorted(list(element_ids))  # Sort for consistent output
            }
            
            # Add to surface faces
            surface_faces.append((elset_name, face_name))
    
    # Create surface definition
    surface_dict = {
        surface_name: {
            'declaration': f"*SURFACE, NAME={surface_name}, TYPE=ELEMENT",
            'type': 'ELEMENT',
            'faces': surface_faces
        }
    }
    
    # Calculate total elements in all sets
    total_elements = sum(len(elem_set['elements']) for elem_set in element_sets.values())
    
    print(f"[INFO] Created surface '{surface_name}' with {len(surface_faces)} face types")
    print(f"[INFO] Created {len(element_sets)} element sets containing {total_elements} total elements")
    for face_name, element_ids in face_name_to_elements.items():
        print(f"[INFO]   - {surface_name}_{face_name}: {len(element_ids)} elements")
    
    return surface_dict, element_sets

def write_expanded_surface_to_inp(surface_dict, element_sets, output_filename, nodes=None, elements=None, include_full_mesh=False):
    """Write the expanded surface and element sets to an INP file
    
    Parameters:
    - surface_dict: Dictionary with surface definitions
    - element_sets: Dictionary with element set definitions
    - output_filename: Path to output INP file
    - nodes: Dictionary of node coordinates (optional, for full mesh output)
    - elements: Dictionary of element data (optional, for full mesh output)
    - include_full_mesh: Whether to include all nodes and elements in output
    """
    print(f"[INFO] Writing expanded surface to: {output_filename}")
    
    with open(output_filename, 'w') as f:
        f.write("*Heading\n")
        f.write("Expanded Surface Definition\n")
        f.write("**\n")
        
        # Write nodes if provided
        if include_full_mesh and nodes:
            f.write("** Nodes\n")
            f.write("*Node\n")
            for node_id, coords in nodes.items():
                # Format each coordinate with 12 significant digits
                x = format(coords[0], '.12g')
                y = format(coords[1], '.12g') 
                z = format(coords[2], '.12g')
                f.write(f"{node_id}, {x}, {y}, {z}\n")
            f.write("**\n")
        
        # Write elements if provided
        if include_full_mesh and elements:
            f.write("** Elements\n")
            
            # Group elements by type for organized output
            elements_by_type = {}
            for elem_id, elem_data in elements.items():
                elem_type = elem_data.get('type', 'UNKNOWN')
                if elem_type not in elements_by_type:
                    elements_by_type[elem_type] = []
                elements_by_type[elem_type].append((elem_id, elem_data.get('nodes', [])))
            
            # Write each element type group
            for elem_type, elements_list in elements_by_type.items():
                f.write(f"*Element, type={elem_type}, elset=ELSET_{elem_type}_ALL\n")
                for elem_id, nodes_list in elements_list:
                    # Write properly formatted multi-line element definition
                    # First line: element ID and first 8 nodes (or fewer if not enough)
                    first_line_nodes = nodes_list[:min(8, len(nodes_list))]
                    first_line = f"{elem_id}, " + ", ".join(map(str, first_line_nodes))
                    
                    if len(nodes_list) <= 8:
                        # Single line element definition
                        f.write(f"{first_line}\n")
                    else:
                        # Multi-line element definition
                        f.write(f"{first_line},\n")
                        
                        # Write remaining nodes in chunks of 8
                        remaining_nodes = nodes_list[8:]
                        for i in range(0, len(remaining_nodes), 8):
                            chunk = remaining_nodes[i:i+8]
                            line = "    " + ", ".join(map(str, chunk))
                            
                            # Add comma if this isn't the last line
                            if i + 8 < len(remaining_nodes):
                                line += ","
                            
                            f.write(f"{line}\n")
            f.write("**\n")
        
        # Write element sets
        f.write("** Element Sets\n")
        for elset_name, elset_data in element_sets.items():
            f.write(f"{elset_data['declaration']}\n")
            elements_list = elset_data['elements']
            
            # Write elements in groups of 16
            for i in range(0, len(elements_list), 16):
                group = elements_list[i:i+16]
                f.write(", ".join(map(str, group)) + "\n")
        
        f.write("**\n")
        
        # Write surfaces
        f.write("** Surfaces\n")
        for surf_name, surf_data in surface_dict.items():
            f.write(f"{surf_data['declaration']}\n")
            
            for elset_name, face_name in surf_data['faces']:
                f.write(f"{elset_name}, {face_name}\n")
        
        f.write("**\n")
        f.write("** End of expanded surface definition\n")
    
    print(f"[INFO] Successfully wrote expanded surface to {output_filename}")

def test_surface_expansion():
    """Test function for surface expansion by angle"""
    print("[TEST] Testing surface expansion by angle...")
    
    # Create test nodes
    test_nodes = {
        1: [0.0, 0.0, 0.0],
        2: [1.0, 0.0, 0.0],
        3: [1.0, 1.0, 0.0],
        4: [0.0, 1.0, 0.0],
        5: [0.0, 0.0, 1.0],
        6: [1.0, 0.0, 1.0],
        7: [1.0, 1.0, 1.0],
        8: [0.0, 1.0, 1.0],
        # Additional nodes for second element
        9: [1.0, 0.0, 0.0],
        10: [2.0, 0.0, 0.0],
        11: [2.0, 1.0, 0.0],
        12: [1.0, 1.0, 0.0],
        13: [1.0, 0.0, 1.0],
        14: [2.0, 0.0, 1.0],
        15: [2.0, 1.0, 1.0],
        16: [1.0, 1.0, 1.0],
    }
    
    # Create test elements (simplified C3D8 for testing)
    test_elements = {
        1: {
            'type': 'C3D20R',
            'nodes': [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20]
        },
        2: {
            'type': 'C3D20R', 
            'nodes': [9, 10, 11, 12, 13, 14, 15, 16, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32]
        }
    }
    
    try:
        # Test surface expansion
        surface_dict, element_sets = expand_surface_by_angle(
            test_elements, test_nodes, 1, 'S1', 30.0, "TEST_SURFACE"
        )
        
        print(f"[TEST] Surface expansion results:")
        print(f"[TEST] Surfaces: {len(surface_dict)}")
        print(f"[TEST] Element sets: {len(element_sets)}")
        
        # Write to test file
        write_expanded_surface_to_inp(surface_dict, element_sets, "test_expanded_surface.inp", 
                                     nodes=test_nodes, elements=test_elements, include_full_mesh=True)
        
        print("[TEST] Surface expansion test completed successfully!")
        
    except Exception as e:
        print(f"[TEST] Error during surface expansion test: {e}")
        import traceback
        traceback.print_exc()

def verify_element_in_bounding_box(elem_id, elem_data, nodes, x_min, x_max, y_min, y_max, z_min, z_max):
    """Verify that an element has at least 10 nodes within the bounding box
    
    Parameters:
    - elem_id: Element ID
    - elem_data: Element data dictionary with 'nodes' key
    - nodes: Dictionary of node coordinates
    - x_min, x_max, y_min, y_max, z_min, z_max: Bounding box coordinates
    
    Returns:
    - Tuple: (nodes_in_box_count, total_valid_nodes, node_positions)
      where node_positions is a list of (node_id, x, y, z, in_box) tuples
    """
    elem_nodes = elem_data['nodes']
    nodes_in_box_count = 0
    total_valid_nodes = 0
    node_positions = []
    
    for node_id in elem_nodes:
        if node_id not in nodes:
            continue
        
        total_valid_nodes += 1
        x, y, z = nodes[node_id]
        
        in_box = (x_min <= x <= x_max and y_min <= y <= y_max and z_min <= z <= z_max)
        if in_box:
            nodes_in_box_count += 1
        
        node_positions.append((node_id, x, y, z, in_box))
    
    return nodes_in_box_count, total_valid_nodes, node_positions

def add_element_set_by_bounding_box(input_filename, output_filename, elset_name, x_min, x_max, y_min, y_max, z_min, z_max, verbose=False):
    """Add an element set to an INP file based on a bounding box
    
    This function reads an INP file, identifies all elements that have at least 10 nodes
    within the specified bounding box, creates a new element set, and writes the modified
    file to the output location.
    
    An element is included in the set only if at least 10 of its nodes fall within
    the bounding box coordinates.
    
    Parameters:
    - input_filename: Path to the input INP file
    - output_filename: Path to the output INP file
    - elset_name: Name for the new element set
    - x_min: Minimum X coordinate of bounding box
    - x_max: Maximum X coordinate of bounding box
    - y_min: Minimum Y coordinate of bounding box
    - y_max: Maximum Y coordinate of bounding box
    - z_min: Minimum Z coordinate of bounding box
    - z_max: Maximum Z coordinate of bounding box
    - verbose: If True, print detailed information
    
    Returns:
    - Dictionary with statistics about the operation
    """
    try:
        if verbose:
            print(f"[INFO] Reading INP file: {input_filename}")
            print(f"[INFO] Bounding box parameters:")
            print(f"[INFO]   X: min={x_min}, max={x_max}, range={x_max-x_min}")
            print(f"[INFO]   Y: min={y_min}, max={y_max}, range={y_max-y_min}")
            print(f"[INFO]   Z: min={z_min}, max={z_max}, range={z_max-z_min}")
            print(f"[INFO]   Volume: {(x_max-x_min)*(y_max-y_min)*(z_max-z_min)}")
        
        # Read the input file
        nodes, elements, existing_sets = read_inp_file(input_filename)
        
        if verbose:
            print(f"[INFO] Loaded {len(nodes)} nodes and {len(elements)} elements")
            
            # Calculate actual node coordinate ranges
            if nodes:
                x_coords = [coords[0] for coords in nodes.values()]
                y_coords = [coords[1] for coords in nodes.values()]
                z_coords = [coords[2] for coords in nodes.values()]
                
                print(f"[INFO] Actual node coordinate ranges in file:")
                print(f"[INFO]   X: [{min(x_coords):.6f}, {max(x_coords):.6f}]")
                print(f"[INFO]   Y: [{min(y_coords):.6f}, {max(y_coords):.6f}]")
                print(f"[INFO]   Z: [{min(z_coords):.6f}, {max(z_coords):.6f}]")
        
        # Find elements within bounding box
        # Require at least 10 nodes to be within the bounding box
        elements_in_box = []
        min_nodes_required = 10
        elements_with_node_counts = {}  # For debugging: {elem_id: (nodes_in_box, total_nodes)}
        
        for elem_id, elem_data in elements.items():
            # Use verification function to count nodes
            nodes_in_box_count, valid_nodes_count, node_positions = verify_element_in_bounding_box(
                elem_id, elem_data, nodes, x_min, x_max, y_min, y_max, z_min, z_max
            )
            
            # Store for debugging
            if nodes_in_box_count > 0:
                elements_with_node_counts[elem_id] = (nodes_in_box_count, valid_nodes_count)
            
            # If at least 10 nodes are in the box, add this element to the set
            if nodes_in_box_count >= min_nodes_required:
                elements_in_box.append(elem_id)
                if verbose:
                    print(f"[DEBUG] Element {elem_id}: {nodes_in_box_count}/{valid_nodes_count} nodes in bounding box")
                    # Show first few node positions for detailed debugging
                    if nodes_in_box_count >= min_nodes_required and len(elements_in_box) <= 3:
                        for node_id, x, y, z, in_box in node_positions[:5]:
                            status = "IN" if in_box else "OUT"
                            print(f"[DEBUG]   Node {node_id}: ({x:.3f}, {y:.3f}, {z:.3f}) [{status}]")
        
        if verbose:
            print(f"[INFO] Found {len(elements_in_box)} elements within bounding box")
            
            # Show distribution of node counts
            if elements_with_node_counts:
                node_count_distribution = {}
                for elem_id, (in_box, total) in elements_with_node_counts.items():
                    if in_box not in node_count_distribution:
                        node_count_distribution[in_box] = 0
                    node_count_distribution[in_box] += 1
                
                print(f"[INFO] Distribution of nodes in bounding box:")
                for count in sorted(node_count_distribution.keys()):
                    print(f"[INFO]   {count} nodes: {node_count_distribution[count]} elements")
        
        if not elements_in_box:
            print(f"[WARNING] No elements found within the specified bounding box")
            return {
                'success': False,
                'elements_found': 0,
                'message': 'No elements found within bounding box'
            }
        
        # Verification: Double-check that selected elements actually have 10+ nodes in box
        if verbose:
            print(f"[INFO] Verifying selected elements...")
            verification_failed = []
            for elem_id in elements_in_box[:min(5, len(elements_in_box))]:  # Check first 5 elements
                count, total, positions = verify_element_in_bounding_box(
                    elem_id, elements[elem_id], nodes, x_min, x_max, y_min, y_max, z_min, z_max
                )
                if count < min_nodes_required:
                    verification_failed.append((elem_id, count))
                    print(f"[WARNING]   Element {elem_id}: {count}/{total} nodes in box (FAILED)")
                    # Show node positions for failed elements
                    for node_id, x, y, z, in_box in positions[:10]:
                        status = "IN" if in_box else "OUT"
                        print(f"[WARNING]     Node {node_id}: ({x:.3f}, {y:.3f}, {z:.3f}) [{status}]")
                else:
                    print(f"[INFO]   Element {elem_id}: {count}/{total} nodes verified in box (OK)")
            
            if verification_failed:
                print(f"[ERROR] Verification failed for {len(verification_failed)} elements!")
                print(f"[ERROR] This indicates a bug in the selection logic.")
        
        # Create complementary set (elements NOT in bounding box)
        # Convert to set for O(1) lookup instead of O(n) list lookup
        elements_in_box_set = set(elements_in_box)
        elements_outside_box = [elem_id for elem_id in elements.keys() if elem_id not in elements_in_box_set]
        
        if verbose:
            print(f"[INFO] Elements outside bounding box: {len(elements_outside_box)}")
        
        # Add the new element sets to existing_sets
        if elset_name in existing_sets['elsets']:
            print(f"[WARNING] Element set '{elset_name}' already exists, it will be overwritten")
        
        existing_sets['elsets'][elset_name] = {
            'declaration': f"*Elset, elset={elset_name}",
            'elements': sorted(elements_in_box)
        }
        
        # Create complementary set name
        complement_elset_name = f"{elset_name}_OUTSIDE"
        if complement_elset_name in existing_sets['elsets']:
            print(f"[WARNING] Element set '{complement_elset_name}' already exists, it will be overwritten")
        
        existing_sets['elsets'][complement_elset_name] = {
            'declaration': f"*Elset, elset={complement_elset_name}",
            'elements': sorted(elements_outside_box)
        }
        
        if verbose:
            print(f"[INFO] Created element set '{elset_name}' with {len(elements_in_box)} elements")
            print(f"[INFO] Created complementary element set '{complement_elset_name}' with {len(elements_outside_box)} elements")
            print(f"[INFO] Total elements: {len(elements_in_box) + len(elements_outside_box)} (should equal {len(elements)})")
            
            # Write diagnostic file with element details
            diagnostic_file = output_filename.replace('.inp', '_diagnostic.txt')
            try:
                with open(diagnostic_file, 'w') as diag:
                    diag.write("=" * 80 + "\n")
                    diag.write("BOUNDING BOX ELEMENT SELECTION DIAGNOSTIC\n")
                    diag.write("=" * 80 + "\n\n")
                    diag.write(f"Bounding Box:\n")
                    diag.write(f"  X: [{x_min}, {x_max}]\n")
                    diag.write(f"  Y: [{y_min}, {y_max}]\n")
                    diag.write(f"  Z: [{z_min}, {z_max}]\n\n")
                    diag.write(f"Total elements selected: {len(elements_in_box)}\n")
                    diag.write(f"Minimum nodes required: {min_nodes_required}\n\n")
                    diag.write("=" * 80 + "\n\n")
                    
                    # Write details for each selected element
                    for i, elem_id in enumerate(elements_in_box[:20]):  # First 20 elements
                        count, total, positions = verify_element_in_bounding_box(
                            elem_id, elements[elem_id], nodes, x_min, x_max, y_min, y_max, z_min, z_max
                        )
                        
                        diag.write(f"Element {elem_id} (#{i+1}):\n")
                        diag.write(f"  Type: {elements[elem_id]['type']}\n")
                        diag.write(f"  Nodes in box: {count}/{total}\n")
                        diag.write(f"  Node details:\n")
                        
                        for node_id, x, y, z, in_box in positions:
                            status = "IN " if in_box else "OUT"
                            diag.write(f"    Node {node_id:6d}: ({x:10.6f}, {y:10.6f}, {z:10.6f}) [{status}]\n")
                        
                        diag.write("\n")
                        
                        if i >= 19:
                            diag.write(f"... (showing first 20 of {len(elements_in_box)} elements)\n")
                            break
                
                print(f"[INFO] Wrote diagnostic file: {diagnostic_file}")
            except Exception as e:
                print(f"[WARNING] Could not write diagnostic file: {e}")
        
        write_inp_file_with_sets(
            output_filename,
            nodes,
            elements,
            existing_sets,
            heading=f"Modified INP file with element set {elset_name}"
        )
        
        if verbose:
            print(f"[INFO] Successfully wrote output file: {output_filename}")
        
        return {
            'success': True,
            'elements_found': len(elements_in_box),
            'elements_outside': len(elements_outside_box),
            'elset_name': elset_name,
            'complement_elset_name': complement_elset_name,
            'output_file': output_filename,
            'bounding_box': {
                'x_min': x_min, 'x_max': x_max,
                'y_min': y_min, 'y_max': y_max,
                'z_min': z_min, 'z_max': z_max
            }
        }
        
    except Exception as e:
        print(f"[ERROR] Failed to add element set by bounding box: {e}")
        import traceback
        traceback.print_exc()
        return {
            'success': False,
            'elements_found': 0,
            'message': str(e)
        }

def write_inp_file_with_sets(output_filename, nodes, elements, existing_sets, heading=None):
    """Write nodes, elements, and defined sets to an INP file."""
    if existing_sets is None:
        existing_sets = {}
    elsets = existing_sets.get('elsets', {})
    nsets = existing_sets.get('nsets', {})
    surfaces = existing_sets.get('surfaces', {})
    
    with open(output_filename, 'w') as f:
        f.write("*Heading\n")
        if heading:
            f.write(f"{heading}\n")
        else:
            f.write("Modified INP file\n")
        f.write("**\n")
        
        # Nodes
        f.write("*Node\n")
        for node_id, coords in sorted(nodes.items()):
            f.write(f"{node_id}, {coords[0]:.6f}, {coords[1]:.6f}, {coords[2]:.6f}\n")
        f.write("**\n")
        
        # Elements grouped by type for readability
        elements_by_type = {}
        for elem_id, elem_data in elements.items():
            elem_type = elem_data['type']
            elements_by_type.setdefault(elem_type, []).append((elem_id, elem_data['nodes']))
        
        for elem_type, elems in sorted(elements_by_type.items()):
            f.write(f"*Element, type={elem_type}\n")
            for elem_id, node_ids in sorted(elems):
                if len(node_ids) <= 8:
                    f.write(f"{elem_id}, {', '.join(map(str, node_ids))}\n")
                else:
                    f.write(f"{elem_id}, {', '.join(map(str, node_ids[:8]))},\n")
                    for i in range(8, len(node_ids), 8):
                        chunk = node_ids[i:i+8]
                        if i + 8 < len(node_ids):
                            f.write(f"    {', '.join(map(str, chunk))},\n")
                        else:
                            f.write(f"    {', '.join(map(str, chunk))}\n")
        f.write("**\n")
        
        if elsets:
            f.write("** Element Sets\n")
            for set_name, set_data in sorted(elsets.items()):
                f.write(f"{set_data['declaration']}\n")
                elem_list = set_data['elements']
                for i in range(0, len(elem_list), 16):
                    group = elem_list[i:i+16]
                    f.write(", ".join(map(str, group)) + "\n")
            f.write("**\n")
        
        if nsets:
            f.write("** Node Sets\n")
            for set_name, set_data in sorted(nsets.items()):
                f.write(f"{set_data['declaration']}\n")
                node_list = set_data['nodes']
                for i in range(0, len(node_list), 16):
                    group = node_list[i:i+16]
                    f.write(", ".join(map(str, group)) + "\n")
            f.write("**\n")
        
        if surfaces:
            f.write("** Surfaces\n")
            for surf_name, surf_data in sorted(surfaces.items()):
                f.write(f"{surf_data['declaration']}\n")
                for elset_name_ref, face_name in surf_data.get('faces', []):
                    f.write(f"{elset_name_ref}, {face_name}\n")
            f.write("**\n")

def _sanitize_elset_name(name):
    """Return a safe elset name for Abaqus."""
    if not name:
        return "CYLINDER"
    sanitized = re.sub(r'\s+', '_', str(name))
    sanitized = re.sub(r'[^A-Za-z0-9_]', '_', sanitized)
    if not sanitized:
        sanitized = "CYLINDER"
    if sanitized[0].isdigit():
        sanitized = f"ELSET_{sanitized}"
    return sanitized.upper()

def _coerce_point_3d(value):
    """Convert various center representations to a 3D point tuple."""
    if value is None:
        raise ValueError("Cylinder center is required")
    
    if isinstance(value, dict):
        if 'center' in value:
            return _coerce_point_3d(value['center'])
        coords = [
            value.get('x', value.get('X', 0.0)),
            value.get('y', value.get('Y', 0.0)),
            value.get('z', value.get('Z', 0.0))
        ]
        return tuple(float(c) for c in coords)
    
    if isinstance(value, (list, tuple)):
        coords = list(value)
    else:
        raise ValueError("Cylinder center must be a list, tuple, or dict")
    
    if len(coords) == 2:
        coords = [coords[0], 0.0, coords[1]]
    if len(coords) != 3:
        raise ValueError(f"Cylinder center must have 2 or 3 values, got {len(coords)}")
    
    return tuple(float(c) for c in coords)

def _coerce_direction_vector(value):
    """Convert various direction representations to a normalized 3D vector."""
    if value is None:
        return (0.0, 1.0, 0.0)
    
    if isinstance(value, dict):
        if 'direction' in value:
            return _coerce_direction_vector(value['direction'])
        components = [
            value.get('x', value.get('X', 0.0)),
            value.get('y', value.get('Y', 0.0)),
            value.get('z', value.get('Z', 0.0))
        ]
    elif isinstance(value, (list, tuple)):
        components = list(value)
    else:
        raise ValueError("Cylinder direction must be a list, tuple, or dict")
    
    if len(components) == 2:
        components = [components[0], 0.0, components[1]]
    if len(components) != 3:
        raise ValueError(f"Cylinder direction must have 3 values, got {len(components)}")
    
    components = [float(c) for c in components]
    length = math.sqrt(sum(c * c for c in components))
    if length < 1e-12:
        return (0.0, 1.0, 0.0)
    
    return tuple(c / length for c in components)

def is_point_inside_cylinder(point, center, direction, height, radius, radial_tolerance=0.0, axial_tolerance=None):
    """Determine whether a point lies within a finite cylinder."""
    if axial_tolerance is None:
        axial_tolerance = radial_tolerance
    
    if height <= 0 or radius < 0:
        return False
    
    dx = point[0] - center[0]
    dy = point[1] - center[1]
    dz = point[2] - center[2]
    
    axial = dx * direction[0] + dy * direction[1] + dz * direction[2]
    half_height = height / 2.0
    if axial < -half_height - axial_tolerance or axial > half_height + axial_tolerance:
        return False
    
    radial_x = dx - axial * direction[0]
    radial_y = dy - axial * direction[1]
    radial_z = dz - axial * direction[2]
    radial_sq = radial_x * radial_x + radial_y * radial_y + radial_z * radial_z
    allowed_radius = radius + radial_tolerance
    
    return radial_sq <= allowed_radius * allowed_radius

def verify_element_in_cylinder(elem_id, elem_data, nodes, center, direction, height, radius, radial_tolerance=0.0, axial_tolerance=None):
    """Count how many element nodes lie inside the specified cylinder."""
    elem_nodes = elem_data['nodes']
    nodes_in_cylinder = 0
    total_valid_nodes = 0
    node_positions = []
    
    for node_id in elem_nodes:
        if node_id not in nodes:
            continue
        
        total_valid_nodes += 1
        x, y, z = nodes[node_id]
        inside = is_point_inside_cylinder(
            (x, y, z),
            center,
            direction,
            height,
            radius,
            radial_tolerance,
            axial_tolerance
        )
        if inside:
            nodes_in_cylinder += 1
        node_positions.append((node_id, x, y, z, inside))
    
    return nodes_in_cylinder, total_valid_nodes, node_positions

def collect_elements_for_cylinders(
    nodes,
    elements,
    cylinders,
    default_min_nodes=10,
    default_tolerance=1e-4,
    verbose=False
):
    """Determine which elements lie inside each cylinder definition."""
    if not cylinders:
        raise ValueError("At least one cylinder definition is required")
    
    cylinder_results = []
    union_inside = set()
    
    for idx, cyl in enumerate(cylinders):
        elset_name = cyl.get('elset_name') or cyl.get('name') or cyl.get('type')
        elset_name = _sanitize_elset_name(elset_name or f"CYLINDER_{idx + 1:02d}")
        
        center = _coerce_point_3d(cyl.get('center'))
        direction = _coerce_direction_vector(cyl.get('direction'))
        
        height_value = cyl.get('height')
        if height_value is None:
            raise ValueError(f"Cylinder {elset_name} is missing a height")
        height = abs(float(height_value))
        if height <= 0:
            raise ValueError(f"Cylinder {elset_name} must have positive height")
        
        radius_value = (
            cyl.get('radius') or
            cyl.get('actual_radius') or
            cyl.get('target_radius')
        )
        if radius_value is None:
            raise ValueError(f"Cylinder {elset_name} is missing a radius value")
        radius = abs(float(radius_value))
        
        tolerance = float(cyl.get('tolerance', default_tolerance))
        axial_tolerance = float(cyl.get('axial_tolerance', tolerance))
        min_nodes_required = int(cyl.get('min_nodes', default_min_nodes))
        if min_nodes_required < 1:
            min_nodes_required = 1
        
        if verbose:
            print(f"[INFO] Selecting elements for {elset_name}")
            print(f"[INFO]   Center: {center}")
            print(f"[INFO]   Direction: {direction}")
            print(f"[INFO]   Height: {height}")
            print(f"[INFO]   Radius: {radius}")
            print(f"[INFO]   Min nodes: {min_nodes_required}, tolerance: {tolerance}")
        
        elements_in_cylinder = []
        preview_logged = 0
        
        for elem_id, elem_data in elements.items():
            nodes_in_cylinder, valid_nodes, node_positions = verify_element_in_cylinder(
                elem_id,
                elem_data,
                nodes,
                center,
                direction,
                height,
                radius,
                tolerance,
                axial_tolerance
            )
            
            if nodes_in_cylinder >= min_nodes_required:
                elements_in_cylinder.append(elem_id)
                union_inside.add(elem_id)
                
                if verbose and preview_logged < 3:
                    print(f"[DEBUG] Element {elem_id}: {nodes_in_cylinder}/{valid_nodes} nodes in cylinder")
                    for node_id, x, y, z, inside in node_positions[:5]:
                        status = "IN" if inside else "OUT"
                        print(f"[DEBUG]   Node {node_id}: ({x:.3f}, {y:.3f}, {z:.3f}) [{status}]")
                    preview_logged += 1
        
        if verbose:
            print(f"[INFO] Found {len(elements_in_cylinder)} elements inside {elset_name}")
        
        cylinder_results.append({
            'elset_name': elset_name,
            'elements': sorted(elements_in_cylinder),
            'radius': radius,
            'height': height,
            'center': center,
            'direction': direction,
            'min_nodes': min_nodes_required,
            'tolerance': tolerance,
            'axial_tolerance': axial_tolerance
        })
    
    return cylinder_results, union_inside

def add_element_set_by_cylinder(
    input_filename,
    output_filename,
    cylinders,
    default_min_nodes=10,
    default_tolerance=1e-4,
    complement_elset_name=None,
    verbose=False
):
    """Create element sets for elements located within one or more cylinders."""
    if not cylinders:
        raise ValueError("At least one cylinder definition is required")
    
    if verbose:
        print(f"[INFO] Reading INP file: {input_filename}")
    
    nodes, elements, existing_sets = read_inp_file(input_filename)
    
    if verbose:
        print(f"[INFO] Loaded {len(nodes)} nodes and {len(elements)} elements")
    
    existing_sets.setdefault('elsets', {})
    existing_sets.setdefault('nsets', {})
    existing_sets.setdefault('surfaces', {})
    
    cylinder_results, union_inside = collect_elements_for_cylinders(
        nodes,
        elements,
        cylinders,
        default_min_nodes=default_min_nodes,
        default_tolerance=default_tolerance,
        verbose=verbose
    )
    
    for result in cylinder_results:
        existing_sets['elsets'][result['elset_name']] = {
            'declaration': f"*Elset, elset={result['elset_name']}",
            'elements': result['elements']
        }
    
    complement_name = None
    elements_outside = []
    if complement_elset_name is not None:
        complement_name = _sanitize_elset_name(complement_elset_name)
        if verbose:
            print(f"[INFO] Creating complementary element set {complement_name}")
        elements_outside = [elem_id for elem_id in elements.keys() if elem_id not in union_inside]
        existing_sets['elsets'][complement_name] = {
            'declaration': f"*Elset, elset={complement_name}",
            'elements': sorted(elements_outside)
        }
    
    write_inp_file_with_sets(
        output_filename,
        nodes,
        elements,
        existing_sets,
        heading="Modified INP file with cylinder element sets"
    )
    
    if verbose:
        print(f"[INFO] Successfully wrote output file: {output_filename}")
    
    return {
        'success': True,
        'cylinders': [
            {
                **result,
                'elements_found': len(result['elements'])
            } for result in cylinder_results
        ],
        'outside_elements': len(elements_outside),
        'complement_elset_name': complement_name,
        'output_file': output_filename
    }
    
def _copy_existing_sets(existing_sets):
    """Create a shallow copy of existing sets to avoid mutating inputs."""
    sets_copy = {'nsets': {}, 'elsets': {}, 'surfaces': {}}
    if not existing_sets:
        return sets_copy
    
    for name, data in existing_sets.get('nsets', {}).items():
        sets_copy['nsets'][name] = {
            'declaration': data.get('declaration', f"*Nset, nset={name}"),
            'nodes': list(data.get('nodes', []))
        }
    for name, data in existing_sets.get('elsets', {}).items():
        sets_copy['elsets'][name] = {
            'declaration': data.get('declaration', f"*Elset, elset={name}"),
            'elements': list(data.get('elements', []))
        }
    for name, data in existing_sets.get('surfaces', {}).items():
        sets_copy['surfaces'][name] = {
            'declaration': data.get('declaration', f"*Surface, name={name}"),
            'type': data.get('type', 'ELEMENT'),
            'faces': list(data.get('faces', []))
        }
    return sets_copy


def _write_inp_with_sets_precise(output_filename, nodes, elements, existing_sets, heading):
    """Write an INP file with sets using 10 decimal precision for nodes."""
    if existing_sets is None:
        existing_sets = {'nsets': {}, 'elsets': {}, 'surfaces': {}}
    elsets = existing_sets.get('elsets', {})
    nsets = existing_sets.get('nsets', {})
    surfaces = existing_sets.get('surfaces', {})

    with open(output_filename, 'w') as f:
        f.write("*Heading\n")
        f.write(f"{heading}\n")
        f.write("**\n")

        f.write("*Node\n")
        for node_id, coords in sorted(nodes.items()):
            f.write(f"{node_id}, {coords[0]:.10f}, {coords[1]:.10f}, {coords[2]:.10f}\n")
        f.write("**\n")

        elements_by_type = {}
        for elem_id, elem_data in elements.items():
            elem_type = elem_data['type']
            elements_by_type.setdefault(elem_type, []).append((elem_id, elem_data['nodes']))

        for elem_type, elems in sorted(elements_by_type.items()):
            f.write(f"*Element, type={elem_type}\n")
            for elem_id, node_ids in sorted(elems):
                if len(node_ids) <= 8:
                    f.write(f"{elem_id}, {', '.join(map(str, node_ids))}\n")
                else:
                    f.write(f"{elem_id}, {', '.join(map(str, node_ids[:8]))},\n")
                    for i in range(8, len(node_ids), 8):
                        chunk = node_ids[i:i+8]
                        if i + 8 < len(node_ids):
                            f.write(f"    {', '.join(map(str, chunk))},\n")
                        else:
                            f.write(f"    {', '.join(map(str, chunk))}\n")
        f.write("**\n")

        if elsets:
            f.write("** Element Sets\n")
            for set_name, set_data in sorted(elsets.items()):
                f.write(f"{set_data['declaration']}\n")
                elem_list = set_data['elements']
                for i in range(0, len(elem_list), 16):
                    group = elem_list[i:i+16]
                    f.write(", ".join(map(str, group)) + "\n")
            f.write("**\n")

        if nsets:
            f.write("** Node Sets\n")
            for set_name, set_data in sorted(nsets.items()):
                f.write(f"{set_data['declaration']}\n")
                node_list = set_data['nodes']
                for i in range(0, len(node_list), 16):
                    group = node_list[i:i+16]
                    f.write(", ".join(map(str, group)) + "\n")
            f.write("**\n")

        if surfaces:
            f.write("** Surfaces\n")
            for surf_name, surf_data in sorted(surfaces.items()):
                f.write(f"{surf_data['declaration']}\n")
                for elset_name_ref, face_name in surf_data.get('faces', []):
                    f.write(f"{elset_name_ref}, {face_name}\n")
            f.write("**\n")


def _merge_coincident_nodes_data(nodes, elements, existing_sets, tolerance=1e-6, verbose=False):
    """Core coincident-node merge routine operating on in-memory data."""
    merged_nodes = dict(nodes)
    merged_elements = {
        elem_id: {**elem_data, 'nodes': list(elem_data.get('nodes', []))}
        for elem_id, elem_data in elements.items()
    }
    merged_sets = _copy_existing_sets(existing_sets)

    if verbose:
        print(f"[INFO] Building spatial index...")

    cell_size = tolerance * 2
    spatial_grid = {}
    for node_id, coords in merged_nodes.items():
        x, y, z = coords
        cell_key = (int(x / cell_size), int(y / cell_size), int(z / cell_size))
        spatial_grid.setdefault(cell_key, []).append(node_id)

    if verbose:
        print(f"[INFO] Created spatial grid with {len(spatial_grid)} cells")
        print(f"[INFO] Finding coincident nodes...")

    node_merge_map = {}
    nodes_to_remove = set()
    coincident_groups = []
    processed_nodes = set()

    for cell_key, cell_nodes in spatial_grid.items():
        if len(cell_nodes) < 2:
            continue

        for node_id in cell_nodes:
            if node_id in processed_nodes:
                continue

            node_coords = merged_nodes[node_id]
            coincident_with = [node_id]

            for dx in [-1, 0, 1]:
                for dy in [-1, 0, 1]:
                    for dz in [-1, 0, 1]:
                        neighbor_key = (cell_key[0] + dx, cell_key[1] + dy, cell_key[2] + dz)
                        if neighbor_key not in spatial_grid:
                            continue

                        for other_node_id in spatial_grid[neighbor_key]:
                            if other_node_id <= node_id or other_node_id in processed_nodes:
                                continue

                            other_coords = merged_nodes[other_node_id]
                            dist = math.sqrt(
                                (node_coords[0] - other_coords[0]) ** 2 +
                                (node_coords[1] - other_coords[1]) ** 2 +
                                (node_coords[2] - other_coords[2]) ** 2
                            )

                            if dist <= tolerance:
                                coincident_with.append(other_node_id)
                                processed_nodes.add(other_node_id)

            if len(coincident_with) > 1:
                kept_node = min(coincident_with)
                coincident_groups.append(coincident_with)

                for removed_node in coincident_with:
                    if removed_node != kept_node:
                        node_merge_map[removed_node] = kept_node
                        nodes_to_remove.add(removed_node)

            processed_nodes.add(node_id)

    if verbose:
        print(f"[INFO] Found {len(coincident_groups)} groups of coincident nodes")
        print(f"[INFO] Total nodes to merge: {len(nodes_to_remove)}")
        if coincident_groups and len(coincident_groups) <= 10:
            print(f"[INFO] Coincident groups:")
            for i, group in enumerate(coincident_groups[:10]):
                print(f"[INFO]   Group {i+1}: {group} -> keeping {min(group)}")

    if not nodes_to_remove:
        return merged_nodes, merged_elements, merged_sets, {
            'success': True,
            'nodes_merged': 0,
            'nodes_kept': len(merged_nodes),
            'elements_updated': 0,
            'coincident_groups': len(coincident_groups),
            'message': 'No coincident nodes found',
            'changes_made': False
        }

    if verbose:
        print(f"[INFO] Updating element definitions...")

    elements_updated = 0
    for elem_id, elem_data in merged_elements.items():
        original_nodes = elem_data['nodes']
        updated_nodes = []
        changed = False

        for node_id in original_nodes:
            if node_id in node_merge_map:
                updated_nodes.append(node_merge_map[node_id])
                changed = True
            else:
                updated_nodes.append(node_id)

        if changed:
            elem_data['nodes'] = updated_nodes
            elements_updated += 1

    if verbose:
        print(f"[INFO] Updated {elements_updated} elements")
        print(f"[INFO] Updating node sets...")

    for nset_name, nset_data in merged_sets['nsets'].items():
        updated_nodes = []
        seen_nodes = set()

        for node_id in nset_data['nodes']:
            target_id = node_merge_map.get(node_id, node_id)
            if target_id not in seen_nodes:
                updated_nodes.append(target_id)
                seen_nodes.add(target_id)

        merged_sets['nsets'][nset_name]['nodes'] = updated_nodes

    for node_id in nodes_to_remove:
        merged_nodes.pop(node_id, None)

    if verbose:
        print(f"[INFO] Final node count: {len(merged_nodes)}")

    return merged_nodes, merged_elements, merged_sets, {
        'success': True,
        'nodes_merged': len(nodes_to_remove),
        'nodes_kept': len(merged_nodes),
        'elements_updated': elements_updated,
        'coincident_groups': len(coincident_groups),
        'message': '',
        'changes_made': True
    }


def _default_part_name_from_path(path):
    """Create a safe part prefix from a file path."""
    base = os.path.splitext(os.path.basename(path))[0]
    safe = re.sub(r'\W+', '_', base).upper()
    return safe if safe else "PART"


def merge_inp_files_with_coincident_nodes(primary_filename, secondary_filename, output_filename, tolerance=1e-6, verbose=False, primary_part_name=None, secondary_part_name=None):
    """Combine two INP files, offset IDs, and merge coincident nodes across them."""
    try:
        if verbose:
            print(f"[INFO] Reading primary INP file: {primary_filename}")
        part_a = primary_part_name or _default_part_name_from_path(primary_filename)
        nodes_a, elements_a, sets_a = read_inp_file(primary_filename, part_name=part_a)

        node_offset = max(nodes_a.keys()) if nodes_a else 0
        element_offset = max(elements_a.keys()) if elements_a else 0

        if verbose:
            print(f"[INFO] Reading secondary INP file: {secondary_filename}")
            print(f"[INFO] Offsetting secondary IDs by nodes +{node_offset}, elements +{element_offset}")
        part_b = secondary_part_name or _default_part_name_from_path(secondary_filename)
        nodes_b, elements_b, combined_sets = read_inp_file(
            secondary_filename,
            max_node_id=node_offset,
            max_elem_id=element_offset,
            existing_sets=sets_a,
            part_name=part_b
        )

        merged_nodes = {**nodes_a, **nodes_b}
        merged_elements = {**elements_a, **elements_b}
        initial_node_count = len(merged_nodes)
        initial_element_count = len(merged_elements)

        merged_nodes, merged_elements, merged_sets, stats = _merge_coincident_nodes_data(
            merged_nodes, merged_elements, combined_sets, tolerance, verbose
        )

        if verbose:
            print(f"[INFO] Writing combined output to {output_filename}")
        _write_inp_with_sets_precise(
            output_filename,
            merged_nodes,
            merged_elements,
            merged_sets,
            heading="Combined INP file with merged coincident nodes"
        )

        stats.update({
            'success': True,
            'output_file': output_filename,
            'node_offset': node_offset,
            'element_offset': element_offset,
            'source_files': [
                {'path': primary_filename, 'nodes': len(nodes_a), 'elements': len(elements_a)},
                {'path': secondary_filename, 'nodes': len(nodes_b), 'elements': len(elements_b)}
            ],
            'nodes_before_merge': initial_node_count,
            'elements_before_merge': initial_element_count
        })
        return stats
    except Exception as e:
        print(f"[ERROR] Failed to merge two INP files: {e}")
        import traceback
        traceback.print_exc()
        return {
            'success': False,
            'nodes_merged': 0,
            'message': str(e)
        }


def merge_coincident_nodes(input_filename, output_filename, tolerance=1e-6, verbose=False):
    """Merge coincident nodes in an INP file."""
    try:
        if verbose:
            print(f"[INFO] Reading INP file: {input_filename}")
            print(f"[INFO] Coincidence tolerance: {tolerance}")

        nodes, elements, existing_sets = read_inp_file(input_filename)
        if verbose:
            print(f"[INFO] Loaded {len(nodes)} nodes and {len(elements)} elements")

        merged_nodes, merged_elements, merged_sets, stats = _merge_coincident_nodes_data(
            nodes, elements, existing_sets, tolerance, verbose
        )

        if stats['nodes_merged'] == 0:
            print(f"[INFO] No coincident nodes found. No changes needed.")
            return stats

        if verbose:
            print(f"[INFO] Writing output file...")

        _write_inp_with_sets_precise(
            output_filename,
            merged_nodes,
            merged_elements,
            merged_sets,
            heading="INP file with merged coincident nodes"
        )

        if verbose:
            print(f"[INFO] Successfully wrote output file: {output_filename}")

        stats.update({'output_file': output_filename})
        return stats

    except Exception as e:
        print(f"[ERROR] Failed to merge coincident nodes: {e}")
        import traceback
        traceback.print_exc()
        return {
            'success': False,
            'nodes_merged': 0,
            'message': str(e)
        }

def translate_inp_file(input_filename, output_filename, dx=0.0, dy=0.0, dz=0.0, mirror_x=False, mirror_y=False, mirror_z=False, verbose=False):
    """Translate all nodes in an INP file by specified amounts
    
    Parameters:
    - input_filename: Path to the input INP file
    - output_filename: Path to the output INP file
    - dx: Translation in X direction (default: 0.0)
    - dy: Translation in Y direction (default: 0.0)
    - dz: Translation in Z direction (default: 0.0)
    - mirror_x: If True, mirror the nodes in the X direction
    - mirror_y: If True, mirror the nodes in the Y direction
    - mirror_z: If True, mirror the nodes in the Z direction
    - verbose: If True, print detailed information about the translation
    
    Returns:
    - True if successful, False otherwise
    """
    try:
        # Read the input file
        with open(input_filename, 'r') as f:
            lines = f.readlines()
        
        if verbose:
            print(f"[INFO] Reading {len(lines)} lines from {input_filename}")
            print(f"[INFO] Translation parameters: dx={dx}, dy={dy}, dz={dz}")
        
        # Process the file line by line
        translated_lines = []
        in_node_section = False
        nodes_translated = 0
        
        for i, line in enumerate(lines):
            stripped_line = line.strip()
            
            # Check if we're entering a node section
            if stripped_line.upper().startswith('*NODE') and 'NSET' not in stripped_line.upper():
                in_node_section = True
                if verbose:
                    print(f"[INFO] Entering node section at line {i+1}")
                translated_lines.append(line)
                continue
            
            # Check if we're leaving the node section (any line starting with *)
            elif in_node_section and stripped_line.startswith('*'):
                in_node_section = False
                if verbose:
                    print(f"[INFO] Exiting node section at line {i+1}, translated {nodes_translated} nodes")
                translated_lines.append(line)
                continue
            
            # Process node lines
            elif in_node_section and stripped_line:
                try:
                    # Parse the node line: ID, X, Y, Z
                    parts = [part.strip() for part in stripped_line.split(',')]
                    if len(parts) >= 4:
                        node_id = parts[0]
                        x = float(parts[1])
                        y = float(parts[2])
                        z = float(parts[3])
                        
                        # Apply translation
                        if mirror_x:
                            x = -x
                        if mirror_y:
                            y = -y
                        if mirror_z:
                            z = -z
                        new_x = x + dx
                        new_y = y + dy
                        new_z = z + dz
                        
                        # Format the translated node line
                        # Preserve the original formatting as much as possible
                        if len(parts) > 4:
                            # If there are additional fields, preserve them
                            translated_line = f"{node_id}, {new_x:.6f}, {new_y:.6f}, {new_z:.6f}"
                            for part in parts[4:]:
                                translated_line += f", {part}"
                            translated_line += "\n"
                        else:
                            translated_line = f"{node_id}, {new_x:.6f}, {new_y:.6f}, {new_z:.6f}\n"
                        
                        translated_lines.append(translated_line)
                        nodes_translated += 1
                    else:
                        # If the line doesn't have enough parts, keep it as is
                        translated_lines.append(line)
                        
                except (ValueError, IndexError) as e:
                    if verbose:
                        print(f"[WARNING] Could not parse node line {i+1}: {stripped_line}")
                        print(f"[WARNING] Error: {e}")
                    # Keep the line as is if we can't parse it
                    translated_lines.append(line)
            else:
                # Keep all other lines unchanged
                translated_lines.append(line)
        
        # Write the translated file
        with open(output_filename, 'w') as f:
            f.writelines(translated_lines)
        
        if verbose:
            print(f"[INFO] Successfully translated {nodes_translated} nodes")
            print(f"[INFO] Output written to {output_filename}")
        
        return True
        
    except Exception as e:
        print(f"[ERROR] Failed to translate INP file: {e}")
        return False

def convert_quadratic_to_linear_elements(nodes, elements, verbose=False):
    """Convert quadratic elements to linear elements by keeping only the corner nodes
    
    This function converts quadratic elements (like C3D10, C3D20, C3D15) to their 
    linear counterparts (like C3D4, C3D8, C3D6) by keeping only the first N nodes 
    where N is the number of corner nodes for the linear element type.
    
    Parameters:
    - nodes: Dictionary of node coordinates {node_id: [x, y, z]}
    - elements: Dictionary of elements {elem_id: {'type': element_type, 'nodes': [node_ids]}}
    - verbose: If True, print detailed information about the conversion
    
    Returns:
    - tuple: (converted_elements, used_nodes, conversion_stats)
        - converted_elements: Dictionary of converted elements
        - used_nodes: Set of node IDs that are still in use after conversion
        - conversion_stats: Dictionary with conversion statistics
    """
    # Define element type conversions (quadratic -> linear)
    ELEMENT_CONVERSIONS = {
        'C3D10': 'C3D4',   # 10-node tetrahedral -> 4-node tetrahedral
        'C3D20': 'C3D8',   # 20-node hexahedral -> 8-node hexahedral
        'C3D20R': 'C3D8',  # 20-node reduced integration hexahedral -> 8-node hexahedral
        'C3D15': 'C3D6',   # 15-node triangular prism -> 6-node triangular prism
        'C3D27': 'C3D8',   # 27-node hexahedral -> 8-node hexahedral (keeping corner nodes)
        'S6': 'S3',        # 6-node triangular shell -> 3-node triangular shell
        'S8': 'S4',        # 8-node quadrilateral shell -> 4-node quadrilateral shell
    }
    
    # Define the number of corner nodes to keep for each conversion
    CORNER_NODE_COUNTS = {
        'C3D10': 4,   # Keep first 4 nodes (corners) from C3D10
        'C3D20': 8,   # Keep first 8 nodes (corners) from C3D20
        'C3D20R': 8,  # Keep first 8 nodes (corners) from C3D20R
        'C3D15': 6,   # Keep first 6 nodes (corners) from C3D15
        'C3D27': 8,   # Keep first 8 nodes (corners) from C3D27
        'S6': 3,      # Keep first 3 nodes (corners) from S6
        'S8': 4,      # Keep first 4 nodes (corners) from S8
    }
    
    converted_elements = {}
    used_nodes = set()
    conversion_stats = {
        'total_elements': len(elements),
        'converted_elements': 0,
        'unchanged_elements': 0,
        'unsupported_elements': 0,
        'element_type_conversions': {},
        'original_node_count': len(nodes),
        'final_node_count': 0
    }
    
    if verbose:
        print(f"[INFO] Starting quadratic to linear element conversion")
        print(f"[INFO] Input: {len(elements)} elements, {len(nodes)} nodes")
    
    for elem_id, elem_data in elements.items():
        elem_type = elem_data['type']
        elem_nodes = elem_data['nodes']
        
        # Check if this element type can be converted
        if elem_type in ELEMENT_CONVERSIONS:
            # Get the new element type and number of corner nodes to keep
            new_elem_type = ELEMENT_CONVERSIONS[elem_type]
            corner_count = CORNER_NODE_COUNTS[elem_type]
            
            # Keep only the corner nodes (first N nodes)
            corner_nodes = elem_nodes[:corner_count]
            
            # Create the converted element
            converted_elements[elem_id] = {
                'type': new_elem_type,
                'nodes': corner_nodes,
                'original_type': elem_type,
                'original_nodes': elem_nodes
            }
            
            # Track used nodes
            used_nodes.update(corner_nodes)
            
            # Update statistics
            conversion_stats['converted_elements'] += 1
            if new_elem_type not in conversion_stats['element_type_conversions']:
                conversion_stats['element_type_conversions'][new_elem_type] = 0
            conversion_stats['element_type_conversions'][new_elem_type] += 1
            
            if verbose:
                print(f"[INFO] Converted element {elem_id}: {elem_type} -> {new_elem_type}")
                print(f"[INFO]   Nodes: {len(elem_nodes)} -> {len(corner_nodes)}")
                
        else:
            # Element type not supported for conversion, keep as is
            converted_elements[elem_id] = elem_data.copy()
            used_nodes.update(elem_nodes)
            
            if elem_type in ['C3D4', 'C3D8', 'C3D6', 'S3', 'S4']:
                conversion_stats['unchanged_elements'] += 1
                if verbose:
                    print(f"[INFO] Element {elem_id}: {elem_type} (already linear, unchanged)")
            else:
                conversion_stats['unsupported_elements'] += 1
                if verbose:
                    print(f"[WARNING] Element {elem_id}: {elem_type} (unsupported type, unchanged)")
    
    # Calculate final node count
    conversion_stats['final_node_count'] = len(used_nodes)
    
    if verbose:
        print(f"\n[INFO] Conversion completed:")
        print(f"[INFO] Total elements: {conversion_stats['total_elements']}")
        print(f"[INFO] Converted elements: {conversion_stats['converted_elements']}")
        print(f"[INFO] Unchanged elements: {conversion_stats['unchanged_elements']}")
        print(f"[INFO] Unsupported elements: {conversion_stats['unsupported_elements']}")
        print(f"[INFO] Original nodes: {conversion_stats['original_node_count']}")
        print(f"[INFO] Final nodes: {conversion_stats['final_node_count']}")
        print(f"[INFO] Node reduction: {conversion_stats['original_node_count'] - conversion_stats['final_node_count']} nodes")
        
        if conversion_stats['element_type_conversions']:
            print(f"[INFO] Element type conversions:")
            for new_type, count in conversion_stats['element_type_conversions'].items():
                print(f"[INFO]   -> {new_type}: {count} elements")
    
    return converted_elements, used_nodes, conversion_stats

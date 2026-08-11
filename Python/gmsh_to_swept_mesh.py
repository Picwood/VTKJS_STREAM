#!/usr/bin/env python
"""
Generalized Mesh Sweeping Tool

This script takes Python scripts that use GMSH API as input and produces swept
3D INP meshes using the CPU implementation bundled in this repository.
"""

import sys
import os
import time
import argparse
import json
import numpy as np
import gmsh
import subprocess
import re

# Import helpers from sibling modules
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from sweep_mesh_cpu import (
    compute_stanchion_translation,
    sweep_2d_mesh_cpu,
    translate_profile_nodes,
)

def parse_arguments():
    """Parse command line arguments"""
    parser = argparse.ArgumentParser(description="Generate 3D swept meshes from GMSH Python scripts")
    
    parser.add_argument("--input", "-i", nargs="+", required=False,
                        help="Input Python scripts that use GMSH API")
    
    parser.add_argument("--output", "-o", nargs="+",
                        help="Output INP file name(s). If not provided, will use input name with .inp extension")
    
    parser.add_argument("--angles", "-a", type=int, default=50,
                        help="Number of angles in the sweep (default: 50)")
    
    parser.add_argument("--total-angle", "-t", type=float, default=360.0,
                        help="Total angle to sweep through (default: 360 degrees)")
    
    parser.add_argument("--config", "-c", type=str,
                        help="JSON configuration file with additional parameters")
    
    parser.add_argument("--combine", action="store_true",
                        help="Combine all output meshes into a single INP file")
    
    parser.add_argument("--combined-output", type=str, default="combined_mesh.inp",
                        help="Output file name for combined mesh (default: combined_mesh.inp)")
    
    parser.add_argument("--use-cpu", action="store_true",
                        help="Deprecated compatibility option. The CPU implementation is always used.")

    parser.add_argument("--stanchion-top", nargs=3, type=float, metavar=("X", "Y", "Z"),
                        help="Automatically position the swept profile so its top matches the supplied point.")
    
    # Add a new argument group for direct INP file combining
    combine_group = parser.add_argument_group('INP File Combining')
    combine_group.add_argument("--combine-inp", nargs="+",
                        help="Directly combine existing INP files without mesh generation")
    combine_group.add_argument("--combine-output", type=str,
                        help="Output file for direct INP file combining")
    
    args = parser.parse_args()
    
    # Validate arguments
    if args.combine_inp and not args.combine_output:
        parser.error("--combine-output is required when using --combine-inp")
    
    if not args.input and not args.combine_inp:
        parser.error("Either --input or --combine-inp is required")
    
    return args

def load_config(config_file):
    """Load configuration from JSON file"""
    if not config_file or not os.path.exists(config_file):
        return {}
    
    with open(config_file, 'r') as f:
        return json.load(f)

def extract_2d_mesh_from_gmsh_script(script_path, return_metadata=False):
    """Wrapper to use shared extractor in utilities_simulation."""
    from utilities_simulation import extract_2d_mesh_from_gmsh_script as _extract  # type: ignore
    return _extract(script_path, return_metadata=return_metadata)

def fallback_extract_from_inp(script_path):
    """
    Fallback method to extract mesh from INP file if direct extraction fails
    """
    try:
        # Get the directory of the script
        script_dir = os.path.dirname(os.path.abspath(script_path))
        script_name = os.path.basename(script_path)
        
        # Read translation parameters from the commented lines at the top of the file
        repos = [0.0, 0.0, 0.0]  # Default values if not found
        try:
            with open(script_path, 'r') as f:
                script_lines = f.readlines()
            
            # Check if there are enough lines in the file
            if len(script_lines) < 4:
                print("[WARNING] Script file doesn't have enough lines for translation parameters")
                print("Using default translation values [0.0, 0.0, 0.0]")
            else:
                # Check if first line contains the expected comment
                if not script_lines[0].strip().startswith("# next lines describe the center position"):
                    print("[WARNING] First line doesn't contain the expected translation parameter comment")
                    print(f"Found: {script_lines[0].strip()}")
                    print("Using default translation values [0.0, 0.0, 0.0]")
                else:
                    # Parse X translation
                    try:
                        if script_lines[1].startswith('#'):
                            x_value = script_lines[1].strip()[1:].strip()
                            if x_value:
                                repos[0] = float(x_value)
                            else:
                                print("[WARNING] X translation value is empty, using default 0.0")
                        else:
                            print(f"[WARNING] X translation line doesn't start with #: {script_lines[1].strip()}")
                    except ValueError:
                        print(f"[ERROR] Invalid X translation value: {script_lines[1].strip()}")
                    
                    # Parse Y translation
                    try:
                        if script_lines[2].startswith('#'):
                            y_value = script_lines[2].strip()[1:].strip()
                            if y_value:
                                repos[1] = float(y_value)
                            else:
                                print("[WARNING] Y translation value is empty, using default 0.0")
                        else:
                            print(f"[WARNING] Y translation line doesn't start with #: {script_lines[2].strip()}")
                    except ValueError:
                        print(f"[ERROR] Invalid Y translation value: {script_lines[2].strip()}")
                    
                    # Parse Z translation
                    try:
                        if script_lines[3].startswith('#'):
                            z_value = script_lines[3].strip()[1:].strip()
                            if z_value:
                                repos[2] = float(z_value)
                            else:
                                print("[WARNING] Z translation value is empty, using default 0.0")
                        else:
                            print(f"[WARNING] Z translation line doesn't start with #: {script_lines[3].strip()}")
                    except ValueError:
                        print(f"[ERROR] Invalid Z translation value: {script_lines[3].strip()}")
                
                print(f"Translation parameters: X={repos[0]}, Y={repos[1]}, Z={repos[2]}")
                    
        except Exception as e:
            print(f"[ERROR] Failed to parse translation parameters: {str(e)}")
            print("Using default translation values [0.0, 0.0, 0.0]")
        
        # Look for gmsh.write() calls to find the output file name
        with open(script_path, 'r') as f:
            script_content = f.read()
            
        write_pattern = re.compile(r'gmsh\.write\(["\'](.+?)["\']\)')
        write_matches = write_pattern.findall(script_content)
        
        output_file_name = None
        if write_matches:
            output_file_name = write_matches[0]
            print(f"Found output file name in script: {output_file_name}")
        
        # Run the original script to generate the mesh
        print(f"Extracting 2D mesh from script {script_path}...")
        subprocess.run([sys.executable, script_path], check=True)
        
        # If we found an output file name in the script, use that
        inp_file = None
        if output_file_name:
            inp_file = os.path.join(script_dir, output_file_name)
            if not os.path.exists(inp_file):
                print(f"Warning: Expected output file {inp_file} not found")
                inp_file = None
        
        # If we couldn't find the output file, look for INP files in the directory
        if not inp_file:
            # Look for INP files in the directory
            print(f"Looking for INP files in {script_dir}")
            print(os.listdir(script_dir))
            inp_files = [f for f in os.listdir(script_dir) if f.endswith('.inp')]
            if inp_files:
                # Use the most recently modified INP file
                inp_files.sort(key=lambda x: os.path.getmtime(os.path.join(script_dir, x)), reverse=True)
                inp_file = os.path.join(script_dir, inp_files[0])
                print(f"Found INP file: {inp_file}")
            else:
                raise FileNotFoundError("No INP files found in the directory")
        
        # Read the mesh from the INP file
        nodes, elements = read_inp_file(inp_file)
        
        # If we have nodes but no elements, try to read the file directly
        if nodes and not elements:
            print("Found nodes but no elements. Trying direct file parsing...")
            
            # Try to read the file directly as text
            with open(inp_file, 'r') as f:
                content = f.read()
            
            # Look for element section
            element_section = None
            patterns = [
                r'\*+\s*E\s*L\s*E\s*M\s*E\s*N\s*T\s*S\s*\*+.*?\n(.*?)(?=\*)',  # ******* E L E M E N T S *******
                r'\*ELEMENT.*?\n(.*?)(?=\*)',  # *ELEMENT
                r'\*Element.*?\n(.*?)(?=\*)'   # *Element
            ]
            
            for pattern in patterns:
                matches = re.findall(pattern, content, re.DOTALL)
                if matches:
                    element_section = matches[0]
                    print(f"Found element section with pattern: {pattern}")
                    break
            
            if element_section:
                # Parse elements from the section
                elements = {}
                element_lines = element_section.strip().split('\n')
                
                for line in element_lines:
                    line = line.strip()
                    if not line or line.startswith('*'):
                        continue
                    
                    parts = line.split(',')
                    if len(parts) >= 5:  # Need at least ID and 4 nodes for a quad
                        try:
                            elem_id = int(parts[0])
                            # Get node IDs, filtering out any non-numeric parts
                            node_ids = []
                            for part in parts[1:]:
                                part = part.strip()
                                if part and part.replace('-', '').isdigit():
                                    node_ids.append(int(part))
                            
                            if len(node_ids) >= 3:  # Need at least 3 nodes for a valid element
                                elements[elem_id] = node_ids
                        except (ValueError, IndexError) as e:
                            print(f"Warning: Could not parse element line: {line}")
                
                print(f"Parsed {len(elements)} elements directly from file")
        
        # Check if we have valid nodes and elements
        if not nodes or not elements:
            print(f"Error: Could not extract valid 2D mesh from {script_path}")
            return None, None, repos
        
        # Convert elements to quads if needed
        quad_elements = {}
        element_type = detect_element_type(inp_file)
        print(f"Detected element type: {element_type}")
        
        if element_type and element_type.startswith("M3D"):
            print(f"Converting {element_type} elements to quads...")
            
            for elem_id, node_ids in elements.items():
                # For M3D9 elements, use the first 4 corner nodes
                if element_type == "M3D9" and len(node_ids) >= 9:
                    quad_elements[elem_id] = node_ids[:4]
                # For M3D8 elements, use the first 4 corner nodes
                elif element_type == "M3D8" and len(node_ids) >= 8:
                    quad_elements[elem_id] = node_ids[:4]
                # For M3D6 elements, convert to quad by duplicating a node
                elif element_type == "M3D6" and len(node_ids) >= 6:
                    quad_elements[elem_id] = node_ids[:3] + [node_ids[0]]  # Triangle to quad by duplicating first node
                # For M3D4 elements, use as is
                elif element_type == "M3D4" and len(node_ids) >= 4:
                    quad_elements[elem_id] = node_ids[:4]
                # For M3D3 elements, convert to quad by duplicating a node
                elif element_type == "M3D3" and len(node_ids) >= 3:
                    quad_elements[elem_id] = node_ids[:3] + [node_ids[0]]  # Triangle to quad by duplicating first node
                # For any other element with at least 4 nodes, use the first 4
                elif len(node_ids) >= 4:
                    quad_elements[elem_id] = node_ids[:4]
                # For any other element with 3 nodes, convert to quad by duplicating a node
                elif len(node_ids) == 3:
                    quad_elements[elem_id] = node_ids + [node_ids[0]]  # Triangle to quad by duplicating first node
        else:
            # If no specific element type detected, just use the elements as is
            for elem_id, node_ids in elements.items():
                if len(node_ids) >= 4:
                    quad_elements[elem_id] = node_ids[:4]
                elif len(node_ids) == 3:
                    quad_elements[elem_id] = node_ids + [node_ids[0]]  # Triangle to quad by duplicating first node
        
        print(f"Converted {len(quad_elements)} elements to quads")
        
        if not quad_elements:
            print(f"Error: Could not convert elements to quads")
            return None, None, repos
            
        return nodes, quad_elements, repos
        
    except Exception as e:
        print(f"Error in fallback extraction: {str(e)}")
        import traceback
        traceback.print_exc()
        return None, None, [0.0, 0.0, 0.0]

def read_inp_file(inp_file):
    """
    Read nodes and elements from an INP file
    
    Handles both 2D and 3D meshes with various formatting variations.
    
    Returns:
    --------
    tuple
        (nodes, elements) where:
        - nodes is a dictionary mapping node ID to (x, y, z) coordinates
        - elements is a dictionary mapping element ID to a list of node IDs
    """
    try:
        print(f"Reading mesh from {inp_file}")
        
        nodes = {}
        elements = {}
        
        with open(inp_file, 'r') as f:
            lines = f.readlines()
        
        # Parse the INP file
        mode = None
        element_lines = []  # Buffer for multi-line element definitions
        current_element_id = None
        element_type = None
        
        i = 0
        while i < len(lines):
            line = lines[i].strip()
            i += 1
            
            # Skip empty lines and comments
            if not line or line.startswith('**'):
                continue
            
            # Check for special element section headers
            if "E L E M E N T S" in line:
                print(f"  Found elements section marker: {line}")
                mode = 'elements'
                # Look ahead for element type
                while i < len(lines) and not re.search(r'type=(\w+)', lines[i], re.IGNORECASE):
                    i += 1
                if i < len(lines):
                    type_match = re.search(r'type=(\w+)', lines[i], re.IGNORECASE)
                    if type_match:
                        element_type = type_match.group(1)
                        print(f"  Found element type: {element_type}")
                continue
            
            # Check for section headers (case insensitive)
            if line.startswith('*'):
                # Process any pending element
                if element_lines and current_element_id is not None:
                    process_element_lines(current_element_id, element_lines, elements)
                    element_lines = []
                    current_element_id = None
                
                # Check for node section (case insensitive)
                if re.search(r'^\*NODE', line.upper()) and 'NSET' not in line.upper():
                    mode = 'nodes'
                    continue
                # Check for element section (case insensitive)
                elif re.search(r'^\*ELEMENT', line.upper()) and 'ELSET' not in line.upper():
                    mode = 'elements'
                    # Extract element type if present
                    type_match = re.search(r'type=(\w+)', line, re.IGNORECASE)
                    if type_match:
                        element_type = type_match.group(1)
                        print(f"  Found element type: {element_type}")
                    continue
                else:
                    # Any other section header switches mode off
                    mode = None
                    continue
            
            # Parse nodes
            if mode == 'nodes':
                parts = line.split(',')
                if len(parts) >= 2:  # Need at least ID and one coordinate
                    try:
                        node_id = int(parts[0])
                        # Handle 2D and 3D coordinates
                        x = float(parts[1]) if len(parts) > 1 and parts[1].strip() else 0.0
                        y = float(parts[2]) if len(parts) > 2 and parts[2].strip() else 0.0
                        z = float(parts[3]) if len(parts) > 3 and parts[3].strip() else 0.0
                        nodes[node_id] = (x, y, z)
                    except (ValueError, IndexError) as e:
                        print(f"  Warning: Could not parse node line: {line}")
            
            # Parse elements
            elif mode == 'elements':
                # Skip lines that don't have commas (might be headers or comments)
                if ',' not in line:
                    continue
                
                # If this is the first line of an element
                if not line.startswith(' '):
                    # Process any pending element
                    if element_lines and current_element_id is not None:
                        process_element_lines(current_element_id, element_lines, elements)
                        element_lines = []
                    
                    # Start a new element
                    parts = line.split(',')
                    try:
                        current_element_id = int(parts[0])
                        element_lines.append(line)
                    except (ValueError, IndexError) as e:
                        print(f"  Warning: Could not parse element ID from line: {line}")
                else:
                    # Continuation line for the current element
                    element_lines.append(line)
        
        # Process any remaining element
        if element_lines and current_element_id is not None:
            process_element_lines(current_element_id, element_lines, elements)
        
        print(f"Read {len(nodes)} nodes and {len(elements)} elements from {inp_file}")
        if element_type:
            print(f"Element type: {element_type}")
        
        # If we have nodes but no elements, try a more aggressive parsing approach
        if len(nodes) > 0 and len(elements) == 0:
            print("  No elements found with standard parsing. Trying alternative parsing...")
            elements = parse_elements_aggressive(inp_file, nodes)
            print(f"  After aggressive parsing: {len(elements)} elements found")
        
        return nodes, elements
        
    except Exception as e:
        print(f"Error reading INP file {inp_file}: {str(e)}")
        import traceback
        traceback.print_exc()
        return {}, {}

def parse_elements_aggressive(inp_file, nodes):
    """
    More aggressive parsing of elements from an INP file
    
    Parameters:
    -----------
    inp_file : str
        Path to the INP file
    nodes : dict
        Dictionary of node IDs
        
    Returns:
    --------
    dict
        Dictionary mapping element ID to a list of node IDs
    """
    elements = {}
    try:
        with open(inp_file, 'r') as f:
            content = f.read()
        
        # Look for element section using various patterns
        element_section = None
        
        # Try to find element section with various markers
        patterns = [
            r'\*+\s*E\s*L\s*E\s*M\s*E\s*N\s*T\s*S\s*\*+.*?\n(.*?)(?=\*)',  # ******* E L E M E N T S *******
            r'\*ELEMENT.*?\n(.*?)(?=\*)',  # *ELEMENT
            r'\*Element.*?\n(.*?)(?=\*)'   # *Element
        ]
        
        for pattern in patterns:
            matches = re.findall(pattern, content, re.DOTALL)
            if matches:
                element_section = matches[0]
                print(f"  Found element section with pattern: {pattern}")
                break
        
        if not element_section:
            print("  Could not find element section in file")
            return elements
        
        # Extract element type
        type_match = re.search(r'type=(\w+)', content, re.IGNORECASE)
        element_type = type_match.group(1) if type_match else "Unknown"
        print(f"  Element type: {element_type}")
        
        # Parse elements from the section
        element_lines = element_section.strip().split('\n')
        current_element = []
        current_element_id = None
        
        for line in element_lines:
            line = line.strip()
            if not line or line.startswith('*'):
                continue
                
            # If this is a new element (starts with a number)
            if re.match(r'^\d+', line):
                # Process any pending element
                if current_element and current_element_id is not None:
                    node_ids = []
                    for part in current_element:
                        if part and part.replace('-', '').isdigit():
                            node_ids.append(int(part))
                    if node_ids:
                        elements[current_element_id] = node_ids
                
                # Start a new element
                parts = line.split(',')
                try:
                    current_element_id = int(parts[0])
                    current_element = [p.strip() for p in parts[1:] if p.strip()]
                except (ValueError, IndexError):
                    print(f"  Warning: Could not parse element line: {line}")
                    current_element_id = None
                    current_element = []
            else:
                # Continuation line
                parts = line.split(',')
                current_element.extend([p.strip() for p in parts if p.strip()])
        
        # Process any remaining element
        if current_element and current_element_id is not None:
            node_ids = []
            for part in current_element:
                if part and part.replace('-', '').isdigit():
                    node_ids.append(int(part))
            if node_ids:
                elements[current_element_id] = node_ids
        
        # Validate elements - ensure all node IDs exist in the nodes dictionary
        valid_elements = {}
        for elem_id, node_ids in elements.items():
            valid_node_ids = [node_id for node_id in node_ids if node_id in nodes]
            if len(valid_node_ids) >= 3:  # At least 3 nodes for a valid element
                valid_elements[elem_id] = valid_node_ids
            else:
                print(f"  Warning: Element {elem_id} has invalid node IDs: {node_ids}")
        
        return valid_elements
        
    except Exception as e:
        print(f"  Error in aggressive element parsing: {str(e)}")
        import traceback
        traceback.print_exc()
        return {}

def process_element_lines(element_id, lines, elements_dict):
    """
    Process a group of element lines to extract node IDs
    
    Parameters:
    -----------
    element_id : int
        Element ID
    lines : list
        List of lines defining the element
    elements_dict : dict
        Dictionary to store the processed element
    """
    node_ids = []
    
    for line in lines:
        # Remove any trailing commas
        if line.endswith(','):
            line = line[:-1]
        
        # Split by comma
        parts = line.split(',')
        
        # Skip the element ID in the first part of the first line
        if line == lines[0]:
            parts = parts[1:]
        
        # Add node IDs
        for part in parts:
            part = part.strip()
            if part and part.replace('-', '').isdigit():  # Handle negative IDs too
                try:
                    node_ids.append(int(part))
                except ValueError:
                    print(f"  Warning: Could not convert '{part}' to integer")
    
    # Store the element
    elements_dict[element_id] = node_ids

def write_inp_file(filename, nodes_3d, elements_3d, boundary_nodes, part_name, profile_elements=None, interface_elements=None, nominal_diameter=None):
    """
    Write a 3D mesh to an INP file for Abaqus
    
    Parameters:
    -----------
    filename : str
        Output INP filename
    nodes_3d : dict
        Dictionary of node IDs to coordinates
    elements_3d : dict
        Dictionary of element IDs to node lists
    boundary_nodes : dict
        Dictionary of boundary node information
    part_name : str
        Name of the part in Abaqus
    profile_elements : set, optional
        Set of element IDs that form the profile
    interface_elements : set, optional
        Set of element IDs that form the interface
    nominal_diameter : float, optional
        Nominal diameter for interface elements
    """
    print(f"Writing INP file: {filename}")
    
    # Create node sets for different parts of the model
    node_sets = {}
    
    # Add boundary nodes to sets
    if boundary_nodes:
        # Start profile (angle = 0)
        start_nodes = [data['start'] for data in boundary_nodes.values() if 'start' in data]
        if start_nodes:
            node_sets["StartProfile"] = set(start_nodes)
        
        # End profile (angle = π)
        end_nodes = [data['end'] for data in boundary_nodes.values() if 'end' in data]
        if end_nodes:
            node_sets["EndProfile"] = set(end_nodes)
    
    # Create element sets
    element_sets = {}
    if profile_elements:
        element_sets["ProfileElements"] = profile_elements
    if interface_elements:
        element_sets[f"{part_name}_INTERFACE"] = interface_elements
    
    with open(filename, 'w') as f:
        # Write header
        f.write(f"*Heading\n")
        f.write(f" {filename}\n")
        
        # Write part definition
        f.write(f"*Part, name={part_name}\n")
        
        # Write nodes
        f.write("*Node\n")
        for node_id, coords in nodes_3d.items():
            f.write(f"{node_id}, {coords[0]:.6f}, {coords[1]:.6f}, {coords[2]:.6f}\n")
        
        # Write elements (C3D20R - 20-node quadratic brick with reduced integration)
        f.write("*Element, type=C3D20R\n")
        for elem_id, nodes in elements_3d.items():
            # For C3D20R, write nodes in three lines
            f.write(f"{elem_id}, " + ", ".join(map(str, nodes[:8])) + ",\n")
            f.write("    " + ", ".join(map(str, nodes[8:16])) + ",\n")
            f.write("    " + ", ".join(map(str, nodes[16:])) + "\n")
        
        # Write element sets
        f.write(f"*Elset, elset={part_name}\n")
        elements_list = sorted(list(elements_3d.keys()))
        for i in range(0, len(elements_list), 8):
            f.write(", ".join(map(str, elements_list[i:i+8])) + "\n")

        # Write interface element set
        if interface_elements:
            f.write(f"*Elset, elset={part_name}_INTERFACE\n")
            interface_list = sorted(list(interface_elements))
            for i in range(0, len(interface_list), 8):
                f.write(", ".join(map(str, interface_list[i:i+8])) + "\n")

        # Write node sets
        for set_name, nodes in node_sets.items():
            if nodes:  # Only write if there are nodes in the set
                f.write(f"*Nset, nset={set_name}\n")
                nodes_list = sorted(list(nodes))
                for i in range(0, len(nodes_list), 8):
                    chunk = nodes_list[i:i+8]
                    # Add comma at the end of the line if it's not the last line
                    if i + 8 < len(nodes_list):
                        f.write(", ".join(map(str, chunk)) + ",\n")
                    else:  # Last line
                        f.write(", ".join(map(str, chunk)) + "\n")

        # Write element sets
        for set_name, elements in element_sets.items():
            if elements:
                f.write(f"*Elset, elset={set_name}\n")
                elements_list = sorted(list(elements))
                for i in range(0, len(elements_list), 8):
                    f.write(", ".join(map(str, elements_list[i:i+8])) + "\n")
        
    
    print(f"INP file written successfully: {filename}")

def validate_combined_mesh(all_nodes, all_elements, node_id_maps, element_id_maps):
    """
    Validate the combined mesh data before writing to file
    
    Parameters:
    -----------
    all_nodes : dict
        Dictionary of nodes for each file
    all_elements : dict
        Dictionary of elements for each file
    node_id_maps : dict
        Mapping of old node IDs to new node IDs
    element_id_maps : dict
        Mapping of old element IDs to new element IDs
        
    Returns:
    --------
    bool
        True if validation passes, False otherwise
    """
    # Check for duplicate new node IDs
    all_new_node_ids = []
    for file_idx, id_map in node_id_maps.items():
        all_new_node_ids.extend(id_map.values())
    
    unique_node_ids = set(all_new_node_ids)
    if len(unique_node_ids) != len(all_new_node_ids):
        print("  Error: Duplicate new node IDs found")
        return False
    
    # Check for duplicate new element IDs
    all_new_elem_ids = []
    for file_idx, id_map in element_id_maps.items():
        all_new_elem_ids.extend(id_map.values())
    
    unique_elem_ids = set(all_new_elem_ids)
    if len(unique_elem_ids) != len(all_new_elem_ids):
        print("  Error: Duplicate new element IDs found")
        return False
    
    # Check that all node IDs referenced in elements exist in the node maps
    for file_idx, elements in all_elements.items():
        for elem_id, node_ids in elements.items():
            for node_id in node_ids:
                if node_id not in node_id_maps[file_idx]:
                    print(f"  Error: Node ID {node_id} from file {file_idx} not found in node map")
                    return False
    
    print("  Validation passed: All node and element IDs are unique and properly mapped")
    return True

def combine_inp_files(input_files, output_file):
    """
    Combine multiple INP files into a single file with proper node and element ID renumbering
    
    Parameters:
    -----------
    input_files : list
        List of input INP files
    output_file : str
        Output INP file
    """
    print(f"Combining {len(input_files)} INP files into {output_file}")
    
    # Check if we have exactly 2 files and the specialized combiner is available
    if len(input_files) == 2:
        try:
            # Try to import the specialized combiner
            import os
            import sys
            
            # Add the current directory to the path to ensure we can import the module
            current_dir = os.path.dirname(os.path.abspath(__file__))
            if current_dir not in sys.path:
                sys.path.append(current_dir)
                
            try:
                from combine_inp_3D_load import combine_inp_files as specialized_combine
                print("Using specialized combiner for two-file case")
                specialized_combine(input_files[0], input_files[1], output_file)
                print(f"Combined mesh written to {output_file}")
                return
            except ImportError as e:
                print(f"Warning: Could not import specialized combiner: {e}")
                print("Falling back to generic combiner")
                print(f"Looked for module in: {sys.path}")
        except Exception as e:
            print(f"Error using specialized combiner: {e}")
            print("Falling back to generic combiner")
    
    # If we get here, either we don't have exactly 2 files or the specialized combiner failed
    # Continue with the generic implementation
    all_nodes = {}  # {file_index: {node_id: (x, y, z)}}
    all_elements = {}  # {file_index: {elem_id: [node_ids]}}
    all_element_types = {}  # {file_index: element_type}
    all_node_sets = {}  # {file_index: {set_name: set(node_ids)}}
    all_element_sets = {}  # {file_index: {set_name: set(elem_ids)}}
    part_names = []  # List of part names
    
    # Read all input files
    for i, inp_file in enumerate(input_files):
        print(f"\nReading file {i+1}/{len(input_files)}: {inp_file}")
        
        # Extract element type from the file
        element_type = detect_element_type(inp_file)
        if element_type:
            all_element_types[i] = element_type
            print(f"  Detected element type: {element_type}")
        else:
            # Default to C3D20R if not detected
            all_element_types[i] = "C3D20R"
            print(f"  Could not detect element type, defaulting to C3D20R")
        
        # Read nodes and elements from the file
        nodes, elements = read_inp_file(inp_file)
        
        if not nodes or not elements:
            print(f"Warning: No valid mesh data found in {inp_file}. Skipping.")
            continue
        
        # Generate part name from file
        part_name = f"PART_{i+1}"
        part_names.append(part_name)
        
        # Store the data
        all_nodes[i] = nodes
        all_elements[i] = elements
        
        # Extract node sets and element sets (if needed)
        # For now, we'll just create a set for each part
        all_node_sets[i] = {}
        all_element_sets[i] = {part_name: set(elements.keys())}
        
        print(f"  Found {len(nodes)} nodes and {len(elements)} elements")
        
        # Validate that all node IDs referenced in elements exist
        missing_nodes = set()
        for elem_id, node_ids in elements.items():
            for node_id in node_ids:
                if node_id not in nodes:
                    missing_nodes.add(node_id)
        
        if missing_nodes:
            print(f"  Warning: Found {len(missing_nodes)} node IDs referenced in elements but not defined in nodes section")
            print(f"  First few missing node IDs: {list(missing_nodes)[:5]}")
    
    # Calculate new IDs
    node_id_maps = {}  # {file_index: {old_id: new_id}}
    element_id_maps = {}  # {file_index: {old_id: new_id}}
    
    next_node_id = 1
    next_element_id = 1
    
    # First pass: create ID mappings for all files
    for i in range(len(input_files)):
        if i not in all_nodes:
            continue
        
        # Create node ID mapping
        node_id_maps[i] = {}
        for old_id in all_nodes[i].keys():
            node_id_maps[i][old_id] = next_node_id
            next_node_id += 1
        
        # Create element ID mapping
        element_id_maps[i] = {}
        for old_id in all_elements[i].keys():
            element_id_maps[i][old_id] = next_element_id
            next_element_id += 1
        
        print(f"  Part {i+1}: Node IDs {min(node_id_maps[i].values())} to {max(node_id_maps[i].values())}")
        print(f"  Part {i+1}: Element IDs {min(element_id_maps[i].values())} to {max(element_id_maps[i].values())}")
    
    # Validate the combined mesh
    print("\nValidating combined mesh...")
    if not validate_combined_mesh(all_nodes, all_elements, node_id_maps, element_id_maps):
        print("Validation failed. Attempting to fix issues...")
        
        # Fix missing node IDs in node maps
        for file_idx, elements in all_elements.items():
            for elem_id, node_ids in elements.items():
                for node_id in node_ids:
                    if node_id not in node_id_maps[file_idx]:
                        print(f"  Adding missing node ID {node_id} from file {file_idx} to node map")
                        node_id_maps[file_idx][node_id] = next_node_id
                        next_node_id += 1
                        # Add a placeholder node coordinate
                        all_nodes[file_idx][node_id] = (0.0, 0.0, 0.0)
        
        # Validate again after fixes
        print("Validating again after fixes...")
        if not validate_combined_mesh(all_nodes, all_elements, node_id_maps, element_id_maps):
            print("Validation still failed. Proceeding with caution...")
    
    # Create a combined nodes dictionary with new IDs
    combined_nodes = {}
    for file_idx, nodes in all_nodes.items():
        for old_id, coords in nodes.items():
            new_id = node_id_maps[file_idx][old_id]
            combined_nodes[new_id] = coords
    
    # Create a combined elements dictionary with new IDs
    combined_elements = {}
    for file_idx, elements in all_elements.items():
        for old_elem_id, old_node_ids in elements.items():
            new_elem_id = element_id_maps[file_idx][old_elem_id]
            # Map old node IDs to new node IDs
            new_node_ids = [node_id_maps[file_idx][node_id] for node_id in old_node_ids if node_id in node_id_maps[file_idx]]
            combined_elements[new_elem_id] = (file_idx, new_node_ids)
    
    # Write combined file
    print(f"\nWriting combined mesh to {output_file}")
    with open(output_file, 'w') as f:
        # Write header
        f.write("*Heading\n")
        f.write(f" Combined mesh from {len(input_files)} files\n")
        f.write("** Generated by gmsh_to_swept_mesh.py\n\n")
        
        # Write all nodes with new IDs
        f.write("*Node\n")
        for new_id, coords in sorted(combined_nodes.items()):
            f.write(f"{new_id}, {coords[0]:.6f}, {coords[1]:.6f}, {coords[2]:.6f}\n")
        
        # Group elements by type and write them
        elements_by_type = {}  # {element_type: [new_elem_id]}
        
        for new_elem_id, (file_idx, new_node_ids) in combined_elements.items():
            element_type = all_element_types[file_idx]
            if element_type not in elements_by_type:
                elements_by_type[element_type] = []
            elements_by_type[element_type].append((new_elem_id, new_node_ids))
        
        # Write elements grouped by type
        for element_type, element_list in elements_by_type.items():
            f.write(f"\n*Element, type={element_type}\n")
            
            # Sort elements by ID to ensure consistent output
            for new_elem_id, new_node_ids in sorted(element_list):
                # Format based on element type and number of nodes
                if element_type.startswith("C3D20"):  # 20-node brick elements
                    # First line: element ID and first 8 nodes
                    f.write(f"{new_elem_id}")
                    for j in range(min(8, len(new_node_ids))):
                        f.write(f", {new_node_ids[j]}")
                    f.write(",\n")
                    
                    # Second line: next 8 nodes (or fewer if not enough nodes)
                    f.write("    ")
                    for j in range(8, min(16, len(new_node_ids))):
                        if j < min(16, len(new_node_ids)) - 1:
                            f.write(f"{new_node_ids[j]}, ")
                        else:
                            f.write(f"{new_node_ids[j]}")
                    if len(new_node_ids) > 16:
                        f.write(",\n")
                    else:
                        f.write("\n")
                    
                    # Third line: remaining nodes (or fewer if not enough nodes)
                    if len(new_node_ids) > 16:
                        f.write("    ")
                        for j in range(16, len(new_node_ids)):
                            if j < len(new_node_ids) - 1:
                                f.write(f"{new_node_ids[j]}, ")
                            else:
                                f.write(f"{new_node_ids[j]}")
                        f.write("\n")
                
                elif element_type.startswith("M3D"):  # 2D elements
                    # Write all nodes on a single line
                    f.write(f"{new_elem_id}")
                    for node_id in new_node_ids:
                        f.write(f", {node_id}")
                    f.write("\n")
                
                else:  # Generic case
                    # Write all nodes on a single line for other element types
                    f.write(f"{new_elem_id}")
                    for node_id in new_node_ids:
                        f.write(f", {node_id}")
                    f.write("\n")
        
        # Write element sets for each part
        for i, part_name in enumerate(part_names):
            if i not in all_elements:
                continue
                
            f.write(f"\n*Elset, elset={part_name}\n")
            
            # Get new element IDs for this part
            new_elem_ids = [element_id_maps[i][old_id] for old_id in all_elements[i].keys()]
            new_elem_ids.sort()
            
            # Write in groups of 8
            for j in range(0, len(new_elem_ids), 8):
                chunk = new_elem_ids[j:j+8]
                f.write(", ".join(map(str, chunk)) + "\n")
        
        # Write section definitions for each part
        for part_name in part_names:
            f.write(f"\n*Solid Section, elset={part_name}, material=STEEL\n")
            f.write("1.\n")
        
        # Write material definition
        f.write("\n*Material, name=STEEL\n")
        f.write("*Elastic\n")
        f.write("210000.0, 0.3\n")
        
        # Write assembly
        f.write("\n*Assembly, name=Assembly\n")
        for i, part_name in enumerate(part_names):
            f.write(f"*Instance, name={part_name}-1, part={part_name}\n")
            f.write("*End Instance\n")
        f.write("*End Assembly\n")
    
    print(f"Combined mesh successfully written to {output_file}")
    print(f"Total nodes: {len(combined_nodes)}, Total elements: {len(combined_elements)}")
    
    # Print summary of node and element ID ranges for each part
    for i, part_name in enumerate(part_names):
        if i in node_id_maps and i in element_id_maps:
            node_ids = node_id_maps[i].values()
            elem_ids = element_id_maps[i].values()
            print(f"  {part_name}: Node IDs {min(node_ids)} to {max(node_ids)}, Element IDs {min(elem_ids)} to {max(elem_ids)}")

def detect_element_type(inp_file):
    """
    Detect the element type from an INP file
    
    Returns:
    --------
    str or None
        Element type if found, None otherwise
    """
    try:
        with open(inp_file, 'r') as f:
            for line in f:
                line = line.strip()
                if '*ELEMENT' in line.upper() or '*element' in line:
                    type_match = re.search(r'type=(\w+)', line, re.IGNORECASE)
                    if type_match:
                        return type_match.group(1)
        return None
    except Exception as e:
        print(f"Error detecting element type: {str(e)}")
        return None

def main():
    """Main function to process input files and generate swept meshes"""
    # Parse command line arguments
    args = parse_arguments()
    
    # Check if we're just combining existing INP files
    if args.combine_inp:
        print(f"Directly combining {len(args.combine_inp)} INP files into {args.combine_output}")
        combine_inp_files(args.combine_inp, args.combine_output)
        print("Combining completed successfully!")
        return
    
    # Load configuration if provided
    config = {}
    if args.config:
        try:
            config = load_config(args.config)
        except Exception as e:
            print(f"Error loading configuration: {str(e)}")
            config = {}
    
    # Combine command line arguments and configuration
    params = {
        'num_angles': args.angles,
        'num_total_angle': args.total_angle,
        'use_cpu': args.use_cpu,
        'combine': args.combine,
        'combined_output': args.combined_output
    }
    
    # Override with config values if present
    if config:
        for key, value in config.items():
            params[key] = value
    
    # Ensure output files list matches input files list
    output_files = args.output if args.output else []
    if len(output_files) < len(args.input):
        # Generate output filenames for missing entries
        for i in range(len(output_files), len(args.input)):
            input_base = os.path.splitext(args.input[i])[0]
            output_files.append(f"{input_base}_swept.inp")
    
    all_output_files = []
    
    for i, (input_file, output_file) in enumerate(zip(args.input, output_files)):
        print(f"\n=== Processing {input_file} ===")
        
        # Extract 2D mesh from GMSH script
        nodes_2d, quad_elements, repos = extract_2d_mesh_from_gmsh_script(input_file)
        
        if nodes_2d is None or not quad_elements:
            print(f"Error: Could not extract valid 2D mesh from {input_file}")
            continue

        placement = list(repos[:3]) if repos is not None and len(repos) >= 3 else [0.0, 0.0, 0.0]
        if args.stanchion_top:
            placement = compute_stanchion_translation(nodes_2d, args.stanchion_top)
            print(
                "Auto-positioning stanchion profile to "
                f"X={placement[0]:.6f}, Y={placement[1]:.6f}, Z={placement[2]:.6f}"
            )
        elif any(abs(value) > 1e-12 for value in placement):
            print(
                "Applying profile translation from script header: "
                f"dx={placement[0]:.6f}, dy={placement[1]:.6f}, dz={placement[2]:.6f}"
            )

        nodes_2d = translate_profile_nodes(nodes_2d, placement)
        
        # Determine starting IDs
        start_node_id = 1
        start_element_id = 1
        
        # Perform the sweep operation
        print(f"\nSweeping 2D mesh with {params['num_angles']} angles over {params['num_total_angle']} degrees...")
        start_time = time.time()
        
        print("Using CPU implementation")
        nodes_3d, elements_3d, boundary_nodes, profile_elements, interface_elements = sweep_2d_mesh_cpu(
            nodes_2d,
            quad_elements,
            repos=placement,
            start_node_id=start_node_id,
            start_element_id=start_element_id,
            num_angles=params['num_angles'],
            total_angle=params['num_total_angle'],
        )
        
        sweep_time = time.time() - start_time
        print(f"Sweep completed in {sweep_time:.2f} seconds")
        
        # Generate part name from input file
        part_name = f"PART_{i+1}"
        
        # Extract nominal diameter if available
        nominal_diameter = None
        if 'nominal_diameter' in params:
            nominal_diameter = params['nominal_diameter']
        
        # Write INP file
        write_inp_file(
            output_file, 
            nodes_3d, 
            elements_3d, 
            boundary_nodes, 
            part_name, 
            profile_elements, 
            interface_elements, 
            nominal_diameter
        )
        all_output_files.append(output_file)
    
    # Combine output files if requested
    if args.combine and len(all_output_files) > 1:
        combine_inp_files(all_output_files, args.combined_output)
    
    print("\nMesh sweeping completed successfully!")

if __name__ == "__main__":
    main() 

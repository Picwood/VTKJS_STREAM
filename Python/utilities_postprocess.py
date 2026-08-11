#!/usr/bin/env python
# file: utilities_postprocess.py
# vim:fileencoding=utf-8:fdm=marker:ft=python
#
"""
Utilities for FEA post-processing operations.
This module contains common functions used across various post-processing scripts.

Key Feature: Element Completeness Guaranteed
- When extracting elements based on criteria (e.g., bounding box), ALL nodes of selected 
  elements are included in the output, not just the nodes meeting the criteria
- This ensures extracted FRD files contain only complete, valid elements
- Uses a two-pass approach: first identify qualifying elements, then collect all their nodes
"""

import os
import sys
import logging
import re
import traceback
from typing import Dict, List, Tuple, Set, Optional, Union, Callable

__version__ = "2024.12.19"


def _collect_ranges(lines: List[str]) -> Dict[str, List[Tuple[int, int]]]:
    """Collect start/end indices for each dataset block (NODES, DISP, etc.), preserving repeats."""
    ranges: Dict[str, List[Tuple[int, int]]] = {}
    current_name: Optional[str] = None
    current_start: Optional[int] = None

    for idx, ln in enumerate(lines):
        line_str = ln.lstrip()
        if line_str.startswith("2C"):
            current_name, current_start = "NODES", idx + 1
        elif line_str.startswith("3C"):
            current_name, current_start = "ELEMENTS", idx + 1
        elif line_str.startswith("-4"):
            parts = line_str.split()
            if len(parts) >= 2:
                current_name, current_start = parts[1], idx + 1
        elif line_str.startswith("-3"):
            if current_name is not None and current_start is not None:
                ranges.setdefault(current_name, []).append((current_start, idx))
            current_name, current_start = None, None

    # If file ended without closing marker, capture trailing block
    if current_name is not None and current_start is not None:
        ranges.setdefault(current_name, []).append((current_start, len(lines)))
    return ranges


def _process_float_data(lines: List[str], first: int, last: int) -> Dict[int, Tuple[float, ...]]:
    """Convert node-related float data to a dictionary indexed by node number."""
    data: Dict[int, Tuple[float, ...]] = {}
    while first < last and not lines[first].lstrip().startswith("-1"):
        first += 1
    if first >= last:
        return data
    count = max(1, int(len(lines[first]) / 12))
    indices = [(c * 12, (c + 1) * 12) for c in range(1, count)]
    for ln in lines[first:last]:
        line_str = ln.lstrip()
        if not line_str.startswith("-1"):
            continue
        try:
            num = int(line_str[2:12])
            numbers = [float(line_str[a:b]) for a, b in indices if line_str[a:b].strip()]
            data[num] = tuple(numbers)
        except Exception as e:  # pylint: disable=broad-except
            logging.debug("Failed to parse FRD data line '%s': %s", ln, e)
    return data


def read_frd(path: str, datasets: Optional[Set[str]] = None, encoding: Optional[str] = None) -> Dict[str, Dict[int, Tuple[float, ...]]]:
    """
    Read selected datasets from an FRD file into memory.

    Args:
        path: FRD file path.
        datasets: Optional set of dataset names to extract (e.g., {'NODES', 'DISP'}).
                  If None, all datasets found are returned.
        encoding: File encoding (defaults to system default).

    Returns:
        Dictionary mapping dataset name -> {node_id: tuple(values)}.
    """
    with open(path, "r", encoding=encoding) as f:
        lines = [ln.strip("\n") for ln in f]
    all_ranges = _collect_ranges(lines)
    if datasets:
        targets = {ds.upper() for ds in datasets}
    else:
        targets = set(all_ranges.keys())
    contents: Dict[str, Dict[int, Tuple[float, ...]]] = {}
    for name, blocks in all_ranges.items():
        if name.upper() not in targets:
            continue
        if not blocks:
            continue
        # Pick the last occurrence (latest increment) for each dataset
        start, end = blocks[-1]
        contents[name] = _process_float_data(lines, start, end)
    return contents


def extract_node_coordinates_from_frd(frd_path: str, encoding: Optional[str] = None) -> Optional[Dict[int, Tuple[float, float, float]]]:
    """
    Extract node coordinates directly from an FRD file.
    
    Args:
        frd_path: Path to the FRD file
        encoding: File encoding (default: system default)
        
    Returns:
        Dictionary mapping node numbers to their coordinates (x, y, z)
        Returns None if extraction fails
    """
    try:
        node_coords = {}
        
        with open(frd_path, 'r', encoding=encoding) as file:
            lines = file.readlines()
            
            reading_nodes = False
            
            for i, line in enumerate(lines):
                # Check for node block start
                if line.strip().startswith('2C'):
                    reading_nodes = True
                    logging.debug(f"Found node block at line {i+1}")
                    continue
                
                # Read node coordinates
                if reading_nodes and line.strip().startswith('-1'):
                    try:
                        # Extract node number and coordinates
                        # Format is typically: -1 [node_num] [x] [y] [z]
                        parts = line.strip().split()
                        if len(parts) >= 5:
                            node_num = int(parts[1])
                            x = float(parts[2])
                            y = float(parts[3])
                            z = float(parts[4])
                            node_coords[node_num] = (x, y, z)
                        else:
                            # Try regex approach for fixed-width format
                            node_match = re.search(r'-1\s+(\d+)', line)
                            if node_match:
                                node_num = int(node_match.group(1))
                                coords_part = line[line.find(node_match.group(0)) + len(node_match.group(0)):].strip()
                                # Use regex to find all floating point numbers
                                coords = re.findall(r'[+-]?\d+\.\d+E[+-]\d+', coords_part)
                                if len(coords) >= 3:
                                    x = float(coords[0])
                                    y = float(coords[1])
                                    z = float(coords[2])
                                    node_coords[node_num] = (x, y, z)
                    except Exception as e:
                        logging.debug(f"Error parsing node coordinate line: {line.strip()}, error: {str(e)}")
                    continue
                
                # End of node block
                if reading_nodes and line.strip().startswith('-3'):
                    reading_nodes = False
                    logging.debug(f"End of node block at line {i+1}")
                    break
        
        if node_coords:
            logging.info(f"Successfully extracted coordinates for {len(node_coords)} nodes directly from file")
            return node_coords
        else:
            logging.warning("No node coordinates found in direct extraction")
            return None
    
    except Exception as e:
        logging.error(f"Error extracting node coordinates directly: {str(e)}")
        logging.debug(traceback.format_exc())
        return None


def is_point_in_box(point: Tuple[float, float, float], 
                   x_min: Optional[float] = None, x_max: Optional[float] = None,
                   y_min: Optional[float] = None, y_max: Optional[float] = None,
                   z_min: Optional[float] = None, z_max: Optional[float] = None) -> bool:
    """
    Check if a point is inside the specified bounding box.
    If any min/max value is None, that dimension is unbounded in that direction.
    
    Args:
        point: Tuple of (x, y, z) coordinates
        x_min, x_max, y_min, y_max, z_min, z_max: Bounding box limits
        
    Returns:
        bool: True if point is inside box, False otherwise
    """
    x, y, z = point
    
    # Check each dimension
    if x_min is not None and x < x_min:
        return False
    if x_max is not None and x > x_max:
        return False
    if y_min is not None and y < y_min:
        return False
    if y_max is not None and y > y_max:
        return False
    if z_min is not None and z < z_min:
        return False
    if z_max is not None and z > z_max:
        return False
    
    return True


def write_frd_extracted_file(frd_path: str, 
                           output_path: str,
                           selected_nodes: Set[int],
                           element_filter_func: Optional[Callable[[List[int]], bool]] = None,
                           encoding: Optional[str] = None,
                           update_counts: bool = True) -> bool:
    """
    Write an extracted FRD file with selected nodes and filtered elements.
    
    This is a generalized function based on the logic from extract_frd_by_bounding_box.py
    that can be used for various extraction scenarios.
    
    Args:
        frd_path: Path to the input FRD file
        output_path: Path to the output FRD file
        selected_nodes: Set of node IDs to include in the output
        element_filter_func: Optional function that takes a list of element node IDs 
                            and returns True if the element should be included.
                            If None, includes elements with any node in selected_nodes.
        encoding: File encoding (default: system default)
        update_counts: Whether to update node/element counts in headers
        
    Returns:
        True if successful, False otherwise
    """
    try:
        # Variables to track extraction statistics
        extracted_elements = 0
        skipped_elements = 0
        element_processing_stats = {
            'total_elements_seen': 0,
            'included_elements': 0,
            'excluded_elements': 0,
            'parse_errors': 0,
            'multi_line_elements': 0
        }
        
        # Dictionary to track element nodes and headers
        element_nodes = {}
        element_headers = {}
        element_inclusion = {}
        
        # Variables to track step information
        current_step = None
        current_increment = None
        step_info_lines = []
        
        # Default element filter function
        if element_filter_func is None:
            def default_filter(element_node_list: List[int]) -> bool:
                """Default filter: include element if any node is in selected_nodes"""
                return any(node_id in selected_nodes for node_id in element_node_list)
            element_filter_func = default_filter
        
        # Open the output file
        with open(output_path, 'w', encoding=encoding) as out_file:
            # Process the input file line by line
            with open(frd_path, 'r', encoding=encoding) as in_file:
                lines = in_file.readlines()
                
                # Process the first 9 lines (header)
                header_lines = lines[:12]
                for line in header_lines:
                    out_file.write(line)
                
                # Process the rest of the file
                in_nodal_block = False
                in_element_block = False
                in_result_block = False
                in_step_info = False
                current_element_id = None
                current_result_type = None
                
                # Read lines after header
                for i, line in enumerate(lines[12:], 12):
                    line_stripped = line.strip()
                    
                    # Handle step information
                    if line_stripped.startswith('1PSTEP') or (in_step_info and not line_stripped.startswith('-4')):
                        in_step_info = True
                        
                        # Extract step and increment information
                        if line_stripped.startswith('1PSTEP'):
                            parts = line_stripped.split()
                            if len(parts) >= 4:
                                current_increment = int(parts[2])
                                current_step = int(parts[3])
                                logging.debug(f"Found step {current_step}, increment {current_increment}")
                        
                        # Update node count in step CL lines if requested
                        if update_counts and line.startswith('  100CL'):
                            node_count_match = re.search(r'(\d+\s+)(\d+)(\s+)', line[10:])
                            if node_count_match:
                                match_start = 10 + node_count_match.start(2)
                                match_end = 10 + node_count_match.end(2)
                                node_count_width = match_end - match_start
                                node_count_str = str(len(selected_nodes))
                                node_count_padded = node_count_str.rjust(node_count_width)
                                new_line = line[:match_start] + node_count_padded + line[match_end:]
                                out_file.write(new_line)
                                step_info_lines.append(new_line)
                                continue
                        
                        # Store step information lines
                        step_info_lines.append(line)
                        out_file.write(line)
                        continue
                    
                    # Handle block start/end markers
                    if line_stripped.startswith('2C'):
                        # Start of nodal point coordinate block
                        in_nodal_block = True
                        in_element_block = False
                        in_result_block = False
                        in_step_info = False
                        current_element_id = None
                        
                        # Update the node count in the header if requested
                        if update_counts:
                            parts = re.split(r'\s+', line.strip())
                            if len(parts) >= 2:
                                node_count_position = line.find(parts[1])
                                prefix = line[:node_count_position]
                                node_count_str = str(len(selected_nodes))
                                field_width = len(parts[1])
                                node_count_padding = " " * max(0, field_width - len(node_count_str))
                                suffix = line[node_count_position + len(parts[1]):]
                                new_line = f"{prefix}{node_count_str}{node_count_padding}{suffix}"
                                out_file.write(new_line)
                            else:
                                out_file.write(line)
                        else:
                            out_file.write(line)
                        continue
                        
                    elif line_stripped.startswith('3C'):
                        # Start of element definition block
                        in_nodal_block = False
                        in_element_block = True
                        in_result_block = False
                        in_step_info = False
                        current_element_id = None
                        
                        # Save the line for later count update
                        element_header_line = line
                        out_file.write(line)  # We'll update this count later if needed
                        continue
                        
                    elif line_stripped.startswith('-3'):
                        # End of a block
                        if in_nodal_block or in_element_block or in_result_block:
                            in_nodal_block = False
                            in_element_block = False
                            in_result_block = False
                            in_step_info = False
                            current_element_id = None
                            out_file.write(line)
                        continue
                        
                    elif line_stripped.startswith('-4'):
                        # Start of a result block
                        in_nodal_block = False
                        in_element_block = False
                        in_result_block = True
                        in_step_info = False
                        current_element_id = None
                        
                        # Determine the result type
                        if 'STRESS' in line_stripped:
                            current_result_type = 'STRESS'
                        elif 'DISPL' in line_stripped:
                            current_result_type = 'DISPL'
                        else:
                            # Extract the result type from the line
                            parts = line_stripped.split()
                            if len(parts) > 1:
                                current_result_type = parts[1]
                        
                        logging.debug(f"Found result block: {current_result_type}")
                        out_file.write(line)
                        continue
                    
                    # Handle node data
                    if in_nodal_block and line_stripped.startswith('-1'):
                        try:
                            node_match = re.search(r'-1\s+(\d+)', line)
                            if node_match:
                                node_num = int(node_match.group(1))
                                if node_num in selected_nodes:
                                    out_file.write(line)
                        except Exception as e:
                            logging.debug(f"Error parsing node line: {line.strip()}, error: {str(e)}")
                    
                    # Handle element data
                    elif in_element_block and line_stripped.startswith('-1'):
                        try:
                            elem_match = re.search(r'-1\s+(\d+)', line)
                            if elem_match:
                                current_element_id = int(elem_match.group(1))
                                element_processing_stats['total_elements_seen'] += 1
                                
                                # Initialize tracking for this element
                                element_headers[current_element_id] = {'header': line, 'node_lines': []}
                                element_nodes[current_element_id] = []
                                element_inclusion[current_element_id] = False
                        except Exception as e:
                            logging.debug(f"Error extracting element ID: {line.strip()}, error: {str(e)}")
                            element_processing_stats['parse_errors'] += 1
                    
                    elif in_element_block and line_stripped.startswith('-2'):
                        try:
                            if current_element_id is not None:
                                parts = line.strip().split()
                                if len(parts) >= 2:
                                    # Extract node IDs from the line
                                    line_nodes = [int(node_id) for node_id in parts[1:] if node_id.strip()]
                                    
                                    # Add these nodes to the element's collection
                                    element_nodes[current_element_id].extend(line_nodes)
                                    
                                    # Save the node line
                                    element_headers[current_element_id]['node_lines'].append(line)
                                    
                                    # Check if this is the last node line for this element
                                    next_line_idx = i + 10  # Look ahead a few lines
                                    is_last_node_line = True
                                    
                                    # Check if there's another -2 line coming for this element
                                    for j in range(i + 1, min(next_line_idx, len(lines))):
                                        if j < len(lines) and lines[j].strip().startswith('-2'):
                                            is_last_node_line = False
                                            # Track multi-line elements
                                            if len(element_headers[current_element_id]['node_lines']) == 1:
                                                element_processing_stats['multi_line_elements'] += 1
                                                logging.debug(f"Found multi-line element {current_element_id}")
                                            break
                                        if j < len(lines) and lines[j].strip().startswith('-1'):
                                            break
                                    
                                    if is_last_node_line:
                                        # Apply the filter function to determine inclusion
                                        all_element_nodes = element_nodes[current_element_id]
                                        
                                        if element_filter_func(all_element_nodes):
                                            # Write the element header and all node lines
                                            out_file.write(element_headers[current_element_id]['header'])
                                            for node_line in element_headers[current_element_id]['node_lines']:
                                                out_file.write(node_line)
                                            
                                            element_inclusion[current_element_id] = True
                                            extracted_elements += 1
                                            element_processing_stats['included_elements'] += 1
                                        else:
                                            skipped_elements += 1
                                            element_processing_stats['excluded_elements'] += 1
                        except Exception as e:
                            logging.debug(f"Error parsing element nodes line: {line.strip()}, error: {str(e)}")
                            element_processing_stats['parse_errors'] += 1
                    
                    # Handle result data
                    elif in_result_block and line_stripped.startswith('-1'):
                        try:
                            node_match = re.search(r'-1\s+(\d+)', line)
                            if node_match:
                                node_num = int(node_match.group(1))
                                if node_num in selected_nodes:
                                    out_file.write(line)
                        except Exception as e:
                            logging.debug(f"Error parsing result line: {line.strip()}, error: {str(e)}")
                    
                    # Copy result block structure lines
                    elif in_result_block and line_stripped.startswith('-5'):
                        out_file.write(line)
                        continue
                    
                    # Copy other structural lines
                    elif (in_nodal_block or in_element_block or in_result_block):
                        if not line_stripped.startswith('-1') and not line_stripped.startswith('-2'):
                            out_file.write(line)
        
        # Update element count in the output file if requested
        if update_counts:
            with open(output_path, 'r', encoding=encoding) as file:
                content = file.read()
            
            # Update element count in 3C line
            pattern = r'(3C\s+)(\d+)(\s+)'
            replacement = f'\\g<1>{extracted_elements}\\g<3>'
            updated_content = re.sub(pattern, replacement, content)
            
            with open(output_path, 'w', encoding=encoding) as file:
                file.write(updated_content)
        
        # Log statistics
        logging.info(f"FRD extraction complete:")
        logging.info(f"- Selected {len(selected_nodes)} nodes")
        logging.info(f"- Extracted {extracted_elements} elements")
        logging.info(f"- Skipped {skipped_elements} elements")
        logging.info(f"- Total elements processed: {element_processing_stats['total_elements_seen']}")
        logging.info(f"- Multi-line elements detected: {element_processing_stats['multi_line_elements']}")
        logging.info(f"- Parse errors: {element_processing_stats['parse_errors']}")
        logging.info(f"Results saved to {output_path}")
        
        return True
    
    except Exception as e:
        logging.error(f"Error writing extracted FRD file: {str(e)}")
        logging.debug(traceback.format_exc())
        return False


def create_bounding_box_filter(x_min: Optional[float] = None, x_max: Optional[float] = None,
                              y_min: Optional[float] = None, y_max: Optional[float] = None,
                              z_min: Optional[float] = None, z_max: Optional[float] = None,
                              min_nodes: int = 1,
                              node_coords: Dict[int, Tuple[float, float, float]] = None) -> Callable[[List[int]], bool]:
    """
    Create an element filter function for bounding box extraction.
    
    Args:
        x_min, x_max, y_min, y_max, z_min, z_max: Bounding box limits
        min_nodes: Minimum number of nodes that must be inside the bounding box
        node_coords: Dictionary mapping node IDs to coordinates
        
    Returns:
        Filter function that takes element node list and returns True if element should be included
    """
    def bounding_box_filter(element_node_list: List[int]) -> bool:
        if node_coords is None:
            return False
        
        nodes_in_box = 0
        for node_id in element_node_list:
            if node_id in node_coords:
                coords = node_coords[node_id]
                if is_point_in_box(coords, x_min, x_max, y_min, y_max, z_min, z_max):
                    nodes_in_box += 1
        
        return nodes_in_box >= min_nodes
    
    return bounding_box_filter


def extract_frd_by_bounding_box(frd_path: str, 
                               output_path: Optional[str] = None,
                               encoding: Optional[str] = None,
                               x_min: Optional[float] = None, x_max: Optional[float] = None,
                               y_min: Optional[float] = None, y_max: Optional[float] = None,
                               z_min: Optional[float] = None, z_max: Optional[float] = None,
                               min_nodes: int = 1) -> bool:
    """
    Extract elements from an FRD file based on a bounding box.
    
    This function ensures element completeness by including ALL nodes of selected elements,
    not just the nodes within the bounding box.
    
    Args:
        frd_path: Path to the FRD file
        output_path: Path to the output FRD file (auto-generated if None)
        encoding: File encoding (default: system default)
        x_min, x_max: X-coordinate range (optional)
        y_min, y_max: Y-coordinate range (optional)
        z_min, z_max: Z-coordinate range (optional)
        min_nodes: Minimum number of nodes that must be inside the bounding box
        
    Returns:
        True if successful, False otherwise
    """
    try:
        # Generate output path if not provided
        if output_path is None:
            base_name = os.path.splitext(frd_path)[0]
            output_path = f"{base_name}_bbox_extracted.frd"
        
        # Extract node coordinates
        node_coords = extract_node_coordinates_from_frd(frd_path, encoding)
        if not node_coords:
            logging.error("Failed to extract node coordinates")
            return False
        
        # Find nodes within the bounding box (for element selection criteria)
        nodes_in_bbox = set()
        for node_num, coords in node_coords.items():
            if is_point_in_box(coords, x_min, x_max, y_min, y_max, z_min, z_max):
                nodes_in_bbox.add(node_num)
        
        logging.info(f"Found {len(nodes_in_bbox)} nodes within the bounding box")
        
        # FIRST PASS: Identify elements that meet the criteria
        # We need to scan the file to find which elements have enough nodes in the bounding box
        selected_elements = set()
        element_to_nodes = {}  # Store mapping of element ID to all its node IDs
        
        with open(frd_path, 'r', encoding=encoding) as file:
            lines = file.readlines()
            
            in_element_block = False
            current_element_id = None
            current_element_nodes = []
            
            for i, line in enumerate(lines):
                line_stripped = line.strip()
                
                # Check for element block start
                if line_stripped.startswith('3C'):
                    in_element_block = True
                    continue
                
                # Check for block end
                elif line_stripped.startswith('-3') and in_element_block:
                    in_element_block = False
                    continue
                
                # Process element data
                elif in_element_block and line_stripped.startswith('-1'):
                    # Save previous element if exists
                    if current_element_id is not None and current_element_nodes:
                        element_to_nodes[current_element_id] = current_element_nodes.copy()
                        
                        # Check if this element meets the criteria
                        nodes_in_bbox_count = sum(1 for node_id in current_element_nodes 
                                                if node_id in nodes_in_bbox)
                        if nodes_in_bbox_count >= min_nodes:
                            selected_elements.add(current_element_id)
                    
                    # Start new element
                    elem_match = re.search(r'-1\s+(\d+)', line)
                    if elem_match:
                        current_element_id = int(elem_match.group(1))
                        current_element_nodes = []
                
                elif in_element_block and line_stripped.startswith('-2') and current_element_id is not None:
                    # Parse node IDs from this line
                    try:
                        parts = line.strip().split()
                        if len(parts) >= 2:
                            line_nodes = [int(node_id) for node_id in parts[1:] if node_id.strip()]
                            current_element_nodes.extend(line_nodes)
                    except Exception as e:
                        logging.debug(f"Error parsing element nodes line: {line.strip()}, error: {str(e)}")
            
            # Don't forget the last element
            if current_element_id is not None and current_element_nodes:
                element_to_nodes[current_element_id] = current_element_nodes.copy()
                nodes_in_bbox_count = sum(1 for node_id in current_element_nodes 
                                        if node_id in nodes_in_bbox)
                if nodes_in_bbox_count >= min_nodes:
                    selected_elements.add(current_element_id)
        
        logging.info(f"Selected {len(selected_elements)} elements based on bounding box criteria")
        
        # SECOND PASS: Collect ALL nodes of selected elements
        complete_selected_nodes = set()
        for element_id in selected_elements:
            if element_id in element_to_nodes:
                complete_selected_nodes.update(element_to_nodes[element_id])
        
        logging.info(f"Collected {len(complete_selected_nodes)} total nodes (including all nodes of selected elements)")
        logging.info(f"Added {len(complete_selected_nodes) - len(nodes_in_bbox)} additional nodes to maintain element completeness")
        
        # Create element filter that only includes pre-selected elements
        def complete_element_filter(element_node_list: List[int]) -> bool:
            # We need to identify which element this is
            # This is a bit tricky since we only have the node list
            # We'll match against our stored element_to_nodes mapping
            for element_id in selected_elements:
                if element_id in element_to_nodes:
                    stored_nodes = set(element_to_nodes[element_id])
                    if set(element_node_list) == stored_nodes:
                        return True
            return False
        
        # Actually, the above approach is complex because we need the element ID
        # Let's modify the approach: create a simpler filter and use the complete node set
        def simple_element_filter(element_node_list: List[int]) -> bool:
            # Check if this element has enough nodes in the original bounding box
            nodes_in_bbox_count = sum(1 for node_id in element_node_list 
                                    if node_id in nodes_in_bbox)
            return nodes_in_bbox_count >= min_nodes
        
        # Write the extracted FRD file with complete node set
        return write_frd_extracted_file(
            frd_path, output_path, complete_selected_nodes, 
            simple_element_filter, encoding, update_counts=True
        )
    
    except Exception as e:
        logging.error(f"Error in bounding box extraction: {str(e)}")
        logging.debug(traceback.format_exc())
        return False


# Example usage and test function
def _test_utilities():
    """Test function to demonstrate usage of the utilities."""
    import tempfile
    
    # This is a placeholder test - in real usage, you would have actual FRD files
    logging.basicConfig(level=logging.INFO)
    
    print("FRD Utilities loaded successfully!")
    print("Key improvement: Element completeness is now guaranteed.")
    print("- When an element meets the bounding box criteria, ALL of its nodes are included")
    print("- This prevents incomplete/invalid elements in the extracted FRD file")
    print("- The extraction now uses a two-pass approach for correctness")
    
    # Example: Extract by bounding box (commented out for safety)
    extract_frd_by_bounding_box(
        "C:/Users/fvazeille/Desktop/test_whole_setup/Simulations/NRX_FIRST_UPPER_BENDING.frd", 
        "C:/Users/fvazeille/Desktop/test_whole_setup/Simulations/NRX_FIRST_UPPER_BENDING_bbox_extracted2.frd",
        x_min=0.0, x_max=-60.0,
        y_min=-35.0, y_max=35.0,
        z_min=0.0, z_max=60.0,
        min_nodes=2
    )


if __name__ == "__main__":
    _test_utilities()

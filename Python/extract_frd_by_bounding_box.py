#!/usr/bin/env python
# file: extract_frd_by_bounding_box.py
# vim:fileencoding=utf-8:fdm=marker:ft=python
#
"""
Extract data from an FRD file for elements within a specified bounding box.
This script extracts elements that have a minimum number of nodes within the specified
coordinate ranges and saves the results to a new FRD file.
"""

import os
import sys
import logging
import argparse
import re
import traceback

# Import functions from common.py
from common import FRD, write_converted_file, renumbered_nodes

__version__ = "2024.07.02"


def _setup():
    """Set up command line arguments and logging."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "-v", "--version", action="version", version=__version__
    )
    parser.add_argument(
        "--log",
        default="info",
        choices=["debug", "info", "warning", "error"],
        help="logging level (defaults to 'info')",
    )
    parser.add_argument(
        "file", metavar="file", help="FRD file to process"
    )
    parser.add_argument(
        "-o", "--output",
        help="Output FRD file",
        default=None
    )
    parser.add_argument(
        "-e", "--encoding",
        help="File encoding (default: system default)",
        default=None
    )
    # Bounding box parameters
    parser.add_argument(
        "--x-min",
        type=float,
        help="Minimum X coordinate",
        default=None
    )
    parser.add_argument(
        "--x-max",
        type=float,
        help="Maximum X coordinate",
        default=None
    )
    parser.add_argument(
        "--y-min",
        type=float,
        help="Minimum Y coordinate",
        default=None
    )
    parser.add_argument(
        "--y-max",
        type=float,
        help="Maximum Y coordinate",
        default=None
    )
    parser.add_argument(
        "--z-min",
        type=float,
        help="Minimum Z coordinate",
        default=None
    )
    parser.add_argument(
        "--z-max",
        type=float,
        help="Maximum Z coordinate",
        default=None
    )
    parser.add_argument(
        "--min-nodes",
        type=int,
        help="Minimum number of nodes that must be inside the bounding box for an element to be included",
        default=1
    )
    args = parser.parse_args(sys.argv[1:])
    logging.basicConfig(
        level=getattr(logging, args.log.upper(), None),
        format="%(asctime)s.%(msecs)03d %(levelname)s: %(message)s",
        datefmt="%H:%M:%S",
    )
    return args


def is_point_in_box(point, x_min=None, x_max=None, y_min=None, y_max=None, z_min=None, z_max=None):
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


def extract_node_coordinates_directly(frd_path, encoding=None):
    """
    Extract node coordinates directly from the FRD file.
    
    Args:
        frd_path: Path to the FRD file
        encoding: File encoding (default: system default)
        
    Returns:
        Dictionary mapping node numbers to their coordinates (x, y, z)
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


def extract_elements_by_bounding_box(frd_path, output_path=None, encoding=None, x_min=None, x_max=None, y_min=None, y_max=None, z_min=None, z_max=None, min_nodes=1):
    """
    Extract elements from an FRD file based on a bounding box.
    
    This function ensures element completeness by including ALL nodes of selected elements,
    not just the nodes within the bounding box.
    
    Args:
        frd_path: Path to the FRD file
        output_path: Path to the output FRD file
        encoding: File encoding (default: system default)
        x_min, x_max: X-coordinate range (optional)
        y_min, y_max: Y-coordinate range (optional)
        z_min, z_max: Z-coordinate range (optional)
        min_nodes: Minimum number of nodes that must be inside the bounding box
        
    Returns:
        True if successful, False otherwise
    """
    try:
        # If output path is not specified, create a default one
        if output_path is None:
            base_name = os.path.splitext(frd_path)[0]
            output_path = f"{base_name}_bbox_extracted.frd"
        
        # First, get all node coordinates
        node_coords = extract_node_coordinates_directly(frd_path, encoding)
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
        
        # Create element filter for the selected elements
        def element_filter(element_node_list):
            # Check if this element has enough nodes in the original bounding box
            nodes_in_bbox_count = sum(1 for node_id in element_node_list 
                                    if node_id in nodes_in_bbox)
            return nodes_in_bbox_count >= min_nodes
        
        # Use the utility function from utilities_postprocess for consistent behavior
        from utilities_postprocess import write_frd_extracted_file
        
        success = write_frd_extracted_file(
            frd_path, output_path, complete_selected_nodes, 
            element_filter, encoding, update_counts=True
        )
        
        # Log final statistics
        logging.info(f"Extraction complete:")
        logging.info(f"- Found {len(nodes_in_bbox)} nodes in bounding box")
        logging.info(f"- Selected {len(selected_elements)} elements with at least {min_nodes} nodes in bounding box")
        logging.info(f"- Included {len(complete_selected_nodes)} total nodes (for element completeness)")
        logging.info(f"Results saved to {output_path}")
        
        return success
    
    except Exception as e:
        logging.error(f"Error extracting data: {str(e)}")
        logging.debug(traceback.format_exc())
        return False


def _main():
    """Entry point for the script."""
    args = _setup()
    
    # Build bounding box description
    box_desc = []
    if args.x_min is not None or args.x_max is not None:
        box_desc.append(f"X: {args.x_min if args.x_min is not None else '-inf'} to {args.x_max if args.x_max is not None else '+inf'}")
    if args.y_min is not None or args.y_max is not None:
        box_desc.append(f"Y: {args.y_min if args.y_min is not None else '-inf'} to {args.y_max if args.y_max is not None else '+inf'}")
    if args.z_min is not None or args.z_max is not None:
        box_desc.append(f"Z: {args.z_min if args.z_min is not None else '-inf'} to {args.z_max if args.z_max is not None else '+inf'}")
    
    logging.info(f"Extracting elements with at least {args.min_nodes} nodes in bounding box:")
    for desc in box_desc:
        logging.info(f"- {desc}")
    
    success = extract_elements_by_bounding_box(
        args.file,
        args.output,
        args.encoding,
        args.x_min,
        args.x_max,
        args.y_min,
        args.y_max,
        args.z_min,
        args.z_max,
        args.min_nodes
    )
    
    if not success:
        sys.exit(1)


if __name__ == "__main__":
    _main() 
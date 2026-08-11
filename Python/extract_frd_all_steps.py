#!/usr/bin/env python
# file: extract_frd_all_steps.py
# vim:fileencoding=utf-8:fdm=marker:ft=python
#
# UPDATED: 2024.12.19 - Element completeness guaranteed
# When extracting based on node criteria, ALL nodes of selected elements are included
"""
Extract data from an FRD file for all steps, for a specific range of nodes.
This script extracts data for specified nodes and saves it to a new FRD file.

Key Feature: Element Completeness Guaranteed
- When extracting elements based on node criteria, ALL nodes of selected 
  elements are included in the output, not just the nodes meeting the criteria
- This ensures extracted FRD files contain only complete, valid elements
"""

import os
import sys
import logging
import argparse
import re
import traceback

# Import functions from common.py
from common import FRD, write_converted_file, renumbered_nodes

# Import utilities for element completeness
from utilities_postprocess import (
    extract_node_coordinates_from_frd,
    write_frd_extracted_file,
    create_bounding_box_filter
)

# Import centralized INP file reading function
import sys
import os
sys.path.append(os.path.join(os.path.dirname(__file__), '..', '..', 'mesh_tools'))
from utilities_simulation import read_inp_file

__version__ = "2024.12.19"


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
        "--node-start",
        help="Start node number",
        type=int,
        required=False
    )
    parser.add_argument(
        "--node-end",
        help="End node number",
        type=int,
        required=False
    )
    parser.add_argument(
        "--inp",
        help="INP file to read element set from",
        default=None
    )
    parser.add_argument(
        "--elset",
        help="Element set name to extract nodes from",
        default=None
    )
    parser.add_argument(
        "--list-elsets",
        action="store_true",
        help="List all element sets in the INP file and exit"
    )
    parser.add_argument(
        "-e", "--encoding",
        help="File encoding (default: system default)",
        default=None
    )
    parser.add_argument(
        "--result-types",
        help="Comma-separated list of result types to extract (e.g., 'STRESS,DISPL')",
        default="ALL"
    )
    parser.add_argument(
        "--include-partial-elements",
        action="store_true",
        help="Include elements with at least partial_threshold of nodes in the target set (default behavior includes elements with at least 1 node)"
    )
    parser.add_argument(
        "--partial-threshold",
        type=float,
        default=0.5,
        help="Threshold for including partial elements (default: 0.5 = 50%% of nodes must match)"
    )
    args = parser.parse_args(sys.argv[1:])
    logging.basicConfig(
        level=getattr(logging, args.log.upper(), None),
        format="%(asctime)s.%(msecs)03d %(levelname)s: %(message)s",
        datefmt="%H:%M:%S",
    )
    return args




def get_nodes_from_elset(inp_path, elset_name):
    """
    Get node IDs from an element set in an INP file.
    
    Args:
        inp_path: Path to the INP file
        elset_name: Name of the element set
        
    Returns:
        Set of node IDs
    """
    try:
        # Initialize empty dictionaries for existing sets
        existing_sets = {'nsets': {}, 'elsets': {}}
        
        # Read the INP file using centralized function
        nodes, elements, existing_sets = read_inp_file(inp_path, existing_sets=existing_sets)
        nsets = existing_sets.get('nsets', {})
        elsets = existing_sets.get('elsets', {})
        
        # Log all available element sets for debugging
        logging.debug(f"Available element sets in INP file:")
        
        # Count different types of element sets
        element_set_types = {
            "from_element_line": 0,
            "from_elset_section": 0,
            "empty_sets": 0
        }
        
        for name in sorted(elsets.keys()):
            element_count = len(elsets[name]['elements'])
            set_type = "*Element line" if '*Element' in elsets[name]['declaration'] else "*Elset section"
            
            if '*Element' in elsets[name]['declaration']:
                element_set_types["from_element_line"] += 1
            else:
                element_set_types["from_elset_section"] += 1
                
            if element_count == 0:
                element_set_types["empty_sets"] += 1
                
            logging.debug(f"  - {name}: {element_count} elements (from {set_type})")
            
            # Print element details for very small sets (helpful for debugging)
            if 0 < element_count <= 5:
                elements_in_set = elsets[name]['elements']
                logging.debug(f"    Elements: {elements_in_set}")
                # Show nodes for each element
                for elem_id in elements_in_set:
                    if elem_id in elements:
                        logging.debug(f"    Element {elem_id} has nodes: {elements[elem_id]['nodes'][:5]}...")
                    else:
                        logging.debug(f"    Element {elem_id} not found in elements dictionary!")
        
        logging.debug(f"Element set statistics:")
        logging.debug(f"  - Total sets: {len(elsets)}")
        logging.debug(f"  - From *Element lines: {element_set_types['from_element_line']}")
        logging.debug(f"  - From *Elset sections: {element_set_types['from_elset_section']}")
        logging.debug(f"  - Empty sets: {element_set_types['empty_sets']}")

        # Try all possible naming patterns
        found_elset = None
        matched_name = None
        match_strategies = [
            "exact_match", 
            "exact_with_prefix", 
            "partial_case_sensitive", 
            "partial_case_insensitive",
            "type_match"  # Match based on element type (C3D8, C3D20, etc.)
        ]
        
        logging.info(f"Looking for element set '{elset_name}' using multiple match strategies")
        
        # Strategy 1: Exact match
        if elset_name in elsets:
            found_elset = elsets[elset_name]
            matched_name = elset_name
            logging.info(f"Found element set using exact match: '{matched_name}'")
        
        # Strategy 2: Exact match with ELSET__ prefix
        elif f"ELSET__{elset_name}" in elsets:
            found_elset = elsets[f"ELSET__{elset_name}"]
            matched_name = f"ELSET__{elset_name}"
            logging.info(f"Found element set using exact match with prefix: '{matched_name}'")
            
        # Strategy 3: Partial match - looking for the elset name within other set names (case sensitive)
        elif not found_elset:
            for name, elset in elsets.items():
                if elset_name in name:
                    found_elset = elset
                    matched_name = name
                    logging.info(f"Found element set via partial match (case sensitive): '{matched_name}'")
                    break
            
        # Strategy 4: Partial match - case insensitive
        if not found_elset:
            elset_upper = elset_name.upper()
            for name, elset in elsets.items():
                if elset_upper in name.upper():
                    found_elset = elset
                    matched_name = name
                    logging.info(f"Found element set via partial match (case insensitive): '{matched_name}'")
                    break
                    
        # Strategy 5: Element type match (if the elset_name looks like an element type code)
        if not found_elset and re.match(r'^[A-Z0-9]+$', elset_name):
            # Check for element type sets like ELSET__C3D8_ALL
            type_set_pattern = f".*{elset_name}.*_ALL"
            for name, elset in elsets.items():
                if re.match(type_set_pattern, name, re.IGNORECASE):
                    found_elset = elset
                    matched_name = name
                    logging.info(f"Found element set matching element type: '{matched_name}'")
                    break
        
        # If no element set is found, show the available sets
        if not found_elset:
            logging.error(f"Element set '{elset_name}' not found after trying {len(match_strategies)} match strategies")
            logging.info(f"Available element sets: {', '.join(sorted(elsets.keys())[:10])}...")
            if len(elsets) > 10:
                logging.info(f"...and {len(elsets) - 10} more sets")
            return None
        
        # Get all nodes from the elements in the set
        node_ids = set()
        element_count = len(found_elset['elements'])
        logging.info(f"Using element set '{matched_name}' with {element_count} elements")
        
        if element_count == 0:
            logging.warning(f"Element set '{matched_name}' contains 0 elements. Check INP file.")
            return node_ids
            
        # Track elements with missing data for debugging
        missing_elements = []
        element_node_counts = {}  # To analyze distribution of node counts
        
        for i, elem_id in enumerate(found_elset['elements']):
            if i == 0 or i == element_count-1 or element_count < 10 or i % 1000 == 0:
                logging.debug(f"Processing element {i+1}/{element_count}: {elem_id}")
                
            if elem_id in elements:
                elem_nodes = elements[elem_id]['nodes']
                node_ids.update(elem_nodes)
                
                # Track node count statistics
                node_count = len(elem_nodes)
                if node_count not in element_node_counts:
                    element_node_counts[node_count] = 0
                element_node_counts[node_count] += 1
            else:
                logging.warning(f"Element {elem_id} in set '{matched_name}' not found in elements dictionary")
                missing_elements.append(elem_id)
        
        # Generate statistics about node counts
        logging.info(f"Node count statistics for elements in '{matched_name}':")
        for node_count, count in sorted(element_node_counts.items()):
            logging.info(f"  - Elements with {node_count} nodes: {count}")
            
        if missing_elements:
            logging.warning(f"Found {len(missing_elements)} missing elements in set '{matched_name}'")
            if len(missing_elements) <= 10:
                logging.warning(f"Missing elements: {missing_elements}")
            else:
                logging.warning(f"First 10 missing elements: {missing_elements[:10]}...")
                
        logging.info(f"Found {len(node_ids)} nodes in element set '{matched_name}'")
        
        # Log a sample of node IDs for verification
        if len(node_ids) > 0:
            sample_nodes = sorted(list(node_ids))[:5]
            logging.debug(f"Sample node IDs: {sample_nodes}")
            
            # Verify these nodes exist in the nodes dictionary
            for node_id in sample_nodes:
                if node_id in nodes:
                    logging.debug(f"Node {node_id} coordinates: {nodes[node_id]}")
                else:
                    logging.warning(f"Node {node_id} not found in nodes dictionary!")
            
        return node_ids
        
    except Exception as e:
        logging.error(f"Error getting nodes from element set: {str(e)}")
        logging.debug(traceback.format_exc())
        return None

def list_available_elsets(inp_path):
    """
    List all element sets in an INP file.
    
    Args:
        inp_path: Path to the INP file
        
    Returns:
        Dictionary of element sets
    """
    try:
        # Initialize empty dictionaries for existing sets
        existing_sets = {'nsets': {}, 'elsets': {}}
        
        # Read the INP file using centralized function
        _, elements, existing_sets = read_inp_file(inp_path, existing_sets=existing_sets)
        elsets = existing_sets.get('elsets', {})
        
        # Filter out element sets starting with "ELSET__Surface_Spring"
        filtered_elsets = {}
        filtered_out = 0
        
        # Count element sets by naming pattern and size
        element_set_stats = {
            'total_sets': len(elsets),
            'filtered_out': 0,
            'from_element_line': 0,  # Sets defined in *Element line
            'from_elset_line': 0,    # Sets defined in *Elset line
            'empty_sets': 0,         # Sets with 0 elements
            'small_sets': 0,         # Sets with 1-10 elements
            'medium_sets': 0,        # Sets with 11-100 elements
            'large_sets': 0,         # Sets with >100 elements
        }
        
        for name, elset in elsets.items():
            # Check naming pattern
            if "ELSET__Surface_Spring" in name:
                element_set_stats['filtered_out'] += 1
                filtered_out += 1
                continue
                
            filtered_elsets[name] = elset
            
            # Count by definition type
            if '*Element' in elset['declaration']:
                element_set_stats['from_element_line'] += 1
            elif '*Elset' in elset['declaration']:
                element_set_stats['from_elset_line'] += 1
                
            # Count by size
            num_elements = len(elset['elements'])
            if num_elements == 0:
                element_set_stats['empty_sets'] += 1
            elif num_elements <= 10:
                element_set_stats['small_sets'] += 1
            elif num_elements <= 100:
                element_set_stats['medium_sets'] += 1
            else:
                element_set_stats['large_sets'] += 1
        
        # Print statistics
        logging.info(f"Element set statistics:")
        logging.info(f"- Total sets: {element_set_stats['total_sets']}")
        logging.info(f"- Filtered out: {element_set_stats['filtered_out']}")
        logging.info(f"- From *Element lines: {element_set_stats['from_element_line']}")
        logging.info(f"- From *Elset lines: {element_set_stats['from_elset_line']}")
        logging.info(f"- Empty sets (0 elements): {element_set_stats['empty_sets']}")
        logging.info(f"- Small sets (1-10 elements): {element_set_stats['small_sets']}")
        logging.info(f"- Medium sets (11-100 elements): {element_set_stats['medium_sets']}")
        logging.info(f"- Large sets (>100 elements): {element_set_stats['large_sets']}")
        
        # Print all element sets
        logging.info(f"Found {len(filtered_elsets)} element sets in INP file (after filtering out {filtered_out}):")
        
        # Sort by size, largest first
        sorted_elsets = sorted(filtered_elsets.items(), key=lambda x: len(x[1]['elements']), reverse=True)
        
        for name, elset in sorted_elsets:
            element_count = len(elset['elements'])
            logging.info(f"  {name}: {element_count} elements")
            
            # Print element set declaration for debugging
            if element_count == 0:
                logging.debug(f"  Declaration: {elset['declaration']}")
            
            # For debugging empty sets, show the first few elements if available
            if element_count > 0 and element_count < 5:
                logging.debug(f"  Sample elements: {elset['elements']}")
        
        return filtered_elsets
        
    except Exception as e:
        logging.error(f"Error listing element sets: {str(e)}")
        logging.debug(traceback.format_exc())
        return None

def extract_frd_all_steps(frd_path, output_path=None, encoding=None, result_types=None, node_start=None, node_end=None, node_ids=None, include_partial_elements=False, partial_threshold=0.5):
    """
    Extract data from an FRD file for all steps, for a specific range of nodes or a set of node IDs.
    
    IMPORTANT: Element Completeness Guaranteed
    - When elements meet the selection criteria, ALL of their nodes are included in the output
    - This ensures extracted FRD files contain only complete, valid elements
    - Additional nodes beyond the original target set may be included to maintain element integrity
    
    Args:
        frd_path: Path to the FRD file
        output_path: Path to the output FRD file
        encoding: File encoding (default: system default)
        result_types: List of result types to extract (default: None, which means all result types)
        node_start: Start node number (exclusive with node_ids)
        node_end: End node number (exclusive with node_ids)
        node_ids: Set of node IDs to extract (exclusive with node_start/node_end)
        include_partial_elements: If True, include elements with at least partial_threshold of their nodes in target set
        partial_threshold: Threshold for including partial elements (default: 0.5 = 50% of nodes must match)
        
    Returns:
        True if successful, False otherwise
    """
    try:
        # If output path is not specified, create a default one
        if output_path is None:
            base_name = os.path.splitext(frd_path)[0]
            if node_ids:
                output_path = f"{base_name}_extracted_nodes_from_elset.frd"
            else:
                output_path = f"{base_name}_extracted_nodes_{node_start}-{node_end}.frd"
        
        # Create a set of node IDs we want to extract
        target_nodes = node_ids if node_ids else set(range(node_start, node_end + 1))
        
        # Log extraction details
        if node_ids:
            logging.info(f"Extracting data from {frd_path} for {len(target_nodes)} nodes from element set")
        else:
            logging.info(f"Extracting data from {frd_path} for nodes {node_start} to {node_end}")
        
        logging.info(f"Output will be saved to {output_path}")
        logging.info(f"Will extract data for {len(target_nodes)} nodes")
        
        # Variables to track element selection (simplified since we're using centralized function)
        element_processing_stats = {
            'total_elements_seen': 0,
            'parse_errors': 0
        }
        
        # Create renumbering mappings
        # First pass: collect all nodes and elements that will be extracted
        nodes_to_extract = set()
        elements_to_extract = set()
        
        # Read through the file once to determine what will be extracted
        with open(frd_path, 'r', encoding=encoding) as file:
            lines = file.readlines()
        
        # Process elements to determine which ones will be included
        temp_element_nodes = {}
        in_element_block = False
        current_element_id = None
        
        for i, line in enumerate(lines[9:], 9):  # Skip header
            line_stripped = line.strip()
            
            if line_stripped.startswith('3C'):
                in_element_block = True
                continue
            elif line_stripped.startswith('-3') and in_element_block:
                in_element_block = False
                continue
            elif in_element_block and line_stripped.startswith('-1'):
                try:
                    elem_match = re.search(r'-1\s+(\d+)', line)
                    if elem_match:
                        current_element_id = int(elem_match.group(1))
                        temp_element_nodes[current_element_id] = []
                except Exception:
                    continue
            elif in_element_block and line_stripped.startswith('-2') and current_element_id is not None:
                try:
                    parts = line.strip().split()
                    line_nodes = [int(node_id) for node_id in parts[1:] if node_id.strip()]
                    temp_element_nodes[current_element_id].extend(line_nodes)
                except Exception:
                    continue
        
        # Determine which elements to include based on node criteria
        for elem_id, elem_nodes in temp_element_nodes.items():
            if elem_nodes:
                nodes_in_target = [node_id for node_id in elem_nodes if node_id in target_nodes]
                node_match_ratio = len(nodes_in_target) / len(elem_nodes) if elem_nodes else 0
                
                should_include = False
                if len(nodes_in_target) >= 1:
                    if len(nodes_in_target) == len(elem_nodes):
                        should_include = True
                    elif include_partial_elements and node_match_ratio >= partial_threshold:
                        should_include = True
                    elif not include_partial_elements:
                        should_include = True
                
                if should_include:
                    elements_to_extract.add(elem_id)
                    # Add ALL nodes from this element to maintain element completeness
                    # This ensures no element becomes incomplete with missing nodes
                    nodes_to_extract.update(elem_nodes)
        
        # Log element completeness improvement
        original_target_count = len(target_nodes)
        complete_node_count = len(nodes_to_extract)
        additional_nodes = complete_node_count - original_target_count
        
        logging.info(f"Selected {len(elements_to_extract)} elements based on node criteria")
        logging.info(f"Target nodes from criteria: {original_target_count}")
        logging.info(f"Total nodes after ensuring element completeness: {complete_node_count}")
        if additional_nodes > 0:
            logging.info(f"Added {additional_nodes} additional nodes to maintain element completeness")
        else:
            logging.info(f"No additional nodes needed - all selected elements were already complete")
        
        # Create element filter function that only includes pre-selected elements
        # We'll create a mapping from element node sets to element IDs for precise matching
        element_nodesets = {}
        for elem_id in elements_to_extract:
            if elem_id in temp_element_nodes:
                # Create a frozenset for hashable comparison
                nodeset = frozenset(temp_element_nodes[elem_id])
                element_nodesets[nodeset] = elem_id
        
        logging.info(f"Created precise element mapping for {len(element_nodesets)} pre-selected elements")
        logging.info(f"This prevents unwanted elements that share nodes from being included")
        
        # Track rejected elements for summary
        rejected_elements_with_shared_nodes = 0
        
        def element_extraction_filter(element_node_list):
            """
            Filter function that only includes elements that were pre-selected in the first pass.
            This prevents unwanted elements (like C3D15s) that share nodes with selected elements.
            """
            nonlocal rejected_elements_with_shared_nodes
            
            if not element_node_list:
                return False
            
            # Check if this exact element (by its node set) was pre-selected
            element_nodeset = frozenset(element_node_list)
            is_preselected = element_nodeset in element_nodesets
            
            if is_preselected:
                elem_id = element_nodesets[element_nodeset]
                logging.debug(f"Including pre-selected element {elem_id}")
                return True
            else:
                # Count elements that share nodes but are rejected
                nodes_in_original_target = [node_id for node_id in element_node_list if node_id in target_nodes]
                if len(nodes_in_original_target) > 0:
                    rejected_elements_with_shared_nodes += 1
                    logging.debug(f"Rejecting element with {len(element_node_list)} nodes that shares {len(nodes_in_original_target)} nodes with target set")
                return False
        
        # Use the centralized FRD writing function from utilities_postprocess
        logging.info(f"Using centralized write_frd_extracted_file function for consistent output")
        success = write_frd_extracted_file(
            frd_path=frd_path,
            output_path=output_path,
            selected_nodes=nodes_to_extract,
            element_filter_func=element_extraction_filter,
            encoding=encoding,
            update_counts=True
        )
        
        if not success:
            logging.error("Failed to write extracted FRD file using centralized function")
            return False
        
        # Log detailed statistics
        logging.info(f"Extraction complete using centralized utilities:")
        logging.info(f"- Target nodes from criteria: {len(target_nodes)}")
        logging.info(f"- Total nodes after ensuring element completeness: {len(nodes_to_extract)}")
        additional_nodes = len(nodes_to_extract) - len(target_nodes)
        if additional_nodes > 0:
            logging.info(f"- Added {additional_nodes} additional nodes to maintain element completeness")
        logging.info(f"- Selected {len(elements_to_extract)} elements based on node criteria")
        
        # Report on element filtering effectiveness
        if rejected_elements_with_shared_nodes > 0:
            logging.info(f"- Rejected {rejected_elements_with_shared_nodes} elements that shared nodes but weren't in the original element set")
            logging.info(f"- This prevents unwanted element types (e.g., C3D15) from being included with C3D10 selections")
        else:
            logging.info(f"- No additional elements sharing nodes were found (perfect element isolation)")
        
        logging.info(f"Results saved to {output_path}")
        
        return True
    
    except Exception as e:
        logging.error(f"Error extracting data: {str(e)}")
        logging.debug(traceback.format_exc())
        return False


def _main():
    """Entry point for the script."""
    args = _setup()
    
    # Check if we're just listing element sets
    if args.list_elsets and args.inp:
        list_available_elsets(args.inp)
        sys.exit(0)
    
    # Handle extraction from element set vs node range
    if args.inp and args.elset:
        # Get nodes from element set
        node_ids = get_nodes_from_elset(args.inp, args.elset)
        if not node_ids or len(node_ids) == 0:
            logging.error(f"No nodes found in element set '{args.elset}'")
            sys.exit(1)
            
        # Parse result types
        result_types = None
        if args.result_types and args.result_types != "ALL":
            result_types = [rt.strip() for rt in args.result_types.split(',')]
            logging.info(f"Extracting result types: {', '.join(result_types)}")
        
        # Log element inclusion criteria  
        if args.include_partial_elements:
            logging.info(f"Including elements with at least {args.partial_threshold:.0%} of nodes in target set")
        else:
            logging.info(f"Including elements with at least 1 node in target set (standard behavior)")
        
        # Extract data using node IDs
        success = extract_frd_all_steps(
            args.file,
            args.output,
            args.encoding,
            result_types,
            node_ids=node_ids,
            include_partial_elements=args.include_partial_elements,
            partial_threshold=args.partial_threshold
        )
    else:
        # Validate node range
        if args.node_start is None or args.node_end is None:
            logging.error("Either node range (--node-start and --node-end) or INP file (--inp) and element set (--elset) must be specified")
            sys.exit(1)
            
        if args.node_start >= args.node_end:
            logging.error(f"Invalid node range: {args.node_start} to {args.node_end}")
            sys.exit(1)
        
        # Parse result types
        result_types = None
        if args.result_types and args.result_types != "ALL":
            result_types = [rt.strip() for rt in args.result_types.split(',')]
            logging.info(f"Extracting result types: {', '.join(result_types)}")
            
        # Log element inclusion criteria
        if args.include_partial_elements:
            logging.info(f"Including elements with at least {args.partial_threshold:.0%} of nodes in target set")
        else:
            logging.info(f"Including elements with at least 1 node in target set (standard behavior)")
        
        # Extract data using node range
        success = extract_frd_all_steps(
            args.file,
            args.output,
            args.encoding,
            result_types,
            args.node_start,
            args.node_end,
            include_partial_elements=args.include_partial_elements,
            partial_threshold=args.partial_threshold
        )
    
    if not success:
        sys.exit(1)


if __name__ == "__main__":
    _main() 
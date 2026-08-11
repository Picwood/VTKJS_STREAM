#!/usr/bin/env python3
"""
Spring Constant Editor - Standalone Script

Usage:
    python spring_editor.py <input_file.inp> [output_file.inp]
    python spring_editor.py test  # Run built-in test

This script allows you to modify spring constants in Abaqus INP files.
"""

import os
import sys
import argparse

# Add parent directory to path so we can import the other scripts
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

try:
    from utilities_simulation import modify_spring_constants
except ImportError as e:
    print(f"Error importing functions: {e}")
    print("Make sure you're running this from the FEA directory")
    sys.exit(1)

def main():
    """Main function for command-line interface"""
    parser = argparse.ArgumentParser(
        description="Modify spring constants in Abaqus INP files",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
    python spring_editor.py model.inp                    # Modify springs in model.inp
    python spring_editor.py model.inp model_new.inp     # Specify output file
    python spring_editor.py test                        # Run built-in test
    
The GUI supports bulk scaling for all springs and per-spring orientation offsets
that multiply each spring by (1 + pct * orientation_z), with a live preview of the resulting factor per spring group.
        """
    )
    
    parser.add_argument(
        'input_file',
        nargs='?',
        default=None,
        help='Input INP file path or "test" to run built-in test (optional; will prompt if omitted)'
    )
    
    parser.add_argument(
        'output_file',
        nargs='?',
        help='Output INP file path (optional, defaults to input_modified.inp)'
    )
    
    args = parser.parse_args()
        
    # If provided, ensure input exists; otherwise selection happens in the GUI flow
    if args.input_file and not os.path.exists(args.input_file):
        print(f"Error: Input file '{args.input_file}' not found")
        return 1
    
    # Process the file (path may be selected interactively later)
    if args.input_file:
        print(f"Processing INP file: {args.input_file}")
    
    try:
        output_file = modify_spring_constants(args.input_file, args.output_file)
        
        if output_file:
            print(f"\nSuccess! Modified file saved as: {output_file}")
            print(f"Original file: {args.input_file}")
            print(f"Modified file: {output_file}")
            return 0
        else:
            print("\nOperation was cancelled by user")
            return 1
            
    except Exception as e:
        print(f"Error processing file: {e}")
        import traceback
        traceback.print_exc()
        return 1

if __name__ == "__main__":
    sys.exit(main()) 

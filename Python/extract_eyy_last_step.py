#!/usr/bin/env python3
"""
Extract the EYY (E22) strain value for a specific node from the last step/increment
of a CalculiX FRD file. This focuses on the TOSTRAIN block and does not require VTK.

Example:
    python extract_eyy_last_step.py results.frd --node 93
"""

import argparse
import json
import os
import re
import sys
from typing import Optional, Tuple, Dict, Any

FIELD_WIDTH = 12


def _chunk_values(segment: str, count: int):
    """Split a fixed-width segment into floats."""
    segment = segment.rstrip("\n").ljust(count * FIELD_WIDTH)
    values = []
    for i in range(count):
        field = segment[i * FIELD_WIDTH : (i + 1) * FIELD_WIDTH]
        field = field.replace("D", "E")  # handle Fortran-style exponents
        if field.strip():
            try:
                values.append(float(field))
            except ValueError:
                pass
    return values


def _parse_step_line(line: str) -> Tuple[Optional[int], Optional[float]]:
    """Parse a '100' step header line."""
    tail = line[12:]
    match = re.search(r"^(.{12})\s+\d+\s+\d+\s+(\d+)", tail)
    if not match:
        return None, None
    inc = float(match.group(1))
    step = int(match.group(2))
    return step, inc


def _read_node_values(first_line: str, ncomps: int, fh) -> Tuple[Optional[int], list]:
    """Read node id and its component values (including continuation lines)."""
    m = re.match(r"^\s*-1\s*([0-9]+)", first_line)
    if not m:
        return None, []
    node_id = int(m.group(1))
    remainder = first_line[m.end() :]
    row_comps = min(6, ncomps)
    values = _chunk_values(remainder, row_comps)
    remaining = ncomps - row_comps

    while remaining > 0:
        cont_line = fh.readline()
        if not cont_line:
            break
        if not cont_line.lstrip().startswith("-2"):
            # Unexpected structure; stop to avoid skipping data
            break
        _, cont_remainder = cont_line.split(None, 1) if " " in cont_line else ("-2", "")
        take = min(6, remaining)
        values.extend(_chunk_values(cont_remainder, take))
        remaining -= take

    return node_id, values


def _parse_tostrain_block(fh, ncomps: int, target_node: int, eyy_index: int) -> Optional[float]:
    """Parse a TOSTRAIN block until '-3' and return EYY for the target node."""
    eyy_value = None
    for line in fh:
        stripped = line.strip()
        if stripped.startswith("-3"):
            break
        if stripped.startswith("-1"):
            node_id, values = _read_node_values(line, ncomps, fh)
            if node_id == target_node and len(values) > eyy_index:
                eyy_value = values[eyy_index]
    return eyy_value


def extract_eyy_last_step(frd_path: str, target_node: int = 93, encoding: Optional[str] = None) -> Optional[Dict[str, Any]]:
    """Extract EYY for the target node from the last step/increment."""
    current_step = None
    current_inc = None
    last_result: Optional[Dict[str, Any]] = None

    with open(frd_path, "r", encoding=encoding or "latin-1") as fh:
        for line in fh:
            key = line[:5].strip()
            if key == "100":
                current_step, current_inc = _parse_step_line(line)
            if line.startswith(" -4  TOSTRAIN"):
                header_match = re.match(r"^-4\s+(\S+)\s+(\d+)", line.strip(), re.IGNORECASE)
                if not header_match:
                    continue
                ncomps = int(header_match.group(2))

                comp_names = []
                for _ in range(ncomps):
                    comp_line = fh.readline()
                    comp_tokens = comp_line.strip().split()
                    comp_name = comp_tokens[1] if len(comp_tokens) > 1 else ""
                    comp_names.append(comp_name.upper())
                print(comp_name)
                # Determine the index for EYY (accept aliases)
                eyy_index = None
                for idx, name in enumerate(comp_names):
                    if name in {"EYY", "YY", "E22", "22"}:
                        eyy_index = idx
                        break
                if eyy_index is None:
                    continue

                eyy_val = _parse_tostrain_block(fh, ncomps, target_node, eyy_index)
                if eyy_val is not None:
                    last_result = {
                        "step": current_step,
                        "increment": current_inc,
                        "node": target_node,
                        "eyy": eyy_val,
                    }

    return last_result


def main():
    parser = argparse.ArgumentParser(description="Extract EYY (E22) for a node from the last step/increment of an FRD file.")
    parser.add_argument("frd_file", help="Path to the FRD file")
    parser.add_argument("--node", type=int, default=93, help="Node number to extract (default: 93)")
    parser.add_argument("--encoding", default=None, help="File encoding (defaults to latin-1)")
    parser.add_argument("--json-out", help="Optional path to write the result as JSON")
    args = parser.parse_args()

    if not os.path.exists(args.frd_file):
        print(f"[ERROR] FRD file not found: {args.frd_file}")
        sys.exit(1)

    result = extract_eyy_last_step(args.frd_file, target_node=args.node, encoding=args.encoding)
    if not result:
        print("[ERROR] No TOSTRAIN/EYY data found")
        sys.exit(1)

    msg = (
        f"Last step {result['step']} increment {result['increment']}: "
        f"EYY at node {result['node']} = {result['eyy']:.6e}"
    )
    print(msg)

    if args.json_out:
        with open(args.json_out, "w", encoding="utf-8") as fh:
            json.dump(result, fh, indent=2)
        print(f"Wrote JSON result to {args.json_out}")


if __name__ == "__main__":
    main()

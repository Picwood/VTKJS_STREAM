"""
Simple GUI to sweep a 2D GMSH profile into a 3D INP mesh using the CPU implementation.
"""

import argparse
import ast
import base64
import os
import re
import sys
import threading
import traceback
import tkinter as tk
from tkinter import filedialog, messagebox, ttk
from typing import Dict, Iterable, List, Optional, Sequence, Tuple

# Ensure repository paths are on sys.path for imports
CURRENT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.dirname(CURRENT_DIR)
# Add parent directory to path so we can import the other scripts
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

if CURRENT_DIR not in sys.path:
    sys.path.append(CURRENT_DIR)

from gmsh_to_swept_mesh import (  # type: ignore
    extract_2d_mesh_from_gmsh_script,
    write_inp_file,
)
from sweep_mesh_cpu import (  # type: ignore
    compute_stanchion_translation,
    sweep_2d_mesh_cpu,
    translate_profile_nodes,
)


PointMap = Dict[int, List[float]]
QuadMap = Dict[int, List[int]]


def _cluster_values(values: Iterable[float], tolerance: float) -> List[float]:
    """Group nearly-equal scalar values and return cluster centroids."""
    sorted_values = sorted(float(value) for value in values)
    if not sorted_values:
        return []

    clusters: List[List[float]] = [[sorted_values[0]]]
    for value in sorted_values[1:]:
        if abs(value - clusters[-1][-1]) <= tolerance:
            clusters[-1].append(value)
        else:
            clusters.append([value])

    return [sum(cluster) / len(cluster) for cluster in clusters]


def _parse_collapse_bands(script_path: str) -> List[Tuple[str, float, float]]:
    """Read `#collapse_X` markers from point definitions and return Y bands."""
    collapse_pattern = re.compile(r"#\s*collapse_([A-Za-z0-9_]+)\b")
    point_pattern = re.compile(r"addPoint\(\s*([^,]+),\s*([^,]+),\s*([^,]+),")
    grouped_y_values: Dict[str, List[float]] = {}

    with open(script_path, "r", encoding="utf-8") as handle:
        for line in handle:
            collapse_match = collapse_pattern.search(line)
            if collapse_match is None:
                continue

            point_match = point_pattern.search(line)
            if point_match is None:
                continue

            try:
                y_value = float(ast.literal_eval(point_match.group(2).strip()))
            except (ValueError, SyntaxError):
                continue

            grouped_y_values.setdefault(collapse_match.group(1), []).append(y_value)

    collapse_bands: List[Tuple[str, float, float]] = []
    for label, y_values in grouped_y_values.items():
        clustered = _cluster_values(y_values, tolerance=1e-9)
        if len(clustered) < 2:
            continue
        y_min = min(clustered)
        y_max = max(clustered)
        if abs(y_max - y_min) <= 1e-9:
            continue
        collapse_bands.append((label, y_min, y_max))

    collapse_bands.sort(key=lambda item: (item[1], item[2], item[0]))
    return collapse_bands


def _get_horizontal_axis_for_node_ids(nodes: PointMap, node_ids: Sequence[int]) -> int:
    """Return the dominant horizontal axis for the given in-plane node subset."""
    if not node_ids:
        return 2

    spans = []
    for axis in (0, 2):
        values = [nodes[node_id][axis] for node_id in node_ids]
        spans.append((max(values) - min(values), axis))

    spans.sort()
    return spans[-1][1]


def _sort_nodes_left_to_right(nodes: PointMap, node_ids: Sequence[int], horizontal_axis: int) -> List[int]:
    """Sort nodes across the profile width."""
    other_axis = 2 if horizontal_axis == 0 else 0
    return sorted(
        node_ids,
        key=lambda node_id: (
            nodes[node_id][horizontal_axis],
            nodes[node_id][other_axis],
            node_id,
        ),
    )


def _map_row_to_top_row(
    nodes: PointMap,
    row_nodes: Sequence[int],
    top_nodes: Sequence[int],
    horizontal_axis: int,
) -> Dict[int, int]:
    """Map a row of nodes onto the top boundary while preserving horizontal order."""
    if not row_nodes or not top_nodes:
        return {}

    row_sorted = _sort_nodes_left_to_right(nodes, row_nodes, horizontal_axis)
    top_sorted = _sort_nodes_left_to_right(nodes, top_nodes, horizontal_axis)

    if len(row_sorted) == len(top_sorted):
        return dict(zip(row_sorted, top_sorted))

    mapping: Dict[int, int] = {}
    last_top_index = len(top_sorted) - 1
    last_row_index = max(1, len(row_sorted) - 1)

    for row_index, node_id in enumerate(row_sorted):
        target_index = round(row_index * last_top_index / last_row_index)
        candidate_indices = range(max(0, target_index - 1), min(last_top_index + 1, target_index + 2))
        node_x = nodes[node_id][horizontal_axis]
        best_index = min(
            candidate_indices,
            key=lambda top_index: abs(nodes[top_sorted[top_index]][horizontal_axis] - node_x),
        )
        mapping[node_id] = top_sorted[best_index]

    return mapping


def _compact_2d_mesh_ids(nodes: PointMap, quad_elements: QuadMap) -> Tuple[PointMap, QuadMap]:
    """Renumber nodes and elements after pruning the 2D mesh."""
    node_ids = sorted(nodes.keys())
    node_map = {old_id: new_id for new_id, old_id in enumerate(node_ids, start=1)}
    compact_nodes = {node_map[old_id]: list(nodes[old_id]) for old_id in node_ids}

    compact_elements: QuadMap = {}
    for new_elem_id, old_elem_id in enumerate(sorted(quad_elements.keys()), start=1):
        compact_elements[new_elem_id] = [node_map[node_id] for node_id in quad_elements[old_elem_id]]

    return compact_nodes, compact_elements


def _collapse_marked_quad_rows(
    nodes: PointMap,
    quad_elements: QuadMap,
    collapse_bands: Sequence[Tuple[str, float, float]],
) -> Tuple[PointMap, QuadMap, List[str]]:
    """Collapse multi-row quad bands tagged by `#collapse_X` markers down to one row."""
    working_nodes: PointMap = {node_id: list(coords) for node_id, coords in nodes.items()}
    working_quads: QuadMap = {elem_id: list(node_ids) for elem_id, node_ids in quad_elements.items()}
    summary: List[str] = []

    for label, y_min, y_max in collapse_bands:
        band_height = abs(y_max - y_min)
        y_tolerance = max(1e-6, band_height * 1e-4)
        used_node_ids = {node_id for quad_nodes in working_quads.values() for node_id in quad_nodes}

        band_node_ids = [
            node_id
            for node_id in used_node_ids
            for coords in [working_nodes[node_id]]
            if (y_min - y_tolerance) <= coords[1] <= (y_max + y_tolerance)
        ]
        horizontal_axis = _get_horizontal_axis_for_node_ids(working_nodes, band_node_ids)
        row_levels = _cluster_values((working_nodes[node_id][1] for node_id in band_node_ids), y_tolerance)

        if len(row_levels) <= 2:
            summary.append(
                f"Collapse {label}: skipped, band already has {len(row_levels)} row(s) between y={y_min:.6f} and y={y_max:.6f}."
            )
            continue

        bottom_y = row_levels[0]
        top_y = row_levels[-1]
        top_nodes = [node_id for node_id in band_node_ids if abs(working_nodes[node_id][1] - top_y) <= y_tolerance]
        if len(top_nodes) < 2:
            summary.append(
                f"Collapse {label}: skipped, top boundary at y={top_y:.6f} has only {len(top_nodes)} node(s)."
            )
            continue

        node_remap: Dict[int, int] = {}
        interior_rows = 0
        for row_y in row_levels[1:-1]:
            row_nodes = [node_id for node_id in band_node_ids if abs(working_nodes[node_id][1] - row_y) <= y_tolerance]
            if not row_nodes:
                continue
            node_remap.update(_map_row_to_top_row(working_nodes, row_nodes, top_nodes, horizontal_axis))
            interior_rows += 1

        if not node_remap:
            summary.append(f"Collapse {label}: skipped, no interior nodes found to merge.")
            continue

        updated_quads: QuadMap = {}
        removed_degenerate = 0
        removed_duplicate = 0
        seen_signatures = set()

        for elem_id, quad_nodes in working_quads.items():
            remapped_nodes = [node_remap.get(node_id, node_id) for node_id in quad_nodes]
            if len(set(remapped_nodes)) < 4:
                removed_degenerate += 1
                continue

            signature = tuple(sorted(remapped_nodes))
            if signature in seen_signatures:
                removed_duplicate += 1
                continue

            seen_signatures.add(signature)
            updated_quads[elem_id] = remapped_nodes

        used_node_ids = {node_id for quad_nodes in updated_quads.values() for node_id in quad_nodes}
        removed_nodes = len(working_nodes) - len(used_node_ids)
        working_nodes = {node_id: working_nodes[node_id] for node_id in used_node_ids}
        working_quads = updated_quads

        summary.append(
            "Collapse "
            f"{label}: merged {interior_rows} interior row(s) in y=[{bottom_y:.6f}, {top_y:.6f}], "
            f"removed {removed_degenerate} degenerate quads, {removed_duplicate} duplicate quads, "
            f"and pruned {removed_nodes} orphan nodes."
        )

    compact_nodes, compact_quads = _compact_2d_mesh_ids(working_nodes, working_quads)
    summary.append(
        f"Collapsed 2D mesh now has {len(compact_nodes)} nodes and {len(compact_quads)} quad elements before sweep."
    )
    return compact_nodes, compact_quads, summary


class SweepMeshApp(tk.Tk):
    """Tkinter based GUI for CPU mesh sweeping."""

    def __init__(
        self,
        report_file: Optional[str] = None,
        stanchion_top: Optional[Sequence[float]] = None,
    ) -> None:
        super().__init__()
        self.title("CPU Mesh Sweeper")
        self.geometry("640x480")
        self.resizable(True, True)
        self.protocol("WM_DELETE_WINDOW", self._on_close)

        self.input_var = tk.StringVar()
        self.output_var = tk.StringVar()
        self.angles_var = tk.StringVar(value="50")
        self.total_angle_var = tk.StringVar(value="360")
        self.start_node_var = tk.StringVar(value="1")
        self.start_element_var = tk.StringVar(value="1")
        self._stanchion_top = [float(value) for value in stanchion_top[:3]] if stanchion_top and len(stanchion_top) >= 3 else None
        self.auto_position_var = tk.BooleanVar(value=self._stanchion_top is not None)
        self._report_file = report_file
        self._close_on_success = bool(report_file)
        self._report_status = "cancelled"
        self._report_output_path = None
        self._report_error = "Sweep mesh dialog closed before a mesh was generated."
        self._write_report(
            self._report_status,
            output_path=self._report_output_path,
            error=self._report_error,
        )

        self._build_layout()

    def _build_layout(self) -> None:
        """Create form widgets."""
        main_frame = ttk.Frame(self, padding=12)
        main_frame.pack(fill=tk.BOTH, expand=True)

        # Input file
        ttk.Label(main_frame, text="GMSH Python script (.py):").grid(
            row=0, column=0, sticky=tk.W, pady=(0, 4)
        )
        input_frame = ttk.Frame(main_frame)
        input_frame.grid(row=1, column=0, columnspan=3, sticky=tk.EW, pady=(0, 8))
        input_entry = ttk.Entry(input_frame, textvariable=self.input_var)
        input_entry.pack(side=tk.LEFT, fill=tk.X, expand=True)
        ttk.Button(input_frame, text="Browse…", command=self._browse_input).pack(
            side=tk.LEFT, padx=(6, 0)
        )

        # Output file
        ttk.Label(main_frame, text="Output INP file:").grid(
            row=2, column=0, sticky=tk.W, pady=(0, 4)
        )
        output_frame = ttk.Frame(main_frame)
        output_frame.grid(row=3, column=0, columnspan=3, sticky=tk.EW, pady=(0, 8))
        output_entry = ttk.Entry(output_frame, textvariable=self.output_var)
        output_entry.pack(side=tk.LEFT, fill=tk.X, expand=True)
        ttk.Button(output_frame, text="Save As…", command=self._browse_output).pack(
            side=tk.LEFT, padx=(6, 0)
        )

        # Parameters grid
        params_frame = ttk.LabelFrame(main_frame, text="Sweep Parameters", padding=8)
        params_frame.grid(row=4, column=0, columnspan=3, sticky=tk.EW, pady=(0, 10))

        ttk.Label(params_frame, text="Number of angles:").grid(
            row=0, column=0, sticky=tk.W, padx=(0, 6), pady=2
        )
        ttk.Entry(params_frame, width=12, textvariable=self.angles_var).grid(
            row=0, column=1, sticky=tk.W, pady=2
        )

        ttk.Label(params_frame, text="Total angle (deg):").grid(
            row=1, column=0, sticky=tk.W, padx=(0, 6), pady=2
        )
        ttk.Entry(params_frame, width=12, textvariable=self.total_angle_var).grid(
            row=1, column=1, sticky=tk.W, pady=2
        )

        ttk.Label(params_frame, text="Start node ID:").grid(
            row=0, column=2, sticky=tk.W, padx=(16, 6), pady=2
        )
        ttk.Entry(params_frame, width=12, textvariable=self.start_node_var).grid(
            row=0, column=3, sticky=tk.W, pady=2
        )

        ttk.Label(params_frame, text="Start element ID:").grid(
            row=1, column=2, sticky=tk.W, padx=(16, 6), pady=2
        )
        ttk.Entry(params_frame, width=12, textvariable=self.start_element_var).grid(
            row=1, column=3, sticky=tk.W, pady=2
        )

        if self._stanchion_top is not None:
            ttk.Checkbutton(
                params_frame,
                text="Auto-position to Stanchion_Insert top",
                variable=self.auto_position_var,
            ).grid(row=2, column=0, columnspan=4, sticky=tk.W, pady=(8, 2))

            ttk.Label(
                params_frame,
                text=(
                    f"Target top: X={self._stanchion_top[0]:.3f}, "
                    f"Y={self._stanchion_top[1]:.3f}, Z={self._stanchion_top[2]:.3f}"
                ),
            ).grid(row=3, column=0, columnspan=4, sticky=tk.W, pady=(0, 2))

        # Run button
        self.run_button = ttk.Button(
            main_frame, text="Generate Mesh", command=self._start_sweep
        )
        self.run_button.grid(row=5, column=0, sticky=tk.W, pady=(0, 10))

        # Status text
        self.log_text = tk.Text(main_frame, height=12, state=tk.DISABLED)
        self.log_text.grid(row=6, column=0, columnspan=3, sticky=tk.NSEW)
        scrollbar = ttk.Scrollbar(main_frame, orient=tk.VERTICAL, command=self.log_text.yview)
        scrollbar.grid(row=6, column=3, sticky=tk.NS)
        self.log_text.configure(yscrollcommand=scrollbar.set)

        # Configure grid weights
        for col in range(3):
            main_frame.columnconfigure(col, weight=1)
        main_frame.rowconfigure(6, weight=1)

    def _browse_input(self) -> None:
        """Ask user to choose the GMSH script."""
        filename = filedialog.askopenfilename(
            title="Select GMSH Python script",
            filetypes=[("Python files", "*.py"), ("All files", "*.*")],
        )
        if filename:
            self.input_var.set(filename)
            if not self.output_var.get():
                base = os.path.splitext(os.path.basename(filename))[0]
                default_out = os.path.join(os.path.dirname(filename), f"{base}_cpu_swept.inp")
                self.output_var.set(default_out)

    def _browse_output(self) -> None:
        """Ask user to choose the INP output location."""
        filename = filedialog.asksaveasfilename(
            title="Save INP mesh",
            defaultextension=".inp",
            filetypes=[("INP files", "*.inp"), ("All files", "*.*")],
        )
        if filename:
            self.output_var.set(filename)

    def _append_log(self, message: str) -> None:
        """Thread-safe wrapper to append to the status log."""
        self.after(0, self._append_log_ui, message)

    def _append_log_ui(self, message: str) -> None:
        """Append a line to the status log (UI thread)."""
        self.log_text.configure(state=tk.NORMAL)
        self.log_text.insert(tk.END, message + "\n")
        self.log_text.see(tk.END)
        self.log_text.configure(state=tk.DISABLED)
        print(message, flush=True)

    def _write_report(
        self,
        status: str,
        output_path: Optional[str] = None,
        error: Optional[str] = None,
    ) -> None:
        """Write a small host-readable report file when requested."""
        if not self._report_file:
            return

        report_dir = os.path.dirname(os.path.abspath(self._report_file))
        if report_dir:
            os.makedirs(report_dir, exist_ok=True)

        def encode(value: Optional[str]) -> str:
            if not value:
                return ""
            return base64.b64encode(value.encode("utf-8")).decode("ascii")

        self._report_status = status
        self._report_output_path = os.path.abspath(output_path) if output_path else None
        self._report_error = error

        lines = [
            f"status={self._report_status}",
            f"output_path_b64={encode(self._report_output_path)}",
            f"error_b64={encode(self._report_error)}",
        ]
        with open(self._report_file, "w", encoding="utf-8") as handle:
            handle.write("\n".join(lines) + "\n")

    def _on_close(self) -> None:
        """Persist the latest state and close the application."""
        self._write_report(
            self._report_status,
            output_path=self._report_output_path,
            error=self._report_error,
        )
        self.destroy()

    def _start_sweep(self) -> None:
        """Validate inputs and start background thread."""
        script_path = self.input_var.get().strip()
        output_path = self.output_var.get().strip()

        if not script_path:
            messagebox.showerror("Input required", "Please select a GMSH Python script.")
            return
        if not os.path.exists(script_path):
            messagebox.showerror("Invalid input", "Selected input script does not exist.")
            return
        if not output_path:
            messagebox.showerror("Output required", "Please choose an output INP path.")
            return

        try:
            num_angles = int(self.angles_var.get())
            total_angle = float(self.total_angle_var.get())
            start_node_id = int(self.start_node_var.get())
            start_element_id = int(self.start_element_var.get())
        except ValueError:
            messagebox.showerror(
                "Invalid parameters",
                "Angles, total angle, start node ID, and start element ID must be numeric.",
            )
            return

        self.run_button.config(state=tk.DISABLED)
        self._append_log("Starting sweep job…")

        thread = threading.Thread(
            target=self._run_sweep_job,
            args=(script_path, output_path, num_angles, total_angle, start_node_id, start_element_id),
            daemon=True,
        )
        thread.start()

    def _run_sweep_job(
        self,
        script_path: str,
        output_path: str,
        num_angles: int,
        total_angle: float,
        start_node_id: int,
        start_element_id: int,
    ) -> None:
        """Execute the sweep logic in a worker thread."""
        completed_successfully = False
        try:
            self._append_log(f"Reading 2D mesh from {script_path} …")
            nodes_2d, quad_elements, repos = extract_2d_mesh_from_gmsh_script(script_path)

            if nodes_2d is None or not quad_elements:
                raise RuntimeError("Failed to extract a valid 2D mesh from the script.")

            placement = list(repos[:3]) if repos is not None and len(repos) >= 3 else [0.0, 0.0, 0.0]
            if self._stanchion_top is not None and self.auto_position_var.get():
                placement = compute_stanchion_translation(nodes_2d, self._stanchion_top)
                self._append_log(
                    "Auto-positioning stanchion profile to "
                    f"X={placement[0]:.6f}, Y={placement[1]:.6f}, Z={placement[2]:.6f}."
                )
            elif any(abs(value) > 1e-12 for value in placement):
                self._append_log(
                    "Applying translation from script header: "
                    f"dx={placement[0]:.6f}, dy={placement[1]:.6f}, dz={placement[2]:.6f}."
                )

            collapse_bands = _parse_collapse_bands(script_path)
            if collapse_bands:
                self._append_log(
                    f"Applying {len(collapse_bands)} tagged collapse band(s) before sweep …"
                )
                nodes_2d, quad_elements, collapse_summary = _collapse_marked_quad_rows(
                    nodes_2d,
                    quad_elements,
                    collapse_bands,
                )
                for line in collapse_summary:
                    self._append_log(line)

            self._append_log(
                f"Running CPU sweep with {num_angles} angles over {total_angle}° …"
            )
            if self._stanchion_top is not None and self.auto_position_var.get():
                placement = compute_stanchion_translation(nodes_2d, self._stanchion_top)

            nodes_2d = translate_profile_nodes(nodes_2d, placement)

            nodes_3d, elements_3d, boundary_nodes, profile_elements, interface_elements = sweep_2d_mesh_cpu(
                nodes_2d,
                quad_elements,
                repos=placement,
                start_node_id=start_node_id,
                start_element_id=start_element_id,
                num_angles=num_angles,
                total_angle=total_angle,
            )

            part_name = os.path.splitext(os.path.basename(script_path))[0].upper()
            self._append_log("Writing INP file …")
            write_inp_file(
                output_path,
                nodes_3d,
                elements_3d,
                boundary_nodes,
                part_name,
                profile_elements,
                interface_elements,
            )

            self._append_log(f"Mesh written to {output_path}")
            self._write_report("completed", output_path=output_path)
            completed_successfully = True

            if self._close_on_success:
                self.after(0, self.destroy)
            else:
                self.after(0, lambda: messagebox.showinfo("Completed", f"INP mesh generated:\n{output_path}"))

        except Exception as exc:  # pylint: disable=broad-except
            error_message = "".join(traceback.format_exception(exc))
            self._append_log("Error during sweep:\n" + error_message)
            self._write_report("failed", error=str(exc))
            self.after(0, lambda: messagebox.showerror("Sweep failed", str(exc)))
        finally:
            if not (completed_successfully and self._close_on_success):
                self.after(0, lambda: self.run_button.config(state=tk.NORMAL))


def parse_args() -> argparse.Namespace:
    """Parse optional arguments for host application integration."""
    parser = argparse.ArgumentParser(description="Sweep a 2D GMSH profile into a 3D INP mesh.")
    parser.add_argument(
        "--report-file",
        dest="report_file",
        default=None,
        help="Write a small completion report for the calling application.",
    )
    parser.add_argument(
        "--stanchion-top",
        nargs=3,
        type=float,
        metavar=("X", "Y", "Z"),
        default=None,
        help="Enable auto-positioning so the profile top matches the supplied point.",
    )
    return parser.parse_args()


def main() -> None:
    """Entry point."""
    args = parse_args()
    app = SweepMeshApp(report_file=args.report_file, stanchion_top=args.stanchion_top)
    app.mainloop()


if __name__ == "__main__":
    main()

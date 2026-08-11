# Added Features and Integration Notes

This file tracks functionality added in this SpectraRev/PrePoMax working tree compared with the original PrePoMax package.

## Fatigue Analysis Workspace

- Added a dedicated **Fatigue** context alongside Geometry, FE Model, and Results.
- Added Fatigue tree entries for:
  - Loads
  - Model
  - Visible result/model parts
  - SN curve
  - Fatigue runner/backend
- Fatigue setup is stored with the `.pmx` project, so fatigue loads and settings persist across project open/save.
- Fatigue model definition is based on the currently opened PMX project and visible result parts instead of requiring the user to manually select matching `.inp` and `.frd` files.
- Fatigue load cases can be associated with result steps from the Results context to reduce mismatch risk after FE model changes.

## Load Definition

- Added a separate Loads setup window.
- Added import support for simplified two-column load files: time and amplitude.
- Added generated/manual load definition for sinusoidal-style load histories.
- Added load preview/update behavior directly in the PrePoMax fatigue UI.
- Added load cards in the Fatigue tree for created/imported loads.
- Added OK/Cancel behavior for the Loads setup window.

## SN Curve Setup

- Added a separate SN Curve setup window.
- Added SN curve parameters based on FATLAB setup fields:
  - Primary and secondary slopes
  - Fatigue strength and associated cycles
  - Knee point cycles/stress
  - Cut-offs
  - Partial safety factor
  - Allowable damage
  - Probability of survival and standard deviation
- Added mean stress settings:
  - Mean stress mode
  - Mean stress sensitivity
  - Yield strength
  - Tensile strength
  - Yield-line and compression extrapolation options
- Added mean stress effect plot updates when the selected mean stress mode changes.
- Added analysis options including fatigue stress mode, cycle counter, and multiaxial/critical-plane parameters.

## FATLAB Package Export

- Added export of a reproducible fatigue package containing:
  - `prepomax_fatigue_handoff.json`
  - `package_manifest.json`
  - Exported CalculiX `.inp`
  - Displayed/result-step `.frd` files
  - Load time-series file
  - Re-run batch script
- The package is designed for future recalculation outside the interactive PrePoMax session.
- Added validation of the generated JSON handoff before starting the backend calculation.

## MATLAB/FATLAB Runtime Backend

- Integrated a MATLAB/FATLAB package runner path for calculations delegated to the MATLAB runtime or MATLAB batch execution.
- Added backend selection in the Fatigue SN/setup workflow.
- Preserved package compatibility with FATLAB-style setup data and naming where practical.

## Python/CUDA Fatigue Backend

- Added `Python/prepomax_fatigue_cuda_runner.py`.
- Added a Python backend that reads the exported package directly without launching the FATLAB executable.
- Uses CuPy/CUDA when available and falls back to NumPy/CPU.
- Writes fatigue results to:
  - `prepomax_fatigue_results.json`
  - `prepomax_fatigue_results.csv`
  - `prepomax_fatigue_results.frd`
- The output `.frd` can be opened in PrePoMax for direct fatigue result visualization.
- Ported FATLAB-style fatigue calculation logic including:
  - SN curve handling
  - Mean stress corrections
  - Reservoir, rainflow full, rainflow half, and single-cycle counting
  - Fatigue stress modes including principal stress and signed stress options
  - Critical-plane normal stress support
  - Shear-based critical-plane criteria including Findley, Dang Van, IIW equivalent, and modified shear logic
- Added reconstruction of surface node normals and axes from package mesh data for critical-plane calculations.
- Added batching to avoid huge stress-history allocations on large models.
- Added a fast path for single linear load cases to avoid repeated per-node cycle counting when the stress history is a scaled load history.
- Added `PREPOMAX_FATIGUE_DISABLE_FAST_PATH=1` to force the general path for comparison/debugging.
- Added `PREPOMAX_FATIGUE_BATCH_MB` to tune stress-history batch memory.

## Fatigue Progress Reporting

- Added machine-readable progress messages from the Python runner:
  - `FATIGUE_PROGRESS:<percent>:<message>`
- Connected Python fatigue progress to the existing PrePoMax status-strip progress bar.
- Fatigue runs now show determinate progress during package reading, stress loading, calculation, and result writing.
- The status bar is reset on completion and on common failure paths.

## Result Visualization

- Added fatigue `.frd` output compatible with PrePoMax result loading.
- Added fatigue result fields such as damage, utilization, life, equivalent stress, load cycles, stress min/max, and critical plane index.
- Added automatic opening of Python fatigue `.frd` results after successful calculation.
- Added a local extrema search workflow in Results for nodal stress/result fields.
- Added reporting of local extrema nodes to support hotspot review and result interrogation directly from the Results context.

## SRSuntour Tools

- Added an SRSuntour-specific tool tab/workflow.
- Integrated supporting Python utilities used by the SRSuntour workflow.
- Added Python-driven mesh/geometry helper workflows for SRSuntour-specific preprocessing tasks.
- Added import/assignment helpers for SRSuntour cylinder definitions.
- Added contact creation helpers based on assigned SRSuntour cylinder data.

## CalculiX Recovery and Mesh Cleanup

- Added a workflow to identify and delete elements related to negative/non-Jacobian failures after a failed CalculiX simulation.
- This supports recovery from failed solver runs by cleaning invalid mesh elements before rerunning or remeshing.

## Project Protection

- Added optional password/hash-key protection for `.pmx` project files.
- Added a protection prompt workflow for protected project files.
- Added protection handling during project open/save workflows.

## UI and Workflow Fixes

- Removed misleading red warning/status icons from Fatigue tree cards.
- Improved Fatigue tree refresh after setup changes and project load.
- Fixed fatigue setup project-context application when opening existing `.pmx` files containing results.
- Fixed load save/persistence behavior in the Fatigue tree.
- Added deterministic progress state handling for fatigue calculations.
- Fixed modal message box ownership when PrePoMax is minimized and closed from the taskbar/app bar, so save prompts are not hidden behind the main window.

## Build/Runtime Notes

- Debug builds still require runtime folders from an official PrePoMax release, such as `NetGen`, `Solver`, and related dependencies.
- `NetGenMesher.exe` is expected under `PrePoMax\bin\x64\Debug\NetGen\NetGenMesher.exe` for CAD assembly splitting/import workflows.
- Python CUDA acceleration requires a working CuPy installation matching the local CUDA runtime.
- The Python backend falls back to NumPy/CPU if CuPy cannot be imported.

## Known Scope

- This file is a high-level feature delta, not a full changelog.
- For upstream PrePoMax historical changes, see `Notes/Versions.txt`.
- For FATLAB-specific notes, see `FATLAB-nonlinear/README_Fatlab.md` and `FATLAB-nonlinear/readme.txt`.

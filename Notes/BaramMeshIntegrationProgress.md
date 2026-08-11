# BaramMesh and BaramFlow Integration Progress

## 2026-06-26

### Context Reviewed
- Read `PLAN.md`; the selected direction is native C# BaramMesh integration inside PrePoMax, not a Python bridge.
- Located BaramMesh reference code under `baram/baramMesh`.
- Characterized the current BaramMesh configuration schema from `baram/baramMesh/db/configurations_schema.py`.
- Confirmed the current BaramMesh configuration version is `6`.
- Confirmed the workflow steps are `None`, `Geometry`, `Region`, `Base Grid`, `Castellation`, `Snap`, `Boundary Layer`, and `Export`.
- Confirmed primary OpenFOAM writer references are under `baram/baramMesh/openfoam/system` and `baram/baramMesh/openfoam/constant`.
- Confirmed PrePoMax already has a `CaeMesh.Meshing` namespace and process execution through `CaeJob.ExecutableJob`.

### Completed
- Started the native C# integration slice in `CaeMesh.Meshing.BaramMesh`.
- Added BaramMesh enums with OpenFOAM/Baram string conversion helpers.
- Added serializable BaramMesh project/configuration model classes with schema-version defaults aligned to BaramMesh configuration version `6`.
- Added service interfaces for OpenFOAM execution, case filesystem handling, dictionary writing, geometry export, and workflow orchestration.
- Added a first `BaramCaseFileSystem` implementation for deterministic OpenFOAM case paths and directory creation.
- Added a deterministic `OpenFoamDictionaryWriter` foundation for OpenFOAM dictionary text generation.
- Added a first native `blockMeshDict` builder for the Base Grid stage, based on BaramMesh's Python `block_mesh_dict.py` behavior.
- Added nullable `BaramMeshProject` storage to `PrePoMax.Controller`, keeping BaramMesh outside `CaeModel.FeModel` so it can become a separate PrePoMax tab/workspace.
- Added PrePoMax meshing settings for the user-installed OpenFOAM root directory and default OpenFOAM processor count.
- Added `BaramOpenFoamRunner` validation/execution service in PrePoMax, implementing `IBaramOpenFoamRunner`.
- Added command-history aware `CCreateBaramMeshCase` and `CUpdateBaramMeshConfiguration` commands plus controller entry points.
- Added a first-class BaramMesh tab to the PrePoMax model tree as a separate workspace from Geometry, Model, Results, and Fatigue.
- Added `Debug|x64` and `Release|x64` build property groups to `UserControls.csproj` so the edited tab control can build through the PrePoMax x64 project-reference graph.
- Kept BaramMesh project state in `PrePoMax.Controller` rather than `CaeModel.FeModel`, matching the separate-tab integration direction.
- Added BaramMesh-specific view handling so switching to the BaramMesh tab does not query displayed-mesh, section-view, exploded-view, follower-view, or transformation state.
- Added BaramMesh as an explicit inactive/non-display view in edge visibility, section view, exploded view, and follower view state collections.
- Wired BaramMesh tree actions: `Case` prompts for a case directory and creates the case through command history; `Geometry`, `Base Grid`, `Castellation`, `Snap`, `Boundary Layer`, and `Export` update the stored BaramMesh workflow step through command history.
- Added `BaramBaseGridCaseWriter`, which creates the OpenFOAM case folders, writes `case/system/blockMeshDict` through the native dictionary writer, and creates `case/case.foam`.
- Added command-history aware `CWriteBaramMeshBaseGrid` plus controller entry points. The BaramMesh `Base Grid` tree node now writes `blockMeshDict` from current model geometry bounds, falling back to mesh bounds if geometry is unavailable.
- Added `BaramGeometryFactory` for the first native geometry-definition flow: seed the current PrePoMax model reference, create a Hex6 bounding volume with six boundary faces, and create a Plane surface.
- Added `FrmBaramMeshGeometry`, a first geometry editor opened immediately after BaramMesh case-folder creation and reopened from the BaramMesh `Geometry` card.
- Wired geometry edits through `CUpdateBaramMeshConfiguration`, preserving command-history behavior and keeping BaramMesh state outside `FeModel`.
- Updated Base Grid bounds generation so a selected/generated Hex6 geometry controls `blockMeshDict` bounds, matching the original BaramMesh initial procedure where the case is defined first and the bounding geometry follows.
- Added BaramMesh geometry source paths for the full PrePoMax model, individual PrePoMax geometry parts, and external STL files.
- Changed initial BaramMesh geometry seeding to split by current PrePoMax geometry parts when available. This lets STL files already imported and split by PrePoMax become separate BaramMesh geometry entries.
- Added `Add PrePoMax parts` and `Import STL` actions to `FrmBaramMeshGeometry`.
- Added command-history aware `CExportBaramMeshGeometry` and controller export logic that writes relevant geometry to `case/constant/triSurface`.
- Reused PrePoMax's existing VTK actor triangle extraction for geometry-part STL export, copied external STL inputs, and generated two-triangle STL files for Plane geometry definitions.
- Added a new serializable `ModelWorkflow` property (`FEA` or `CFD`) to model properties, defaulting old files and old commands to `FEA`.
- Added an explicit FEA/CFD workflow selector to the New Model dialog and wired it through `CSetNewModelProperties`.
- Added a BaramMesh feature-angle STL splitter engine that segments STL triangles by feature angle, supports minimum-area assimilation into neighboring segments, and writes assigned segment groups as STL files.
- Added `FrmBaramMeshStlSplit`, a Split STL dialog with Feature Angle, Min Area, editable segment names, `Assign`, and `Assign Remaining` controls matching the original BaramMesh assignment flow.
- Wired `Split STL` into `FrmBaramMeshGeometry`; assigned split outputs are stored under the BaramMesh project `geometry\split` folder and added as separate triSurface geometry definitions.
- Changed empty/new BaramMesh cases to use a fallback unit bounds box for geometry editing and base-grid initialization, so case creation no longer reports missing model geometry/mesh bounds before the user imports STL or creates a Hex6 volume.
- Changed the BaramMesh tab redraw path to use a dedicated BaramMesh renderer pass. It shows normal PrePoMax geometry plus external STL geometry stored in the BaramMesh configuration, including feature-angle split outputs.
- Removed the embedded split-dialog VTK preview after it proved unreliable in the modal tool context; split/imported STL surfaces now render back in the main BaramMesh workspace after the geometry dialog closes.
- Extended BaramMesh redraw coverage so form-level open/redraw paths call the BaramMesh renderer, and the BaramMesh renderer falls back to model mesh parts when no geometry parts are available.
- Changed feature-angle STL splitting so the split dialog only creates one STL row per detected segment. Final surface assignment/grouping was moved to the BaramMesh geometry editor.
- Added `Assign Surface` and `Assign Remaining` actions to the BaramMesh geometry editor. These merge selected or remaining split STL segment rows into named grouped STL files under `geometry\assigned` and replace the segment rows with the assigned surface row.
- Fixed the BaramMesh tab switch redraw for STL geometry imported through the normal Geometry workspace. The controller now performs a second BaramMesh redraw after the VTK view is re-enabled from the `Changing view` state and uses an isolated non-pickable BaramMesh geometry preview path instead of reusing Geometry-tab actor visibility behavior.
- Updated the BaramMesh draw path to force a camera reset whenever actors are drawn and to write draw diagnostics to the output window, including geometry part count, mesh part count, external STL entry count, and actor count.
- Fixed the actual BaramMesh visibility blocker in `FrmMain.SetMenuAndToolStripVisibility`: the BaramMesh branch was explicitly setting `_vtk.Visible = false`, hiding successfully drawn actors. The BaramMesh branch now leaves the VTK control visible and enables the view toolbar.
- Revised the BaramMesh case creation workflow so selecting the case folder still opens the Geometry dialog, but empty cases are no longer auto-seeded from the current PrePoMax model.
- Simplified the BaramMesh Geometry dialog to the current intended entry points: `Add PrePoMax part` remains as a placeholder for later implementation, and `Import STL` now always launches the feature-angle STL split flow before adding geometry entries.
- Removed the modal assignment workflow from the active Geometry dialog surface. Split STL outputs are now added as individual BaramMesh surface geometry definitions and are confirmed by closing the dialog.
- Added dynamic BaramMesh surface group nodes under the BaramMesh tree `Geometry` parent. The tree count updates from the stored BaramMesh configuration and is refreshed after case creation, geometry edits, renames, and tree regeneration.
- Added BaramMesh-only surface renaming from the tree. Renaming a surface group updates the BaramMesh configuration through command history, re-exports the geometry, refreshes the BaramMesh tree, and redraws the BaramMesh workspace.
- Added visible vertex markers for BaramMesh external STL surfaces in the main BaramMesh viewer. STL coordinates are deduplicated while building the actor data so shared corners are shown as one vertex marker.
- Added BaramMesh tree selection highlighting for surface groups. Selecting one or more child nodes under `Geometry` clears the normal selection layer and draws the selected surfaces' STL vertices in the highlight color.
- Added BaramMesh STL boundary-edge extraction for split/imported surface groups. The BaramMesh viewer now draws patch edge lines and larger edge-endpoint vertex markers, and selected surface groups highlight both their boundary edges and boundary edge vertices.
- Added an explicit all-triangle-edge STL mesh overlay for BaramMesh surface groups. This makes the imported/split STL mesh visible even when the STL surface is closed/manifold and therefore has no free boundary edges.
- Added BaramMesh tree context actions for surface assignment. Right-clicking a surface group under `Geometry` now exposes `Edit` plus `Assign -> Inlet/Outlet/Symmetry/Wall`, which renames selected surfaces to unique boundary names and keeps them as CFD boundary surfaces.
- Added BaramMesh `Geometry` parent context actions to assign every not-yet-assigned surface as a wall and to group all wall surfaces into a single merged `wall` STL entry under `geometry\assigned`.
- Added `FrmBaramMeshBaseGrid`, opened from the BaramMesh `Base Grid` tree card, with editable Hex6 bounds and X/Y/Z cell counts.
- Added `BaramGeometryFactory.SetBaseGridBounds` so repeated Base Grid edits update the existing hidden Hex6 bounding volume instead of creating duplicate boxes.
- Extended BaramMesh default bounds discovery to include external STL surfaces stored in the BaramMesh configuration, so BaramMesh-only STL imports can initialize a meaningful base grid.
- The Base Grid OK flow now updates the BaramMesh configuration through command history, writes `system/blockMeshDict`, refreshes the BaramMesh geometry tree, and redraws the BaramMesh workspace.
- Base-grid helper Hex6 surfaces are filtered out of the BaramMesh `Geometry` tree and skipped by surface assignment/grouping actions.
- Base-grid helper Hex6 entries are also hidden from the BaramMesh Geometry dialog while being preserved through later geometry edits.
- Replaced the Base Grid viewer outline with an exact C3D8 preview mesh for reasonable grid sizes. The BaramMesh viewer now draws the generated hexahedral cells plus an explicit grid-line overlay so all base-grid divisions are visible in context with STL surfaces.
- Added a temporary CalculiX/Abaqus `.inp` artifact for the Base Grid C3D8 preview under `%TEMP%\PrePoMax\BaramMesh\barammesh_base_grid_preview_<pid>.inp`; it is regenerated only when the base-grid bounds or cell counts change and is not imported into the FE model.
- Added a preview safety cap for very dense base grids. If the generated C3D8 preview would exceed the cap, PrePoMax falls back to the bounding outline and writes a BaramMesh preview warning to the output window.
- Added the first native Castellation step implementation:
  - `FrmBaramMeshCastellation` opens from the BaramMesh `Castellation` tree card.
  - Global controls are editable for feature angle, cells between refinement levels, max local/global cells, min refinement cells, max load unbalance, and free-standing zone faces.
  - Surface/feature refinement is assigned per BaramMesh surface group with enabled, minimum level, maximum level, and feature-edge refinement level columns.
  - Enabled surfaces are stored as one-to-one castellation groups on the corresponding BaramMesh geometry entries; disabling a row clears that surface refinement without changing its CFD boundary assignment.
- Added `BaramSnappyHexMeshDictBuilder` and `BaramCastellationCaseWriter` for the castellation-stage `system/snappyHexMeshDict` with geometry, castellated mesh controls, refinement surfaces, feature entries, snap controls, add-layer defaults, mesh quality controls, and merge tolerance.
- Added `CWriteBaramMeshCastellation` and controller wiring so accepting the Castellation dialog updates BaramMesh configuration through command history, re-exports STL geometry, writes feature OBJ edge files for refined STL surfaces, writes `snappyHexMeshDict`, refreshes the tree, and redraws BaramMesh.
- Added `BaramOpenFoamCaseControlsWriter`, which writes minimal `system/controlDict`, `system/fvSchemes`, and `system/fvSolution` files into BaramMesh cases so OpenFOAM utilities have the case context they require.
- Extended `BaramOpenFoamRunner` to launch `.bat`/`.cmd` OpenFOAM wrappers through `cmd.exe`, while still supporting direct `.exe` utilities.
- Wired explicit OpenFOAM execution from the BaramMesh `Base Grid` and `Castellation` dialogs via a new `Run` button. `OK` still only saves/writes dictionaries.
- Wired left double-click on the BaramMesh `Castellation` tree node to write the current `snappyHexMeshDict` and run the Castellation OpenFOAM step directly. Enter/context-menu edit behavior remains unchanged.
- Corrected the Castellation tree action route so the generic BaramMesh action handler also runs Castellation instead of only updating the stored workflow step. This covers the path observed in the output log.
- Fixed the Settings dialog crash caused by `ViewMeshingSettings` exposing OpenFOAM properties before its backing `MeshingSettings` field was initialized by the settings property-grid descriptor.
- Hardened `MeshingSettings.OpenFoamRootDirectory` so an unset OpenFOAM path is represented as an empty string rather than passing null through path conversion.
- Relaxed BaramMesh OpenFOAM execution so an empty OpenFOAM root setting can still run if OpenFOAM utilities resolve from `PATH`; otherwise the error now instructs the user to set Settings -> Meshing -> OpenFOAM root directory.
- Isolated BaramMesh base-grid preview drawing failures so STL geometry rendering continues even if the generated C3D8 base-grid preview cannot be drawn.
- Changed the visible Base Grid preview to render the generated grid-line actor only. The previous filled C3D8 actor used `VTK_HEXAHEDRON` through the default `vtkMaxActor` poly-data path, which throws `NotSupportedException`; the temporary C3D8 `.inp` artifact is still generated.
- Extended OpenFOAM utility resolution and execution PATH construction for Windows distributions:
  - searches OpenFOAM `bin`, `lib`, `lib\msmpi`, `lib\sys-openmpi`, `lib\dummy`, sibling `mingw64\bin/lib`, and `platforms\...\bin/lib`;
  - prepends the resolved utility directory and those OpenFOAM library directories to PATH before launching;
  - infers `WM_PROJECT_DIR` from a resolved utility path when the configured root is empty;
  - adds a clearer diagnostic for Windows exit code `-1073741515` / `0xC0000135` indicating a missing runtime DLL.
- Added controller execution for:
  - Base Grid: `blockMesh`, then `checkMesh -allRegions -writeFields (cellAspectRatio cellVolume nonOrthoAngle skewness) -time 0`.
  - Castellation: auto-runs `blockMesh` first if `constant/polyMesh/points` is missing, then runs `snappyHexMesh`, then `checkMesh ... -time 1`.
- Wired the PrePoMax cancel button to terminate the active BaramMesh OpenFOAM process.
- Fixed castellation feature-file generation so `snappyHexMeshDict` only references feature OBJ files for surfaces with a positive feature-edge refinement level, matching the OBJ exporter behavior.
- Hardened feature OBJ generation to recreate the source triSurface STL before writing the OBJ if the STL is unexpectedly missing from the case directory.
- Added display-only OpenFOAM `polyMesh` visualization in the BaramMesh viewer. The primary path uses the bundled VTK OpenFOAM reader to load the latest numeric-time `internalMesh` as a real unstructured volume-grid actor with exterior cell edges and section-view support. The existing ASCII boundary-face reader remains as a fallback and can also display `constant/polyMesh`.
- Made a completed numeric-time Castellation mesh the primary BaramMesh scene. Once available, the viewer suppresses the editable STL/model and Base Grid preview actors that could mask the generated mesh, then renders the castellated boundary faces with their mesh-edge overlay.
- Added post-run Castellation output validation. The workflow now verifies that `snappyHexMesh` produced a numeric-time `polyMesh` with drawable boundary faces before logging completion, reports the selected mesh path and face count, and explicitly redraws the BaramMesh viewer after the OpenFOAM task returns.
- Extended the Castellation editor with separate Surface Refinement and Volume Refinement tabs. Surface rows now expose min/max levels, feature level, and curvature refinement options. Volume rows now expose volume level, gap refinement settings, and level-increment/split settings.
- Reworked Castellation refinement editing to follow the original BaramMesh group model. Surface and volume refinements are now named groups that can be assigned to multiple geometry entries, edited by double-click, and removed without deleting their geometry.
- Added the original surface-refinement controls: minimum/maximum surface level, feature-edge level, curvature cells-per-radius, curvature maximum level, sharp-surface exclusion, and minimum radius. Refinement levels show their physical cell sizes from the active Base Grid.
- Added searchable Box, Sphere, and Cylinder refinement-volume creation/editing to the BaramMesh Geometry step, including wireframe visualization in the main BaramMesh 3D viewer.
- Added the original volume-refinement controls: volume level, gap minimum layers/start/max/direction/self detection, and directional level increment with X/Y/Z split counts. Volume levels also show their physical cell size.
- Aligned `snappyHexMeshDict` refinement generation with the Python BaramMesh implementation, including shared group settings, `curvatureLevel`, `gapLevel`, `gapMode`, `gapSelf`, and `levelIncrement` entries.
- Fixed the Castellation tree command routing so context-menu `Edit` and Enter open the Castellation/refinement parameters, while the separate double-click run event continues to execute Castellation.
- Added `FrmBaramMeshSnap`, opened from the BaramMesh `Snap` tree card, with the original surface/internal smoothing, mesh displacement and global relaxation, feature snapping type/iterations, multi-surface feature snap, tolerance, concave angle, and minimum area ratio controls.
- Added Snap buffer-layer controls for Laplacian or GETMe point smoothing, point-smoother iterations, GETMe transformation parameter, and multi-surface assignment. Interface surfaces are retained as mandatory buffer-layer selections, matching the original BaramMesh workflow.
- Added `BaramSnapCaseWriter` and Snap-mode `snappyHexMeshDict` generation. The dictionary now emits `castellatedMesh false`, `snap true`, explicit or implicit feature controls, level-0 feature OBJ entries, per-surface `addBufferLayers`, and the `castellatedBufferLayer` displacement smoother configuration when enabled.
- Wired Snap through PrePoMax command history and tree actions. Context-menu `Edit` and Enter open the Snap editor; the form `Run` button and Snap tree double-click write the dictionary and execute the stage.
- Wired single-region Snap OpenFOAM execution from the time-1 Castellation mesh to the time-2 snapped mesh. Rerunning Snap removes stale later numeric-time outputs, selects `latestTime` in `controlDict`, runs `snappyHexMesh` and `checkMesh`, validates the time-2 `polyMesh`, and redraws it through the existing latest-time VTK viewer.
- Added an explicit guard for multi-region Snap until the required `topoSet` stage and dictionary are integrated, preventing a silently incorrect multi-region result.
- Added `FrmBaramMeshExport`, opened from the BaramMesh `Export` tree card, with a BaramFlow `.bf` project destination selector and validation that the destination is new or empty.
- Added command-history wiring for the Export workflow and a native `BaramMeshExportWriter`.
- Implemented the original single-region 3D export path: select the time-3 boundary-layer mesh when present, otherwise the time-2 Snap mesh, and copy its OpenFOAM `polyMesh` without parsing or rewriting binary data.
- Added the BaramFlow case scaffold under the selected `.bf` directory: `case/case.foam`, `case/system/controlDict`, `fvSchemes`, `fvSolution`, `case/constant/polyMesh`, `case/constant/triSurface`, and `case/boundaryConditions`.
- Excluded transient `polyMesh/sets` from the exported solver case and guarded source/destination path overlap.
- Added explicit export guards for multi-region meshes, cell zones, and conformal interfaces until their required `splitMeshRegions`, `topoSet`, and `createPatch` transformations are integrated.

### Verification
- The new `CaeMesh/Meshing/BaramMesh` C# files compile cleanly as an isolated C# 7.3 library using Visual Studio 2022 Roslyn.
- Runtime smoke-tested the `BaramBlockMeshDictBuilder` plus `OpenFoamDictionaryWriter`; output contains a `FoamFile` header, padded default bounds, six default patches, and default `(10 10 10)` base-grid cells.
- `CaeMesh.csproj`, `CaeModel.csproj`, and `PrePoMax.csproj` compile in Debug/x64 with `BuildProjectReferences=false` after copying existing debug binaries to the project-reference output paths expected by MSBuild.
- Corrected `GmshCommon/GmshCommon.vcxproj` to use `$(MSBuildProjectDirectory)` for the bundled gmsh SDK include/lib folders; the previous `$(SolutionDir)\GmshCommon\...` path resolved to `GmshCommon\GmshCommon\...` when built through project references.
- Smoke-tested BinaryFormatter serialization/deserialization of the BaramMesh project model; deserialized configuration version is `6` and working step is `Geometry`.
- `PrePoMax.csproj` builds successfully in `Debug|x64` with project references enabled.
- The latest `Debug|x64` build after the BaramMesh tab crash fix and tree-action wiring completed with 0 warnings and 0 errors.
- After the edge-visibility fix, `CoreCompile` completed with 0 warnings and 0 errors. A full Debug/x64 build reached output copy but could not replace `bin\x64\Debug\PrePoMax.exe` because the currently running PrePoMax process locked it.
- `CaeMesh.csproj` builds successfully in `Debug|x64` after adding `BaramBaseGridCaseWriter`; remaining warnings are existing project warnings.
- `PrePoMax.csproj` builds successfully in `Debug|x64` when output is redirected to a temporary build folder to avoid the running PrePoMax executable lock; remaining warnings are existing project warnings.
- `CaeMesh.csproj` builds successfully in `Debug|x64` after adding the geometry factory and Hex6-aware base-grid bounds; remaining warnings are existing project warnings.
- `PrePoMax.csproj` builds successfully in `Debug|x64` with output redirected to `BuildCheck\PrePoMax` after wiring the geometry editor; remaining warnings are existing project warnings.
- `PrePoMax.csproj` also builds successfully to the normal `PrePoMax\bin\x64\Debug\PrePoMax.exe` output after confirming no running PrePoMax process was locking the executable; remaining warnings are existing project warnings.
- `CaeModel.csproj` builds successfully in `Debug|x64` after adding `ModelWorkflow`; remaining warnings are existing project warnings.
- `PrePoMax.csproj` builds successfully in `Debug|x64` with output redirected to `BuildCheck\PrePoMax` after adding triSurface geometry export/import source handling and the New Model FEA/CFD selector; remaining warnings are existing project warnings.
- `PrePoMax.csproj` builds successfully to the normal `PrePoMax\bin\x64\Debug\PrePoMax.exe` output after the running PrePoMax process exited; remaining warnings are existing project warnings.
- `PrePoMax.csproj` builds successfully in `Debug|x64` with output redirected to `BuildCheck\PrePoMax` after adding the feature-angle STL splitter and direct STL project reference; remaining warnings are existing project warnings.
- `PrePoMax.csproj` builds successfully to the normal `PrePoMax\bin\x64\Debug\PrePoMax.exe` output after adding the splitter; remaining warnings are existing project warnings.
- `PrePoMax.csproj` builds successfully to the normal `PrePoMax\bin\x64\Debug\PrePoMax.exe` output after the empty-case bounds fallback, dedicated BaramMesh STL redraw path, and removal of the modal split-dialog VTK preview; remaining warnings are existing project warnings.
- `PrePoMax.csproj` `CoreCompile` passes after moving split assignment/grouping into the BaramMesh geometry editor and extending BaramMesh viewer redraw. Full executable copy is currently blocked by a running `PrePoMax.exe` process locking `bin\x64\Debug\PrePoMax.exe`; the full build reaches `PrePoMax.exe` before the lock/post-build packaging failure.
- `PrePoMax.csproj` builds successfully to the normal `PrePoMax\bin\x64\Debug\PrePoMax.exe` output after the BaramMesh tab-switch redraw fix for Geometry-workspace STL imports; remaining warnings are existing project warnings.
- `PrePoMax.csproj` builds successfully to the normal `PrePoMax\bin\x64\Debug\PrePoMax.exe` output after adding forced BaramMesh camera framing and draw diagnostics; remaining warnings are existing project warnings.
- `PrePoMax.csproj` builds successfully to the normal `PrePoMax\bin\x64\Debug\PrePoMax.exe` output after fixing the BaramMesh VTK visibility flag; remaining warnings are existing project warnings.
- `PrePoMax.csproj` builds successfully to the normal `PrePoMax\bin\x64\Debug\PrePoMax.exe` output after revising the STL import/split workflow and moving surface group naming to the BaramMesh tree; remaining warnings are existing project warnings.
- `PrePoMax.csproj` builds successfully to the normal `PrePoMax\bin\x64\Debug\PrePoMax.exe` output after adding BaramMesh STL vertex markers and selected-surface vertex highlighting; remaining warnings are existing project warnings.
- `PrePoMax.csproj` builds successfully to the normal `PrePoMax\bin\x64\Debug\PrePoMax.exe` output after adding BaramMesh STL boundary-edge and edge-vertex display; remaining warnings are existing project warnings.
- `PrePoMax.csproj` builds successfully to the normal `PrePoMax\bin\x64\Debug\PrePoMax.exe` output after adding the all-triangle-edge BaramMesh STL mesh overlay; remaining warnings are existing project warnings.
- `PrePoMax.csproj` builds successfully to the normal `PrePoMax\bin\x64\Debug\PrePoMax.exe` output after adding BaramMesh tree surface assignment and wall grouping context actions; remaining warnings are existing project warnings.
- After adding the Base Grid editor, Geometry-dialog filtering, and wireframe draw path, `PrePoMax.csproj` compiles and produces `BuildCheck\PrePoMax\PrePoMax.exe`. The normal build copy is currently blocked by running process `PrePoMax (68264)` locking `PrePoMax\bin\x64\Debug\PrePoMax.exe`; redirected-output builds still reach `PrePoMax.exe` before the existing post-build DLL move step fails on already-present files in the normal debug `lib` folder.
- `PrePoMax.csproj` builds successfully in `Debug|x64` with output redirected to `BuildCheck\PrePoMax` after replacing the Base Grid outline with a generated C3D8 preview mesh and temporary `.inp` writer; remaining warnings are existing project warnings.
- `PrePoMax.csproj` builds successfully in `Debug|x64` with output redirected to `BuildCheck\PrePoMax` after adding the Castellation editor, `snappyHexMeshDict` writer, command wiring, and feature OBJ generation; remaining warnings are existing project warnings.
- `PrePoMax.csproj` builds successfully in `Debug|x64` with output redirected to `BuildCheck\PrePoMax` after wiring OpenFOAM execution for Base Grid and Castellation; remaining warnings are existing project warnings.
- `PrePoMax.csproj` builds successfully in `Debug|x64` with output redirected to `BuildCheck\PrePoMax` after wiring Castellation tree double-click execution; remaining warnings are existing project warnings.
- `PrePoMax.csproj` builds successfully in `Debug|x64` with output redirected to `BuildCheck\PrePoMax` after correcting the Castellation action handler route; remaining warnings are existing project warnings.
- `PrePoMax.csproj` builds successfully in `Debug|x64` with output redirected to `BuildCheck\PrePoMax` after fixing the OpenFOAM settings crash and allowing PATH-based utility resolution; remaining warnings are existing project warnings.
- `PrePoMax.csproj` builds successfully in `Debug|x64` with output redirected to `BuildCheck\PrePoMax` after isolating base-grid draw warnings; remaining warnings are existing project warnings.
- `PrePoMax.csproj` builds successfully in `Debug|x64` with output redirected to `BuildCheck\PrePoMax` after switching visible Base Grid rendering to wire grid lines; remaining warnings are existing project warnings.
- `PrePoMax.csproj` builds successfully in `Debug|x64` with output redirected to `BuildCheck\PrePoMax` after extending Windows OpenFOAM PATH construction; remaining warnings are existing project warnings.
- `PrePoMax.csproj` builds successfully in `Debug|x64` with output redirected to `BuildCheck\PrePoMax` after aligning `snappyHexMeshDict` feature entries with generated `.obj` files; remaining warnings are existing project warnings.
- `PrePoMax.csproj` builds successfully to the normal `PrePoMax\bin\x64\Debug\PrePoMax.exe` output after aligning `snappyHexMeshDict` feature entries with generated `.obj` files; remaining warnings are existing project warnings.
- `PrePoMax.csproj` builds successfully to the normal `PrePoMax\bin\x64\Debug\PrePoMax.exe` output after adding BaramMesh OpenFOAM boundary polyMesh visualization; remaining warnings are existing project warnings.
- `PrePoMax.csproj` `CoreCompile` passes after adding Castellation surface and volume refinement editing. A normal Debug/x64 build reached executable copy but could not replace `PrePoMax\bin\x64\Debug\PrePoMax.exe` because running process `PrePoMax (8872)` locked it. A redirected-output build emitted `BuildCheck\PrePoMax\PrePoMax.exe` before the existing post-build DLL move target failed on already-present files in the normal debug `lib` folder.
- The normal Debug/x64 build compiles the Castellation-result display changes without source errors and produces `PrePoMax\obj\x64\Debug\PrePoMax.exe`; final copy to `bin\x64\Debug\PrePoMax.exe` is currently blocked by running processes `PrePoMax (11608)` and `PrePoMax (18668)`.
- Runtime-probed the bundled `vtkOpenFOAMReader` against the existing `pitzDailySteady` case: it loaded the `internalMesh` leaf as a `vtkUnstructuredGrid` with 25,012 points and 12,225 cells. A `vtkMaxActor` created from that grid retained all 12,225 cells after the reader was disposed. `PrePoMax.csproj /t:Compile` passes after placing the reader implementation in the VTK-owning `vtkControl` project.
- After the two running PrePoMax instances exited, the full `Debug|x64` build completed successfully and installed the Castellation volume-grid viewer in `PrePoMax\bin\x64\Debug\PrePoMax.exe`; remaining warnings are existing project warnings.
- `PrePoMax.csproj /t:Compile` passes after implementing named multi-geometry refinement groups, searchable refinement volumes, refinement-volume visualization, and the expanded validation rules.
- Runtime smoke-tested `BaramSnappyHexMeshDictBuilder` with one curved surface group and one searchable-box volume group. The generated dictionary contains the expected surface levels, curvature level, searchable volume, inside level, gap settings, and directional level increment.
- The full `Debug|x64` build completed successfully and installed the original-style refinement workflow in `PrePoMax\bin\x64\Debug\PrePoMax.exe`; remaining compiler warnings are existing project warnings.
- The full `Debug|x64` build completed successfully after separating Castellation Edit from Run and installed the corrected tree routing in the normal debug executable.
- `PrePoMax.csproj /t:Compile` passes after adding the Snap editor, command/writer wiring, Snap-mode dictionary generation, and time-2 execution path.
- Runtime smoke-tested Snap dictionary generation with enabled GETMe and Laplacian buffer layers, mandatory surface flags, level-0 feature OBJ entries, and disabled-buffer behavior. All expected original BaramMesh keys were present, and specialized solver controls were absent when disabled.
- Runtime-constructed and disposed `FrmBaramMeshSnap` from the installed PrePoMax executable with a default BaramMesh configuration, confirming that the new WinForms control tree initializes successfully.
- The full `Debug|x64` build completed successfully and installed the Snap workflow in `PrePoMax\bin\x64\Debug\PrePoMax.exe`; remaining compiler warnings are existing project warnings.
- Runtime smoke-tested the single-region export writer with a synthetic binary/ASCII `polyMesh`. The exported points hash matched the source, all required BaramFlow case files were created, `polyMesh/sets` was omitted, and a non-empty destination was rejected.
- Runtime-constructed and disposed `FrmBaramMeshExport` from the installed PrePoMax executable and verified that `CExportBaramMeshCase` and the controller export route are present.
- The full `Debug|x64` build completed successfully and installed the Export workflow in `PrePoMax\bin\x64\Debug\PrePoMax.exe`; remaining compiler warnings are existing project warnings.

### Current Scope
- This is scaffolding and core model work only.
- BaramMesh is exposed as a separate PrePoMax tab/workspace, not as part of the FE model tree.
- The BaramMesh tab currently supports case creation, initial geometry definition/editing, selecting the active BaramMesh workflow step, editing Base Grid bounds/cell counts, writing and running the Base Grid `blockMeshDict`, editing/running Castellation with surface and volume refinements, editing/running single-region Snap including buffer layers, visualizing the latest generated OpenFOAM volume `polyMesh`, and exporting the completed single-region 3D mesh as a BaramFlow `.bf` project.
- The Geometry editor currently supports case-local STL import through mandatory feature-angle splitting, then confirms the resulting surface groups as BaramMesh geometry definitions. Surface group naming is now handled from the BaramMesh tree under `Geometry`.
- No BaramMesh edit form is wired yet for boundary layer settings.
- OpenFOAM execution is wired for Base Grid, Castellation, and single-region Snap.
- Export currently supports single-region 3D cases without cell-zone or conformal-interface post-processing. Multi-region, cell-zone, conformal-interface, and 2D export paths remain guarded until their OpenFOAM transformation stages are integrated.
- No Python runtime dependency has been added.

### Next Work
- Add golden dictionary fixtures from the Python BaramMesh reference.
- Add golden tests for `blockMeshDict` and tune formatting until it matches BaramMesh/PyFoam output after agreed normalization.
- Implement `Add PrePoMax part` in the revised Geometry dialog.
- Add the BaramMesh boundary-layer editor, dictionary generation, execution, and time-3 result handling.
- Extend Export with `splitMeshRegions`, `topoSet`, conformal-interface `createPatch`, and 2D plane/wedge conversion.
- Smoke-test OpenFOAM execution against the user's installed OpenFOAM distribution and tune command arguments/environment if the distribution needs a specific shell initialization script.
- Add patch-specific coloring and tree visibility controls for generated OpenFOAM meshes if needed; the current result is one display-only volume actor and is not imported into the FE model.

## 2026-07-28

### BaramFlow Workspace Completed
- Reviewed the original BaramFlow navigator and reproduced its full workflow hierarchy in PrePoMax: `Case`; `Setup` with General, Models, Materials, Cell Zone Conditions, Boundary Conditions, Dynamic Mesh, and Reference Values; `Solution` with Numerical Conditions, Monitors, Initialization, Run Conditions, Run, and Reduced Order Model; and `Results` with Scaffolds, Graphics, and Reports.
- Added BaramFlow as a separate sixth PrePoMax workspace tab, parallel to BaramMesh and outside `CaeModel.FeModel`.
- Added the serializable `BaramFlowProject` and `BaramFlowSection` model under `CaeModel.CFD.BaramFlow`. A project is rooted at a `.bf` directory and validates the required `case/constant/polyMesh` files before it is attached.
- Added command-history routes for opening a BaramFlow project and changing the active BaramFlow section.
- Wired the BaramFlow `Case` node to select an existing `.bf` project directory. The current project and selected section persist in the PrePoMax project data.
- Changed BaramMesh Export to attach the newly exported `.bf` project automatically, allowing the user to switch directly from BaramMesh to BaramFlow.
- Added a dedicated BaramFlow 3D draw path. It displays the exported OpenFOAM volume mesh through the existing VTK OpenFOAM reader and falls back to the existing ASCII boundary-face and mesh-edge renderer when a volume actor cannot be produced.
- Added BaramFlow handling to current-view switching, menu/toolbar visibility, edge visibility, section view, exploded view, follower view, redraw dispatch, selection clearing, tree regeneration, and tree expand/collapse persistence.
- Kept older PrePoMax project tree-state data compatible by accepting the previous five-tree state array while saving the new six-tree state array.
- Characterized the original BaramFlow General page, General database defaults, case-wizard solver constraints, solver selection, `controlDict`, gravity, and operating-condition writers.
- Added serializable General settings for pressure-based/density-based solver type, incompressible/compressible flow, steady/transient time mode, gravity direction, and operating pressure.
- Added a native BaramFlow General editor opened from the `General` workflow leaf. Density-based mode follows the original case-wizard constraint by forcing compressible flow and disabling/zeroing gravity and operating pressure.
- Added command-history-aware General updates and project-local persistence in `<project>.bf\prepomax-baramflow.xml`, while retaining the same settings inside the serialized PrePoMax controller.
- Added native General case generation for `case/system/controlDict`, `case/system/fvSchemes`, `case/constant/g`, and `case/constant/operatingConditions`, seeded with the original BaramFlow run-condition defaults until the dedicated Run Conditions section takes ownership.
- Added original single-region solver selection for the implemented scope: `buoyantSimpleNFoam`/`buoyantPimpleNFoam` for pressure-based steady/transient cases and `TSLAeroFoam`/`UTSLAeroFoam` for density-based steady/transient cases.
- Changed BaramMesh Export to initialize the exported `.bf` project with the default pressure-based, incompressible, steady General configuration and its OpenFOAM files.

### Verification
- `PrePoMax.csproj /t:Compile` succeeds in `Debug|x64`; remaining compiler warnings are existing project warnings.
- The full `Debug|x64` build succeeds and installs the workspace in `PrePoMax\bin\x64\Debug\PrePoMax.exe`.
- Runtime-constructed the installed `UserControls.ModelTree` and verified six tabs, four BaramFlow root groups, seventeen actionable BaramFlow leaf nodes, and six tree-state slots.
- Runtime-opened a synthetic `.bf` project with a complete `case/constant/polyMesh` and successfully round-tripped the `BaramFlowProject` through BinaryFormatter serialization. An incomplete project is rejected by validation.
- The bundled VTK OpenFOAM reader was already runtime-probed against an existing OpenFOAM case and loaded a 12,225-cell unstructured volume grid, which is the primary renderer now reused by BaramFlow.
- `CaeModel.csproj /t:Compile` and `PrePoMax.csproj /t:Compile` succeed after adding the General model, writer, command, and form; remaining warnings are existing project warnings.
- The full `Debug|x64` build succeeds and installs the General workflow in `PrePoMax\bin\x64\Debug\PrePoMax.exe`.
- Runtime-constructed and disposed the installed `FrmBaramFlowGeneral` for both solver types. The density-based form correctly locks flow type, gravity, and operating pressure.
- Off-screen rendered the installed General form at its intended dialog size and verified that all controls, values, labels, and buttons are visible without overlap or clipping.
- Runtime-tested pressure-based transient output: `buoyantPimpleNFoam`, `Euler`, gravity `(0 -9.81 0)`, operating pressure `101325`, end time `10`, time step `0.01`, `writeControl runTime`, and binary field output.
- Runtime-tested density-based steady output: `TSLAeroFoam`, `steadyState`, zero gravity, zero operating pressure, project-local XML reload, and BinaryFormatter round-trip.

### Current Scope
- The BaramFlow workspace foundation, project attachment, persistence, navigation, and mesh visualization are operational.
- BaramMesh single-region Export creates and automatically attaches the `.bf` project consumed by this workspace.
- The `General` leaf now edits and persists solver type, flow type, time behavior, gravity, and operating pressure and writes the corresponding initial OpenFOAM dictionaries.
- Other workflow leaves currently record the active BaramFlow section but do not yet have solver configuration forms.
- Project-local General persistence is native PrePoMax XML. Importing the original BaramFlow `configuration.h5` is not part of this slice.
- The displayed solver mesh remains a display-only VTK actor and is not inserted into the FE model.

### Next Work
- Parse the exported OpenFOAM boundary dictionary and populate editable Boundary Conditions entries from the actual mesh patches.
- Implement Models and Materials state before Boundary Conditions so available fields and wall/inlet thermal options are defined consistently.
- Implement Cell Zone Conditions and Boundary Conditions forms before adding numerical controls and solver execution.
- Add BaramFlow dictionary writers and golden comparisons against the original project before wiring OpenFOAM solve execution.

## 2026-08-03

### BaramMesh FSI Interface Definition
- Added a persistent `BaramBoundaryRole` to BaramMesh surface geometry instead of relying only on inlet/outlet/wall name prefixes. Existing serialized configurations remain compatible because the optional field defaults to `Unassigned`.
- Added `Assign > FSI Interface` to the BaramMesh Geometry tree context menu. Assignment creates a uniquely named `fsi_interface` patch that can be renamed to the physical interface name, such as `shim_primary`, without losing its role.
- FSI surfaces are labeled with `[FSI Interface]` in the Geometry tree and use a distinct red surface color in the BaramMesh viewer.
- FSI interfaces are excluded from `Assign Remaining as Walls` and `Group Walls`, so bulk wall operations cannot silently consume a coupling surface.
- The generated `snappyHexMeshDict` emits both Wall and FSI Interface roles as OpenFOAM `wall` patch types. Inlet, outlet, and unassigned boundary surfaces remain generic `patch` types.
- BaramMesh Export writes `case/constant/fsiInterfaces`, listing the final OpenFOAM patch names selected for FSI. This file is coupling metadata for the future BaramFlow pressure export and FEA displacement-return stages and does not alter OpenFOAM execution.

### Verification
- `PrePoMax.csproj` builds successfully in `Debug|x64` with output redirected to `BuildCheck\FsiInterface` because two running PrePoMax processes lock the normal debug executable. Remaining compiler warnings are existing project warnings.
- Runtime-tested an `FsiInterface` geometry named `shim_primary`: configuration validation succeeds, generated castellation controls contain `type wall`, and single-region export writes `shim_primary` to `case/constant/fsiInterfaces`.
- Runtime-constructed the isolated-build `ModelTree` and verified the `Assign > FSI Interface` item and its `fsi_interface` assignment token.
- BinaryFormatter round-trip testing preserves the `FsiInterface` role on serialized BaramMesh geometry.

### FSI Scope Boundary
- This phase identifies and preserves the CFD/structure coupling patch. It does not yet map CFD face pressure to an FEA surface, run CalculiX, transfer structural displacement back to CFD points, or control the outer convergence loop.
- The next coupling phases should consume the exported FSI patch list, define the matching PrePoMax/FEA surface, establish conservative pressure mapping, and add displacement-based mesh motion with pressure and displacement convergence criteria.

## 2026-08-04

### BaramFlow Models Completed
- Characterized the original BaramFlow Models page, turbulence database defaults, energy-model constraint, and `constant/turbulenceProperties` generation.
- Added serializable Models state for energy and the original turbulence families: Inviscid, Laminar, Spalart-Allmaras, k-epsilon, k-omega SST, Transition SST, DES, and LES.
- Added k-epsilon standard/RNG/realizable variants, standard/enhanced near-wall treatment, DES RANS/shielding/length-scale options, LES subgrid/length-scale options, model coefficients, and turbulent transport numbers using the original BaramFlow defaults.
- Added a native Models editor opened from the BaramFlow `Models` tree leaf. It exposes only steady-compatible turbulence choices for steady cases, enables the relevant RANS/DES/LES controls, and forces the energy equation on for compressible flow.
- Added command-history-aware Models updates and extended project-local XML persistence to state version 2. Existing version-1 General-only state files continue to load with default Models settings.
- Added deterministic `constant/turbulenceProperties` generation for laminar, RAS, DES, and LES configurations, including Baram-specific model names and coefficient dictionaries.
- Added a normalized model-to-field contract for later boundary-condition generation. It resolves the active OpenFOAM fields (`p`, `U`, optional `T`/`alphat`, and the applicable `nut`, `k`, `epsilon`, `omega`, `nuTilda`, `gammaInt`, or `ReThetat` fields) without mutating saved Models state.
- BaramMesh Export now initializes `turbulenceProperties` when it creates and attaches a new BaramFlow `.bf` project.
- General updates validate the existing Models state before rewriting case files, preventing a steady-flow change from leaving an active DES/LES configuration partially updated.

### Verification
- The full `Debug|x64` build succeeds with output redirected to `BuildCheck\BaramFlowModels`. One running PrePoMax process still locks the normal debug executable, so that executable has not been replaced during this phase. Remaining compiler warnings are existing project warnings.
- Runtime-tested nine turbulence dictionary variants covering laminar, all implemented RANS mappings, realizable enhanced wall treatment, DES/DDES/IDDES, LES WALE, dynamic k-equation, and length-scale coefficient dictionaries.
- Runtime-tested the solver-field matrix for steady k-epsilon, compressible k-epsilon, Transition SST, DES with Spalart-Allmaras and k-omega SST, LES WALE and dynamic k-equation, and inviscid flow. Compressible field resolution also confirms that forced energy activation does not modify the saved settings object.
- Runtime-tested project-local XML version-2 persistence and BinaryFormatter round trips with LES/WALE Models state.
- Runtime-constructed the Models form for steady and transient/compressible configurations. Steady mode exposes six turbulence choices, transient mode exposes all eight, and compressible flow locks the enabled energy equation.
- Off-screen rendered the Models dialog and verified that its RANS, DES, and LES controls fit without overlap or clipping; enum choices use readable engineering labels.

### BaramFlow Materials Completed
- Added a serializable single-region fluid material with gas/liquid phase, material name, density treatment, density, dynamic viscosity, specific heat, thermal conductivity, and molecular weight.
- Matched the original BaramFlow air and liquid-water defaults. The editor provides phase-specific defaults and uses locale-safe numeric controls for small transport values.
- Added automatic density-model normalization: incompressible fluids and compressible liquids use constant density, while compressible gases use the ideal-gas equation of state.
- Added deterministic `constant/thermophysicalProperties` generation using `heRhoThermo/rhoConst` or `hePsiThermo/perfectGas`, constant `hConst` thermodynamics, and constant transport. The Prandtl number is calculated from `Cp * mu / k`; inviscid models emit zero viscosity and BaramFlow's fallback Prandtl number of 0.7.
- Added the native Material editor to the BaramFlow `Materials` tree leaf, command-history-aware updates, export-time initialization, and automatic thermophysical regeneration after compatible General or Models changes.
- Extended project-local XML persistence to state version 3. Version-1/2 projects without material data reopen with normalized default air, and BinaryFormatter compatibility initializes optional material state after deserialization.

### Materials Verification
- The full `Debug|x64` build succeeds both in `BuildCheck\BaramFlowMaterial` and the normal `PrePoMax\bin\x64\Debug` output.
- Runtime-tested thermophysical dictionaries for incompressible air, compressible ideal-gas air, incompressible water, inviscid transport, and density-based solver `Tref` output, plus rejection of invalid viscosity.
- Runtime-tested version-2 XML migration, version-3 XML material round trips, and BinaryFormatter round trips.
- Off-screen rendered the final Material dialog and verified compressible ideal-gas density locking, exact small-value input precision, locale-safe formatting, and unclipped controls.

### Current BaramFlow Scope
- `General`, `Models`, and single-region `Materials` are operational and write their case dictionaries. Cell zones, boundary conditions, solution controls, execution, and post-processing remain pending.
- Energy is persisted and constrained, and the material thermophysical properties are generated. Initial and boundary field files remain part of the Boundary Conditions phase.
- Multiphase, species transport, user-defined scalars, and discrete-phase models remain outside the current single-region scope because they require mixture and additional-field infrastructure.

### Next Work
- Parse `case/constant/polyMesh/boundary` and `constant/fsiInterfaces` into boundary records, then generate initial and patch entries only for fields required by the selected Models configuration.

### BaramFlow Boundary Conditions Completed
- Added a native parser for `case/constant/polyMesh/boundary`. Patch names, OpenFOAM mesh types, face counts, and start-face addressing are read from the exported mesh instead of duplicated in the PrePoMax model.
- Boundary roles are inferred for inlet, pressure outlet, wall, and symmetry patches. `constant/fsiInterfaces` takes precedence over name/type inference and preserves BaramMesh FSI assignments as protected `FsiInterface` records.
- Added persistent initial-field values and per-patch boundary settings for velocity, gauge pressure, temperature, turbulence intensity, and turbulent viscosity ratio. Refreshing the mesh metadata preserves user-entered values for patches whose names still exist.
- Added the native Boundary Conditions editor to the BaramFlow tree and command history. Mesh type and face count are read-only; the physical boundary role and applicable field values are editable. FSI roles can only originate from BaramMesh coupling metadata.
- Extended project-local XML persistence to state version 4. Older projects initialize boundary settings by parsing the current mesh, while version-4 projects preserve edited values and refresh authoritative face metadata.
- Added deterministic OpenFOAM initial and boundary field generation under `case/0`. Only fields required by the active General and Models configuration are emitted, and stale known fields are removed after model changes.
- Implemented field generation for `p`, `U`, `T`, `nut`, `alphat`, `k`, `epsilon`, `omega`, `nuTilda`, `gammaInt`, and `ReThetat`, including inlet turbulence conditions, pressure-outlet conditions, symmetry conditions, turbulence wall functions, and no-slip/adiabatic wall treatment.
- FSI interfaces currently emit the same CFD-side no-slip, adiabatic wall conditions as ordinary walls. Their separate role remains available for the later pressure-export and structural-displacement coupling stages.
- General, Models, and Material edits now regenerate compatible active fields so solver-family, energy, operating-pressure, density, viscosity, and turbulent-Prandtl changes cannot leave stale boundary files.

### Boundary Verification
- The full `Debug|x64` build succeeds both in `BuildCheck\BaramFlowBoundary` and the normal `PrePoMax\bin\x64\Debug` output; remaining compiler warnings are existing project warnings.
- Runtime-tested boundary parsing and role inference with inlet, outlet, wall/FSI, and symmetry patches, including comment handling, face-address refresh, and preservation of edited values.
- Runtime-tested version-4 XML persistence and reopening, default k-epsilon output, compressible Transition SST output, stale-field removal, OpenFOAM field classes, and representative patch dictionary types.
- Generated every required field for inviscid, laminar, Spalart-Allmaras, k-epsilon, k-omega SST, Transition SST, both DES RANS bases, and LES WALE/dynamic-k/k-equation configurations without serialization errors.
- Regression-tested the shared OpenFOAM writer's existing dictionary header overload and off-screen rendered the Boundary Conditions dialog with inlet, outlet, FSI, and symmetry rows without overlap or clipping.

### Current BaramFlow Scope
- `General`, `Models`, single-region `Materials`, mesh-boundary parsing, and initial/boundary field generation are operational.
- The implemented boundary roles are velocity inlet, pressure outlet, wall, symmetry, and FSI interface. Mass-flow inlets, pressure inlets, thermal wall modes, cyclic/periodic interfaces, moving walls, and multiphase/species boundary fields remain outside this phase.
- Cell zones, dynamic-mesh displacement, reference values, solution controls, solver execution, FSI load/displacement transfer, and post-processing remain pending.

### Next Work
- Implement the next solver-setup dependency, preferably Numerical Conditions and `fvSolution` ownership, before Run Conditions and OpenFOAM solve execution.
- Add dynamic-mesh boundary motion and FSI pressure/displacement transfer after the static single-region solver path is operational.

### BaramFlow Numerical Conditions Completed
- Characterized the original BaramFlow Numerical Conditions defaults and the single-region `fvSolution` and `fvSchemes` writers.
- Added serializable numerical state for SIMPLE/SIMPLEC coupling, momentum prediction, density-based flux selection, entropy-fix and cut-off Mach coefficients, time/pressure/momentum/energy/turbulence discretization, high-order term relaxation, corrector counts, under-relaxation factors, and absolute/relative convergence criteria.
- Added a native tabbed Numerical Conditions editor opened from the BaramFlow Solution tree. Controls are enabled according to pressure- or density-based solver mode, steady/transient time mode, energy activation, and turbulence activation.
- Extended project-local XML persistence to state version 5. Version-1 through version-4 projects reopen with the original BaramFlow numerical defaults; BinaryFormatter compatibility initializes optional numerical state.
- Moved `system/fvSchemes` ownership from General to Numerical Conditions. General now owns `controlDict`, gravity, and operating conditions, while Numerical Conditions deterministically writes both `fvSolution` and `fvSchemes`.
- Added pressure-based steady/transient generation with SIMPLE/PIMPLE controls, PCG or PBiCGStab pressure solvers, active energy/turbulence solvers, residual controls, relaxation factors, Euler/backward time schemes, bounded steady convection, configurable upwind schemes, and configurable pressure interpolation.
- Added density-based steady/transient generation with `localEuler`/`backwardDualTime`, LU-SGS residual controls, Roe-FDS/AUSM/AUSM-up Riemann flux controls, field bounds, and active turbulence discretization.
- BaramMesh Export initializes the numerical files. Compatible General and Models edits regenerate them so solver type, time mode, energy, and turbulence changes cannot leave stale algorithms or schemes.

### Numerical Verification
- The full `Debug|x64` build succeeds both in `BuildCheck\BaramFlowNumerical` and the normal `PrePoMax\bin\x64\Debug` output; remaining compiler warnings are existing project warnings.
- Runtime-tested pressure-based steady, pressure-based transient with energy and Transition SST, density-based steady, and density-based transient LES configurations.
- Verified SIMPLE/SIMPLEC, pressure solver selection, second-order time output, bounded steady convection, active energy/turbulence entries, density Riemann flux selection, pseudo-time iterations, and both density time schemes.
- Runtime-tested actual case-file writing, confirmed General no longer overwrites `fvSchemes`, and verified version-4 migration, version-5 XML round trips, and BinaryFormatter round trips.
- Off-screen rendered all four editor tabs in pressure- and density-based modes. Labels and values remain unclipped, inactive controls are disabled, and convergence values retain 12 decimal places.

### Current BaramFlow Scope
- `General`, `Models`, single-region `Materials`, Boundary Conditions, and Numerical Conditions are operational and generate the core static single-region OpenFOAM case dictionaries.
- Numerical Conditions currently covers single-phase flow fields only. Multiphase, species, user-defined scalar, solid-region, and advanced collateral-field controls remain outside this phase.
- Reference pressure in `fvSolution` remains zero gauge until Reference Values is implemented. General still seeds `controlDict`; Run Conditions has not yet taken ownership of execution duration, time step, write interval, Courant controls, or parallel settings.
- Solver execution, monitors, result loading, dynamic mesh, and FSI pressure/displacement transfer remain pending.

### Next Work
- Implement Run Conditions and transfer `controlDict` run-time ownership from General, then wire static single-region OpenFOAM solver execution and streamed output.
- Implement Reference Values and Monitors before force/pressure reporting and the FSI pressure-export stage.

### BaramFlow Run Conditions And Serial Execution Completed
- Characterized the original BaramFlow Run Conditions defaults, editor behavior, and `controlDict` generation for steady, fixed-transient, adaptive-transient, and density-based cases.
- Added serializable Run Conditions state for iteration count, time-stepping method, time-step size, Courant and diffusion limits, end time, steady/transient report intervals, data-file retention, write format, write precision, and time precision.
- Added a native tabbed Run Conditions editor opened from the BaramFlow Solution tree. Steady and transient controls switch automatically, adaptive stepping exposes Courant/diffusion limits, density-based transient flow remains fixed-step, and output retention controls its maximum-file setting.
- Extended project-local XML persistence to state version 6. Version-1 through version-5 projects reopen with the original BaramFlow run defaults, and BinaryFormatter compatibility preserves optional Run Conditions state.
- Transferred `system/controlDict` ownership from General to the dedicated Run writer. General now writes only gravity and operating conditions, so unrelated General regeneration cannot overwrite duration, stepping, or output settings.
- BaramMesh Export now initializes Run Conditions and `controlDict` when creating a BaramFlow project. Compatible General changes regenerate `controlDict` with the selected solver application.
- Wired the BaramFlow `Run` tree leaf to asynchronous serial OpenFOAM execution. Before launch, the complete supported case is regenerated from persistent state; solver output is streamed to the PrePoMax output panel and the existing stop action cancels the running process.
- Solver selection follows General settings: `buoyantSimpleNFoam`, `buoyantPimpleNFoam`, `TSLAeroFoam`, or `UTSLAeroFoam`. The configured Meshing OpenFOAM root and environment resolver are reused, including missing-DLL diagnostics.

### Run Verification
- The full `Debug|x64` PrePoMax build succeeds with output redirected to `BuildCheck\BaramFlowRun`; remaining compiler warnings are existing project warnings. A running normal-debug PrePoMax process currently locks the normal executable, so it was not replaced during this phase.
- Runtime-tested steady, pressure-based fixed transient, pressure-based adaptive transient, and density-based transient `controlDict` output, including application, duration, time step, write control, purge count, precision, `maxCo`, and `maxDi`.
- Runtime-tested actual case-file writing, verified that General no longer overwrites `controlDict`, and verified version-5 migration, version-6 XML round trips, and BinaryFormatter round trips.
- Runtime-constructed and off-screen rendered the Run Conditions editor in steady, adaptive, density-based, and output-retention states. Controls enable correctly and all labels, values, tabs, and buttons remain visible without clipping.
- None of the four BaramFlow solver executables is available on the current shell `PATH`, so this phase could not run a real CFD solve from the command line. In-app execution will use the OpenFOAM root configured under Settings -> Meshing -> OpenFOAM.

### Current BaramFlow Scope
- A static, serial, single-region case can now be configured through General, Models, Material, Boundary Conditions, Numerical Conditions, and Run Conditions and launched from the BaramFlow tree.
- Parallel decomposition/reconstruction, multi-region execution, live residual charts, result import, dynamic mesh, and FSI load/displacement transfer remain pending.
- Run starts from the latest available time, matching original BaramFlow behavior. Re-running a case whose configured end condition is already reached will complete without advancing until Run Conditions are extended.

### Next Work
- Implement Initialization and Reference Values, then add residual/force Monitors and solver-result loading.
- Use the preserved FSI-interface patch metadata to export the converged CFD pressure field onto the matching FEA surface before implementing displacement return and outer-loop convergence.

# BaramMesh to PrePoMax C# Integration Plan

## Summary
Build a phased native C# BaramMesh implementation inside PrePoMax, preserving BaramMesh’s functional workflow and OpenFOAM output while fitting PrePoMax’s existing .NET Framework 4.8, WinForms, command/history, `CaeMesh`, `CaeJob`, and `vtkControl` architecture.

The first production target is not a Python bridge and not a FeMesh conversion. It is a native C# BaramMesh workflow that generates and visualizes an OpenFOAM case using a user-configured OpenFOAM installation.

## Key Interfaces And Types
Add a new `CaeMesh.Meshing.BaramMesh` subsystem with serializable model classes:

- `BaramMeshProject`: root state stored in the PrePoMax project/session.
- `BaramMeshConfiguration`: equivalent of BaramMesh `configurations_schema.py`, including versioned migrations.
- `BaramGeometry`, `BaramRegion`, `BaramBaseGrid`, `BaramCastellationSettings`, `BaramSnapSettings`, `BaramBoundaryLayerSettings`, `BaramMeshQualitySettings`.
- Enums mirroring BaramMesh: `Step`, `GeometryType`, `Shape`, `CfdType`, `RegionType`, `ThicknessModel`, `FeatureSnapType`, `GapRefinementMode`, `BufferLayerPointSmoothingMethod`.

Add service interfaces:

- `IBaramOpenFoamRunner`: validates OpenFOAM path, runs utilities, streams output, supports cancel.
- `IBaramCaseFileSystem`: creates and manages `case/system`, `case/constant`, `triSurface`, time folders, processor folders.
- `IBaramDictionaryWriter`: writes OpenFOAM dictionaries with deterministic formatting.
- `IBaramGeometryExporter`: writes STL/OBJ feature files from PrePoMax/VTK geometry.
- `IBaramMeshWorkflow`: runs Base Grid, Castellation, Snap, Boundary Layer, Export stages.

Add PrePoMax commands:

- `CCreateBaramMeshCase`
- `CUpdateBaramMeshConfiguration`
- `CRunBaramMeshStep`
- `CCancelBaramMeshStep`
- `CExportBaramOpenFoamCase`

## Implementation Changes
Phase 1: characterization and parity baseline

- Create Python-side golden cases from the current BaramMesh behavior before porting logic.
- For each case, capture `blockMeshDict`, `snappyHexMeshDict`, `topoSetDict`, `createPatchDict`, `extrudeMeshDict`, `regionProperties`, STL/OBJ geometry files, command sequence, and final OpenFOAM folder layout.
- Cover at least: single-region STL, multi-region, cell zone, boundary, conformal interface, non-conformal interface, buffer layer, boundary layer, 2D plane export, 2D wedge export, and parallel decomposition.
- Treat dictionary text and OpenFOAM utility return codes as the primary parity contract.

Phase 2: native C# core model

- Port BaramMesh schema defaults and migrations to serializable C# classes rather than carrying over HDF5/YAML as the main storage.
- Store BaramMesh state inside PrePoMax’s existing project model and command history.
- Add a later optional one-way importer for existing BaramMesh `configurations.h5`; do not make HDF5 required for the first implementation.
- Implement validation rules equivalent to BaramMesh’s schema: numeric ranges, required names, valid geometry references, region/interface consistency, and locked-step behavior.

Phase 3: OpenFOAM runtime integration

- Add a PrePoMax setting for user-installed OpenFOAM root path.
- Validate required utilities: `blockMesh`, `snappyHexMesh`, `checkMesh`, `topoSet`, `splitMeshRegions`, `createPatch`, `extrudeMesh`, `collapseEdges`, `decomposePar`, `reconstructParMesh`.
- Implement environment setup equivalent to Baram’s `libbaram.run`, but adapted to Windows-first PrePoMax execution.
- Use `CaeJob.ExecutableJob` style process handling where possible, extending it for environment variables, live output, cancellation, and child-process cleanup.
- Do not bundle OpenFOAM in v1.

Phase 4: dictionary and case generation

- Port the OpenFOAM writers:
  `blockMeshDict`, `snappyHexMeshDict`, `topoSetDict`, `surfacePatchDict`, `createPatchDict`, `extrudeMeshDict`, `collapseDict`, and `regionProperties`.
- Implement deterministic OpenFOAM dictionary formatting so golden tests can compare output.
- Preserve BaramMesh stage times:
  `0` base grid, `1` castellation, `2` snap, `3` boundary layer, `4` export.
- Preserve the command sequence:
  `blockMesh` -> optional `decomposePar` -> `checkMesh`;
  `snappyHexMesh` castellation -> `checkMesh`;
  `snappyHexMesh` snap -> optional `topoSet` -> optional overwrite `snappyHexMesh` -> `checkMesh`;
  optional layer `snappyHexMesh` -> `checkMesh`;
  export with `splitMeshRegions`, `topoSet`, `createPatch`, optional `extrudeMesh`, optional `collapseEdges`.

Phase 5: geometry and visualization

- Reuse PrePoMax geometry parts and `vtkControl` where possible.
- Implement Baram-specific virtual primitives: box, hex6 bounding box, cylinder, sphere, plane, and plate faces.
- Port STL import/splitting behavior needed by BaramMesh: surface-name sanitization, solid splitting, closed-volume detection, feature-barrier edges, and region grouping.
- Account for PrePoMax’s ActiViz 5.8 dependency; avoid direct reliance on Baram’s VTK 9-only APIs unless replaced by compatible code.
- First display target: show generated OpenFOAM mesh boundaries/internal mesh in PrePoMax. Full OpenFOAM-to-`FeMesh` conversion is out of v1.

Phase 6: PrePoMax UI integration

- Add a BaramMesh workflow panel under meshing rather than porting PySide pages one-for-one.
- Use PrePoMax forms and property grids for:
  Geometry, Region, Base Grid, Castellation, Snap, Boundary Layer, Export, OpenFOAM settings.
- Keep the same stage-lock behavior: once a stage output exists, later stages are locked until reset.
- Stream OpenFOAM output to PrePoMax’s output window.
- Add cancel/reset actions for long-running stages.
- Add mesh quality display using `checkMesh` fields where available.

## Test Plan
- Golden dictionary tests: C# writer output must match Python-generated golden files after normalized whitespace.
- Golden command-sequence tests: each workflow stage must invoke the same OpenFOAM utilities with equivalent arguments.
- Geometry tests: STL import, split, sanitize names, primitive geometry, feature OBJ, and STL export must match expected topology/counts.
- Workflow tests: run base grid, castellation, snap, boundary layer, export on small fixtures.
- Failure tests: missing OpenFOAM path, missing utility, invalid region point, invalid interface configuration, canceled run, nonzero utility exit.
- PrePoMax integration tests: command history serialization, project save/open, UI reset/lock behavior, output streaming, and no regression to existing Gmsh/Netgen/MMG meshing.

## Assumptions
- Target framework remains PrePoMax’s current .NET Framework 4.8 / C# 7.3 style.
- The first integrated output is an OpenFOAM case, not a PrePoMax `FeMesh`.
- OpenFOAM is user-installed and configured by path in PrePoMax settings.
- Existing BaramMesh project-file compatibility is optional after v1; feature parity and generated OpenFOAM behavior are higher priority.
- Python BaramMesh remains only a reference during characterization, not a runtime dependency in the final implementation.

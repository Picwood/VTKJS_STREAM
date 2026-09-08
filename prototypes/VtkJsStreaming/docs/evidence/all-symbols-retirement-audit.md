# All-symbols retirement audit

Date: 2026-09-01

The audit enumerated the public symbol-producing methods in `vtkControl` and their `FrmMain` seams. Every method used by `Controller.Draw*Symbols` now chooses VTK.js when the opt-in renderer is active while retaining its original ActiViz call in default mode.

| Legacy entry point | VTK.js family | Process evidence |
|---|---|---|
| `AddSphereActor` | retained instanced spheres | passed |
| `AddCoordinateAxis` / `AddCaptionActor` | retained axes and safe DOM captions | passed |
| `AddOrientedDisplacementConstraintActor` | 31-sided constraint cones | passed |
| `AddOrientedConeActors` | 11-sided line-preview cones | passed |
| `AddOrientedRotationalConstraintActor` | cube/line rotation symbols | passed |
| `AddOrientedArrowsActor` | normal/inverted relative arrows | passed |
| `AddOrientedDoubleArrowsActor` | moment double arrows | passed |
| `AddOrientedSpringActor` | tubed spring symbols | passed |
| `AddOrientedThermoActor` | compound thermometer symbols | passed |
| `AddOrientedFluxActor` | centered/offset flux spheres | passed |

The audit also enumerated controller call sites, including constraints, temperature conditions and fields, mechanical loads, moment loads, pretension, heat flux, film/radiation transfer, meshing previews, and selection line arrows. They all converge on the guarded methods above; no additional direct symbol renderer call remains.

Optional symbol silhouettes are not a separate application symbol family. They are a conditional edge actor added by legacy `AddSymbolEdges`. The bundled vtk.js has no equivalent camera-dependent polydata-silhouette filter, so this visual enhancement remains explicitly unclaimed rather than silently approximated. Core symbol geometry, color, orientation, constant-screen scale, identity, state replacement, removal, navigation replay, and shutdown ownership are migrated.

# ActiViz compatibility call-site inventory

Last audited: 2026-09-08

Scope: public renderer seams in `PrePoMax/Forms/FrmMain.cs`, especially the `vtkControl` region. “Retired” means the VTK.js branch performs no hidden ActiViz mutation/query for that capability; the default branch remains unchanged. “Mirrored” still depends on the hidden control and therefore does not satisfy PEND-004.

| Capability group | `FrmMain` seams | VTK.js status | Required next evidence/work |
|---|---|---|---|
| Camera and bounds | view get/set, named views, fit/adjust, box zoom, closest-up/normal | Retired | Keep process regressions; no remaining hidden call after a valid retained camera snapshot |
| Follower, exploded, section | follower apply/update/remove; preview/remove; section create/update/remove | Retired | Keep changed-origin, animation, pick, and section-cap regressions |
| Annotations | arrow-widget add/named-remove/remove-all | Retired | Keep interaction/settings process regressions |
| Engineering symbols | spheres, coordinate axes/captions, eight directional families | Retired | Optional camera-dependent silhouettes remain PEND-011, not a hidden rendering dependency |
| Viewport appearance | background gradient/colors; ambient/diffuse lighting | Retired 2026-09-02 | Evidence: `viewport-appearance-retirement.md` |
| Authoritative selection clear | `ClearSelection`, `Clear3DSelection`, clear-with-scene | Retired 2026-09-03 | Evidence: `evidence/selection-and-scene-clear-lifecycle.md`; retain part-selection populate/clear and zero-overlay regressions |
| Scene lifecycle | `Clear`, `ClearButKeepParts` | Partial/mirrored | Browser topology and all retained overlays now clear authoritatively; hidden registry cleanup remains until every core actor-construction path stops populating the hidden registry |
| Core actor construction | part surfaces; points, generic/selection cells and actors, OpenFOAM mesh, scalar and animated-scalar installation | Partial: ordinary/static/animated parts, selection previews, generic VTK surface types 5/6/8/9/22/23, primitive types 3/4/21, non-pickable base/overlay points, managed-boundary OpenFOAM display, and native-handle replay mutation retired by 2026-09-07 | Generic cell capture is strict and atomic; other render types reject the whole actor. Pickable points have no audited production producer and remain rejected rather than losing pick semantics. Exact overlay depth ordering, volume-cell rendering/OpenFOAM parity, widgets, and final cleanup remain |
| Actor registry/presentation | `ContainsActor`, rename/color, contour visibility, hide/show | Retired 2026-09-02 | Evidence: `actor-presentation-retirement.md`; retain protocol, host-process, and startup-resync regressions |
| Temporary actor highlight/3D clear | `HighlightActor`, `Clear3DSelection` | Retired 2026-09-02 | Evidence: `actor-highlight-retirement.md`; retain add/clear, rename, removal, visibility, section, and exploded-position regressions |
| Transformations | symmetry, linear/circular pattern, apply/hide/show transforms | Retired for viewport 2026-09-04 | Evidence: `evidence/browser-transformations-retirement.md`; transformed section-cap and 3MF projection remain explicit downstream work |
| Result/export readback | `GetVtkMaxActorData`, `GetLookupTable`; 3MF/export consumers | Partial: captured actor and lookup-table readback retired 2026-09-03; animation PNG/AVI capture retired 2026-09-04 | Evidence: `evidence/retained-3mf-actor-input.md`, `evidence/retained-3mf-lookup-table.md`, `evidence/browser-animation-png-export.md`, and `evidence/browser-animation-avi-export.md`; generated element edges, transformed/auxiliary actors, and native 3MF packaging remain |
| Result widgets | scale widget; scalar legend; categorical color bar; status block and positions | Retired 2026-09-08 | Evidence: `evidence/scale-widget-retirement.md`, `evidence/scalar-legend-retirement.md`, `evidence/categorical-color-bar-retirement.md`, and `evidence/status-block-and-widget-layout-retirement.md`; exact typography/settings gestures remain review items rather than hidden render-state dependencies |
| Coordinate-system orientation widget | `SetCoorSysVisibility` | Retired 2026-09-07 | Evidence: `evidence/orientation-marker-retirement.md`; stock vtk.js arrow shafts and absent X/Y/Z captions remain a disclosed manual visual-review item |
| Smoothing and highlight settings | point/line smoothing; primary/secondary/mouse highlight colors | Open | Decide supported vtk.js semantics, then add bounded state and visual/process regressions |
| Symbol-edge setting | `SetDrawSymbolEdges` | Conditional | PEND-011 owns optional silhouette parity; document final product decision |
| User-pick mode | `UserPick` activation/deactivation | Open | Route mode through renderer-neutral interaction state; prove normal selection isolation |
| Mesh editing helpers | cylinder/cube crop and smooth-part file operations | Open classification | Determine whether these are renderer readback or domain operations; remove actor dependence or explicitly move them outside the renderer boundary |
| Animation | scale query, acceleration, metadata/storage, interactive frame activation, AVI/image export | Retired 2026-09-04 | Retained/browser-owned construction, activation, stable capture, PNG, and fixed-MJPG AVI; evidence: `evidence/animation-frame-activation-retirement.md`, `evidence/animated-result-construction-retirement.md`, `evidence/browser-animation-png-export.md`, and `evidence/browser-animation-avi-export.md` |

Additional domain-boundary debt remains outside the `FrmMain` call block: controller and domain-facing signatures still expose `vtkControl` enums and actor data types. Those types must be replaced only after their consumers have renderer-neutral equivalents; deleting the default backend early would obscure rather than solve this dependency.

## Next recommended slice

Continue with smoothing/highlight settings, followed by user-pick mode. Orientation, scale, scalar-legend, categorical color-bar, status-block, and shared-layout state are browser-owned. Exact orientation captions/line shafts, widget typography, and settings gestures remain disclosed review items; overlay depth ordering, volume-cell rendering/OpenFOAM parity, transformed/export projection, tools, and final hidden-registry cleanup remain.

# Auxiliary and OpenFOAM construction retirement

Date: 2026-09-04

## Outcome

Non-pickable base-layer auxiliary surfaces, lines, and points no longer require hidden ActiViz construction in VTK.js mode. Surfaces enter the normal retained scene and binary resource pipeline. Points, VTK lines, and quadratic edges enter a persistent bounded browser-primitive collection; quadratic edges retain start-mid/end expansion. The default renderer remains unchanged, and unsupported/pickable/overlay content retains an explicit traced fallback.

The former `Add3DCells(vtkMaxActorData[])` dual write now classifies every item independently and sends only rejected items to ActiViz. Single non-pickable base-layer `Add3DCells` and `Add3DNodes` similarly return no native handle after successful capture. Persistent primitives share the existing 4,096-actor, 500,000-point, 750,000-line, 64 MiB, coordinate, connectivity, and style bounds. They are non-pickable, origin-relative in the browser, section-clipped, navigation-replayed, visibility-aware, rename/removal-aware, and cleared with the scene—but not by selection clear.

In VTK.js mode `AddOpenFoamMesh` now returns no native handle before touching the file and emits `native-reader-bypassed`. Existing Baram callers consequently use their managed OpenFOAM `polyMesh` boundary reader, whose surface and unique edge actors follow the renderer-neutral paths above. This removes runtime dependence on `vtkOpenFOAMReader` from the browser path.

## Packaged acceptance

```powershell
.\scripts\build-vtkjs-mvp.ps1 -Configuration Release -Platform x64
.\scripts\smoke-vtkjs-winforms.ps1 `
  -ModelPath C:\Users\fvazeille\Documents\GitHub\SpectraRev\PMX2.4.0\BENDING_NEON36.pmx `
  -ExerciseAuxiliaryConstruction
```

The Release/x64 process reported `AuxiliaryConstructionBrowserOnly=True`. Normal model publication contained 40 retained actors / 904,768 render points / 1,766,120 triangles while retaining 2,140,668 locator points / 931,033 locator cells. The deterministic probe added one surface plus two primitive actors (three primitive points and one line), proved both native handles null, and observed `2|2|3|1`. Selection clear retained the same state; hiding one primitive produced `2|1|3|1`; show restored `2|2|3|1`; discrete removal produced `0|0|0|0` and removed all three registry names. No auxiliary fallback trace occurred, and desktop/renderer shutdown was clean.

The first probe exposed asynchronous diagnostic/mutation ordering in the harness. The second proved all intermediate states but incorrectly waited for a full-scene counter after discrete removal. Both assumptions were corrected; the final run passed through the protocol-specific registry/diagnostic gates.

## Regression verification

- Client unit tests: 69/69 passed, including optional primitive visibility validation.
- Renderer contract/server tests: 80/80 passed.
- Browser progressive-rendering and integrity-recovery tests: 2/2 passed.
- Prototype isolation audit: pass, 34 C# files across 6 projects, zero forbidden references.
- Strict production client build and full Release/x64 PrePoMax build/staging: pass.
- Prior packaged selection-primitive regression: pass with one retained actor, `SelectionPrimitiveConstructionBrowserOnly=True`, and clean shutdown, confirming selection clear still owns only the transient subset.

## Remaining boundary

This does not claim general native-handle ingestion. `AddActor(vtkMaxActor)`, pickable or overlay generic actors, and unsupported VTK cell types still use the explicit compatibility path. The managed OpenFOAM route displays boundary faces and unique edges, not the native reader's volumetric unstructured grid; volumetric selection, internal element edges, and section-cap parity remain open. Persistent point/line primitives are not currently projected into 3MF or transformation copies.

At this milestone PEND-004 remained open for renderer-neutral handling of already-materialized `vtkMaxActor` inputs and pickable/overlay generic actors; the subsequent [generic/native-handle construction slice](generic-native-handle-construction-retirement.md) blocks native replay and retires supported pickable surfaces plus non-pickable overlay points. Pickable points, unsupported cells, widget/tool seams, transformed/export projection, and final hidden-registry cleanup remain.

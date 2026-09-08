# Temporary actor-highlight retirement evidence

Date: 2026-09-02

## Outcome

In opt-in VTK.js mode, `FrmMain.HighlightActor` and `FrmMain.Clear3DSelection` no longer mutate the hidden ActiViz control. Default ActiViz behavior remains unchanged.

The host retains a bounded set of highlighted actor names and publishes full replacement state through `prepomax.actor-highlights.updated-v1`. Repeated adds accumulate; clear publishes an empty set. The browser owns distinct non-pickable orange wireframe overlays, keeping temporary highlight state independent of `prepomax.selection.updated-v1` and its authoritative node/element/geometry/part selection.

Lifecycle handling includes browser navigation and scene recreation, actor rename/removal, visibility, section clipping, exploded offsets, scene clear, and VTK object disposal. Invalid, duplicate, and over-limit host messages are rejected atomically.

## Automated verification

- Client unit tests: 63/63 passed, including valid clone/clear and malformed/duplicate/4,097-name rejection.
- Strict TypeScript and production Vite build: passed.
- Contracts tests: 11/11 passed.
- Server tests: 69/69 passed (80/80 .NET total).
- Playwright: 2/2 passed.
- Full mixed C#/C++ `Release|x64` build and renderer staging through Visual Studio MSBuild: passed. Existing architecture/ruleset/source warnings are unchanged.

Commands:

```powershell
cd prototypes\VtkJsStreaming\src\client
npm test -- --run
npm run build
npm run test:e2e

cd ..\..\..\..
dotnet test prototypes\VtkJsStreaming\tests\PrePoMax.Render.Contracts.Tests\PrePoMax.Render.Contracts.Tests.csproj -c Release --no-restore
dotnet test prototypes\VtkJsStreaming\tests\PrePoMax.Render.Server.Tests\PrePoMax.Render.Server.Tests.csproj -c Release --no-restore
.\scripts\build-vtkjs-mvp.ps1 -Configuration Release -Platform x64
```

## Packaged process acceptance

The production `PrePoMax.exe`/WebView2 path loaded `BENDING_NEON36.pmx`, waited for exact browser scene acknowledgement, called the public `FrmMain` highlight and clear seams, and required both compatibility-bypass traces plus browser diagnostics for the add and empty replacement. The smoke closed PrePoMax normally and verified renderer-child exit.

```powershell
.\scripts\smoke-vtkjs-winforms.ps1 `
  -ModelPath C:\Users\fvazeille\Documents\GitHub\SpectraRev\PMX2.4.0\BENDING_NEON36.pmx `
  -ExerciseActorHighlight
```

Result: `ActorHighlightApplied=True`; 15 actors, 828,619 render points, 1,623,416 triangles, 2,140,668 locator points, and 931,033 locator cells. One retained name produced at least one chunk overlay, then clear produced zero names and zero overlays. Graceful desktop and renderer-child shutdown passed.

## Deliberate boundary and pending work

The streamed topology currently carries triangle surfaces but not the legacy actor's explicit `ModelEdges` polydata. The browser therefore uses a wireframe of the actor surface, matching the established browser part-selection visual rather than claiming exact feature-edge/silhouette parity. Camera-dependent silhouette parity remains PEND-011 and manual visual disposition remains PEND-005.

`FrmMain.ClearSelection` is a controller selection-history operation and is intentionally not conflated with temporary `Clear3DSelection`. Scene-wide `Clear`, `ClearButKeepParts`, and the authoritative selection-state lifecycle remain the next PEND-004 slice.

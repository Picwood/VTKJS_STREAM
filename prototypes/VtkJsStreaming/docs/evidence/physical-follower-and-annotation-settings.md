# Physical follower redraw and annotation settings evidence

Date: 2026-09-01

## Deterministic fixture

`tests/fixtures/follower-redraw-smoke.frd` contains one linear tetrahedron, four locator nodes, one cell, and one `DISP` field. All nodes have a 0.2-unit X displacement. Nodes 1/2/3 form a non-collinear plane, so scale-factor animation provides a deterministic in-view-plane follower translation from frame 0 to frame 1 in the standard front view.

## Follower redraw

Command:

```powershell
scripts/smoke-vtkjs-winforms.ps1 `
  -ModelPath prototypes/VtkJsStreaming/tests/fixtures/follower-redraw-smoke.frd `
  -ExpectFiniteScalars -ExerciseAnimation -ExerciseFollowerRedraw -AnimationMode ScaleFactor
```

Result: pass. PrePoMax selected and applied a three-node plane through `Controller.ApplyFollowerView`; the VTK.js seam emitted follower-apply and follower-update compatibility bypasses. Frame 1 advanced the retained scene to revision 2 without changing topology. The browser downloaded 192 bytes and reported `followerAdjusted=True`, `cameraAdjusted=True`, and 22.9 ms total stable activation. The application closed normally and the renderer child exited.

## Annotation settings

Command:

```powershell
scripts/smoke-vtkjs-winforms.ps1 `
  -ModelPath prototypes/VtkJsStreaming/tests/fixtures/follower-redraw-smoke.frd `
  -ExpectFiniteScalars -ExerciseAnnotationSettings
```

Result: pass. The browser emitted the bounded Settings action for a real node annotation. PrePoMax opened `FrmSettings`, reported the live selected page as `Annotations`, and UI Automation found the physical `Edit Settings` window and invoked its Cancel button. The run exposed and fixed pre-handle ListView selection failing to populate the property grid. The application then closed normally and the renderer child exited.

# VTK.js launch and remaining ActiViz dependency

Last audited: 2026-09-08

## Current answer

This document describes the original Windows `PrePoMax.exe` and its in-process
VTK.js compatibility mode. That executable can currently run with its **visible
viewport rendered by VTK.js**, but it cannot yet start after ActiViz is
uninstalled or its assemblies are removed.

The native GTK Linux application has a different build boundary. Its portable
host, converter and renderer server do not reference `vtkControl`,
`Kitware.VTK` or `Kitware.mummy.Runtime`; its visible renderer is VTK.js/WebGL.
The Debian package therefore already installs and runs without ActiViz. PEND-004
tracks retirement of the compatibility backend from the original desktop source
and the remaining shared-domain type cleanup, rather than a missing Linux runtime
library.

VTK.js mode is selected before launch with:

```powershell
$env:PREPOMAX_RENDERER = 'vtkjs'
.\PrePoMax\bin\x64\Release\PrePoMax.exe
```

The repository launcher applies that environment only to the child process and restores the calling shell:

```powershell
.\scripts\start-prepomax-vtkjs.ps1
```

To open a model at startup:

```powershell
.\scripts\start-prepomax-vtkjs.ps1 -ModelPath '.\examples\model.inp'
```

Build first when sources changed:

```powershell
.\scripts\build-vtkjs-mvp.ps1 -Configuration Release -Platform x64
```

## Why ActiViz binaries are still required

- `PrePoMax` has a project reference to `vtkControl`, and `vtkControl` directly references `Kitware.VTK` and `Kitware.mummy.Runtime`.
- `FrmMain` constructs `vtkControl.vtkControl` unconditionally before it creates the optional `VtkJsRendererControl`.
- Startup still initializes and briefly draws the legacy control before bringing the VTK.js control forward.
- Rejected/unsupported renderer-neutral construction can still use traced legacy fallback, and scene cleanup still mirrors `Clear`/`ClearButKeepParts` for that state.
- Several public renderer seams and domain-facing signatures still expose `vtkControl`/ActiViz types.

Consequently, renaming/deleting the ActiViz DLLs or uninstalling ActiViz is not a supported test yet. `PREPOMAX_RENDERER=vtkjs` means browser-owned visible rendering, not a fully ActiViz-free process.

## Remaining capability groups

The authoritative detailed inventory is [the compatibility call-site inventory](../prototypes/VtkJsStreaming/docs/activiz-compatibility-inventory.md). The remaining groups are:

- user-pick activation/deactivation;
- crop/smooth mesh-editing helper classification;
- unsupported generic/volume rendering and volumetric OpenFOAM parity;
- transformed/auxiliary 3MF projection and native 3MF packaging;
- mirrored hidden scene cleanup and construction fallback;
- renderer-neutral replacement of `vtkControl` enums/data types in application/domain signatures;
- conditional symbol silhouettes and exact visual differences tracked by PEND-005/PEND-011.

The coordinate orientation marker, result widgets/shared layout, and smoothing/highlight-color settings are browser-owned as of this audit. WebGL context antialiasing replaces the unavailable VTK 5.8 point/line smoothing toggles; user-pick hover remains a separate open capability.

## Exit gate for a truly ActiViz-free launch

Do not claim an ActiViz-free executable until all of the following pass:

1. `FrmMain` does not construct or initialize `vtkControl` in VTK.js-only packaging.
2. No VTK.js capability performs hidden render-state mutation/query or legacy fallback.
3. PrePoMax/domain boundaries no longer require ActiViz objects or enums.
4. A build configuration excludes `vtkControl`, `Kitware.VTK`, and `Kitware.mummy.Runtime` references and output files.
5. The packaged executable starts and completes representative geometry, result, selection, section, animation, export, and shutdown workflows on a machine without ActiViz installed.
6. Default ActiViz mode remains available until its removal is separately approved.

# PrePoMax Linux Port Evaluation

> Migration started on 2026-09-10. See [current Linux migration progress](docs/LINUX_PORT_PROGRESS.md)
> for implementation and validation. This historical evaluation predates the
> VTK.js renderer work; consult that progress record and the renderer pending-work
> register before acting on its ActiViz hosting recommendations.

**Repository evaluated:** `SpectraRev` working tree  
**Evaluation date:** 2026-08-04  
**Purpose:** engineering assessment only; no porting work was performed

## Executive conclusion

PrePoMax cannot be made into a supported native Linux application by changing build settings or replacing a few DLLs. The current application is a Windows desktop product built around .NET Framework 4.8, WinForms, GDI+/`System.Drawing`, Win32 calls, a Windows-only C++/CLI Gmsh bridge, and the Windows Open Source release of ActiViz 5.8.

A native Linux port is feasible, and a large part of the finite-element domain and file-processing code can be retained. However, it should be planned as a **cross-platform modernization with a desktop UI replacement**, not as a recompilation.

The most credible route is:

1. move reusable libraries to SDK-style projects on **.NET 10 LTS**;
2. separate presentation, operating-system services, rendering, and external-process execution from the domain layer;
3. replace WinForms with a Linux-capable desktop framework, most plausibly **Avalonia**;
4. upgrade the rendering layer to a Linux-capable VTK/ActiViz stack;
5. replace C++/CLI Gmsh interop with the official C API/C# binding and Linux `libgmsh`;
6. replace or bridge `BinaryFormatter` project persistence while preserving existing `.pmx` compatibility;
7. supply Linux-native solver/mesher executables and packaging, then establish automated Windows/Linux tests.

An engineering-quality Linux MVP is approximately **14-22 person-months**. Broad feature parity and production hardening are approximately **26-45 person-months**, or roughly **9-15 calendar months for a stable team of three to four experienced engineers**. The range is dominated by UI/renderer behavior, legacy project-file compatibility, external tool parity, and the absence of a C# regression suite.

This is a medium-to-large product migration with high integration risk, but it is not a ground-up rewrite of the engineering core.

## Scope and method

The review covered the full solution and the adjacent functionality present in this working tree:

- all 12 C# or C++ project files and `PrePoMax.sln`;
- approximately 1,338 C# files and 271,000 lines of first-party C# source;
- UI forms, controls, resources, rendering code, command history, model/mesh/results code, job execution, settings, and serialization;
- Gmsh, 3MF, Netgen/MMG, CalculiX/Abaqus, OpenFOAM/Baram, Python/CUDA, and FATLAB integration points;
- project packaging and build events;
- the added SpectraRev fatigue, SRSuntour, project-protection, and Baram functionality documented in `ADDED_FEATURES.md` and `PLAN.md`.

Generated `bin`/`obj` files and third-party SDK examples were inventoried but not treated as first-party source. No Linux build was attempted because the request was evaluation-only and the current host/workspace contains the Windows dependency set.

## Repository snapshot

| Area | Approximate C# files | Approximate lines | Linux outlook |
|---|---:|---:|---|
| `PrePoMax` | 623 | 122,317 | Mostly UI/orchestration; substantial rewrite/refactor |
| `CaeModel` | 251 | 37,667 | Mostly reusable after dependency cleanup |
| `CaeMesh` | 119 | 30,498 | Mostly reusable; Gmsh and UI leakage must change |
| `CaeGlobals` | 166 | 23,632 | Mixed core/UI/Win32; must be split |
| `CaeResults` | 50 | 16,795 | Mostly reusable; remove rendering dependency |
| `UserControls` | 76 | 16,362 | WinForms control library; effectively replaced |
| `vtkControl` | 37 | 15,007 | Algorithms partly reusable; binding/host substantially changed |
| `3MF` | 2 | 7,142 | Generated wrapper reusable after native library resolution changes |
| `CaeJob` | 4 | 1,317 | Concept reusable; Windows process management must change |
| `STL` | 6 | 714 | Highly reusable |
| `GmshCaller` | 2 | 88 | Reusable concept; rebuild on new Gmsh interop |

Additional indicators:

- 53 `*.Designer.cs` files and 92 `.resx` files;
- 228 source files directly importing `System.Windows.Forms`;
- 325 source files directly importing `System.Drawing`;
- at least 13 first-party files declaring or using Win32 P/Invoke;
- 1014 classes marked `[Serializable]` and about 128 classes implementing `ISerializable`;
- no C# test project and no solution-level CI configuration were found;
- the repository contains Windows `.exe`, `.dll`, `.lib`, and `.mexw64` artifacts rather than a Linux runtime payload.

The largest and most consequential files are:

- `PrePoMax/Controller.cs`: 24,208 lines;
- `PrePoMax/Forms/FrmMain.cs`: 15,279 lines;
- `PrePoMax/Forms/FrmMain.Designer.cs`: 5,435 lines;
- `vtkControl/vtkControl.cs`: 7,493 lines;
- `UserControls/ModelTree.cs`: 4,142 lines;
- `CaeMesh/FeMesh.cs`: 11,133 lines;
- `CaeResults/FeResults.cs`: 6,642 lines.

## Current architecture and portability boundary

### Reusable engineering core

The following code is mostly ordinary managed C# and should be retained rather than rewritten:

- finite-element nodes, elements, sets, surfaces, parts, and mesh algorithms in `CaeMesh`;
- model entities, materials, sections, steps, loads, constraints, and CalculiX/Abaqus import/export in `CaeModel`;
- result structures, CalculiX FRD/DAT parsing, interpolation, and transformations in `CaeResults`;
- equation, unit, collection, and geometry utilities in the non-UI portions of `CaeGlobals`;
- STL processing;
- most OpenFOAM dictionary/case generation recently added under `CaeMesh/Meshing/BaramMesh`;
- most fatigue-domain logic and the Python/NumPy/CuPy backend;
- the command objects, subject to replacing UI calls and updating serialization.

This is the main reason a port is viable. The engineering data model is large but not inherently Windows-specific.

### The boundary is currently blurred

The projects are not cleanly layered:

- `CaeGlobals` contains editors, dialog helpers, WinForms controls, global keyboard/mouse hooks, message-box manipulation, `System.Management`, and `System.Drawing` alongside domain utilities.
- `CaeMesh` references WinForms and the C++/CLI `GmshCommon` project. `GmshAPI` calls `Application.DoEvents()` and uses `Thread.Abort()`.
- `CaeModel` references both `vtkControl` and `ThreeMF`, so model code is not independent of rendering/native export.
- `CaeResults` references `vtkControl`.
- `CaeJob` references WinForms, `System.Management`, and WMI.
- `Controller` imports WinForms and holds a concrete `FrmMain`, rather than an interface or platform-neutral presenter.

`Controller.cs` makes approximately 731 `_form.*` calls across 143 distinct form methods. This is the most important architectural finding: replacing WinForms affects the orchestration layer as well as the visible forms.

## Hard Linux blockers

### 1. .NET Framework 4.8 and WinForms

Every project targets the legacy, non-SDK `.NETFramework,Version=v4.8` format. The application output is `WinExe`, and the main program starts the WinForms event loop. Microsoft explicitly documents that .NET Framework, WinForms, and WPF remain Windows-only even when WinForms is moved to modern .NET. Moving from `net48` to modern .NET is necessary, but it does not make this UI cross-platform.

Affected areas include the main window, about 50 substantial dialogs, property-grid views, custom model tree, charting, editors, context menus, message boxes, drag/drop, clipboard, keyboard/mouse handling, and file dialogs.

**Required response:** replace the desktop application model. For a C#-first codebase, Avalonia is the strongest current candidate. Its desktop runtime supports Linux and uses X11 by default, with Wayland support still requiring explicit consideration. WPF-specific compatibility products do not help because this application is WinForms, not WPF.

### 2. UI coupling in `Controller`

The main controller directly manipulates `FrmMain`, selection UI, output UI, state text, toolbars, views, and rendering. It is not currently possible to attach a new UI to the domain layer without either imitating a very large `FrmMain` API or extracting proper services/presenters.

**Required response:** introduce interfaces such as `IApplicationView`, `IRenderScene`, `IFileDialogService`, `INotificationService`, `IUiDispatcher`, and job/progress services. Extract use cases from `Controller` incrementally, with characterization tests around commands and model transitions.

### 3. ActiViz 5.8 / VTK rendering

`vtkControl.csproj` references:

- `C:\Program Files\ActiViz.NET 5.8.0 OpenSource Edition\bin\Kitware.VTK.dll`;
- `Kitware.mummy.Runtime.dll`, AMD64;
- a custom WinForms `UserControl` and Win32 `SetFocus`.

The repository documentation also requires the Windows ActiViz installation. The current renderer contains valuable VTK pipeline and interaction logic, but its generated wrapper/runtime and hosting control cannot be used as-is on Linux.

Current commercial ActiViz releases support Windows, Linux, macOS, and Avalonia integration, while Kitware identifies 5.8 as the last open-source ActiViz release. Therefore there are two realistic choices:

1. **Commercial ActiViz upgrade:** lowest technical risk and greatest chance of retaining the existing C# VTK logic, but requires licensing, API migration from VTK 5.8 to current VTK, and a new Avalonia/native render host.
2. **Open-source VTK interop replacement:** avoid ActiViz licensing by creating/adopting another binding or a native rendering service, but accept much higher wrapper, embedding, deployment, and maintenance cost.

The commercial route should be evaluated in a short proof of concept before committing to the port. Rendering, picking, clipping, overlays, scalar bars, animations, screenshots, DPI, and X11/Wayland behavior all require explicit validation.

### 4. C++/CLI Gmsh bridge

`GmshCommon.vcxproj` uses:

- Visual Studio toolset `v143`;
- Windows SDK 10;
- `<CLRSupport>true>`;
- a bundled `gmsh-4.13.0-Windows64-sdk`;
- `gmsh.lib` and MSVC linker paths.

The wrapper is approximately 1,174 lines and exposes about 135 static methods from `gmsh.h`. Microsoft documents C++/CLI on modern .NET as Windows-only, so this project is a hard blocker even after upgrading the managed projects.

**Required response:** remove the C++/CLI project. Gmsh publishes SDKs for Windows, Linux, and macOS. Use Gmsh's C API with a generated/maintained C# P/Invoke layer, or adapt the official `gmsh.cs` binding, and resolve `gmsh.dll`/`libgmsh.so` by runtime identifier. Retain `GmshCaller` as a separate worker process if crash isolation is valuable.

The actual `CaeMesh/Meshing/Gmsh/GmshAPI.cs` workflows can mostly remain after changing the binding surface, although `Application.DoEvents`, thread lifecycle, cancellation, and error handling must be made UI-independent.

### 5. Win32 P/Invoke and WMI

First-party native calls include:

- global keyboard and mouse hooks (`SetWindowsHookEx`);
- message-box hooks and button text manipulation;
- dialog centering and window movement;
- tree/text-box scroll messages;
- `SetFocus`, `LockWindowUpdate`, console attachment, and handle lookup;
- `Win32_Process` WMI queries for killing child processes.

These are located in `CaeGlobals`, `UserControls`, `PrePoMax`, and `vtkControl`. They are behavior implementations, not portable abstractions.

**Required response:** use framework input/focus/dialog APIs in the new UI. Replace WMI process-tree termination with modern `Process.Kill(entireProcessTree: true)` where suitable, process groups on Linux, or a small platform service. Remove “run as administrator” assumptions.

### 6. `System.Drawing`

`System.Drawing` is pervasive: colors and points are used in domain-facing objects, while bitmaps, icons, fonts, image resources, and GDI rendering are concentrated in the UI. Microsoft supports `System.Drawing.Common` only on Windows in modern .NET.

**Required response:** distinguish harmless value data from drawing operations. Introduce portable color/point DTOs where these cross project boundaries; use Avalonia/Skia types in presentation code; move image manipulation to a supported library such as SkiaSharp. Resource conversion will be required for existing `.resx` images and icons.

### 7. Binary serialization and `.pmx` compatibility

PrePoMax relies heavily on `[Serializable]`, `ISerializable`, `BinaryFormatter`, custom binders, optional fields, and embedded serialized objects for clone, settings, history, Gmsh worker data, and `.pmx` persistence. Modern .NET removed the built-in `BinaryFormatter` implementation starting with .NET 9; the compatibility package is unsupported and retains the security problem.

This is not only a technical migration. Existing users will expect Linux PrePoMax to open Windows-created `.pmx`, `.pmh`, settings, and material-library files.

**Required response:** define a versioned safe format for new writes, preferably an explicit schema using JSON, MessagePack, protobuf, or a purpose-built binary container. Keep a quarantined legacy import path for old files. The safest pattern is a separate restricted conversion helper that reads legacy files and emits the new format, rather than enabling unsafe deserialization throughout the main application.

Project protection/encryption and backward-compatible type binders require dedicated compatibility tests. Project data must never be treated as trustworthy merely because it has a `.pmx` extension.

### 8. Windows paths, executable names, and build scripts

Examples include:

- `\NetGen\NetGenMesher.exe`, `\NetGen\mmgs.exe`, `\NetGen\mmg3d.exe`;
- `\lib\GmshCaller.exe`;
- descriptions that require `ccx.exe`;
- `Application.StartupPath` for writable settings/history/materials;
- MSBuild `del`, `copy`, and output-moving commands;
- hard-coded ActiViz paths and several development-only `C:\Temp` paths;
- Windows `.bat`/`.cmd` execution through `ComSpec`.

The leading-backslash paths are especially error-prone on Unix. Linux also uses case-sensitive file names and `/` separators, and installed application directories should normally be read-only.

**Required response:** centralize path layout and executable discovery; use `AppContext.BaseDirectory` for immutable assets and XDG directories for settings/cache/data; use `Path.Combine`; store tool names without `.exe`; make build/publish operations platform-neutral MSBuild targets or scripts.

## External tools and adjacent modules

| Component | Current repository state | Linux work required |
|---|---|---|
| CalculiX | User-configured Windows executable and job runner | Use native Linux `ccx`; validate arguments, environment, signals, output monitoring, and process-tree cancellation |
| Abaqus | External executable plus ODB conversion script | Linux Abaqus can be configured if available; validate command names, licensing environment, and Python/ODB tooling separately |
| Gmsh | Windows SDK through C++/CLI | Replace interop and package `libgmsh.so`/runtime assets |
| Netgen | Expected `NetGenMesher.exe` payload | Build/adopt a Linux command-line equivalent and confirm that its CLI/output matches the PrePoMax-specific Windows helper |
| MMG | Windows `mmgs.exe`/`mmg3d.exe` | Package Linux MMG binaries and make names/permissions platform-aware |
| 3MF | Generated C# wrapper hard-codes `lib3mf.dll` | Ship Linux `lib3mf.so`, use logical library resolution, and test UTF-8 paths/calling conventions |
| OpenFOAM/Baram | Process runner already searches extensionless tools, but retains Windows batch logic and Windows-oriented directory discovery | Linux is the natural target; add shell-environment initialization, process groups, executable permissions, and actual OpenFOAM integration tests |
| Python tools | Configurable Python executable; scripts include NumPy/CuPy and workflow-specific dependencies | Prefer `python3`, declare dependencies, isolate environments, use portable paths, and test both CPU and NVIDIA/CUDA cases |
| FATLAB | MATLAB sources plus Windows `Fatlab.exe` and `.mexw64` files | Treat Windows compiled artifacts as unavailable; rebuild MATLAB/MEX for Linux with licensing/runtime implications, or designate the Python backend as the Linux-supported path |
| ZedGraph | Old WinForms chart package | Replace charts with an Avalonia-compatible chart/plot library |
| FastColoredTextBox | Old WinForms editor package | Replace keyword/editor views with an Avalonia-compatible text editor |
| Baram Python tree | Nested Python/PySide project, apparently retained mainly as a reference after native C# integration | Do not make it part of the C# application runtime unless a separate product decision requires its GUI |

The large `PMX2.4.0/BENDING_NEON36.pmx` fixture may be useful as a compatibility/performance sample, but it is not a substitute for small, versioned test fixtures.

## Recommended target architecture

```text
PrePoMax.Desktop (Avalonia)
  |-- presentation/view models
  |-- dialogs, tree, property editors, charts
  |-- Linux/Windows desktop integration
  |
PrePoMax.Application
  |-- use cases and command orchestration
  |-- UI-neutral Controller replacement/facades
  |-- progress, notifications, selection contracts
  |
  +--> PrePoMax.Rendering.Abstractions --> ActiViz/VTK implementation
  +--> PrePoMax.Platform.Abstractions  --> Linux and Windows services
  +--> PrePoMax.Persistence            --> new format + legacy importer
  +--> PrePoMax.Tools                  --> solver/mesher process adapters
  |
CaeModel / CaeMesh / CaeResults / portable CaeGlobals
  |-- no UI framework references
  |-- no Win32 or concrete renderer references
  |
Native runtime assets selected by RID
  |-- linux-x64: libgmsh.so, lib3mf.so, VTK/ActiViz assets
  +-- win-x64: corresponding Windows libraries
```

The same core should remain usable from a command-line application. The existing “no GUI” mode still constructs and runs a minimized `FrmMain`, so it is not actually headless. A genuine CLI is valuable for automated regression tests and Linux/HPC workflows.

## Recommended implementation sequence

### Phase 0: definition and characterization

- Select supported distributions, initially one baseline such as Ubuntu LTS x64.
- Define MVP features and explicitly defer low-priority UI behavior.
- Capture representative `.pmx`, `.inp`, `.frd`, `.dat`, STL, 3MF, Gmsh, OpenFOAM, and fatigue cases.
- Add golden tests for import/export, command regeneration, mesh counts, results, and solver job state.
- Record UI workflows and rendering reference images where exact behavior matters.
- Obtain and validate commercial ActiViz terms if that route is acceptable.

**Gate:** prove that a minimal Avalonia window can host the chosen VTK renderer on both Windows and Linux, with picking, resize, DPI scaling, and clean shutdown. Do not begin a broad form rewrite before this gate passes.

### Phase 1: project and core modernization

- Convert projects to SDK-style incrementally.
- Target .NET 10 for portable libraries; keep a temporary Windows build where necessary during transition.
- Split `CaeGlobals` into portable and desktop/platform portions.
- Remove `vtkControl` references from model/results projects through data contracts.
- Introduce dependency inversion around UI, rendering, processes, paths, clock/threading, and dialogs.
- Establish Windows and Linux CI for every portable project.

### Phase 2: native and process services

- Replace Gmsh C++/CLI with cross-platform interop.
- Add RID-aware 3MF and renderer native-library resolution.
- Make solver/mesher discovery platform-aware.
- Replace WMI/Win32 process handling and implement cancellation using process groups/tree termination.
- Port OpenFOAM environment setup and validate CalculiX, Gmsh, MMG, Netgen, Python, and OpenFOAM on the target distribution.

### Phase 3: persistence

- Specify a stable versioned project schema independent of assembly-qualified CLR type names.
- Implement legacy `.pmx` import and round-trip test fixtures from several historical versions.
- Migrate settings, material libraries, command history, and worker messages.
- Test corrupt, hostile, partial, protected, and very large files.

### Phase 4: renderer and UI shell

- Port the VTK pipeline behind `IRenderScene`.
- Build the main shell, menus, context switching, status/output areas, tree, selection, and settings.
- Port reusable workflows by functional slice: geometry import, mesh, FE model, analysis, results, then fatigue/Baram/specialized tools.
- Replace ZedGraph, property-grid behavior, FastColoredTextBox, custom message boxes, and WinForms-specific editors.

Porting screen by screen without first extracting application services would reproduce the present coupling in a second UI framework and should be avoided.

### Phase 5: packaging and production hardening

- Publish self-contained `linux-x64` output initially.
- Choose a distribution method: tarball plus launcher first, then AppImage/Flatpak or distribution packages as justified.
- Include licenses and native dependency notices; confirm GPL and proprietary ActiViz distribution obligations with qualified legal review.
- Test X11 and XWayland; treat native Wayland as a separately qualified target.
- Test Mesa and NVIDIA drivers, multiple DPI/scaling values, locale/case-sensitive paths, non-ASCII paths, read-only install locations, and low-memory/large-model cases.
- Maintain Windows regression coverage so the cross-platform refactor does not silently reduce existing behavior.

## Effort estimate

The estimates below are person-months for engineers already strong in C#/.NET desktop software, native interop, and testing. They include implementation and ordinary verification but not long procurement delays, solver vendor coordination, or extensive UX redesign.

| Workstream | Estimate |
|---|---:|
| Characterization, architecture seams, test harness | 2-4 PM |
| SDK-style/.NET modernization and dependency cleanup | 2-4 PM |
| Controller/application-layer extraction | 3-6 PM |
| Gmsh, 3MF, process, path, and external-tool portability | 3-6 PM |
| Project persistence and legacy compatibility | 2-5 PM |
| VTK/ActiViz upgrade and Linux rendering integration | 3-7 PM with commercial ActiViz; 6-12 PM otherwise |
| Avalonia shell and replacement of common controls | 3-5 PM |
| Feature-dialog/workflow migration | 7-12 PM |
| Packaging, CI, accessibility, performance, distribution testing | 3-6 PM |

These workstreams overlap; summing every upper bound is intentionally conservative.

### Deliverable bands

| Deliverable | Scope | Effort | Likely calendar time |
|---|---|---:|---:|
| Technical spike | Avalonia + renderer, portable core build, one Gmsh/CalculiX path | 1.5-3 PM | 6-10 weeks with 2 engineers |
| Linux MVP | Geometry import, Gmsh mesh, FE setup, CalculiX run, basic result viewing; limited dialogs and file compatibility | 14-22 PM | 6-9 months with 3 engineers |
| Broad production parity | Most Windows workflows, robust old-file support, fatigue/Baram/special tools, packaging and extensive QA | 26-45 PM | 9-15 months with 3-4 engineers |

Add contingency if any of the following is mandatory from the first release:

- pixel/interaction parity in every VTK feature;
- no commercial ActiViz dependency;
- support for several distributions and native Wayland immediately;
- full FATLAB/MATLAB Linux support instead of the Python backend;
- flawless import of every historical/corrupt `.pmx` variant;
- simultaneous major UX redesign;
- continued binary/plugin compatibility with existing third-party .NET Framework assemblies.

## Risk assessment

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| VTK hosting or picking differs on Linux | High | Critical | Renderer proof of concept as the first gate; use vendor-supported ActiViz path if acceptable |
| UI migration expands because logic is embedded in forms | High | High | Extract use cases incrementally and test behavior before translating views |
| `.pmx` migration loses old projects | Medium-high | Critical | Corpus of historical files, separate legacy importer, deterministic compatibility tests |
| No C# regression suite permits engineering regressions | High | High | Golden/core tests before structural changes; genuine headless runner |
| Third-party WinForms controls have no direct equivalent | High | Medium | Prototype property grid, tree, plot, and editor early; budget bespoke controls |
| Native-library deployment varies by distribution/driver | Medium-high | High | One initial distro/RID, self-contained builds, dependency diagnostics, GPU test matrix |
| External solver/mesher behavior differs on Linux | Medium | High | Adapter contracts and end-to-end golden cases per tool/version |
| Controller refactor destabilizes command history/regeneration | Medium-high | High | Command-level characterization tests and small vertical slices |
| ActiViz licensing is unacceptable or delayed | Medium | High | Make the renderer decision before UI investment; retain a costed open-source fallback |
| Scope includes all SpectraRev additions at once | High | High | Define an MVP and stage fatigue, Baram, SRSuntour, and MATLAB paths explicitly |

## Options not recommended as the primary solution

### Mono WinForms on Linux

Mono can run some .NET Framework applications, but it does not solve the Windows ActiViz/Mummy native dependency, C++/CLI wrapper, Win32 hooks, WMI, Windows executables, build events, and fidelity of a large custom WinForms application. It may be useful only as a disposable experiment, not as a supportable product architecture.

### Wine

Wine could potentially run a packaged Windows release sooner than a native port. That is deployment under a compatibility layer, not a Linux port. GPU/VTK integration, child tools, paths, dialogs, and support burden would remain fragile. It can be considered as a short-term user workaround, never as evidence that the codebase is portable.

### Full C++/Qt rewrite

Qt and native VTK are strong Linux technologies, but rewriting roughly 271,000 lines of C# domain/application code would discard the most reusable part of the product. This route likely exceeds **45-70 person-months** before broad parity and introduces substantial model/file-compatibility risk. It is justified only if the product intends to leave .NET entirely for reasons beyond Linux support.

### Browser UI as a shortcut

A web shell does not remove the need to refactor the controller, port VTK interaction, manage large local engineering data, run native tools, and design secure local file/process services. It is a different product architecture, not a lower-effort translation of the existing desktop application.

## Acceptance criteria for a native Linux release

A release should not be called a successful Linux port until it can:

- build from a clean checkout on a Linux CI agent without Visual Studio or Windows SDKs;
- run without Wine, Mono WinForms, Windows DLLs, or Windows executables;
- open supported existing `.pmx` projects safely and save them in a documented versioned format;
- import/export representative geometry, mesh, model, and result files without case/path corruption;
- render, select, clip, animate, annotate, and export images reliably on supported graphics stacks;
- create a Gmsh mesh and run/monitor/cancel CalculiX natively;
- clean up child processes and temporary files after cancellation or crash;
- store mutable state in user data/configuration directories, not beside application binaries;
- pass automated core, serialization, native-integration, and end-to-end workflow tests on both Linux and Windows;
- provide a documented dependency/license manifest and a reproducible package.

## Recommended decision

Proceed only if the product can fund a modernization rather than a quick compatibility patch. Start with a **6-10 week feasibility phase** whose explicit go/no-go outputs are:

1. a Linux Avalonia shell embedding the chosen ActiViz/VTK renderer;
2. a portable build of the principal domain libraries;
3. one end-to-end path: import geometry, mesh with Linux Gmsh, export/run Linux CalculiX, and display a result;
4. an old `.pmx` compatibility prototype;
5. a confirmed ActiViz licensing/deployment decision;
6. a measured list of the WinForms workflows included in the MVP.

If those gates succeed, a Linux MVP is realistic. If cross-platform ActiViz licensing is rejected and no acceptable renderer binding is demonstrated, the estimate should be revised upward before further work. The renderer decision and legacy persistence strategy are the two highest-leverage early decisions.

## External references used for platform facts

- Microsoft, [Port from .NET Framework to .NET](https://learn.microsoft.com/en-us/dotnet/core/porting/framework-overview) — .NET Framework and WinForms/WPF platform limits, Windows APIs, and porting guidance.
- Microsoft, [Migrate C++/CLI projects to .NET](https://learn.microsoft.com/en-us/dotnet/core/porting/cpp-cli) — C++/CLI support remains Windows-only.
- Microsoft, [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy) — .NET 10 is the active LTS release as of this evaluation.
- Microsoft, [`BinaryFormatter` migration guide](https://learn.microsoft.com/en-us/dotnet/standard/serialization/binaryformatter-migration-guide/) — implementation removal and migration guidance.
- Microsoft, [`System.Drawing.Common` only supported on Windows](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/6.0/system-drawing-common-windows-only) — non-Windows drawing constraints and alternatives.
- Avalonia, [Desktop Linux](https://docs.avaloniaui.net/docs/platform-specific-guides/linux) and [supported platforms](https://docs.avaloniaui.net/docs/supported-platforms) — Linux/X11/Wayland and distribution support considerations.
- Kitware, [ActiViz 9.1 Linux and macOS support](https://www.kitware.com/activiz-9-1-release/) and [current ActiViz product information](https://www.kitware.fr/activiz/) — cross-platform ActiViz availability and licensing model.
- Gmsh, [official documentation](https://gmsh.info/dev/doc/texinfo/gmsh.html) — cross-platform SDK and API availability.

## Confidence and limitations

Confidence is **high** on the architectural blockers and recommended migration shape, **medium** on the effort range, and **low-to-medium** on exact UI/renderer effort until a Linux ActiViz/Avalonia prototype exists. Static inspection cannot reveal every runtime dependency or undocumented workflow. The absence of automated C# tests increases uncertainty, as do generated/bundled binaries whose provenance and precise release packaging were not reconstructed during this read-only evaluation.

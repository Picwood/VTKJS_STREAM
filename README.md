# Browser pre- and post-processing

The current product direction is a browser workspace for **model editing →
CalculiX input export → external solving → result upload and inspection**.
The ultimate target is [Prepomac functional parity](docs/PREPOMAC_WEB_PARITY.md),
including its advanced and custom engineering workflows; this first release
does not define the final feature scope.
The [implementation roadmap](docs/PREPOMAC_IMPLEMENTATION_ROADMAP.md) orders the
work into dependency-based milestones with [matched acceptance tests](docs/PREPOMAC_PARITY_TESTS.md)
from Prepomac's existing suites.
The first local web pilot reuses the portable production core and VTK.js, with
separate workspace state and private renderer processes. See the
[online pivot, implemented scope and next release gates](docs/ONLINE_PRE_POST.md).

From this directory, build and start the pilot, then open http://localhost:5180:

```bash
bash scripts/start-web-workspace.sh --build
```

This is a local pilot with temporary workspaces. Hosted accounts, persistent
project storage and an online deployment remain delivery gates. Download PMA
projects to retain pilot work. The existing Linux/GTK evidence below remains
the engineering baseline.

# Linux development and migration

The native Linux port is in progress. Start with the [migration status and next gates](docs/LINUX_PORT_PROGRESS.md). The VTK.js server/client are portable; the complete PrePoMax desktop application still requires Windows.

For the supported GTK project workflow, run `scripts/build-linux-viewer.sh`, then
`scripts/start-gtk-viewer.sh` (or pass `PROJECT.pma` to open it immediately). See [Linux viewer prerequisites and validation](docs/LINUX_VIEWER_BUILD.md).

The .NET 10 portable build now includes STL, the CaeGlobals core, and the `FeMesh` aggregate with parts, topology, meshing configuration, renumbering, and copied transforms. The portable model imports INP, Medit and bounded Gmsh MSH 2.2 volume meshes, preserves Gmsh CAD-face/physical regions, and exports STL/Gmsh MSH 4.1. The portable [CaeResults aggregate](docs/FERESULTS_AGGREGATE_EVIDENCE.md) supports FRD preprocessing, mesh deformation and validated derived fields. It imports [CalculiX FRD meshes and raw fields](docs/FRD_IMPORT_EVIDENCE.md), reads DAT histories, [browses, plots and exports result histories](docs/LINUX_HISTORY_RESULTS.md), and includes [field values and stress invariants](docs/RESULT_FIELD_EVIDENCE.md). **5,583 checks pass on Linux**, with native Gmsh and CalculiX acceptance. The GTK application meshes CAD through a bounded cancellable Gmsh job, retains the checksummed source geometry and global remesh controls, authors the supported analysis subset including [multiline plasticity](docs/LINUX_PLASTICITY_EDITING.md), solves it and displays results through VTK.js. See the current [Linux migration status](docs/LINUX_PORT_PROGRESS.md).

**Full Linux application status:** core libraries build, but the desktop app remains Windows-only. The next gate is production model-to-solver integration; see the [current status and remaining build gates](docs/LINUX_PORT_PROGRESS.md).

```bash
bash scripts/test-portable.sh
```

With Gmsh's command-line tool and Python package installed, run the optional native workflow:

```bash
python3 scripts/test-native-gmsh.py
```

CalculiX 2.23 now passes the analytic cube solve and production DAT-reader checks in both locales. Run `python3 scripts/test-native-calculix.py`; see [solver setup](docs/CALCULIX_INTEGRATION.md).

See [native workflow evidence](docs/NATIVE_GMSH_EVIDENCE.md) for dependencies and scope.

The instructions below describe the existing Windows build.

# Prerequisites
*  Visual Studio 2022 Community (development environment) - https://www.visualstudio.com/downloads/
*  ActiViz OpenSource Edition 5.8.0 (64-bit Windows XP or later) (3D library used for graphics - must be installed only on the development PC; users do not need it) - https://prepomax.fs.um.si/downloads/

# PrePoMax Visual Studio setup
*  Download a Master branch of the PrePoMax package and extract it to a PrePoMax folder
*  Open the solution: "PrePoMax\PrePoMax.sln"

# Recreate the references to the VTK library
First delete the two existing references to the VTK library:
*  Open Solution Explorer: View->Solution Explorer
*  In Solution Explorer find the vtkControl project and then find the References branch
*  Delete the references starting with Kitware. (right click and Delete).
 
Secondly add the two references again using the following procedure:
*  Right click on References from vtkControl project in the Solution Explorer Window and selected Add Reference
*  A Reference manager window opens where you select Browse on the left side and then press the Browse button on the bottom right
*  Browse for the file Kitware.VTK.dll which should be in the ActiViz installation folder: "C:\Program Files\ActiViz.NET 5.8.0 OpenSource Edition\bin\Kitware.VTK.dll"
*  Repeat the procedure for the file Kitware.mummy.Runtime.dll: "C:\Program Files\ActiViz.NET 5.8.0 OpenSource Edition\bin\Kitware.mummy.Runtime.dll"

At last change the active solution platform using the main menu: **Build** -> **Configuration Manager** and select **x64** as the **Active solution platform**.

Start the compilation and execution of the project by pressing the Start button...

Compiling PrePoMax only creates some of its subfolders and default settings are prepared. To fully use a compiled version of PrePoMax, first look at the latest released version of the PrePoMax�s base folder. Then copy all folders that are missing in the compiled version from the released version (Models, NetGen, Solver�). Then you have to set the working folder and solvers (CalculiX) executables file name in the Settings->Calculix. In order to use the Gmsh mesher a file gmsh-4.12.dll must be copied from the release version lib subfolder to the compiled lib subfolder.

# Structure

The PrePoMax is a solution which consists of 11 projects:
*  3MF: classes for 3MF geometry export
*  CaeGlobals: global classes for all other projects to use
*  CaeJob: classes for running the analysis
*  CaeMesh: classes for FE mesh: nodes, elements, sets, ...
*  CaeModel: classes for FE model. Model contains FE mesh + materials, sections, ...
*  CaeResults: classes for FE results. Results contain FE mesh + field outputs, ...
*  GmshCaller: a stand alone executable used for running the Gmsh routines
*  GmshCommon: wrapper for the Gmsh mesher from https://github.com/tsvilans/gmsh_common 
*  PrePoMax: classes for user interface
*  STL: classes for stl geometry import
*  UserControls: classes for more complex user controls, as model tree view...
*  vtkControl: classes for 3D visualization

PrePoMax is compiled in an exe file all the other projects are compiled in dll files.

The internal structure of the program is quite complex and there are almost no comments in the code (no time to write them) but I am using very descriptive names. Each class has its own file with .cs extension. You can browse the files in the Solution Explorer.

The PrePoMax project has a Forms folder and inside it is a FrmMain.sc file/class. This is the main form. The form communicates exclusively with the Controller.sc file/class which holds all data about the model. The program records all user actions in order to be able to repeat them later (while running PrePoMax select Edit -> Regenerate) so all needed user functions/subroutines are not called directly but via Commands. There is a special Command class for each user action...

# CalculiX solver acceptance

Updated 2026-10-22.

## Ubuntu distribution package

The Debian application package recommends Ubuntu's `calculix-ccx`. The clean
Ubuntu 24.04 package harness installs version 2.21, validates it through the
installed `prepomax-linux-viewer --check-solver` entry point, and solves an
analytical C3D8 cube. All four loaded-face nodes report U1=0.001 with zero
transverse displacement. Solver discovery passes again after an in-place
application package upgrade. This provides a distribution-managed solver path;
the application package does not copy a CalculiX executable or its numerical
libraries.

CalculiX 2.21 prints a valid `This is Version 2.21` response to `ccx -v` while
returning status 201. The launcher therefore validates both that known status
and the version response instead of treating any nonzero version-query status as
a startup failure. The full analytical solve remains the functional acceptance.

Run the package lifecycle acceptance with:

```bash
scripts/test-linux-deb-clean.sh prepomax-linux-viewer.deb newer.deb
```

## Project-local CalculiX 2.23 acceptance

Validated 2026-09-16 on Linux using the user-provided executable
CalculiX/ccx_2.23/src/ccx_2.23. Its banner reports CalculiX 2.23, built
2025-10-19. The earlier CalculiX-Builder output remains a separate build path.

```bash
python3 scripts/test-native-calculix.py
```

The script prefers PREPOMAX_CCX, then an explicitly configured
PREPOMAX_CALCULIX_BUILDER target. Otherwise it uses the project-local 2.23
executable when present, falling back to $HOME/CalculiX-Builder/src/ccx_2.21_MT.
PREPOMAX_DOTNET selects the SDK, with PATH/user-local fallback.

## Runtime dependency

The supplied binary needs libgfortran.so.4; this host only provided version 5.
The first run failed in the dynamic loader and retained that diagnostic in
stderr.log. The compatible runtime was downloaded from the
[Ubuntu archive](https://archive.ubuntu.com/ubuntu/pool/main/g/gcc-7/libgfortran4_7.3.0-16ubuntu3_amd64.deb)
and extracted, not system-installed, under
$XDG_DATA_HOME/prepomax/ccx-runtime (default ~/.local/share/prepomax/ccx-runtime).
Package SHA-256: 5070f3ccfea523ff94c0f47655687b6c5f4cab0a4dcd63accd131280eff32de1.
No system library was replaced or symlinked across ABI versions.

The smoke test adds the extracted usr/lib/x86_64-linux-gnu directory to its
child environment's LD_LIBRARY_PATH when present. PREPOMAX_CCX_LIBRARY_PATH
can select another runtime directory. The script does not download dependencies;
on a fresh machine provide a compatible runtime or a locally rebuilt solver.
This local runtime arrangement is acceptance-test setup, not finished packaging.

## Evidence

NativeJob ran the model-object-generated unit-cube C3D8 input with E=1000, Poisson ratio 0
and total axial force 1. The job exited successfully. All four loaded-face nodes
(2, 3, 6, 7) have axial displacement **0.001**, zero transverse displacement,
and output time **1**. Both the independent Python checker and the production
DatFileReader validate these results; production reading passes in en-US and
it-IT. This now qualifies an actual solver-output path, not just a synthetic DAT
fixture. Complete input is assembled by the scoped
[static workflow](STATIC_MODEL_WORKFLOW.md) using production keywords, without
reading or patching an input fixture. The portable production writer now exports
prepared supported models; broader model coverage remains separate. Both
locale-generated inputs must match byte-for-byte before solving.

Each run creates a unique temporary directory and retains input, stdout.log,
stderr.log, cube.dat and other solver files. The printed directory is the place
to inspect failures. Native execution has a 120-second timeout; outer .NET
invocations have 180-second timeouts. Existing logs are never overwritten.

## Remaining boundaries

These linear-static cases do not establish nonlinear/contact, modal, thermal,
parallel solver or restart support. Portable job execution, production input
export, FRD/DAT ingestion and the bounded GTK solve/result workflow are now
connected. Broader solver cases, supported versions/distributions and progress
detail remain release work. The default portable suite remains independent of
native solver availability; native solver checks are opt-in. Windows execution
was not run locally.

## Parameterized block acceptance

The native script additionally validates changed geometry, modulus and total force
through the scoped model-object generator. Independent DAT checks confirm a long
bar (U1=0.0075), compression (U1=-0.00075) and zero load (U1=0). All four loaded-face
nodes are checked against FL/(EA), with zero transverse displacement for nu=0.
Complete generated input matches byte-for-byte across en-US and it-IT in each case.
These cases do not yet exercise general FeModel export or nonlinear material behavior.

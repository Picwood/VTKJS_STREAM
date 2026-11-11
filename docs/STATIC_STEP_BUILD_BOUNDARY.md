# Static-step build boundary

StaticStep now compiles on Linux with Step, SteadyStateDynamicsStep, step controls,
damping definitions and output definitions. Legacy imported pressure/traction
classes are linked for the existing type compatibility checks; their previews and
FeModel-dependent operations remain in desktop-only partial files. OpenFOAM import
and validation in ImportedPressure remain desktop-only as well.

CalStepHeader and CalStaticStep generate native acceptance input headers. The
single-cube and two-step acceptance cases use production step keywords, alongside
previously generated materials, sections, loads, boundaries and output requests.
Mesh and node-set definitions now come from explicitly constructed mesh objects
through production keywords. FeModel now builds with legacy methods excluded; the complete
CalculixFileWriter is not yet portable. See [aggregate evidence](FEMODEL_AGGREGATE_EVIDENCE.md).

Portable pressure, temperature and traction writers accept explicitly resolved
surfaces, nodal values or element loads. Automatic distribution evaluation,
surface integration, generated pretension nodes and preview migration remain
pending. StaticStep compiling does not imply those operations are available.

SteadyStateDynamicsStep and legacy imported-load types compile as dependencies;
this milestone does not validate harmonic solves, imported-load workflows or
complete project persistence. Windows compilation remains unverified locally.


Later integration: the production writer now builds and native acceptance uses
its prepared-model file API. See [current evidence](CALCULIX_FULL_WRITER_EVIDENCE.md);
exporter-exclusion statements above describe the earlier slice.

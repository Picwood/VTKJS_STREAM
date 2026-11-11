# Portable model materials and CalculiX keywords

The portable CaeModel project now links 35 production source files: the existing
five mesh I/O sources, ten material/property classes, eleven section/data classes,
the CalculixKeyword base and eight material keyword writers. Internal keyword
classes remain internal; the portable test assembly has friend access.

The existing ToCalculiX16String helper moved to the shared numeric extension
partial. It now formats both normal and exponential numbers with invariant
culture. Previously Italian decimal commas produced extra CalculiX fields.
The elastic-material regression failed before the fix.

Ten checks across en-US and it-IT cover material property retrieval,
temperature-dependent elastic rows, constant elastic output, forced decimal
separators and numeric token width. The full suite passes 5,141 checks.

The optional native CalculiX smoke constructs Material and Elastic objects,
generates their production keyword text and inserts it into both cube inputs.
Both native solves and downstream FRD/DAT/aggregate/history checks run with that
material block. Mesh, sections, loads and steps in those inputs remain fixture
authored. This is the first model-to-keyword integration slice, not the complete
CalculixFileWriter or FeModel port. Other material laws and section behavior
compile but still require runtime validation.

## Solid sections and concentrated loads

The portable model project now links 42 production sources. Seven additions
cover CalSolidSection, Load/CLoad, Amplitude, CalLoad/CalCLoad and CalOpParameter.
The existing phase-normalization helper moved to the shared Tools partial without
an algorithm change.

Eight checks across both locales verify section region/material references,
2D thickness formatting, axial node-set load text and the second step's op=New
load replacement. The native smoke now generates the material, solid section
and both concentrated-load blocks using these production objects and keywords.
Both cube solves and existing downstream result checks use the generated text.
Mesh, boundary conditions, steps and output requests remain fixture-authored;
full FeModel/CalculixFileWriter export is still pending. Complex load behavior
and other region types compile but are not established by these cases.

## Displacement boundary conditions

The portable CaeModel project now links 46 production sources, adding
BoundaryCondition, DisplacementRotation, CalBC and CalDisplacementRotation.
Both native cube inputs now use generated LEFT/U1 and ALLNODES/U2,U3 constraints.
Eight checks across both locales verify constrained and free directions,
nonzero prescribed-displacement formatting and model validation.

This extends generated native input to materials, solid sections, concentrated
loads and displacement boundaries. Mesh, step definitions and output requests
remain fixture-authored. Reference-point, surface and complex boundary variants
still need runtime validation. No production algorithm changes were needed for
this slice.

## Output requests and step termination

The verified portable CaeModel project now includes 121 distinct production sources.
Output definitions and production CalNodeFile, CalElFile, CalNodePrint and CalEndStep
keywords generate displacement, stress/strain, right-face history and step endings
for the native cube fixtures. Eight checks across both locales verify those requests.

CalNodePrint's FeModel-dependent constructor remains in a legacy partial; its
existing constructor accepting an explicit region name is shared. No fake FeModel
was introduced. Duplicate source includes from an unfinished step expansion were
removed; unresolved step/load preview sources remain on disk and are inventoried
in [the static-step build boundary](STATIC_STEP_BUILD_BOUNDARY.md).

Static headers and mesh data remain fixture-authored. Full Step/FeModel export
must resolve those recorded dependencies before this can be called a complete
model-to-solver path.

## Fixed and submodel boundaries

The portable project links FixedBC, SubmodelBC, CalFixedBC and CalSubmodelBC
unchanged from production. Twelve checks per locale (en-US and it-IT) cover
model DOFs versus CalculiX 2D output, node sets, resolved surface node sets,
missing surface mappings, separate reference-point translation/rotation nodes,
submodel DOF filtering, replacement operations and source-step clamping.

Validation: `bash scripts/test-portable.sh` passed 3,225 checks, including 214
mesh I/O checks. These additions have keyword-level coverage; native submodel
analysis and the Windows build were not run for this slice. No full StaticStep
or FeModel support is claimed.

## Mechanical body loads and moments

GravityLoad, MomentLoad, CentrifLoad and their three production CalculiX writers
are linked unchanged, bringing portable CaeModel to 66 sources. Eight checks
per locale verify gravity magnitude/direction, amplitude and replacement flags,
nodal and reference-point torque DOFs, centrifugal squared speed and normalized
axis with an offset origin. Invariant numeric parsing checks both en-US and it-IT.

`bash scripts/test-portable.sh` passes 3,241 checks, including 230 mesh I/O checks.
These are model/keyword checks; native solves for these load types, complex
centrifugal behavior and the Windows build have not been validated in this slice.
StaticStep still requires the pressure, traction and temperature model dependencies.

## Thermal load definitions and keywords

Five load definitions and five matching CalculiX writers are linked unchanged:
CFlux, BodyFlux, DFlux, FilmHeatTransfer and RadiationHeatTransfer. The portable
model project now contains 76 production sources. Twenty-eight checks per locale
(en-US and it-IT) cover thermal DOF 11, signed flux, amplitude/add/replacement
flags, body and surface targets, and all six face mappings for both 2D and 3D
convection and radiation. Decimal values are checked independently of locale.

`bash scripts/test-portable.sh` passes 3,297 checks, including 286 mesh I/O checks.
Native thermal solves, cavity radiation, and the Windows build were not run in
this slice. Temperature boundaries, thermal steps and complete model generation
remain pending; linking these loads does not enable a complete thermal workflow.

## Shell-edge and pretension dependencies

ShellEdgeLoad, PreTensionLoad, CalShellEdgeLoad and CalPreTensionSection are
linked unchanged, bringing the portable project to 80 production sources.
Twelve checks per locale cover all four shell edges, signed line force, rejected
face interiors and 2D use, automatic/manual pretension direction, independent
force/displacement magnitudes, and invariant section direction output.

`bash scripts/test-portable.sh` passes 3,321 checks, including 310 mesh I/O checks.
Native shell/pretension solves and Windows validation were not run for this slice.
Full pretension assembly still requires the excluded model writer; pressure,
traction and temperature previews remain the main load-related StaticStep boundary.

## Hydrostatic numerical core

VariablePressure and HydrostaticPressure bring the portable project to 82
production sources. FeModel-dependent abstract signatures, desktop preview and
compatibility overloads were extracted into desktop-only partial files. The
original numerical implementation is shared; desktop overloads delegate to it.
No FeModel placeholder or alternate pressure algorithm was introduced.

Seven checks per locale cover interpolation/extrapolation, transverse position
independence, a non-unit pressure direction, point/batch agreement, both pressure
cutoffs, zero direction and coincident pressure planes.
`bash scripts/test-portable.sh` passes 3,335 checks, including 324 mesh I/O checks.
The Windows build and native hydrostatic solves were not run. Existing single-point
one-second coefficient caching is retained; these checks do not validate immediate
parameter changes during that cache interval. Pressure previews and distribution-
based loads remain outside the portable project.

## Pressure definitions and output

DLoad, Distribution, IDistribution, CalDLoad and CalVariablePressureLoad bring
the portable model project to 87 production sources. FeModel-dependent methods
were moved unchanged into desktop-only partial files and included in the legacy
project. Portable callers supply surfaces or resolved element loads explicitly;
distribution evaluation and model-to-element pressure conversion remain pending.

Seventeen checks per locale cover constant-distribution defaults, six pressure
face labels, shell S2 sign reversal for both surface and element output, and
four 2D edge mappings with invariant numeric values.
`bash scripts/test-portable.sh` passes 3,369 checks, including 358 mesh I/O checks.
Native pressure solves and Windows compilation were not run for this slice.

## Temperature definitions and boundary output

TemperatureBC, DefinedField, DefinedTemperature and CalTemperatureBC bring
the portable project to 91 production sources. Preview and distribution evaluation
methods are preserved in desktop-only partial files. The existing desktop writer
constructor delegates to a new constructor accepting resolved nodal temperatures,
which also supports constant node-set and surface boundaries.

Seven checks per locale cover thermal DOF output, amplitude/replacement flags,
surface mapping and missing-map rejection, resolved node identities, constant
definition defaults and file source-step clamping.
`bash scripts/test-portable.sh` passes 3,383 checks, including 372 mesh I/O checks.
Native thermal solves and Windows compilation were not run for this slice.
Temperature previews and automatic distribution resolution remain pending.

## Surface traction definitions and resolved output

STLoad and CalSTLoad bring the portable model project to 93 production sources.
Preview, distribution evaluation and FeModel-based nodal force conversion remain
in desktop-only partial files. The portable writer accepts resolved CLoad arrays;
the desktop constructor delegates after performing its original conversion.

Five checks per locale cover resultant magnitude, distribution defaults, node
identity and signed force components, zero-component omission, amplitude and
replacement flags, empty output and 2D force constraints.
`bash scripts/test-portable.sh` passes 3,393 checks, including 382 mesh I/O checks.
Native traction solves and Windows compilation were not run. Automatic surface
integration and full StaticStep construction are still pending.

## Static-step integration

StaticStep, Step, SteadyStateDynamicsStep, controls, damping and remaining output
definitions now compile alongside compatibility imported-load definitions.
CaeModel references portable CaeResults for the latter's interpolation types.
Model-dependent imported-load operations remain desktop-only. The existing
enum display-name helper is shared with the portable globals project.

CalStepHeader and CalStaticStep now generate the native cube fixtures' step
headers. Nine checks per locale cover default outputs, load/boundary/defined-field
compatibility, time clamping, automatic/direct increments, no-analysis output and
nonlinear header settings. The portable project contains 114 production sources.
`bash scripts/test-portable.sh` passes 3,411 checks (400 mesh I/O checks).
Windows compilation, harmonic analysis and complete model export remain unverified.

Native validation: `python3 scripts/test-native-calculix.py` passes both the
single-cube and two-step solves with production-generated static headers. FRD/DAT
checks pass in en-US and it-IT; the cube displacement remains U1=0.001.

## Mesh and region export into native inputs

CalNode, CalElement and CalNodeSet now compile with shared ModelSpace definitions
and the pyramid-conversion enum (119 model sources total). CalNode's desktop
constructor delegates to an explicit mesh-data constructor. Coordinate formatting
now uses invariant culture for ordinary, additional and reference-point nodes.

Five checks per locale cover sorted node output, deformed coordinates, repeatable
export without mesh mutation, sorted non-mutating node sets and the cube element
keyword. The native acceptance script replaces fixture mesh/region sections using
production-generated node, C3D8 element and node-set keywords. This is explicit
keyword assembly, not full FeModel/CalculixFileWriter export.

`bash scripts/test-portable.sh` passes 3,421 checks (410 mesh I/O checks).
Windows compilation and general mesh-type export remain unvalidated in this slice.

`python3 scripts/test-native-calculix.py` passes single- and two-step solves with
the generated mesh and regions; FRD/DAT checks pass in en-US and it-IT and the
single-cube displacement remains U1=0.001.

## Complete scoped static workflow

The native script now constructs and exports complete one-/two-step cube inputs
from FeMesh, material, section and StaticStep objects, without reading or replacing
fixture text. Each step explicitly replaces boundary and concentrated-load state.
The complete input must match byte-for-byte between en-US and it-IT before solving.
Five portable checks per locale cover repeatability, mesh preservation, step
balance, separate step forces and propagation of edited load values.

`bash scripts/test-portable.sh` passes 3,431 checks (420 mesh I/O checks).
See [the workflow scope and remaining gates](STATIC_MODEL_WORKFLOW.md). This is
integration acceptance, not a replacement general-purpose FeModel exporter.

## Model properties and physical constants

ModelProperties and CalPhysicalConstants now compile unchanged in the portable
project (121 model sources). The scoped static workflow owns ModelProperties and
uses its model-space setting for node export and its constants for keyword output.
Eight checks per locale cover defaults, unset constants, invariant physical
constants, submodel result-path visibility, wear-cycle/smoothing bounds and
3D/plane-stress element compatibility.

`bash scripts/test-portable.sh` passes 5,141 checks (2,126 mesh I/O checks). Native
solves were not repeated for this slice; undefined constants emit no additional
keywords in the existing cube workflow. Full FeModel and Windows validation remain
pending. Wear and thermal workflows are not validated by these property checks.

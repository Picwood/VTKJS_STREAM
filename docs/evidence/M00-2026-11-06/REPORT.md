# M00 execution evidence

M00 is complete for baseline capture and harness qualification. The reconciled
reference outcomes are **57 passed / 20 failed API runs**, plus **12 passed /
2 failed / 1 blocked offline/custom checks**. All required run labels have an
outcome; known failures remain unresolved feature/platform gates. The two M00
acceptance cases passing does not mean the remaining roadmap features match.

Executed 2026-11-06 against the current root Prepomac sources and its rebuilt
.NET 10 runtime. The reference was not edited to make tests green. The
[summary](summary.json) and [per-run outcome index](reference-outcomes.json)
are authoritative for completion and counts; original logs/archives remain
in local ignored artifacts. The outcome index retains earlier attempts when
controlled test configuration or session-path corrections were needed.

## Qualified harness behavior

The [model comparison](model-comparison.json) and
[field comparison](field-comparison.json) pass between real Prepomac and the
owned web HTTP adapter. The model includes every sparse tetrahedral node,
element type/connectivity, explicit units/model space, elastic constants and the selected node set.
The field includes every cube displacement value as exact Float32 bits,
node IDs, frame/time, units and undeformed topology/coordinates.

All deliberate corruptions are detected:

- [Wrong document property](harness-wrong-property.json).
- [Wrong elastic material value](harness-wrong-material-value.json).
- [Wrong selected source ID](harness-wrong-source-id.json).
- [Wrong field value](harness-wrong-field-value.json).

The [harness outcome](harness-summary.json) also records preservation of the
project after a rejected edit and a read-only snapshot query. The implementation
checks the workspace revision as well as the downloaded project bytes.
These comparisons qualify the initial harness, not complete Prepomac features
or full original script execution through the browser.

## Reference findings

The baseline exposes failures that must remain visible before the corresponding
feature can be qualified. Known categories include:

| Area | Observed finding | Follow-up gate |
| --- | --- | --- |
| Structural API audit | `add_from_surfaces` has no matching replacement in the audit | M04 / M13 |
| Enum vocabulary | Pressure-source/stiffness-law enums are missing from the API vocabulary inventory | M11 / M13 |
| Meshing on Linux | Missing `lpsolve55.dll` mapping and failed Netgen/sweep/native meshing paths in multiple reference fixtures | M07 and the affected replay/cancellation cases |
| Solver qualification | The axisymmetric section-output assertion expects full-revolution values while the observed configured solver returns approximately half | M08; preserve solver build/input/output evidence |
| Automation setup | Initial child sessions lacked a test token or searched a macOS session path on Linux; dedicated profiles/path alignment were used in recorded follow-up attempts | M13 test-driver configuration |
| Screenshots on Linux | After reaching the endpoint, reference screenshot calls try to load the macOS OpenGL framework | M03 / M09 / M13 platform qualification |
| PMX verification | The shipped `cooperhood` project opens, then the verifier crashes while iterating a null user-keyword collection | M01: distinguish verifier failure from a failed project open |

Failures are recorded as failures, not silently skipped or rewritten as passing
web behavior. A private-model chamfer verification remains explicitly blocked
without a supplied consented real-model fixture. This does not suppress the
generated SRSuntour/chamfer checks.

## Additional acceptance evidence

The unchanged custom reference verifiers pass solver integration, cylinder UI/
lifecycle, custom-feature integration, fatigue CPU checks, file-tool offline/
application behavior, pressure-spring generation/solve, result-spring import,
SRSuntour generated cases and navigation. The individual-color source GPU check
reports the physical NVIDIA device and red/blue/unassigned-gray pixel samples
`[255,0,0]`, `[0,0,255]`, `[153,153,153]`. This is a small fixture qualification,
not a large-model benchmark or web individual-color implementation.

Candidate regressions pass the existing HTTP workflow/isolation checks and
2,892 mesh/project characterization checks. The parity diagnostic route is
absent from normal product launches. See [harness operations and scope](../../PARITY_HARNESS.md)
for reproducible commands and the snapshot schema limitations.

## Source and artifact boundaries

The outer Prepomac repository and nested VTKJS repository have independent
source identities. The reference source/runtime archive hashes and any
independent candidate capture are recorded in the summary. Candidate work does
not invalidate an unchanged Prepomac reference; reference source/runtime drift
does prevent evidence publication. Personal project files and runtime settings
are not included in the source bundle. Shipped PMX example fixtures have their
own hashes/capture.

Reference baseline failures and blocked prerequisites do not establish parity.
M00 completion means the source baseline and comparison mechanism are recorded;
later milestones still need their full behavioral, file, numerical, browser
and platform acceptance evidence.

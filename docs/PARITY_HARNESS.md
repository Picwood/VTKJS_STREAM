# Executable parity baseline and comparison harness

Implemented for M00 on 2026-11-06. The [execution evidence](evidence/M00-2026-11-06/REPORT.md)
and [machine-readable summary](evidence/M00-2026-11-06/summary.json) separate
reference outcomes from the bounded desktop/web comparisons. See the
[roadmap](PREPOMAC_IMPLEMENTATION_ROADMAP.md) for the wider feature gates.

## Repository boundaries

The Prepomac reference is the outer repository at the workspace root. The
`VTKJS_STREAM` directory is a **separate nested Git repository**, with its own
history and dirty working tree. Root `git ls-files` does not traverse that
repository. The captured reference archive therefore freezes Prepomac root
sources/runtime inputs; candidate sources and compiled web assemblies are
identified independently. Do not use one repository's commit ID as the other's
identity, or discard either working tree's existing changes.

Capture the nested candidate independently from the trial root:

```bash
python3 scripts/capture-prepomac-baseline.py --output artifacts/parity/candidate-new --candidate-only
```

Large source/runtime archives and raw process logs stay in ignored
`artifacts/parity/`. Small semantic snapshots, comparisons and outcome indexes
are retained in `docs/evidence/M00-2026-11-06/`. The current captures are local
evidence, not a hermetic OS image or a public release. Third-party tools/drivers
are identified by version/hash where available. Runtime settings and personal
PMX/PMA files are excluded from source bundles; shipped PMX fixtures have a
separate capture.

## Reference execution

Build Prepomac from the workspace root without changing its sources:

```bash
bash macOS/build.sh Release
```

From `PERSONAL/Linux_Port_trial/VTKJS_STREAM`, create a **new** output directory:

```bash
python3 scripts/capture-prepomac-baseline.py --output artifacts/parity/reference-new --run-api
```

The runner uses Prepomac's existing test collector, declared expected exits,
report checks and generated-result dependencies. It executes the unchanged
scripts, retains stdout/reports for successful and failed runs, passes
`--noSettingsSave`, and uses Xvfb where available. The default timeout preserves
the source suite's own timeout and margin. An explicitly requested timeout cap
is recorded as blocked, never passed.

The initial run reads the existing reference configuration without saving it.
Its observed solver invocation is retained in the logs. To qualify automation
with a controlled profile, use a fresh private settings file and pass `--settings`.
`tests/parity/create_reference_settings.py` runs inside Prepomac to create that
profile from defaults, with a new token and explicitly selected solver. It never
copies the user's token or prints the generated one. Its environment inputs are
`PREPOMAX_PARITY_SETTINGS` and `PREPOMAX_PARITY_SOLVER`. Keep the containing
directory private and remove the profile after the test processes exit.

Targeted cohorts reuse the frozen capture:

```bash
python3 scripts/capture-prepomac-baseline.py --output artifacts/parity/reference-cohort-new --reference-manifest artifacts/parity/reference-2026-11-06/capture.json --settings /absolute/path/to/private-test-profile.pmx --run-api --filter Test_Mcp Test_Rpc
```

The configured runner aligns `LOCALAPPDATA` and `XDG_DATA_HOME` in an isolated
session directory. This adapts existing clients that otherwise search a macOS
fallback directory on Linux. It does not change their assertions. Earlier
failed attempts remain in the outcome index. A new profile/driver adjustment
is not silently presented as the original configuration.

Run custom verifiers using their own generated or shipped fixtures:

```bash
python3 scripts/run-prepomac-custom-baseline.py --output artifacts/parity/custom-new --solver /absolute/path/to/qualified-ccx
```

The tool invokes embedded child scripts through Prepomac, exercises file-tool
offline/application paths and the individual-color GPU check, and captures
the shipped PMX files. The private real-model chamfer regression stays blocked
unless an explicitly consented `--real-model` is supplied. Successful GPU pixel
checks qualify that fixture on the reported device, not the outstanding 3M
physical-GPU performance gate. CUDA/MATLAB/FATLAB qualification is separate.

## Differential cases

Run against the captured reference binaries:

```bash
python3 scripts/run-web-parity.py --output artifacts/parity/harness-new --baseline artifacts/parity/reference-2026-11-06/capture.json
```

The runner checks the reference runtime hashes, builds the candidate probe/web
host, prepares small PMA fixtures, executes the unchanged Prepomac application
with an embedded snapshot adapter, and performs real cookie-owned web HTTP
operations. All inputs, bounded adapter sources and web assembly identities are
recorded. Each run owns and shuts down its web host and process groups.

The controlled cases are:

1. Document model-space/explicit unit-system and elastic modulus/Poisson ratio
   readback on a sparse tetrahedron, including a reference API material creation.
2. Creation/readback of node set `PARITY_SELECTED = [10, 30]`, complete mesh
   coordinates/types/connectivity, and rejection of an unknown node without
   changing the model/revision.
3. Every nodal `DISP.U1` value of the immutable cube FRD at step 1/increment 1,
   with source IDs, unit, time, topology and undeformed coordinates.

The initial application snapshot query is registered only when
`PREPOMAX_WEB_PARITY=1`. The ordinary workspace ownership and serialization
middleware applies. The query is read-only and capped at 100,000 nodes/elements.
Normal launches omit the route; the ordinary HTTP regression asserts HTTP 404.
It exposes no arbitrary code execution, filesystem path parameter or credentials.

These are **bounded harness qualification cases**. They are not complete runs
of `Test_Model_Document.py`, `Test_Selection_Correctness.py` or
`Test_Result_Field_Outputs.py` through the web, nor graphical selection, general
result support or complete model persistence. Full-script web coverage remains
separately tracked as uncovered.

Both adapters receive explicit `M_KG_S_C` state. A prepared PMA supplies that
state to the web app; this does not claim the current mesh-import UI's
`Undefined` default is equivalent. The source API and controlled fixture setup
remain distinguishable from the user-facing import workflow.

## Comparator contract

`tests/parity/compare.py` rejects unknown/incomplete schemas, empty/malformed
meshes, duplicate/unordered IDs, missing field rows, invalid connectivity and
unknown region members. It compares concrete element types and every captured
value rather than sampled summaries. A bad reference snapshot raises an error;
a bad candidate generates a failing report with a path.

Raw field values are represented as signed **Float32 bit patterns**, keyed by
source node ID. This avoids false differences between Python's expanded Float32
decimal representation and .NET's shortest JSON float representation while
preserving exact raw values. Coordinates/time remain finite numeric values.
The first reader is for the finite, static field fixture; non-finite/complex/
derived-layout qualification remains future work.

The desktop result viewport deforms `CurrentResult.Mesh` in place. Its adapter
therefore reads **undeformed nodes through the public result API**, rather than
confusing display coordinates with source geometry. This was diagnosed without
relaxing the comparator. No rounding, ID dropping or broad numeric tolerance
was added to make a difference disappear.

Each harness run deliberately changes a document property, elastic material
value, selected source ID and field value in copied candidate snapshots; all
four must fail. The retained reports
show the difference paths. Compare a retained pair directly with:

```bash
python3 tests/parity/compare.py docs/evidence/M00-2026-11-06/reference-field.json docs/evidence/M00-2026-11-06/web-field.json --output /absolute/path/to/new-comparison.json
```

The comparator refuses to overwrite an existing output. The fixed fixtures
also retain the rejected-edit and read-only-query preservation checks.

## Evidence and roadmap status

`scripts/record-parity-evidence.py` verifies the reference source/runtime/archive
hashes, rechecks both snapshot pairs with the current comparator, retains all
reference attempts, and reconciles the last recorded outcome per run into the
plan. It updates the generated catalogue. `--complete-m00` refuses missing
reference outcomes or an unfinished suite. The baseline may legitimately contain
reference failures and explicitly blocked prerequisites: capturing them is M00;
claiming full parity while they remain unresolved is forbidden by the final gate.

The record command supports `--additional-reference` cohorts and an independent
`--candidate-capture` manifest for the nested repository. Never mark a full
source script's web coverage complete merely because these bounded comparisons
match. Keep archived logs local when they contain workspace/configuration paths;
publish only reviewed summaries and redistributable fixtures.

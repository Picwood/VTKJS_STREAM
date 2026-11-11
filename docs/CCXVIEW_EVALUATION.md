# ccxview evaluation for the Linux port and VTK.js renderer

Assessment date: 2026-10-15. Decision status: **recommendation accepted by the user**.
Implementation now includes [result sessions](LINUX_RESULT_SESSIONS.md) and
[indexed binary field access](LINUX_INDEXED_RESULTS.md), with large-model measurements pending.
The assessment below records the pre-implementation baseline; replacement of the renderer remains deferred.

## Recommendation

Keep the portable C# engineering core, PMA persistence and VTK.js renderer as the main application architecture. Use ccxview first as a comparison tool and a source of specific implementation ideas. Its strongest contributions are **on-demand result decoding, retained displacement buffers, a cohesive post-processing interface, and a useful CalculiX fixture corpus**.

ccxview is already substantially broader than our current Linux viewer in interactive post-processing. It does not replace our model-authoring, persistence or solver workflow. Replacing the renderer would also discard capabilities we already have in the Windows/VTK.js integration and create another integration project. A small external-viewer experiment is a reasonable option; embedding or forking the whole application is not justified by the evidence collected here.

The highest-priority investigation should be the path from an archived result to a selected field on screen. Our recent streaming work reduced memory significantly, but it does not yet provide ccxview-style lazy field access. This opportunity exists without changing rendering libraries.

## Scope and evidence

- **ccxview:** local `ccxview/` checkout, version **0.1.15**, commit `41304fca6d9376fc9aedd99a53eb0800fb5a1735`, dated 2026-10-13. The public [release listing](https://github.com/BasemRajjoub/ccxview/releases) was checked on the assessment date and showed v0.1.15 as latest.
- **Our project:** current working tree, based on `ae5954ddefb5fbe017a35fa93594a6fdb7627b6c`, including the substantial uncommitted Linux-port changes. The commit alone does **not** reproduce the implementation assessed here.
- Three baselines are distinguished: the portable Linux application, the shared VTK.js renderer/Windows adapter, and legacy PrePoMax functionality. A feature missing from the Linux UI is not necessarily missing from our libraries or Windows application.
- Source inspection covered readers, result loading, mesh/section construction, rendering interfaces, application workflows, tests and release scripts. ccxview's headless checks were run locally; no graphical or physical-GPU comparison was performed.
- Our existing validation and performance documents are cited as **previously recorded evidence**, not freshly repeated measurements. The current Linux progress page reports 5,141 checks; the top-level README's older figure is not the baseline used here.

The current source takes precedence over old descriptions. For example, ccxview's initial [design document](../ccxview/docs/spec.md) lists features as non-goals that are now implemented and describes six cache slots; the current code has eight. Our early displacement milestone describes only linear hexahedra, while the current scene exporter also handles quadratic faces.

## What ccxview does

### Architecture and data flow

ccxview is a C99 application using Sokol for the window/GPU layer and Nuklear for its interface. The native Linux path uses X11/OpenGL; Windows and a browser/WASM build are also packaged. Its headless engineering modules are separated from the graphical application, but the application layer uses shared global state. There is no ready-made equivalent of our renderer service contract.

The important stages are:

1. **Map and index the file.** Native FRD input is memory-mapped. Geometry and connectivity are read, while field descriptors record names, components, step information and byte offsets. ASCII and binary FRD are handled.
2. **Store a compact mesh.** Coordinates and fields use flat float arrays. Element connectivity uses offset/connectivity arrays; node IDs map to dense indices through a flat table or hash map. Exterior surfaces and edges are built from visible elements.
3. **Decode selected fields.** `cache_get` decodes a `(step, field)` only when needed, applies coordinate/unit transformations and retains recently used arrays. The cache has eight slots and a nominal 768 MiB budget. A single oversized field can exceed that budget; it is not a process-memory limit.
4. **Retain GPU data.** Position, displacement and scalar buffers are separate. Ordinary deformation scaling and color-range changes use uniforms; visibility changes rebuild index buffers. Geometry-dependent overlays and cuts can still require CPU work.
5. **Compose post-processing tools.** Probe, selection, labels, history/path plots, engineering calculations and exports operate on the same loaded model and active field.

Sources: [FRD contract](../ccxview/src/frd.h), [file mapping](../ccxview/src/os.c), [mesh representation](../ccxview/src/mesh.h), [field cache](../ccxview/src/app_field.c), [cache slots/application state](../ccxview/src/app.h), [rendering interface](../ccxview/src/render.h).

### Feature comparison

“Implemented” below means supported by inspected source/documentation; it does not mean every tool received independent numerical or visual validation in this assessment.

| Capability | ccxview 0.1.15 | Our current project | Implication |
|---|---|---|---|
| Open solver files directly | FRD ASCII/binary, INP, DAT integration-point blocks, FBD geometry and related sidecars | Linux workflow centers on PMA projects; INP mesh import and FRD/DAT library readers exist. Current FRD reader uses text lines | Consider a read-only direct-result workflow and binary FRD support |
| Result loading | Field index plus lazy decoding/cache | PMA payload streaming exists, but listing/selecting binary results still reconstructs `FeResults`; FRD path reads all lines and fields | Highest-value performance investigation |
| Contours and deformation | Interactive fields, scaling, animation, imaginary displacement basis | Linux exposes static real fields and explicit scaling through scene republication. Windows/VTK.js has broader result/animation support | Reuse existing seams before adding a new protocol |
| Quadratic surfaces | Uses midside nodes; optional corner-only display | Linux already triangulates 6-node faces into four triangles and 8-node faces into six | This is existing overlap, not a missing capability. Compare interpolation and visual accuracy |
| Clip planes and filled cuts | Volume-cell cuts; reusable projections/ranges when translating a plane | VTK.js server already has a section-geometry builder and explicit capacity bounds; full Linux workflow parity is not established | Benchmark and compare semantics before porting an algorithm |
| Probe, selection and annotations | Node/element details, CAD-style box selection, extrema, sets/materials, label thinning | Shared renderer has source-ID selection, spatial culling, annotation and overlay seams; Linux authoring has bounded node-set workflows | Improve Linux tool exposure and label readability; preserve existing selection architecture |
| Histories and paths | FRD histories, surface paths and plotting | CaeResults supports histories, interpolation and history export; Linux UI exposure is limited | Build on our existing calculations and use ccxview as a UX reference |
| DAT results | Integration-point data and Gauss-point display; **does not read nodal prints/totals** | Our DAT/history path includes displacement histories | Readers are complementary; ccxview is not a drop-in replacement |
| Derived results | Formulas, tensors, trajectories, stress linearization, shell resultants, failure criteria, reinforcement and mesh-quality tools | Invariants, equations, coordinate transforms, interpolation and derived histories exist in CaeResults; many specialized ccxview tools have no equivalent identified in this review | Select tools by actual analysis needs, then validate independently |
| Contact investigation | CONTACT fields, `.cel` geometry, warning-node sets and contact overlays | Portable model persistence includes contact definitions; this does not establish equivalent Linux contact-result tooling | High-value candidate if brake/contact work is representative |
| Model/run workflow | Deck inspection and post-processing; preprocessing explicitly out of scope | Bounded material/load/boundary/node-set editing, CalculiX jobs, cancellation, project save/reopen and PMX conversion | Preserve our application/core investment |
| Export and sharing | PNG, MP4, CSV, VTK; scriptable commands and view sidecars | Linux reference capture/fingerprints and history export; Windows browser PNG/AVI export exists | Useful product additions, but not all are new renderer requirements |
| Packaging | Native release archives and single-file browser build | Relocatable Linux preview exists; production shell/package/GPU qualification remains open | Learn from release automation; native Wayland/accessibility still need separate evaluation |

Our implementation references: [Linux status](LINUX_PORT_PROGRESS.md), [ProjectResults](../portable/PrePoMax.Application/ProjectResults.cs), [ProjectScene](../portable/PrePoMax.Application/ProjectScene.cs), [FRD reader](../CaeResults/FileInOut/Input/CalculiX/FrdFileReader.cs), [FeResults](../CaeResults/FeResults.cs), [renderer status](../prototypes/VtkJsStreaming/README.md), [pending work and completed integration seams](VTKJS_PENDING_WORK.md), [section builder](../prototypes/VtkJsStreaming/src/PrePoMax.Render.Server/SectionGeometryBuilder.cs).

ccxview feature references: [features](../ccxview/docs/features.md), [result semantics and limitations](../ccxview/docs/results.md), [usage/export](../ccxview/docs/usage.md), [deck interpretation](../ccxview/docs/keywords.md), [open limitations](../ccxview/TODO.md).

## Improvements worth investigating

Effort below is relative engineering scope, not a delivery estimate. Each item needs its own measured acceptance before adoption.

### 1. Retain a result session and decode fields on demand — first priority

`ProjectResults.List` loads the result collection to enumerate fields. `Values` loads it again for a field selection; `ViewerHost` handles each `field` request by publishing a scene. `ReadBinaryArchive` streams the archive to a temporary result file, but `ReadCollection` then reconstructs the collection. The existing optimization avoids materializing the editable mesh and all archive envelopes together; it does not make the result fields lazy.

The previously recorded brake-project figures are 7.93 seconds/about 2.85 million KiB RSS for field listing and 33.55 seconds/3,632,488 KiB for GTK model-to-MISES. These operations include much more than FRD parsing. They identify a practical target, not a measured ccxview speedup. See [result-streaming evidence](LINUX_RESULT_STREAMING.md).

Proposed progression:

- Instrument archive hashing/copying, result decoding, surface extraction, upload and first stable paint separately.
- Introduce a bounded result session keyed to immutable project content, with explicit close/replacement/cancellation. Initially it can retain the existing decoded result to measure the latency/memory tradeoff.
- Add an indexed result accessor for catalog and selected-field access. FRD can use offsets; the current serialized `results.bin` may need an index/cache or a versioned format rather than a small reader change.
- Preserve archive integrity checks. Reuse a verified immutable snapshot inside a session; do not simply bypass hashes on subsequent requests or trust a mutable pathname.

**Effort:** medium for lifecycle/profiling, high for a fully lazy compatible storage path. **Acceptance:** unchanged field catalog, source IDs and fingerprints; malformed archives still rejected; bounded memory across repeated frame changes and project replacement; measured cold/warm latency and RSS on the same real project.

### 2. Reuse topology and displacement data — second priority

Linux `ProjectScene` currently writes complete surface geometry for each selected field and applies displacement on the CPU. ccxview demonstrates the benefit of keeping reference positions plus displacement vectors and varying a scale uniform. Our shared renderer already supports scalar-only updates and animation-frame transport; this should be evaluated before creating another update mechanism.

Start with scalar-only field changes on unchanged topology. Then measure whether retained displacement bases justify extending the Linux host path or protocol. This aligns with [PEND-010](VTKJS_PENDING_WORK.md#pend-010--retained-displacement-basis-transport), whose adoption is explicitly conditional on a demonstrated benefit.

**Effort:** medium to high. **Acceptance:** no topology re-upload for scalar-only changes; zero-scale restoration; correct extrema, picks, sections, camera bounds and exports at positive/negative scales; exact frame acknowledgement; memory and latency improvement. Preserve our double-coordinate/origin-relative rendering boundary rather than copying ccxview's float mesh representation wholesale.

### 3. Make partial results useful without concealing missing data

ccxview renders missing field values as no-data, enabling sparse CONTACT fields to color only relevant nodes. Our FRD layer already has partial-field characterization, but the portable application's `Values` method rejects any non-finite selected nodal value. That application boundary needs an explicit policy before equivalent sparse-result tools can work.

Represent field presence separately from invalid geometry or corrupt input. Exclude absent values from extrema and show their meaning in probes, legends and export. Keep a complete finite displacement requirement where deformation depends on it. Do not copy the permissive reader policy into editable-project validation.

**Effort:** medium across result contracts, viewer and export. **Acceptance:** sparse contact fixture, explicit no-data display, correct finite ranges, no invented zero stresses, and unchanged rejection of invalid mesh connectivity.

### 4. Turn existing core capabilities into a coherent Linux post-processing workflow

Prioritize field/frame navigation, node/element probe, history plotting, selected-region extrema, unit labels and deformation controls. Our equations, invariants and history/export code should remain the calculation authority. ccxview's layered display, step-aware load symbols, screen-space label thinning and persisted view settings are useful interface references.

The label implementation provides a coarse spatial pass plus screen-space spacing and pinned-label handling. This is more directly reusable as an algorithm/design than its Nuklear UI code. See [label interface](../ccxview/src/label.h).

**Effort:** medium, incremental. **Acceptance:** users can select a result, identify its step/units, probe it, graph a history and reproduce/export the view through the Linux application; labels remain readable at representative density and DPI.

### 5. Use the corpus and an independent reader for numerical comparisons

The samples cover element families, shells, thermal symbols, model changes, contact, ties and multi-step cases. Reuse a pinned selection as differential fixtures, retaining provenance and applicable notices. Compare raw components by source ID before comparing MISES, transformations, deformation or screenshots.

Our deterministic fingerprint/reference-capture tools are a good starting point. ccxview should be one independent implementation, not the sole correctness oracle: analytic solutions and solver output remain necessary. Numeric tolerance is needed where float arithmetic, invariants or element averaging differ; hashes only establish exact equality under matched representation.

**Effort:** small to medium. **Acceptance:** a documented shared corpus with matching node/element IDs, step/increment/time, component order, coordinate systems and units, plus explicit explanations for every discrepancy.

### 6. Add specialized engineering tools only after choosing a use case

For contact-heavy work, investigate `.cel`/warning-set overlays and sparse CONTACT fields first. For shell work, investigate resultants; for pressure-vessel work, investigate stress linearization. These are independent numerical features, not reasons to replace the renderer.

Useful source starting points are [contact](../ccxview/src/contact.c), [shell integration](../ccxview/src/shell.h), [linearization](../ccxview/src/app_linearize.c), [failure criteria](../ccxview/src/failure.c), [quality metrics](../ccxview/src/quality.c) and their tests. Algorithm names and built-in presets do not establish engineering suitability. Validate coordinate conventions, shear conventions, layer averaging, thickness/offset handling and applicability against agreed cases before exposing calculations as accepted results.

**Effort:** medium to high per feature. **Acceptance:** analytic/solver reference cases and documented limitations; no general design-code compliance claim inferred from the presence of a formula.

## Where copying the implementation would be a mistake

- **Performance claims are not comparable measurements.** ccxview reports roughly 0.45 s parsing/0.89 s surface construction for a synthetic 5-million-element binary FRD on one desktop. Our 3-million-source-node renderer gate and the real PMA workflow measure different data and stages. Neither establishes which application wins on our models. The published timings were not reproduced here. [ccxview build/timing notes](../ccxview/docs/build.md)
- **Lazy decoding still has costs.** An index pass must find field blocks; ASCII scanning, file mapping, mesh storage, decoded fields and GPU resources all consume time or memory. The 768 MiB cache budget does not bound total RSS.
- **A permissive viewer is not an editable-project validator.** ccxview skips some malformed/unsupported records with messages. Our archive and selected-field checks serve different purposes. Differential tests must compare warnings and omitted data as well as success codes. [FRD implementation](../ccxview/src/frd.c)
- **The browser build is a separate deployment tradeoff.** It uses WebGL2/WASM, an in-browser filesystem, a maximum WASM memory setting of 4 GiB and synchronous execution of the loader where native builds use a worker. It is not a streaming client for our .NET service, and large-file responsiveness must be measured separately. [Makefile](../ccxview/Makefile), [OS abstraction](../ccxview/src/os.c), [web adapter](../ccxview/src/web.c)
- **Native packaging still depends on the platform.** Linux bundles a launcher, binary and supporting libraries; optional Mesa software rendering adds size. X11/OpenGL does not establish native Wayland support, accessibility or IME behavior. The “small executable” pitch is not a complete distribution comparison. [bundle script](../ccxview/scripts/bundle.sh), [GPU fallback](../ccxview/src/gpu.h)
- **Precision and selection matter.** ccxview's stored mesh coordinates are float, and its `cv_pick_ray` scans surface triangles. Our host uses double input coordinates with origin-relative Float32 rendering and has spatially culled GPU/source-ID selection. Preserve those properties unless a measured alternative is better. [mesh implementation](../ccxview/src/mesh.c), [host upload conversion](../prototypes/VtkJsStreaming/src/PrePoMax.Render.Server/HostSceneUploadReader.cs)
- **Some attractive tools have explicit limits.** DAT nodal prints are omitted; cyclic display repeats sector values without nodal-diameter expansion; run subtraction uses step index and requires matching model assumptions; shell transverse shear and reinforcement have documented limitations. They require case-specific acceptance. [result semantics](../ccxview/docs/results.md), [TODO](../ccxview/TODO.md)
- **Release maturity is still early.** v0.1.15 is a useful working implementation with substantial tests, but the workflow runs on version changes/manual dispatch rather than every ordinary push. The checkout references a separate `internal` submodule; it was not needed for the public headless build used here. Do not assume all development assets are included. [release workflow](../ccxview/.github/workflows/release.yml), [submodules](../ccxview/.gitmodules)
- **Reuse needs provenance.** ccxview declares GPL-3.0-or-later; our root license contains GPL v3. Vendored libraries/fonts have separate notices. Any later copy, wrapper or distributed bundle should record the exact source version and retain the relevant notices; this assessment does not decide distribution/licensing details. [ccxview license](../ccxview/LICENSE), [our license](../LICENSE), [vendor inventory](../ccxview/vendor/README.md)

## Local validation performed

Environment: Linux x86_64, kernel `7.0.0-38-generic`, GCC `15.2.0`. Commands and output excerpts are retained in [evaluation evidence](evidence/ccxview-2026-10-15.txt).

| Check | Result | What it establishes |
|---|---|---|
| `make -j4 test bench` | **30,279 checks, 0 failed**; 99-file source-list check and launcher test passed | Public headless build and upstream tests work here. Test code uses ASan/UBSan, except the explicitly excluded vendored MP4 wrapper |
| `build/bench --quiet` on bundled `samples/*/*.frd` and `samples/*/*.inp` | **34 files, zero nonzero exits** | Reader/surface smoke coverage; not independent numeric correctness of every field |
| `make fuzz` | **9 inputs × 300 mutations = 2,700 cases**, all completed | Bounded mutation smoke. This target uses the ordinary benchmark binary, not the sanitized test binary |
| Our `cube.frd` | 8 nodes, 1 element, 1 increment/4 fields; D1 range 0–0.001, SXX 1 | Small shared-fixture agreement with our expected analytic case |
| Our `cube-two-step.frd` | 8 nodes, 1 element, 2 increments/8 fields; last D1 0–0.002, SXX 2 | Small multi-step reading check |

Compiler warnings were present in test/vendor code; the build completed. The graphical application, UI tests, WASM build, large-model performance, original-PrePoMax visual comparison and specialist engineering tools were not independently exercised. Our complete portable suite was not rerun for this documentation-only assessment. The upstream assertion count and our portable check count are not comparable measures of coverage.

## Options for the later decision

| Option | Benefit | Cost / unresolved issue | Assessment |
|---|---|---|---|
| Keep architecture; adopt selected ideas and tests | Improves the slow result path and Linux usability while preserving current work | Requires result-session/storage and UI work | **Recommended default** |
| Offer ccxview as an optional external post-processor | Quickly exposes mature post tools for raw CalculiX files | Separate state/UI; export and sidecar lifecycle; no PMA/PMX reader | **Worth a bounded experiment** |
| Wrap selected headless C modules | Could reuse a reader or specialist algorithm | Native ABI, memory ownership, platform packaging and duplicated engineering semantics | Consider only for a measured, isolated advantage |
| Embed WASM UI or replace the viewport | Broad ready-made post UI | Rebuild project/selection/command synchronization, acceptance and accessibility; not an existing adapter | Defer |
| Fork ccxview as the main application | Small native rendering stack | Preprocessing, PMA/PMX, jobs and engineering-core integration still required; two stacks to maintain | Not supported by present evidence |

An external-viewer experiment should pass an isolated FRD plus the matching INP/DAT and required sidecars. PMA projects containing only `results.bin` cannot be handed over directly: they need a result exporter or access to the original FRD. Preserve units, step identities and expanded-shell/result-mesh correspondence. The application's authoritative project state should remain in PMA; ccxview view sidecars would be separate artifacts.

Before committing implementation effort, collect one shared decision package:

1. **Correctness:** analytic cube/two-step cases, a quadratic solid, a shell, sparse contact and the representative brake results, where equivalent FRD is available. Match fields, units and IDs; document reader warnings and missing values.
2. **Performance:** same workstation/GPU, viewport and files; at least three runs with cache conditions recorded. Measure open-to-first-stable-frame, field/frame switch, scale interaction, selection and section latency, peak RSS and available GPU-memory evidence. Separate archive validation from parser and renderer timings.
3. **Workflow:** open, inspect, probe, change field/frame, deform, plot and export. Include X11/Wayland session behavior, DPI and keyboard/accessibility checks for any proposed native replacement.
4. **Decision:** choose the first improvement using those results. Stop a replacement experiment if it cannot retain our required workflow or shows no material measured benefit. Agree numeric performance targets before implementation rather than inferring them from ccxview's README.

The current evidence is sufficient to prioritize result-session/lazy-access investigation and shared-fixture validation. It is not sufficient to choose a replacement renderer or accept all of ccxview's engineering calculations.

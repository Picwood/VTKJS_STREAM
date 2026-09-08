# WP5 benchmark report

Status: physical-GPU benchmark completed on Windows x64 on 2026-08-05. Linux x64 functional qualification passed on 2026-09-01; a Linux physical-GPU benchmark remains pending and no Linux performance result is inferred from Windows or software WebGL.

## Reference system and method

- OS: Windows 10 x64.
- .NET SDK/host: 9.0.316 / 9.0.18; application target `net8.0`.
- Node.js/npm: 22.14.0 / 10.9.2.
- Browser: Chrome 150.0.0.0, headed, DevTools closed.
- GPU: Intel(R) UHD Graphics, ANGLE D3D11; this is the available integrated-GPU reference system.
- Canvas: 1920×1080 CSS and backing pixels, device pixel ratio 1.
- Dataset: 2000×1500, 3,000,000 unique source nodes, 2,996,501 logical quads, and 5,993,002 rendered triangles.
- Layout: 30 chunks of at most 50 cell rows; 3,058,000 chunk point tuples include 29 duplicated boundary rows.
- Transport: concurrency 3, 8 MiB configured resource cap, 2,398,800-byte largest response.
- Statistics: medians of three fresh server/browser Release runs. Orbit uses 60 frame samples per run; point-pick p95 uses 30 completed client/server picks per run.

The three run summaries are committed as [wp5-windows-runs.json](evidence/wp5-windows-runs.json). A complete additional run containing every orbit and pick sample is [wp5-windows-final-run.json](evidence/wp5-windows-final-run.json). The benchmark always emits the same full JSON as a Playwright attachment and between `WP5_RESULT_BEGIN`/`WP5_RESULT_END` markers.

## Quantitative gates

No threshold was changed. All gates passed on the documented integrated-GPU system.

| Metric | Target | Hard failure | Median result | Status |
|---|---:|---:|---:|---|
| server ready to manifest | ≤ 2 s | > 5 s | 665.6 ms | Pass |
| manifest to first visible full-resolution chunk | ≤ 5 s | > 12 s | 62.9 ms | Pass |
| complete 3M-node loopback load | ≤ 15 s | > 30 s | 1,610.9 ms | Pass |
| longest main-thread task during load | ≤ 200 ms | > 1,000 ms | 0 ms ≥50-ms tasks observed | Pass |
| steady orbit, integrated GPU | ≥ 20 FPS | report-only without reference system | 50.25 FPS | Pass |
| point-pick latency | ≤ 100 ms p95 | > 250 ms p95 | 92.8 ms p95 | Pass |
| scalar update including download/activation | ≤ 500 ms | > 1,500 ms | 462.7 ms | Pass |
| browser JS/array-buffer peak above baseline | ≤ 700 MiB | > 1.2 GiB | 262.8 MiB | Pass |
| approximate VTK.js GPU memory | ≤ 700 MiB | > 1.2 GiB/context loss | 120.84 MB | Pass |
| backend managed-memory peak above baseline | ≤ 400 MiB | > 800 MiB | 9.39 MiB | Pass |
| retained memory after five unloads | ≤ 10% | > 25% | 1.193% | Pass |

The browser figure is `Runtime.getHeapUsage.usedSize + backingStorageSize` after forced collection, so it includes live array-buffer backing stores. VTK.js supplies the approximate GPU allocation. Backend memory is sampled from `GC.GetTotalMemory(false)`; process peak working set is retained in the raw run. These are different memory domains and are not added together.

## Multi-million selection addendum (2026-08-31)

The same canonical scene now includes a physical selection acceptance. Three fresh headed Chrome 151 runs on the Intel UHD/ANGLE D3D11 reference system selected 3,000,000 authoritative node IDs matching 3,058,000 resident chunk tuples. Stable deterministic sampling produced 436,858 visible overlay points in 30 actors on every run.

| Metric | Target | Three-run median | Status |
|---|---:|---:|---|
| retained authoritative IDs | = 3,000,000 | 3,000,000 | Pass |
| sampled overlay points | 1–500,000 | 436,858 | Pass |
| selection activation | ≤ 2,000 ms | 474.6 ms | Pass |
| selected-scene orbit | ≥ 20 FPS | 27.78 FPS | Pass |

The exact visual-overlay policy remains unchanged through 1,000,000 matches. Above that boundary, the sampled representation is capped at 500,000 points while all authoritative IDs remain retained. Raw evidence is committed in `../../../docs/evidence/wp5-multimillion-selection-2026-08-31.json` and its `-run2`/`-run3` companions. The pre-existing point-pick and scalar-update target gates varied around their target boundaries during these addendum runs; none crossed its hard-failure threshold, and the dedicated selection assertions passed in all three runs.

## Structured-diagnostics overhead addendum (2026-09-02)

A paired Windows headless Chrome 152 run measured the bounded structured diagnostics path without replacing or reinterpreting the physical-GPU baseline. Information-level application events versus the same category suppressed measured 3,306.7 versus 3,189.3 ms complete initial load (+117.4 ms, +3.68%) and 11,875,528 versus 11,174,400 bytes backend managed peak above baseline (+701,128 bytes). Initial cardinality was exactly 152 correlated requests, including 150 resource events for the exact 157,048,032 descriptor bytes; there is no per-node, per-triangle, or per-animation-frame event. See [structured diagnostics evidence](evidence/structured-production-diagnostics.md) and its two raw JSON files. Headless timing variance is explicitly non-qualifying for GPU performance.

## Functional and network evidence

- The generator and browser assert exactly 3,000,000 unique source nodes and 5,993,002 triangles. Chunk point tuples are higher only because neighboring independent PolyData chunks share one boundary row.
- First geometry appeared while fewer than all 30 chunks were resident.
- Initial transfer was exactly 157,048,032 descriptor bytes across 150 unique immutable resource URLs. Manifest JSON was 82,317 bytes and contained no Base64/data URI.
- Scalar replacement made exactly 30 scalar requests totaling 12,232,000 bytes (`4 × 3,058,000` chunk point tuples); camera, projection, orbit, and visibility caused zero resource requests.
- Full-scale picking returned a server-validated element, candidate node, triangle, and large-origin world position for all 30 samples.
- Every unload reported zero actors and zero chunk pipelines. Five loaded/unloaded CDP samples are in each raw run.
- A deliberately truncated resource produces a visible integrity error; a clean reload recovers. WebSocket interruption performs a clean authenticated snapshot reload.
- No prototype project references Kitware.VTK, ActiViz, Windows Forms, or System.Drawing.

## Findings and decisions

The initial 100-row configuration failed the pick target at 126.9 ms p95 (but stayed below the 250 ms hard threshold). Without coarse culling, selection was approximately 850 ms p95 because the selector redrew all 5.99M triangles. Ray/AABB chunk culling plus the documented 50-row default reduced the qualifying three-run median to 92.8 ms p95. The tradeoff is 30 actors and 0.6 MB more initial payload from duplicated boundaries; load, orbit, and memory gates remain comfortably within target.

No Web Worker conversion is justified by this reference result because no ≥50-ms load task was observed. If production models make chunks spatially overlapping or pick latency regresses, the next recommendation is a server-built chunk BVH/LOD selection representation; remote rendering is not justified by these measurements.

## Reproduction

From the prototype root:

```powershell
./scripts/benchmark.ps1
```

The default is a headed, hardware-qualified attempt. Use `-Headless` only for functional smoke evidence; the harness marks software/headless GPU gates failed. Bash uses `PREPOMAX_BENCHMARK_HEADLESS=0 ./scripts/benchmark.sh`. Chunk rows and the response cap are configurable in both scripts.

## Linux x64 functional qualification

On 2026-09-01, Ubuntu 22.04 under WSL2 passed the self-contained `linux-x64` server lifecycle and production same-origin client gate. Linux Chromium 151 reported WebGL2 and rendered the deterministic small fixture with two actors, two chunks, six points, two polygons, and exactly 152 transferred resource bytes. This closes cross-platform functional correctness only; the 3M physical-GPU performance and memory benchmark remains pending on Linux. See [the captured environment and results](evidence/linux-x64-functional.md).

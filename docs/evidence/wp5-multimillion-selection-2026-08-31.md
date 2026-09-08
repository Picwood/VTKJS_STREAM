# WP5 physical multi-million selection evidence

Status: passed on Windows x64 on 2026-08-31.

Three fresh headed Chrome 151 runs used the canonical 3,000,000-node scene at 1920×1080/DPR 1 on Intel UHD Graphics through ANGLE D3D11. The real `VtkSceneView.setSelection` path retained all 3,000,000 authoritative IDs, found 3,058,000 resident point tuples (including chunk-boundary duplicates), and created 30 VTK overlay actors.

| Run | Activation | Selected orbit | Overlay points | Result |
|---|---:|---:|---:|---|
| 1 | 518.9 ms | 27.78 FPS | 436,858 | Pass |
| 2 | 474.6 ms | 28.17 FPS | 436,858 | Pass |
| 3 | 448.9 ms | 26.39 FPS | 436,858 | Pass |
| Median | 474.6 ms | 27.78 FPS | 436,858 | Pass |

Acceptance requires exactly 3,000,000 retained IDs, 1–500,000 overlay points, activation at or below 2 seconds, and at least 20 FPS selected-scene orbit on qualifying hardware. Every dedicated gate passed in every run. Clearing selection returned the diagnostics to zero overlay actors and points.

The first implementation sampled up to 1,000,000 points and measured below 20 FPS on this integrated GPU. The final policy preserves exact overlays through 1,000,000 matches, then uses `ceil(matchCount / 500,000)` as a stable actor/chunk/point-order stride. This changes only duplicated visualization geometry; the authoritative ID set remains exact.

Raw runs:

- `wp5-multimillion-selection-2026-08-31.json`
- `wp5-multimillion-selection-2026-08-31-run2.json`
- `wp5-multimillion-selection-2026-08-31-run3.json`

The addendum runs also record the original WP5 gates. Point-pick p95 and scalar activation varied around their target boundaries, but remained below their documented hard-failure thresholds. The original WP5 qualification remains the separate three-run result in the prototype benchmark report.

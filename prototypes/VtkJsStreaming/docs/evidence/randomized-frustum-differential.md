# Randomized ActiViz frustum differential

## Objective

Expand full-cell rectangle-selection parity beyond the fixed pathological corpus without duplicating the managed selection algorithm in the oracle. The Windows-only harness uses the installed ActiViz 5.8 runtime as the reference and runs the production `SelectionSpatialIndex` methods through xUnit.

## Method

- `scripts/compare-frustum-randomized-activiz.ps1` generates deterministic valid cells from canonical templates for VTK types 1, 3, 5, 9-14, 21-27, and 29.
- Every non-voxel template receives seeded rotation, independent scale, translation, corner perturbation, and larger midside/interior perturbation. VTK voxels remain axis-aligned as required by their cell contract.
- Coordinates are read back from `vtkPoints` before serialization so ActiViz and the managed selector receive the same stored Float32 values.
- ActiViz captures strict point selection and CELL intersection against the unit-box planes. A temporary corpus path is passed through `VTKJS_FRUSTUM_CORPUS_PATH`; the existing test then calls the production `ContainsPoint`, actor-bounds, and `IntersectsCell` paths.
- `-Seed`, `-CasesPerType`, and optional `-CorpusOutput` make every run reproducible and preserve a failing corpus when diagnosis is needed.

## Findings and changes

Seed 58103 initially found three quadratic-tetra and one quadratic-pyramid over-selections. VTK 5.8's default quadratic-face branch reads later edges from the parent volume cell rather than the current face. `LegacyClipFaces` now reproduces that observable behavior only for volume types 24-27 and 29, including VTK's quadratic-wedge face ordering. Two failures are retained in the committed corpus.

Seed 58105 exposed a type-29 point difference when the cell bounds enclosed the complete frustum. VTK 5.8 applies `OverallBoundsTest` before iterating points; its bounds voxel rejects this containment arrangement. Production query now applies the same actor-level gate, and the corpus test exercises it.

An early rotated/warped voxel signal was rejected as invalid test data: `VTK_VOXEL` is axis-aligned, while rotated boxes are `VTK_HEXAHEDRON`. The generator now preserves that invariant.

## Results

| Check | Result |
|---|---|
| Committed ActiViz corpus | 10/10 passed |
| Seed 58103 | 204/204 passed |
| Seed 58104 | 204/204 passed |
| Seed 58105 | 204/204 passed |
| .NET solution | 78/78 passed; zero build warnings/errors |
| Client unit/build/Playwright | 26/26 unit tests and 2/2 browser tests passed; production build passed |
| Packaged real FRD | `fatigue_test.frd` resolved 10 geometry IDs through `locator=True`, displayed the overlay, and exited both processes cleanly |

The differential is intentionally bounded. It does not claim malformed/inverted cells, arbitrary generated perspective frusta, exhaustive geometric space, or behavior of VTK releases after 5.8.

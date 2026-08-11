# Randomized quadratic-section differential evidence

Date: 2026-08-11

## Purpose

The fixed canonical and deliberately ambiguous tests prove specific legacy cases, but they do not sample arbitrary plane orientations. This harness executes the production section builder and live ActiViz 5.8 from one shared corpus so the comparison does not rely on manually transcribed expected counts.

## Harness

- `tools/PrePoMax.Render.SectionOracle` is a development-only .NET 8 CLI with friend access to the production `SectionGeometryBuilder`. It reads the shared JSON cases and emits canonical triangle multisets.
- `scripts/compare-section-randomized-activiz.ps1` deterministically creates the corpus, invokes the production CLI, then constructs the same cells, scalar field, and planes in ActiViz 5.8.
- Canonical cases vary the interior plane. Warped cases additionally apply bounded scale/shear transformations, independent corner perturbations, and larger independent perturbations to midside/face/center input nodes before choosing a new interior plane.
- Each triangle vertex contains quantized world-relative coordinates and its interpolated scalar. Comparison performs one-to-one triangle and vertex matching, removing point-ID, insertion-order, and winding differences without discarding connectivity.
- Positions use a `1e-5` grid and scalars a `1e-4` grid. Unique matches may differ by at most two grid units (`2e-5` position and `2e-4` scalar), accommodating the production Float32 wire geometry. The first warped probe exercised this guard with a measured one-unit coordinate difference.

## Qualified run

| Property | Result |
|---|---|
| Seeds | 58027, 58028, and 58029 |
| Cell types | VTK 24, 25, 26, 27, and 29 |
| Cases per seed | 8 canonical planes and 8 independently warped cells/planes per type; 80 cases |
| Compared output | 240 cases and 3,159 triangles with position and scalar values |
| Result | Passed with no missing, extra, or differing triangles |

The executable command is:

```powershell
prototypes\VtkJsStreaming\scripts\compare-section-randomized-activiz.ps1
prototypes\VtkJsStreaming\scripts\compare-section-randomized-activiz.ps1 -Seed 58028
```

`PlanesPerType`, `WarpedPlanesPerType`, and `Seed` are explicit bounded parameters, so a failure can be replayed without changing the harness.

## Residual scope

The corpus uses bounded perturbations that preserve the intended element shapes; it does not claim behavior for inverted or self-intersecting cells. Fixed warped corpora separately cover ambiguous hexa case 90, wedge case 43, and pyramid case 21. The supplied `mesh-C3D13.inp` covers the real type-27 importer/process path, while the separate coincident multi-cell differential covers exact shared faces, shared nodes, and duplicate-ID locator merging. Later VTK versions remain an explicit follow-up.

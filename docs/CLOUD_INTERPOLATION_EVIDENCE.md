# Portable cloud interpolation

The portable CaeResults project now links 31 production sources, adding
CloudPoint, CloudInterpolatorEnum and CloudInterpolator. An unused Priority_Queue
namespace import was removed; no new dependency or substitute implementation
was needed.

Gaussian weighting previously computed exp((-d/r)^2), increasing influence with
distance. It now computes exp(-(d/r)^2). This changes shared production numerical
behavior on Windows as well as Linux. A two-point analytic regression failed
before the correction and passes afterward.

Six checks per locale verify nearest-point lookup, owned result arrays,
Shepard exact-point reproduction and inverse-square interpolation, Gaussian
weights and constant-component preservation. The complete Linux suite passes
3,065 checks, including 252 result checks.

This establishes the tested interpolation cases, not comprehensive numerical
validation. Empty clouds, degenerate geometry, search-radius boundary behavior,
large clouds and missing sample values still need validation. Full FeResults
integration and Windows build verification remain pending.

## Spherical search support

Gaussian and Shepard interpolation now reject candidate points whose Euclidean
distance is greater than or equal to the supplied radius. Previously the
coordinate checks admitted points within an axis-aligned cube, including
corners outside the requested sphere. The strict boundary exclusion used by
the existing coordinate checks is retained.

Two checks per locale use a valid sample at (0.1, 0, 0) with value 7 and an
out-of-radius sample at (0.8, 0.8, 0) with value 100. With query (0, 0, 0) and
radius 1, both methods now return 7. The regression failed before the fix.
This changes shared production interpolation behavior for both platforms;
nearest-point lookup is unaffected.

## Cloud file ingestion

The portable project now links the production CloudPointReader as its 32nd
source file. Coordinates and component values parse with invariant culture and
floating-point syntax, preventing decimal points from being interpreted as
locale-specific grouping separators. Commas remain field separators, not decimal
separators. The same parsing change applies to the legacy Windows build.

Twenty-four checks across en-US and it-IT cover space, comma, semicolon, colon
and tab separators, comments and blank lines, signed exponent values, two
components, interpolation of imported data, inconsistent component counts and
the existing missing-file contract. The locale regression failed before the fix.
This establishes file-to-interpolator operation for these fixtures; general
input validation and full application integration remain pending.

## Single-point clouds and input shape

Component-count initialization now includes the first cloud point. Previously
single-point clouds retained a count of -1 and two-point clouds could miss a
component-count mismatch. Null/empty clouds and missing or malformed point
arrays now fail explicitly before spatial-index allocation. Points require
three coordinates and at least one component, with consistent component counts.

Twenty-two checks across both locales cover single-point component counts,
all three interpolation methods and seven invalid input shapes. The single-point
count regression failed before the fix. This does not validate nonfinite
coordinates, extreme magnitudes or later mutation of supplied point arrays.

## Query validation

Gaussian and Shepard queries now require a finite, positive radius. All three
methods require three finite query coordinates, checked before spatial-index
calculations. Direct method calls share these checks with InterpolateAt.
Nearest-point dispatch continues to ignore its radius argument.

Twenty-four checks across both locales cover zero, negative, NaN and infinite
weighted radii, plus null, short, NaN and infinite query coordinates. The invalid
radius assertion failed before the fix. Valid interpolation regressions remain
in the suite. Extreme finite coordinate magnitudes and source-coordinate
validation remain separate work.

## Source-coordinate validation

The interpolator now rejects NaN and positive/negative infinity in source
coordinates before adding them to the bounding box. Its argument error identifies
the offending point index. Previously NaN could cause an index-out-of-range
exception during spatial-region assignment.

Thirty-six checks across both locales cover all three nonfinite values in each
coordinate axis, at both the first and second source point. The regression
failed before the fix. Finite extreme magnitudes, mutations after construction,
and missing component-value semantics remain unvalidated. Component values are
not restricted by this coordinate validation.

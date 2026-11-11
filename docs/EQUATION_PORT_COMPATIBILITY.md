# Units and equation migration evidence

2026-09-10, Ubuntu 26.04.1 x64, SDK 10.0.401.

## Runtime boundary

The portable CaeGlobals build now includes all source files under `UnitSystem`,
`Equation`, the `MyNCalc` wrapper and `IContainsEquations`. UnitsNet stays at
4.89.1, using its .NET Standard 2.0 asset. The portable evaluator uses NCalc
7.1.0 and its .NET 10 assets. The Windows net48 project keeps NCalc 1.3.8.

The modern API uses different option and callback types; the shared wrapper
adapts those behind `PREPOMAX_MODERN_NCALC`, defined only in the portable project.
The mapping preserves case-insensitive built-in functions, parameter iteration,
and the custom Ln/Dist/DistX/DistY/DistZ callbacks. No unsafe serialization package
or Windows framework fallback is used by the portable production project;
NU1701 is treated as an error there.

Reference: [NCalc options](https://ncalc.github.io/ncalc/api/NCalc.ExpressionOptions.html)
and [upstream NCalc](https://github.com/ncalc/ncalc). API compatibility alone was
not used as evidence of formula compatibility.

## Shared evaluator corpus

`EquationChecks.cs` is compiled by two separate executables:

- the CaeGlobals harness, referencing the portable production library;
- `NCalc.LegacyOracle`, linking the production wrapper and the pinned 1.3.8
  evaluator. Only this test project permits the old framework asset on .NET 10.
  It must never be referenced or shipped by a production project.

Both engines pass the same 178 assertions (89 under each of en-US and it-IT).
The cases cover precedence, arithmetic, comparisons/booleans, lazy conditionals,
all advertised built-in function families, mixed-case function names, Pi,
parameters with dotted names, custom node-distance callbacks, parameter discovery,
array iteration, scalar-to-array fallback, and rejection of malformed/unknown
expressions. Numeric results use explicit expected values with a 1e-10 tolerance.
Error messages/types are not required to match between engine versions.

One observed difference required adaptation: modern parameter iteration returns
a scalar when no parameter is enumerable, whereas the old engine returns an
empty list. `SolveArrayEquation` retains its existing one-element array fallback
for that case. The shared corpus exercises this explicitly.

The oracle also **builds successfully for net48** on Linux using reference
assemblies. The test script runs that executable on Windows CI in addition to
the .NET 10 runs. Actual net48 execution and the full Windows application build
have not been performed on this Linux host.

## Units and equation containers

Another 50 assertions exercise SI, millimetre/tonne and imperial length/mass/
pressure conversions, Celsius/Fahrenheit offsets and absolute zero, localized
unit formatting/parsing, all configured unit-system initializers, formula-valued
lengths, and EquationContainer normalization, callbacks, clamping and nonfinite
result rejection. These run against the portable library under both cultures.

The complete current suite passes:

```text
60 STL checks
178 legacy evaluator oracle checks
766 CaeGlobals checks (including the 178 modern evaluator and 50 unit checks)
```

Run `bash scripts/test-portable.sh`. The legacy parser prints diagnostics for
the intentionally malformed expression; its process still exits successfully
only if every expected result/rejection passes.

## Limits and next work

This corpus establishes the tested migration boundary, not full equivalence
between NCalc releases or compatibility with every saved project. Extend it with
real model formulas as CaeMesh/CaeModel enter the portable graph. Overflow,
date/string coercion, every unit abbreviation and concurrent mutation of the
existing static converter/parameter state are not qualified here. Persisted
equation-container type names and legacy `.pmx` imports need their own tests.

Two existing-source compiler/analyzer warnings remain in the imported converter
code: CS0168 in `StringLengthPixelConverter` and CA2200 in `EquationToString`.
Unused Remoting and WinForms imports were removed where they blocked compilation;
the desktop project continues to compile the same source files.

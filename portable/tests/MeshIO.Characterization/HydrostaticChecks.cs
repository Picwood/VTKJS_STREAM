using CaeGlobals;
using CaeModel;

internal static class HydrostaticChecks
{
    public static void Run(Action<bool, string> check)
    {
        var pressure = new HydrostaticPressure("water", "FACE", RegionTypeEnum.SurfaceName,
            new[] { 0d, 0, 0 }, new[] { 0d, 0, 2 }, 0, 0, 4, -10, 10, false, false, 0);
        var points = new[] { new[] { 0d, 0, 0 }, new[] { 3d, 4, 1 }, new[] { 0d, 0, 2 }, new[] { 0d, 0, 3 } };
        check(pressure.IsProperlyDefined(out _), "hydrostatic distinct planes and non-unit direction validate");
        check(pressure.GetPressuresForPoints(points).SequenceEqual(new[] { -10d, 0, 10, 20 }),
            "hydrostatic interpolation and extrapolation ignore transverse position");
        check(pressure.GetPressureForPoint(points[1]) == 0, "single-point hydrostatic evaluation agrees with batch");
        pressure.HydrostaticPressureCutoff = HydrostaticPressureCutoffEnum.Positive;
        check(pressure.GetPressuresForPoints(points).SequenceEqual(new[] { -10d, 0, 0, 0 }), "positive pressure cutoff");
        pressure.HydrostaticPressureCutoff = HydrostaticPressureCutoffEnum.Negative;
        check(pressure.GetPressuresForPoints(points).SequenceEqual(new[] { 0d, 0, 10, 20 }), "negative pressure cutoff");
        var invalid = new HydrostaticPressure("invalid", "FACE", RegionTypeEnum.SurfaceName, false, false, 0);
        check(!invalid.IsProperlyDefined(out string error) && error.Length > 0, "zero hydrostatic direction rejected");
        var coincident = new HydrostaticPressure("invalid", "FACE", RegionTypeEnum.SurfaceName,
            new[] { 0d, 0, 0 }, new[] { 1d, 0, 0 }, 0, 0, 1, 0, 10, false, false, 0);
        check(!coincident.IsProperlyDefined(out _), "coincident pressure planes rejected");
    }
}

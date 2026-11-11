using CaeGlobals;
using CaeModel;
using FileInOut.Output.Calculix;

internal static class TractionChecks
{
    public static void Run(Action<bool, string> check)
    {
        var traction = new STLoad("traction", "FACE", RegionTypeEnum.SurfaceName, 3, 4, 0, false, false, 0);
        check(traction.FMagnitude.Value == 5 && traction.DistributionName == Distribution.DefaultDistributionName,
            "traction resultant magnitude and constant distribution");
        var loads = new[] { new CLoad("a", 42, .25, 0, -.5, false, false, 0),
                            new CLoad("b", 43, .75, 0, -1.5, false, false, 0) };
        var writer = new CalSTLoad(traction, loads, ComplexLoadTypeEnum.None);
        check(writer.GetDataString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .SequenceEqual(new[] { "42, 1, 0.25", "42, 3, -0.5", "43, 1, 0.75", "43, 3, -1.5" }),
            "resolved traction preserves node IDs and force components, omitting zeros");
        traction.AmplitudeName = "RAMP";
        writer.OpType = OpTypeEnum.New;
        check(writer.GetKeywordString().Contains("*Cload, Amplitude=RAMP, op=New"), "traction amplitude and replacement");
        check(new CalSTLoad(traction, Array.Empty<CLoad>(), ComplexLoadTypeEnum.None).GetDataString() == "",
            "empty resolved traction produces no nodal force rows");
        var planar = new STLoad("planar", "FACE", RegionTypeEnum.SurfaceName, 3, 4, 7, true, false, 0);
        check(planar.F3.Value == 0, "2D traction removes out-of-plane force");
    }
}

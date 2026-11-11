using CaeGlobals;
using CaeModel;
using FileInOut.Output.Calculix;

internal static class TemperatureChecks
{
    public static void Run(Action<bool, string> check)
    {
        var boundary = new TemperatureBC("heat", "NODES", RegionTypeEnum.NodeSetName, 20.5, false);
        var writer = new CalTemperatureBC(boundary, null, null);
        check(writer.GetDataString().Trim() == "NODES, 11, 11, 20.5", "temperature boundary thermal DOF and invariant value");
        boundary.AmplitudeName = "RAMP";
        writer.OpType = OpTypeEnum.New;
        check(writer.GetKeywordString().Contains("*Boundary, Amplitude=RAMP, op=New"), "temperature boundary amplitude and operation");
        boundary.RegionType = RegionTypeEnum.SurfaceName;
        check(new CalTemperatureBC(boundary, null, "FACE_NODES").GetDataString().Trim() == "FACE_NODES, 11, 11, 20.5",
            "temperature surface resolves backing node set");
        bool rejected = false;
        try { writer.GetDataString(); } catch (ArgumentException) { rejected = true; }
        check(rejected, "temperature surface requires backing node set");
        var resolved = new CalTemperatureBC(boundary, new[] { new TemperatureBC("a", 42, -1.25), new TemperatureBC("b", 43, 30.5) }, null);
        check(resolved.GetDataString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .SequenceEqual(new[] { "42, 11, 11, -1.25", "43, 11, 11, 30.5" }), "resolved temperatures preserve node identity and values");
        var defined = new DefinedTemperature("initial", "NODES", RegionTypeEnum.NodeSetName, 25.5);
        check(defined.Type == DefinedTemperatureTypeEnum.ByValue && defined.Temperature.Value == 25.5 &&
            defined.DistributionName == Distribution.DefaultDistributionName, "defined temperature defaults");
        defined.Type = DefinedTemperatureTypeEnum.FromFile;
        defined.FileName = "thermal.frd";
        defined.StepNumber = 0;
        check(defined.StepNumber == 1 && defined.FileName == "thermal.frd", "defined temperature file source step clamps to one");
    }
}

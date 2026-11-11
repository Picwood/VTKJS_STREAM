using CaeGlobals;
using CaeModel;
using FileInOut.Output.Calculix;
using System.Globalization;

internal static class MechanicalLoadChecks
{
    public static void Run(Action<bool, string> check)
    {
        var gravity = new GravityLoad("gravity", "SOLID", RegionTypeEnum.ElementSetName,
            0, -9.81, 0, false, false, 0);
        var gravityWriter = new CalGravityLoad(gravity, ComplexLoadTypeEnum.None);
        var tokens = gravityWriter.GetDataString().Trim().Split(',');
        var values = tokens.Skip(2).Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
        check(tokens[0] == "SOLID" && tokens[1].Trim() == "Grav", "gravity targets element region");
        check(Math.Abs(values[0] - 9.81) < 1e-12 && values.Skip(1).SequenceEqual(new[] { 0d, -1, 0 }),
            "gravity magnitude and unit direction preserve acceleration");
        gravity.AmplitudeName = "RAMP";
        gravityWriter.OpType = OpTypeEnum.New;
        check(gravityWriter.GetKeywordString().Contains("*Dload, Amplitude=RAMP, op=New"), "gravity amplitude and replacement operation");
        var moment = new MomentLoad("torque", 42, 0, 0, 2.5, false, false, 0);
        check(new CalMomentLoad(moment, null, ComplexLoadTypeEnum.None).GetDataString().Trim() == "42, 6, 2.5",
            "nodal torque exports rotational DOF and invariant value");
        var reference = new MomentLoad("torque", "RP", RegionTypeEnum.ReferencePointName, 0, 0, 2.5, false, false, 0);
        var momentWriter = new CalMomentLoad(reference, new() { ["RP"] = new[] { 101, 102 } }, ComplexLoadTypeEnum.None);
        check(momentWriter.GetDataString().Trim() == "102, 3, 2.5", "CalculiX torque uses reference rotation node");
        momentWriter.MaxNumberNodeDOFs = 6;
        check(momentWriter.GetDataString().Trim() == "101, 6, 2.5", "six-DOF torque uses reference primary node");
        var spin = new CentrifLoad("spin", "SOLID", RegionTypeEnum.ElementSetName,
            new[] { .25, .5, .75 }, new[] { 0d, 0, 2 }, 3.5, false, false, false, 0);
        var spinWriter = new CalCentrifLoad(spin, ComplexLoadTypeEnum.None);
        var spinValues = spinWriter.GetDataString().Trim().Split(',').Skip(2)
            .Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
        check(spinValues.SequenceEqual(new[] { 12.25, .25, .5, .75, 0, 0, 1 }),
            "centrifugal export squares speed and normalizes axis without moving origin");
        check(spinWriter.GetKeywordString().Contains("*Dload") && spinWriter.GetDataString().Contains("SOLID, CENTRIF"),
            "centrifugal keyword targets element region");
    }
}

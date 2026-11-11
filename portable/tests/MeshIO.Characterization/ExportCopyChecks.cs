using CaeGlobals;
using CaeModel;
using FileInOut.Output.Calculix;

internal static class ExportCopyChecks
{
    public static void Run(Action<bool, string> check)
    {
        var root = new CalculixUserKeyword("*Heading") { Parent = new object(), Active = false };
        root.Suppress();
        var child = new CalculixUserKeyword("original") { Parent = root };
        var deactivated = new CalDeactivated("unused");
        root.AddKeyword(child);
        root.AddKeyword(deactivated);
        var equation = new CalEquation(new double[] { 1, 1, .5, 2, 1, -.5 });
        root.AddKeyword(equation);
        var copy = root.CopyForExport();
        check(copy.Parent == null && !copy.Active && copy.IsSuppressed, "export copy retains flags without editor parent graph");
        check(copy.Keywords.Count == 3 && copy.Keywords[1] is CalDeactivated,
            "export copy retains child order and deactivated marker");
        check(copy.Keywords[2].GetDataString() == equation.GetDataString(), "export copy freezes non-user keyword output");
        child.Data = "changed";
        root.ClearKeywords();
        check(((CalculixUserKeyword)copy.Keywords[0]).Data == "original", "export tree owns child state");
        ((CalculixUserKeyword)copy.Keywords[0]).Data = "copy change";
        check(child.Data == "changed", "export tree edits do not change source");
        root.AddKeyword(root);
        bool rejected = false;
        try { root.CopyForExport(); }
        catch (InvalidOperationException) { rejected = true; }
        check(rejected, "cyclic keyword tree rejected");

        var gravity = new GravityLoad("gravity", "mass", RegionTypeEnum.MassSection, 0, -9.81, 0, false, true, 60)
            { AmplitudeName = "ramp" };
        var gravityExport = CalGravityLoad.CreateForExport(gravity, "generated", ComplexLoadTypeEnum.Real);
        string gravityData = gravityExport.GetDataString();
        check(gravityData.StartsWith("generated, Grav,") && gravity.RegionName == "mass",
            "gravity export resolves region without changing load");
        check(gravityExport.GetKeywordString().Contains("Amplitude=ramp") && gravityExport.GetKeywordString().Contains("Load case=1"),
            "gravity export preserves amplitude and complex case");
        gravity.F2 = new EquationContainer(typeof(StringAccelerationConverter), -1);
        gravity.AmplitudeName = "changed";
        gravity.PhaseDeg = new EquationContainer(typeof(StringAngleDegConverter), 0);
        check(gravityExport.GetDataString() == gravityData && gravityExport.GetKeywordString().Contains("Amplitude=ramp"),
            "gravity export owns evaluated force phase and amplitude");

        foreach (bool axisymmetric in new[] { false, true })
        {
            var centrifugal = new CentrifLoad("spin", "mass", RegionTypeEnum.MassSection,
                new[] { 1d, 2d, 3d }, new[] { 0d, 1d, 0d }, 12.5, false, axisymmetric, false, 0);
            var original = new CalCentrifLoad(centrifugal, ComplexLoadTypeEnum.None);
            var exported = CalCentrifLoad.CreateForExport(centrifugal, "generated", ComplexLoadTypeEnum.None);
            string data = exported.GetDataString();
            check(data == original.GetDataString().Replace("mass,", "generated,") && centrifugal.RegionName == "mass",
                "centrifugal export preserves point axis speed and axisymmetry " + axisymmetric);
            centrifugal.RotationalSpeed = new EquationContainer(typeof(StringRotationalSpeedConverter), 100);
            check(exported.GetDataString() == data, "centrifugal export owns speed " + axisymmetric);
        }
    }
}

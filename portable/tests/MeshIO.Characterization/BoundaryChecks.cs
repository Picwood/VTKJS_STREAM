using CaeGlobals;
using CaeModel;
using FileInOut.Output.Calculix;

internal static class BoundaryChecks
{
    public static void Run(Action<bool, string> check)
    {
        var fixed3D = new FixedBC("clamp", "SUPPORT", RegionTypeEnum.NodeSetName, false);
        check(fixed3D.GetConstrainedDirections().SequenceEqual(new[] { 1, 2, 3, 4, 5, 6 }), "3D fixed boundary constrains six model directions");
        check(new CalFixedBC(fixed3D, null, null).GetDataString().Trim() == "SUPPORT, 1, 6, 0", "fixed node-set keyword");
        var fixed2D = new FixedBC("clamp", "SUPPORT", RegionTypeEnum.NodeSetName, true);
        check(fixed2D.GetConstrainedDirections().SequenceEqual(new[] { 1, 2, 6 }), "2D fixed boundary model directions");
        check(new CalFixedBC(fixed2D, null, null).GetDataString().Trim() == "SUPPORT, 1, 2, 0", "CalculiX 2D fixed export uses translational DOFs");
        var surface = new FixedBC("surface", "FACE", RegionTypeEnum.SurfaceName, false);
        check(new CalFixedBC(surface, null, "FACE_NODES").GetDataString().Trim() == "FACE_NODES, 1, 6, 0", "fixed surface resolves backing node set");
        bool rejected = false;
        try { new CalFixedBC(surface, null, null).GetDataString(); }
        catch (ArgumentException) { rejected = true; }
        check(rejected, "fixed surface requires resolved node set");
        var reference = new FixedBC("reference", "RP", RegionTypeEnum.ReferencePointName, true);
        var referenceWriter = new CalFixedBC(reference, new() { ["RP"] = new[] { 101, 102 } }, null);
        check(referenceWriter.GetDataString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .SequenceEqual(new[] { "101, 1, 2, 0", "102, 3, 3, 0" }), "2D reference rotation maps to separate CalculiX node");
        var submodel = new SubmodelBC("cut", "CUT", RegionTypeEnum.NodeSetName, true)
            { U1 = true, U3 = true, UR1 = true, UR2 = true, UR3 = true };
        check(submodel.GetConstrainedDirections().SequenceEqual(new[] { 1, 6 }), "2D submodel rejects out-of-plane DOFs");
        var writer = new CalSubmodelBC(submodel, null) { OpType = OpTypeEnum.New };
        check(writer.GetKeywordString().Contains("Submodel, Step=1, op=New"), "submodel step and replacement operation");
        check(writer.GetDataString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .SequenceEqual(new[] { "CUT, 1, 1", "CUT, 6, 6" }), "submodel exports selected directions only");
        submodel.RegionType = RegionTypeEnum.SurfaceName;
        check(new CalSubmodelBC(submodel, "CUT_NODES").GetDataString().StartsWith("CUT_NODES, 1, 1"), "submodel surface mapping");
        submodel.StepNumber = new EquationContainer(typeof(StringIntegerConverter), 0);
        check(submodel.StepNumber.Value == 1, "submodel source step clamps to first step");
    }
}

using CaeGlobals;
using CaeMesh;
using CaeModel;
using FileInOut.Output.Calculix;

internal static class ShellPretensionChecks
{
    public static void Run(Action<bool, string> check)
    {
        var faces = new[] { FeFaceName.S3, FeFaceName.S4, FeFaceName.S5, FeFaceName.S6 };
        for (int i = 0; i < faces.Length; i++)
        {
            var surface = new FeSurface("EDGE");
            surface.AddElementFace(faces[i], "EDGE_ELEMENTS");
            var load = new ShellEdgeLoad("edge", "EDGE", RegionTypeEnum.SurfaceName, -2.5, false, false, 0);
            check(new CalShellEdgeLoad(load, surface, ComplexLoadTypeEnum.None).GetDataString().Trim() ==
                $"EDGE_ELEMENTS, EDNOR{i + 1}, -2.5", $"shell edge {faces[i]} preserves signed line force");
        }
        foreach (var item in new[] { (FeFaceName.S1, false), (FeFaceName.S2, false), (FeFaceName.S3, true) })
        {
            var surface = new FeSurface("EDGE");
            surface.AddElementFace(item.Item1, "EDGE_ELEMENTS");
            var load = new ShellEdgeLoad("edge", "EDGE", RegionTypeEnum.SurfaceName, 1, item.Item2, false, 0);
            bool rejected = false;
            try { new CalShellEdgeLoad(load, surface, ComplexLoadTypeEnum.None).GetDataString(); }
            catch (NotSupportedException) { rejected = true; }
            check(rejected, "shell edge rejects face interiors and 2D use");
        }
        var tension = new PreTensionLoad("bolt", "CUT", RegionTypeEnum.SurfaceName, 2.5, false);
        check(tension.AutoComputeDirection && tension.Type == PreTensionLoadType.Force && tension.GetMagnitudeValue() == 2.5,
            "pretension defaults to force and automatic direction");
        tension.SetMagnitudeValue(4.5);
        tension.Type = PreTensionLoadType.Displacement;
        tension.SetMagnitudeValue(.125);
        check(tension.GetMagnitudeValue() == .125 && tension.ForceMagnitude.Value == 4.5,
            "pretension force and displacement magnitudes remain independent");
        var planar = new PreTensionLoad("bolt", "CUT", RegionTypeEnum.SurfaceName, 1, 2, 3, 4, true);
        check(!planar.AutoComputeDirection && planar.X.Value == 1 && planar.Y.Value == 2 && planar.Z.Value == 0,
            "manual 2D pretension direction removes out-of-plane component");
        var automatic = new CalPreTensionSection("CUT", 101);
        check(automatic.GetKeywordString().Contains("Surface=CUT, Node=101") && automatic.GetDataString() == "",
            "automatic pretension section delegates direction calculation to solver");
        check(new CalPreTensionSection("CUT", 101, .25, .5, .75).GetDataString().Trim() == "0.25, 0.5, 0.75",
            "manual pretension section exports invariant direction");
    }
}

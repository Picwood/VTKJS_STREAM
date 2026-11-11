using CaeGlobals;
using CaeMesh;
using CaeModel;
using FileInOut.Output.Calculix;

internal static class PressureKeywordChecks
{
    public static void Run(Action<bool, string> check)
    {
        var load = new DLoad("pressure", "FACE", RegionTypeEnum.SurfaceName, 2.5, false, false, 0);
        check(load.DistributionName == Distribution.DefaultDistributionName && load.Magnitude.Value == 2.5,
            "pressure defaults to constant distribution");
        var faces = new[] { FeFaceName.S1, FeFaceName.S2, FeFaceName.S3, FeFaceName.S4, FeFaceName.S5, FeFaceName.S6 };
        for (int i = 0; i < faces.Length; i++)
        {
            var surface = new FeSurface("FACE") { SurfaceFaceTypes = FeSurfaceFaceTypes.ShellFaces };
            surface.AddElementFace(faces[i], "FACE_ELEMENTS");
            string magnitude = i == 1 ? "-2.5" : "2.5";
            check(new CalDLoad(load, surface, ComplexLoadTypeEnum.None).GetDataString().Trim() ==
                $"FACE_ELEMENTS, P{i + 1}, {magnitude}", "surface pressure face mapping and shell-side sign");
            var element = new DLoad("element", 42, faces[i], 2.5, false, false, 0);
            check(new CalVariablePressureLoad(load, new[] { element }, ComplexLoadTypeEnum.None,
                FeSurfaceFaceTypes.ShellFaces).GetDataString().Trim() == $"42, P{i + 1}, {magnitude}",
                "resolved variable pressure agrees with surface pressure sign");
        }
        var planar = new DLoad("pressure", "FACE", RegionTypeEnum.SurfaceName, .125, true, false, 0);
        for (int i = 2; i < faces.Length; i++)
        {
            var surface = new FeSurface("FACE");
            surface.AddElementFace(faces[i], "EDGE_ELEMENTS");
            check(new CalDLoad(planar, surface, ComplexLoadTypeEnum.None).GetDataString().Trim() ==
                $"EDGE_ELEMENTS, P{i - 1}, 0.125", "2D pressure edge remapping");
        }
    }
}

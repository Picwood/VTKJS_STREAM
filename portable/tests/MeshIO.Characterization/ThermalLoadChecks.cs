using CaeGlobals;
using CaeMesh;
using CaeModel;
using FileInOut.Output.Calculix;

internal static class ThermalLoadChecks
{
    public static void Run(Action<bool, string> check)
    {
        var nodal = new CFlux("heat", "NODES", RegionTypeEnum.NodeSetName, 2.5, false)
            { AddFlux = true, AmplitudeName = "RAMP" };
        var nodalWriter = new CalCFlux(nodal) { OpType = OpTypeEnum.New };
        check(nodalWriter.GetDataString().Trim() == "NODES, 11, 2.5", "nodal heat flux uses thermal DOF");
        check(nodalWriter.GetKeywordString().Contains("*Cflux, Amplitude=RAMP, Add, op=New"), "nodal flux flags");
        check(new CalBodyFlux(new BodyFlux("heat", "SOLID", RegionTypeEnum.ElementSetName, .125, false))
            .GetDataString().Trim() == "SOLID, BF, 0.125", "body heat generation targets element set");
        check(new CalDFlux(new DFlux("heat", "FACE", RegionTypeEnum.SurfaceName, -1.25, false))
            .GetDataString().Trim() == "FACE, S, -1.25", "surface heat flux preserves sign");
        var faces = new[] { FeFaceName.S1, FeFaceName.S2, FeFaceName.S3, FeFaceName.S4, FeFaceName.S5, FeFaceName.S6 };
        var film2D = new[] { "FN", "FP", "F1", "F2", "F3", "F4" };
        var radiation2D = new[] { "RN", "RP", "R1", "R2", "R3", "R4" };
        foreach (bool twoD in new[] { false, true })
        {
            for (int i = 0; i < faces.Length; i++)
            {
                var surface = new FeSurface("FACE");
                surface.AddElementFace(faces[i], "FACE_ELEMENTS");
                var film = new FilmHeatTransfer("film", "FACE", RegionTypeEnum.SurfaceName, 20.5, .125, twoD);
                var radiation = new RadiationHeatTransfer("radiation", "FACE", RegionTypeEnum.SurfaceName, 30.5, .75, twoD);
                string filmKey = twoD ? film2D[i] : "F" + (i + 1);
                string radiationKey = twoD ? radiation2D[i] : "R" + (i + 1);
                check(new CalFilmHeatTransfer(film, surface).GetDataString().Trim() ==
                    $"FACE_ELEMENTS, {filmKey}, 20.5, 0.125", $"film face {faces[i]} mapping, twoD={twoD}");
                check(new CalRadiationHeatTransfer(radiation, surface).GetDataString().Trim() ==
                    $"FACE_ELEMENTS, {radiationKey}, 30.5, 0.75", $"radiation face {faces[i]} mapping, twoD={twoD}");
            }
        }
    }
}

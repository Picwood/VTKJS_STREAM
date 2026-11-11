using CaeGlobals;
using CaeMesh;
using CaeModel;
using FileInOut.Output.Calculix;

internal static class ModelPropertiesChecks
{
    public static void Run(Action<bool, string> check)
    {
        var properties = new ModelProperties();
        check(properties.ModelSpace == ModelSpaceEnum.Undefined && properties.ModelType == ModelType.GeneralModel,
            "model properties require explicit space and default to general model");
        check(new CalPhysicalConstants(properties).GetKeywordString() == "", "undefined physical constants omitted");
        properties.AbsoluteZero = -273.15;
        properties.StefanBoltzmann = 5.67e-8;
        string text = new CalPhysicalConstants(properties).GetKeywordString();
        check(text.Contains("Absolute zero=-273.15") && text.Contains("Stefan Boltzmann=5.67E-08") &&
            !text.Contains("Newton gravity"), "physical constants use invariant output and omit unset values");
        properties.GlobalResultsFileName = "global.frd";
        check(properties.GlobalResultsFileName == null, "general model hides submodel results path");
        properties.ModelType = ModelType.Submodel;
        check(properties.GlobalResultsFileName == "global.frd", "submodel retains assigned results path");
        properties.NumberOfCycles = new EquationContainer(typeof(StringIntegerConverter), 0);
        properties.CyclesIncrement = new EquationContainer(typeof(StringIntegerConverter), -2);
        properties.NumOfSmoothingSteps = new EquationContainer(typeof(StringIntegerConverter), -1);
        check(properties.NumberOfCycles.Value == 1 && properties.CyclesIncrement.Value == 1 &&
            properties.NumOfSmoothingSteps.Value == 0, "wear cycle and smoothing bounds");
        var solid = ModelSpaceEnum.ThreeD.GetAvailableElementTypes(false);
        check(solid[typeof(FeElementTypeLinearHexa)].Contains(FeElementTypeLinearHexa.C3D8), "3D model permits C3D8");
        var planar = ModelSpaceEnum.PlaneStress.GetUnavailableElementTypeNames();
        check(planar[typeof(FeElementTypeLinearHexa)].Contains("C3D8"), "plane-stress model excludes solid hexahedron");
    }
}

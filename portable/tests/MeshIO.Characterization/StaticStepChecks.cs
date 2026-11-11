using CaeGlobals;
using CaeModel;
using FileInOut.Output.Calculix;

internal static class StaticStepChecks
{
    public static string CubeStep()
    {
        var step = new StaticStep("cube") { IncrementationType = IncrementationTypeEnum.Default };
        var keyword = new CalStaticStep(step);
        return new CalStepHeader(step).GetKeywordString() + keyword.GetKeywordString() + keyword.GetDataString();
    }
    public static void Run(Action<bool, string> check)
    {
        var step = new StaticStep("static");
        check(step.FieldOutputs.Count == 2, "static step supplies displacement/reaction and stress/strain outputs");
        check(step.IsLoadTypeSupported(typeof(CLoad)) && step.IsLoadTypeSupported(typeof(STLoad)) &&
            !step.IsLoadTypeSupported(typeof(CFlux)), "static step distinguishes mechanical and thermal loads");
        check(step.IsBoundaryConditionSupported(new FixedBC("fixed", "N", RegionTypeEnum.NodeSetName, false)) &&
            !step.IsBoundaryConditionSupported(new TemperatureBC("temperature", "N", RegionTypeEnum.NodeSetName, 20, false)),
            "static step boundary compatibility");
        check(step.IsDefinedFieldSupported(new DefinedTemperature("initial", 42, 20)), "static step accepts defined temperature");
        step.TimePeriod = .5;
        step.InitialTimeIncrement = 2;
        check(step.InitialTimeIncrement == .5, "static step clamps initial increment to period");
        step.IncrementationType = IncrementationTypeEnum.Automatic;
        step.InitialTimeIncrement = .125;
        step.MinTimeIncrement = .001;
        step.MaxTimeIncrement = .25;
        check(new CalStaticStep(step).GetDataString().Trim() == "0.125, 0.5, 0.001, 0.25", "automatic static increment output");
        step.IncrementationType = IncrementationTypeEnum.Direct;
        check(new CalStaticStep(step).GetKeywordString().Contains(", Direct") &&
            new CalStaticStep(step).GetDataString().Trim() == "0.125, 0.5", "direct static increment output");
        step.RunAnalysis = false;
        check(new CalStaticStep(step).GetDataString().Contains("*No Analysis"), "static no-analysis option");
        step.Nlgeom = true;
        step.MaxIncrements = 25;
        check(new CalStepHeader(step).GetKeywordString().Contains("*Step, Nlgeom, Inc=25"), "static header nonlinear and increment settings");
    }
}

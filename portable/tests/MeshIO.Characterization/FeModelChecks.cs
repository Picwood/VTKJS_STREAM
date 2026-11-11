using CaeGlobals;
using CaeModel;

internal static class FeModelChecks
{
    public static void Run(FeModel model, Action<bool, string> check)
    {
        check(model.Materials.ContainsKey("elastic") && model.Sections.ContainsKey("SOLID"),
            "FeModel preserves case-insensitive material and section lookup");
        check(model.StepCollection.StepsList.Count == 2 && model.Geometry != null,
            "FeModel owns steps and geometry alongside analysis mesh");
        check(model.GetSectionAssignments(out var sections).Length == 0 && sections[1] == 0,
            "FeModel assigns the solid section to the block element");
        model.GetMaterialAssignments(out var materials);
        check(materials[1] == 0, "FeModel maps section to material");
        var section = model.Sections.Values.Single();
        string region = section.RegionName;
        section.RegionName = "missing";
        check(!model.IsSectionRegionValid(section), "FeModel rejects missing section region");
        section.RegionName = region;
        check(model.IsSectionRegionValid(section), "FeModel validates restored section region");
        var load = (STLoad)model.StepCollection.StepsList[0].Loads["force"];
        check(model.IsLoadRegionValid(load), "FeModel resolves traction surface");
        check(model.GetNodalCLoadsFromSurfaceTraction(load).Sum(l => l.F1.Value) == 1,
            "FeModel dispatches constant traction conversion");
        var existing = MyNCalc.ExistingParameters;
        try
        {
            model.UpdateNCalcParameters();
            model.Parameters.Add("scale", new EquationParameter { Name = "scale",
                Equation = new EquationContainer(typeof(StringDoubleConverter), 2.5) });
            model.UpdateNCalcParameters();
            check(Convert.ToDouble(MyNCalc.ExistingParameters["scale"]) == 2.5,
                "FeModel publishes model equation parameters");
            check(model.Parameters["SCALE"].Value == 2.5, "FeModel parameter lookup is case insensitive");
        }
        finally { MyNCalc.ExistingParameters = existing; }
    }
}

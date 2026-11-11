using CaeResults;

internal static class AggregateChecks
{
    public static void Run(string file, Action<bool, string> check)
    {
        var results = FrdFileReader.Read(file);
        check(results != null && results.Mesh.Nodes.Count == 8 && results.Mesh.Elements.Count == 1,
              "aggregate imports cube mesh");
        check(results.GetAllStepIds().SequenceEqual(new[] { 1, 2 }), "aggregate preserves both steps");
        var data = results.GetFieldData("STRESS", "MISES", 2, 1);
        var stress = results.GetField(data);
        check(stress.GetComponentValues("MISES").All(x => float.IsFinite(x) && Math.Abs(x - 2) < .0002),
              "aggregate preprocessing computes stress invariants");
        var previousParameters = CaeGlobals.MyNCalc.ExistingParameters;
        try
        {
            CaeGlobals.MyNCalc.ExistingParameters = new CaeGlobals.OrderedDictionary<string, object>("Parameters");
            var equation = new ResultFieldOutputEquation("HALF_STRESS", "=[STRESS.MISES] / 2");
            equation.SetParentNames(new[] { "STRESS" });
            results.AddResultFieldOutput(equation);
            var derived = results.GetField(results.GetFieldData("HALF_STRESS", "VALUE", 2, 1));
            check(derived.GetComponentValues("VALUE").All(x => float.IsFinite(x) && Math.Abs(x - 1) < .0001),
                  "aggregate evaluates derived stress equation");
        }
        finally { CaeGlobals.MyNCalc.ExistingParameters = previousParameters; }
        results.SetMeshDeformation(10, 2, 1);
        check(Math.Abs(results.Mesh.Nodes[2].X - 1.02) < 1e-7 && results.Mesh.Nodes[1].X == 0,
              "aggregate scales second-step deformation");
        check(results.UndeformedNodes[2].X == 1, "aggregate retains original geometry");
        results.SetMeshDeformation(0, 2, 1);
        check(results.Mesh.Nodes[2].X == 1, "aggregate resets deformation");
        results.SetMeshDeformation(10, 1, 1);
        check(Math.Abs(results.Mesh.Nodes[2].X - 1.01) < 1e-7, "aggregate switches deformation step without accumulation");
    }
}

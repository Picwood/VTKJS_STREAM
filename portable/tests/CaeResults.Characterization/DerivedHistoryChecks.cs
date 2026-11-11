using CaeGlobals;
using CaeResults;

internal static class DerivedHistoryChecks
{
    public static void Run(string file, Action<bool, string> check)
    {
        var results = FrdFileReader.Read(file);
        results.Mesh.NodeSets.Add("right-nodes", new CaeMesh.FeNodeSet("right-nodes", new[] { 2, 3, 6, 7 }));
        results.Mesh.Surfaces.Add("right-surface", new CaeMesh.FeSurface("right-surface") { NodeSetName = "right-nodes" });
        results.SetMeshDeformation(10, 2, 1);
        foreach (var region in new[] { RegionTypeEnum.NodeSetName, RegionTypeEnum.SurfaceName })
        {
            foreach (var mode in new[] { OutputNodeCoordinatesEnum.Undeformed, OutputNodeCoordinatesEnum.Deformed })
            {
                string name = region + "-" + mode;
                var output = new ResultHistoryOutputFromField(name, "DISP", new[] { "U1" },
                    region == RegionTypeEnum.NodeSetName ? "right-nodes" : "right-surface", region)
                    { OutputNodeCoordinates = mode };
                results.AddResultHistoryOutput(output);
                var set = results.GetHistoryResultSet(name);
                var entries = set.Fields["DISP"].Components["U1"].Entries;
                check(entries.Keys.ToHashSet().SetEquals(new[] { "2", "3", "6", "7" }) &&
                      entries.Values.All(e => e.Time.SequenceEqual(new[] { 1d, 2 })),
                      "named region history selects correct nodes and times");
                var coordinates = set.Fields["COORDINATES"].Components["X"].Entries;
                double[] expected = mode == OutputNodeCoordinatesEnum.Undeformed ? new[] { 1d, 1 } : new[] { 1.001, 1.002 };
                check(coordinates.Values.All(e => e.Values.Count == 2 &&
                      Math.Abs(e.Values[0] - expected[0]) < 1e-8 && Math.Abs(e.Values[1] - expected[1]) < 1e-8),
                      "coordinate history uses physical displacement independent of display scale");
                check(Math.Abs(results.Mesh.Nodes[2].X - 1.02) < 1e-7,
                      "coordinate history leaves displayed deformation unchanged");
            }
        }
        var definition = new ResultHistoryOutputFromField("right-face", "DISP", new[] { "U1", "U3" },
            null, RegionTypeEnum.Selection) { CreationIds = new[] { 2, 3, 6, 7 } };
        results.AddResultHistoryOutput(definition);
        var field = results.GetHistoryResultSet("right-face").Fields["DISP"];
        check(field.Components["U1"].Entries.Keys.ToHashSet().SetEquals(new[] { "2", "3", "6", "7" }),
              "field-derived history respects selected nodes");
        foreach (var entry in field.Components["U1"].Entries.Values)
        {
            check(entry.Time.SequenceEqual(new[] { 1d, 2 }) && entry.Values.Count == 2 &&
                  Math.Abs(entry.Values[0] - .001) < 1e-8 && Math.Abs(entry.Values[1] - .002) < 1e-8,
                  "field-derived history retains both steps and displacement values");
        }
        check(field.Components["U3"].Entries.Values.All(e => e.Values.All(v => v == 0)),
              "field-derived transverse history preserves zeros");
        var secondStep = new ResultHistoryOutputFromField("second-step", "STRESS", new[] { "MISES" },
            null, RegionTypeEnum.Selection) { CreationIds = new[] { 2 }, StepId = 2, StepIncrementId = 1 };
        results.AddResultHistoryOutput(secondStep);
        var stress = results.GetHistoryResultSet("second-step").Fields["STRESS"].Components["MISES"].Entries["2"];
        check(stress.Time.SequenceEqual(new[] { 2d }) && stress.Values.Count == 1 &&
              Math.Abs(stress.Values[0] - 2) < .0002, "field-derived invariant history filters requested step");
    }
}

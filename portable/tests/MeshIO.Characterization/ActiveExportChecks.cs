using CaeGlobals;
using CaeMesh;
using CaeModel;
using FileInOut.Output;
using FileInOut.Output.Calculix;

internal static class ActiveExportChecks
{
    public static void Run(Action<bool, string> check)
    {
        var workflow = new StaticModelWorkflow(1);
        var model = workflow.Model;
        var mesh = model.Mesh;
        foreach (var node in mesh.Nodes.Values.ToArray())
            mesh.Nodes.Add(node.Id + 8, new FeNode(node.Id + 8, node.X + 10, node.Y, node.Z));
        int partId = mesh.Parts.Values.Max(p => p.PartId) + 1;
        var extra = new LinearHexaElement(2, Enumerable.Range(9, 8).ToArray()) { PartId = partId };
        mesh.Elements.Add(2, extra);
        var inactive = new MeshPart("inactive", partId, Enumerable.Range(9, 8).ToArray(), new[] { 2 },
            new[] { typeof(LinearHexaElement) }) { Active = false };
        mesh.Parts.Add(inactive.Name, inactive);
        mesh.NodeSets.Add("mixed", new FeNodeSet("mixed", new[] { 1, 9 }));
        mesh.ElementSets.Add("mixed-elements", new FeElementSet("mixed-elements", new[] { 1, 2 }));
        var point = new FeReferencePoint("RP", 1, 2, 3);
        mesh.ReferencePoints.Add(point.Name, point);
        var coordinate = new CoordinateSystem("CS", false);
        mesh.CoordinateSystems.Add(coordinate.Name, coordinate);
        var snapshot = mesh.CreateExportSnapshot();
        snapshot.NodeSets["mixed"].Labels[0] = 8;
        snapshot.ReferencePoints["RP"].X = new EquationContainer(typeof(StringLengthConverter), 99);
        snapshot.CoordinateSystems["CS"].X1 = new EquationContainer(typeof(StringLengthConverter), 99);
        check(mesh.NodeSets["mixed"].Labels[0] == 1 && point.X.Value == 1 && coordinate.X1.Value != 99,
            "export mesh snapshot owns regions reference points and coordinate equations");
        var prepared = model.CreateActiveExportView();
        check(prepared.Mesh.Elements.Count == 1 && prepared.Mesh.Nodes.Count == 8 && prepared.Mesh.Parts.Count == 1,
            "active preparation removes inactive elements and unreferenced nodes");
        check(prepared.Mesh.NodeSets["mixed"].Labels.SequenceEqual(new[] { 1 }) &&
              prepared.Mesh.ElementSets["mixed-elements"].Labels.SequenceEqual(new[] { 1 }),
            "active preparation filters mixed node and element sets");
        check(mesh.Elements.Count == 2 && mesh.Nodes.Count == 16 && mesh.NodeSets["mixed"].Labels.Length == 2 && !inactive.Active,
            "active preparation leaves original mesh unchanged");
        string text = CalculixFileWriter.GetActiveInputText(model, ConvertPyramidsToEnum.Wedges);
        check(text.Contains("*Step") && text.Contains("2, 1, 0.25"), "active export produces complete loaded model");
        check(text == CalculixFileWriter.GetActiveInputText(model, ConvertPyramidsToEnum.Wedges), "active export is repeatable");
        var load = (STLoad)workflow.Steps[0].Loads["force"];
        load.SurfaceName = "missing";
        bool rejected = false;
        try { CalculixFileWriter.GetActiveInputText(model, ConvertPyramidsToEnum.Wedges); }
        catch (CaeException) { rejected = true; }
        finally { load.SurfaceName = "LOADED"; }
        check(rejected && ReferenceEquals(model.Mesh, mesh) && ReferenceEquals(mesh.Elements[2], extra),
            "failed active export preserves source mesh and inactive element");
    }
}

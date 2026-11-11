using CaeMesh;
using CaeGlobals;

internal static class FeMeshChecks
{
    private static FeMesh Create() => new FeMesh(
        new Dictionary<int, FeNode> { [4] = new FeNode(4, 0, 0, 1), [2] = new FeNode(2, 1, 0, 0),
            [1] = new FeNode(1, 0, 0, 0), [3] = new FeNode(3, 0, 1, 0) },
        new Dictionary<int, FeElement> { [10] = new LinearTetraElement(10, new[] { 1, 2, 3, 4 }) }, MeshRepresentation.Mesh);

    public static void Run(Action<bool, string> check)
    {
        var mesh = Create();
        var name = mesh.Parts.Keys.Single();
        var part = mesh.Parts[name];
        check(mesh.Nodes.Keys.SequenceEqual(new[] { 1, 2, 3, 4 }) && mesh.MaxNodeId == 4 && mesh.MaxElementId == 10, "aggregate sorted IDs and maxima");
        check(part is MeshPart && part.NodeLabels.Length == 4 && part.Labels.SequenceEqual(new[] { 10 }), "aggregate part extraction");
        check(part.Visualization.Cells.Length == 4 && Math.Abs(part.MassProperties.Volume - 1d / 6) < 1e-12, "aggregate tetra topology and volume");
        check(Math.Abs(mesh.BoundingBox.GetDiagonal() - Math.Sqrt(3)) < 1e-12, "aggregate bounds");
        var selected = new FeMesh(mesh, new[] { name });
        selected.Nodes[1] = new FeNode(1, 99, 0, 0); selected.Elements[10].NodeIds[0] = 2;
        check(mesh.Nodes[1].X == 0 && mesh.Elements[10].NodeIds[0] == 1, "selected-part copy owns nodes and elements");
        mesh.NodeSets.Add("Selected", new FeNodeSet("Selected", new[] { 1, 2 }));
        mesh.RenumberNodes(101); mesh.RenumberElements(201);
        check(mesh.Nodes.Keys.Order().SequenceEqual(new[] { 101, 102, 103, 104 }) && mesh.NodeSets["selected"].Labels.SequenceEqual(new[] { 101, 102 }), "node renumbering updates sets with case-insensitive lookup");
        check(mesh.Elements[201].NodeIds.SequenceEqual(new[] { 101, 102, 103, 104 }) && part.Labels.SequenceEqual(new[] { 201 }), "element renumbering updates part connectivity");
        check(part.Visualization.Cells.SelectMany(x => x).All(mesh.Nodes.ContainsKey), "renumbered visualization references");
        foreach (string operation in new[] { "translate", "rotate", "mirror", "scale" })
        {
            mesh = Create(); name = mesh.Parts.Keys.Single();
            string[] copied;
            if (operation == "translate") copied = mesh.TranslateParts(new[] { name }, new[] { 2d, 0, 0 }, 1, null, null);
            else if (operation == "rotate") copied = mesh.RotateParts(new[] { name }, new double[3], new[] { 0d, 0, 1 }, Math.PI / 2, 1, null, null);
            else if (operation == "mirror") copied = mesh.MirrorParts(new[] { name }, new double[3], new[] { 1d, 0, 0 }, true, null, null);
            else copied = mesh.ScaleParts(new[] { name }, new double[3], new[] { 2d, 2, 2 }, true, null, null);
            check(copied.Length == 1 && copied[0] != name && mesh.Parts.Count == 2 && mesh.Nodes.Count == 8 && mesh.Elements.Count == 2, operation + " creates independent part");
            check(mesh.Nodes[2].X == 1 && mesh.Nodes[2].Y == 0 && mesh.Elements[10].NodeIds.SequenceEqual(new[] { 1, 2, 3, 4 }), operation + " leaves original intact");
            var newPart = mesh.Parts[copied[0]];
            check(!newPart.NodeLabels.Intersect(mesh.Parts[name].NodeLabels).Any(), operation + " allocates disjoint node IDs");
            var coordinates = newPart.NodeLabels.Select(id => mesh.Nodes[id]).ToArray();
            double[] expected = operation == "translate" ? new[] { 3d, 0, 0 } : operation == "rotate" ? new[] { 0d, 1, 0 } : operation == "mirror" ? new[] { -1d, 0, 0 } : new[] { 2d, 0, 0 };
            check(coordinates.Any(n => n.Coor.Zip(expected).All(p => Math.Abs(p.First - p.Second) < 1e-10)), operation + " analytic transformed vertex");
        }
        mesh = Create(); name = mesh.Parts.Keys.Single();
        var copies = mesh.TranslateParts(new[] { name }, new[] { 2d, 0, 0 }, 2, null, null);
        check(copies.Length == 2 && mesh.Parts.Count == 3, "multiple translation copies");
        check(copies.Select(n => mesh.Parts[n].NodeLabels.Min(id => mesh.Nodes[id].X)).Order().SequenceEqual(new[] { 2d, 4 }), "translation copies use original coordinates");
        var originalIds = mesh.Parts[name].NodeLabels.ToArray();
        mesh.TranslateParts(new[] { name }, new[] { 0d, 3, 0 }, 0, null, null);
        check(mesh.Parts.Count == 3 && originalIds.All(id => mesh.Nodes[id].Y >= 3), "in-place translation preserves part count");
        var names = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Part-1"] = 1, ["PART-3"] = 3 };
        check(names.GetNextNumberedKey("part") == "part-2", "numbered key respects dictionary comparer");
        check(new Dictionary<int, int> { [1] = 1, [4] = 4 }.FindFreeIntervalOfKeys(2, 4) == 2, "free ID interval uses gap");
        check(new Dictionary<int, int> { [1] = 1, [4] = 4 }.FindFreeIntervalOfKeys(3, 4) == 5, "free ID interval appends when gap too small");
        mesh = Create();
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) FeMesh.WriteToBinaryWriter(mesh, writer);
        stream.Position = 0;
        var restored = new FeMesh(mesh, mesh.Parts.Keys.ToArray());
        restored.Nodes[2] = new FeNode(2, 99, 0, 0);
        using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true)) FeMesh.ReadFromBinaryReader(restored, reader, -1);
        check(restored.Nodes[2].X == 1 && restored.Elements[10].NodeIds.SequenceEqual(mesh.Elements[10].NodeIds), "aggregate binary payload restores geometry");
        check(stream.Position == stream.Length && restored.Parts.Values.Single().Visualization.Cells.Length == 4, "aggregate binary payload consumes topology");

        using var legacyStream = new MemoryStream();
        using (var writer = new BinaryWriter(legacyStream, System.Text.Encoding.UTF8, true))
            FeMesh.WriteToBinaryWriter(mesh, writer, 2_004_000);
        legacyStream.Position = 0;
        var reconstructed = new FeMesh(MeshRepresentation.Results);
        using (var reader = new BinaryReader(legacyStream, System.Text.Encoding.UTF8, true))
            FeMesh.ReadFromBinaryReader(reconstructed, reader, 2_004_000);
        check(reconstructed.Nodes.Count == 4 && reconstructed.Elements.Count == 1 && reconstructed.Parts.Count == 1,
            "legacy result mesh reconstructs unnamed parts without serialized metadata");
        check(legacyStream.Position == legacyStream.Length && reconstructed.Parts.Values.Single().Visualization.Cells.Length == 4,
            "legacy result mesh consumes reconstructed-part visualization data");
    }
}

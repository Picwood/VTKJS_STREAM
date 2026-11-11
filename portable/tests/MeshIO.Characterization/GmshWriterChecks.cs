using System.Globalization;
using CaeMesh;
using FileInOut.Output;

internal static class GmshWriterChecks
{
    public static void Run(string directory, Action<bool, string> check)
    {
        var mesh = new FeMesh(new Dictionary<int, FeNode> { [10] = new(10, .25, 0, 0), [20] = new(20, 1.5, 0, 0),
            [30] = new(30, .25, 2.5, 0), [40] = new(40, 10, 0, 0), [50] = new(50, 11, 0, 0), [60] = new(60, 10, 1, 0) },
            new Dictionary<int, FeElement> { [17] = new LinearTriangleElement(17, new[] { 10, 20, 30 }),
                [42] = new LinearTriangleElement(42, new[] { 40, 50, 60 }) }, MeshRepresentation.Mesh);
        string path = Path.Combine(directory, "gmsh é.msh");
        void SaveFixture(string name)
        {
            string artifacts = Environment.GetEnvironmentVariable("PREPOMAX_MESH_IO_ARTIFACTS");
            if (string.IsNullOrEmpty(artifacts)) return;
            Directory.CreateDirectory(artifacts);
            File.Copy(path, Path.Combine(artifacts, name + "-" + CultureInfo.CurrentCulture.Name + ".msh"), true);
        }
        string part = mesh.Parts.Values.Single(p => p.Labels.Contains(42)).Name;
        GmshMshFileWriter.Write(path, mesh, new[] { part });
        var selected = Parse(path, check);
        SaveFixture("selected");
        check(selected.elements.Keys.SequenceEqual(new[] { 42 }) && selected.nodes.Keys.Order().SequenceEqual(new[] { 40, 50, 60 }), "MSH selected-part IDs");
        GmshMshFileWriter.Write(path, mesh);
        var all = Parse(path, check);
        SaveFixture("all");
        check(all.nodes[10][0] == .25 && all.nodes[20][0] == 1.5 && all.nodes[30][1] == 2.5, "MSH invariant coordinates");
        check(all.elements[17].SequenceEqual(new[] { 10, 20, 30 }), "MSH triangle connectivity");
        GmshMshFileWriter.Write(path, mesh, Array.Empty<string>());
        var empty = Parse(path, check);
        SaveFixture("empty");
        check(empty.nodes.Count == 0 && empty.elements.Count == 0, "MSH empty selection");
        // A shared node must be written once even when referenced by different dimensions.
        mesh.Elements.Add(90, new LinearBeamElement(90, new[] { 10, 20 }));
        GmshMshFileWriter.Write(path, mesh);
        var mixed = Parse(path, check);
        SaveFixture("mixed");
        check(mixed.nodes.Count == 6 && mixed.elements.Count == 3 && mixed.elements[90].SequenceEqual(new[] { 10, 20 }), "MSH mixed-dimension shared nodes");
    }

    private static (Dictionary<int, double[]> nodes, Dictionary<int, int[]> elements) Parse(string path, Action<bool, string> check)
    {
        string[] lines = File.ReadAllLines(path);
        int i = 0;
        string Next() => lines[i++];
        int[] Integers() => Next().Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToArray();
        check(Next() == "$MeshFormat" && Next() == "4.1 0 8" && Next() == "$EndMeshFormat", "MSH header");
        check(Next() == "$Nodes", "MSH nodes section");
        var header = Integers(); var nodes = new Dictionary<int, double[]>();
        for (int block = 0; block < header[0]; block++)
        {
            var metadata = Integers();
            check(metadata.Length == 4 && metadata[2] == 0, "MSH nonparametric node block");
            int[] ids = Enumerable.Range(0, metadata[3]).Select(_ => int.Parse(Next(), CultureInfo.InvariantCulture)).ToArray();
            foreach (int id in ids)
            {
                var coordinates = Next().Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(x => double.Parse(x, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
                check(coordinates.Length == 3 && nodes.TryAdd(id, coordinates), "MSH unique node record");
            }
        }
        check(header[1] == nodes.Count && header[2] == nodes.Keys.DefaultIfEmpty(0).Min() && header[3] == nodes.Keys.DefaultIfEmpty(0).Max(), "MSH node counts and bounds");
        check(Next() == "$EndNodes" && Next() == "$Elements", "MSH element section");
        header = Integers(); var elements = new Dictionary<int, int[]>();
        for (int block = 0; block < header[0]; block++)
        {
            var metadata = Integers();
            check(metadata.Length == 4 && (metadata[2] == 2 || metadata[2] == 1), "MSH fixture element type");
            for (int j = 0; j < metadata[3]; j++)
            {
                int[] record = Integers();
                check(record.Length == (metadata[2] == 2 ? 4 : 3) && record.Skip(1).All(nodes.ContainsKey), "MSH element references");
                elements.Add(record[0], record.Skip(1).ToArray());
            }
        }
        check(header[1] == elements.Count && header[2] == elements.Keys.DefaultIfEmpty(0).Min() && header[3] == elements.Keys.DefaultIfEmpty(0).Max(), "MSH element counts and bounds");
        check(Next() == "$EndElements" && i == lines.Length, "MSH complete file");
        return (nodes, elements);
    }
}

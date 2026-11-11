using CaeMesh;
using CaeModel;
using FileInOut.Output.Calculix;
using System.Globalization;

internal static class MeshKeywordChecks
{
    private static string Text(CalculixKeyword keyword) => keyword.GetKeywordString() + keyword.GetDataString();
    public static string CubeMesh()
    {
        double[][] coordinates = { new double[] {0,0,0}, new double[] {1,0,0}, new double[] {1,1,0},
            new double[] {0,1,0}, new double[] {0,0,1}, new double[] {1,0,1}, new double[] {1,1,1}, new double[] {0,1,1} };
        var nodes = coordinates.Select((p, i) => new FeNode(i + 1, p[0], p[1], p[2])).ToDictionary(n => n.Id);
        return Text(new CalNode(nodes, new Dictionary<string, FeReferencePoint>(), ModelSpaceEnum.ThreeD,
            new List<FeNode>(), new Dictionary<string, int[]>(), null)) +
            Text(new CalElement("C3D8", "SOLID", new List<FeElement> { new LinearHexaElement(1, Enumerable.Range(1, 8).ToArray()) })) +
            Text(new CalNodeSet(new FeNodeSet("LEFT", new[] { 1, 4, 5, 8 }))) +
            Text(new CalNodeSet(new FeNodeSet("RIGHT", new[] { 2, 3, 6, 7 }))) +
            Text(new CalNodeSet(new FeNodeSet("ALLNODES", Enumerable.Range(1, 8).ToArray())));
    }
    public static void Run(Action<bool, string> check)
    {
        var nodes = new Dictionary<int, FeNode> { [9] = new FeNode(9, .25, .5, .75), [2] = new FeNode(2, 1, 2, 3) };
        var writer = new CalNode(nodes, new Dictionary<string, FeReferencePoint>(), ModelSpaceEnum.ThreeD,
            new List<FeNode>(), new Dictionary<string, int[]>(), new() { [9] = new[] { .125, 0d, 0 } });
        var rows = writer.GetDataString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        check(rows[0].StartsWith("2,") && rows[1].StartsWith("9,"), "node export sorts IDs");
        var values = rows[1].Split(',').Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
        check(values.SequenceEqual(new[] { 9d, .375, .5, .75 }), "node export uses invariant deformed coordinates");
        check(nodes[9].X == .25 && writer.GetDataString().Split(Environment.NewLine)[1] == rows[1],
            "node export leaves source coordinates unchanged across repeated writes");
        var set = new FeNodeSet("N", new[] { 9, 2 });
        check(new CalNodeSet(set).GetDataString().Trim() == "2, 9" && set.Labels.SequenceEqual(new[] { 9, 2 }),
            "node-set writer sorts without mutating membership");
        check(CubeMesh().Contains("*Element, Type=C3D8, Elset=SOLID"), "cube mesh uses solid element keyword");
    }
}

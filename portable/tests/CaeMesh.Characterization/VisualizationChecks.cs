using CaeMesh;
using System.Text;

internal static class VisualizationChecks
{
    public static void Run(Action<bool, string> check)
    {
        var box = new BoundingBox();
        box.IncludeCoor(new[] { 1d, 2, 3 }); box.IncludeCoor(new[] { 3d, 5, 7 }); box.Tag = 4;
        var boxCopy = box.DeepCopy();
        check(boxCopy.GetCenter().SequenceEqual(new[] { 2d, 3.5, 5 }) && boxCopy.GetVolume() == 24 && (int)boxCopy.Tag == 4, "bounding geometry and index copy");
        boxCopy.MaxX = 99;
        check(box.MaxX == 3, "bounding copy isolation");
        box.Tag = new List<int> { 1, 2 };
        bool rejected = false;
        try { box.DeepCopy(); } catch (NotSupportedException) { rejected = true; }
        check(rejected, "arbitrary tag requires explicit copy policy");
        boxCopy = box.DeepCopy(tag => new List<int>((List<int>)tag));
        ((List<int>)boxCopy.Tag).Add(3);
        check(((List<int>)box.Tag).Count == 2 && ((List<int>)boxCopy.Tag).Count == 3, "explicit tag copy isolation");
        box.Tag = null;
        check(box.DeepCopy().Tag == null, "null bounding tag");

        var topologyCases = new (int[], ElementFaceType, int[][])[] {
            (new[] { 1,2 }, ElementFaceType.Edge, new[] { new[] { 1,2 } }),
            (new[] { 1,2,3 }, ElementFaceType.Edge, new[] { new[] { 1,2,3 } }),
            (new[] { 1,2,3 }, ElementFaceType.Face, new[] { new[] { 1,2 },new[] { 2,3 },new[] { 3,1 } }),
            (new[] { 1,2,3,4 }, ElementFaceType.Face, new[] { new[] { 1,2 },new[] { 2,3 },new[] { 3,4 },new[] { 4,1 } }),
            (new[] { 1,2,3,4,5,6 }, ElementFaceType.Face, new[] { new[] { 1,2,4 },new[] { 2,3,5 },new[] { 3,1,6 } }),
            (new[] { 1,2,3,4,5,6,7,8 }, ElementFaceType.Face, new[] { new[] { 1,2,5 },new[] { 2,3,6 },new[] { 3,4,7 },new[] { 4,1,8 } })
        };
        foreach (var (cell, type, expected) in topologyCases)
        {
            var actual = MeshTopology.GetVisualizationEdgeCells(cell, type);
            check(actual.Length == expected.Length && actual.Zip(expected).All(pair => pair.First.SequenceEqual(pair.Second)), "topology edge ordering");
            actual[0][0] = -1;
            check(cell[0] == 1, "topology output isolation");
        }
        var visualization = new VisualizationData();
        var elements = new Dictionary<int, FeElement> {
            [10] = new LinearTetraElement(10,new[] {1,2,3,4}),
            [20] = new LinearTetraElement(20,new[] {1,3,2,5})
        };
        visualization.ExtractVisualizationCellsFromElements3D(elements,new[] {10,20});
        check(visualization.Cells.Length == 6 && visualization.CellIds.Count(id=>id==10) == 3 && visualization.CellIds.Count(id=>id==20) == 3, "shared tetra face removed");
        check(!visualization.Cells.Any(cell => cell.OrderBy(x=>x).SequenceEqual(new[] {1,2,3})), "interior face absent");
        visualization.CellIdsByFace = new[] {new[] {0,1,2},new[] {3,4,5}};
        visualization.FaceAreas = new[] {1d,2};
        visualization.FaceTypes = new[] {(GeomFaceType)1,(GeomFaceType)2};
        visualization.FaceEdgeIds = new[] {new[] {0},new[] {1}};
        visualization.EdgeCells = new[] {new[] {1,2},new[] {2,3}};
        visualization.EdgeCellIdsByEdge = new[] {new[] {0},new[] {1}};
        visualization.EdgeLengths = new[] {1d,2};
        visualization.EdgeTypes = new[] {(GeomCurveType)1,(GeomCurveType)2};
        visualization.VertexNodeIds = new[] {1,2,3};
        byte[] bytes = Write(visualization);
        using var reader = new BinaryReader(new MemoryStream(bytes));
        VisualizationData.ReadFromBinaryStream(out var restored,reader,-1);
        check(Write(restored).SequenceEqual(bytes) && reader.BaseStream.Position == bytes.Length, "visualization binary round trip");
        var restoredCopy = restored.DeepCopy();
        restoredCopy.Cells[0][0] = -1; restoredCopy.EdgeCells[0][0] = -1; restoredCopy.FaceAreas[0] = -1;
        check(restored.Cells[0][0] != -1 && restored.EdgeCells[0][0] == 1 && restored.FaceAreas[0] == 1, "visualization nested copy isolation");
        check(Write(null).SequenceEqual(Convert.FromHexString("FFFFFFFF")), "null visualization marker");

        foreach (BasePart part in new BasePart[] {
            new BasePart("Base",1,new[] {1,2,3,4},new[] {10},new[] {typeof(LinearTetraElement)}),
            new MeshPart("Mesh",2,new[] {1,2,3,4},new[] {10},new[] {typeof(LinearTetraElement)}),
            new ResultPart("Result",3,new[] {1,2,3,4},new[] {10},new[] {typeof(LinearTetraElement)}) })
        {
            part.Visualization = restored;
            part.BoundingBox = box;
            part.MassProperties = new PartMassProperties(true) { Volume = 24 };
            BasePart copy = part.DeepCopy();
            check(copy.GetType() == part.GetType() && copy.Name == part.Name && copy.PartId == part.PartId && copy.PartType == PartType.Solid, "part subtype and identity");
            check(copy.MassProperties.Volume == 24 && copy.Color == part.Color && copy.BoundingBox.GetVolume() == 24, "part properties preserved");
            copy.NodeLabels[0] = -1; copy.Labels[0] = -1; copy.Offset[0] = 99; copy.BoundingBox.MaxX = 99;
            copy.Visualization.Cells[0][0] = -1; copy.MassProperties.CenterOfMass[0] = 99;
            check(part.NodeLabels[0] == 1 && part.Labels[0] == 10 && part.Offset[0] == 0 && part.BoundingBox.MaxX == 3 &&
                part.Visualization.Cells[0][0] != -1 && part.MassProperties.CenterOfMass[0] == 0, "part mutable data isolation");
        }
        var graph = new Graph<int>();
        var a = new Node<int>(1); var b = new Node<int>(2); var c = new Node<int>(3);
        graph.AddNode(a);graph.AddNode(b);graph.AddNode(c);graph.AddUndirectedEdge(a,b);
        var groups = graph.GetConnectedSubgraphs().Select(g=>g.GetValues().OrderBy(x=>x).ToArray()).ToArray();
        check(groups.Length == 2 && groups.Any(g=>g.SequenceEqual(new[] {1,2})) && groups.Any(g=>g.SequenceEqual(new[] {3})), "graph connected components");
        var graphCopy = new Graph<int>(graph); graphCopy.Remove(2);
        check(graph.Count == 3 && graph.Contains(2) && graphCopy.Count == 2, "graph structure copy isolation");
    }
    private static byte[] Write(VisualizationData data)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream,Encoding.UTF8,true);
        VisualizationData.WriteToBinaryStream(data,writer);
        return stream.ToArray();
    }
}

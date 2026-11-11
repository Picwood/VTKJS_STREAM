using CaeGlobals;
using Octree;
using System.Globalization;

int checks = 0;
CultureInfo original = CultureInfo.CurrentCulture;
try
{
    foreach (string culture in new[] { "en-US", "it-IT" })
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        SelectionChecks.Run(Check);
        EquationChecks.Run(Check);
        UnitChecks.Run(Check);
        BinaryArrayChecks.Run(Check);
        Near(Geometry.PointToSegmentDistance(new[] { 1d, 3, 0 }, new[] { 0d, 0, 0 }, new[] { 2d, 0, 0 }), 3, "segment interior");
        Near(Geometry.PointToSegmentDistance(new[] { 5d, 4, 0 }, new[] { 0d, 0, 0 }, new[] { 2d, 0, 0 }), 5, "segment endpoint");
        Near(Geometry.PointToTriangleDistance(new[] { 0.25, 0.25, 2 },
            new[] { new[] { 0d, 0, 0 }, new[] { 1d, 0, 0 }, new[] { 0d, 1, 0 } }), 2, "triangle projection");
        double[][] triangle = { new[] { 0d, 0, 0 }, new[] { 2d, 0, 0 }, new[] { 0d, 2, 0 } };
        foreach (double[][] winding in new[] { triangle, triangle.Reverse().ToArray() })
        {
            Near(Geometry.PointToTriangleDistance(new[] { 0.5, 0.5, 0 }, winding), 0, "point on triangle");
            Near(Geometry.PointToTriangleDistance(new[] { 0.5, 0.5, -3 }, winding), 3, "negative plane side");
            Near(Geometry.PointToTriangleDistance(new[] { 1d, -2, 0 }, winding), 2, "outside edge");
            Near(Geometry.PointToTriangleDistance(new[] { 2d, 2, 0 }, winding), Math.Sqrt(2), "outside diagonal edge");
            Near(Geometry.PointToTriangleDistance(new[] { -3d, -4, 0 }, winding), 5, "outside vertex");
        }
        Near(Geometry.PointToTriangleDistance(new[] { 1d, 3, 0 },
            new[] { new[] { 0d, 0, 0 }, new[] { 2d, 0, 0 }, new[] { 1d, 0, 0 } }), 3, "collinear triangle");
        Near(Geometry.PointToTriangleDistance(new[] { 1d, 3, 0 },
            new[] { new[] { 0d, 0, 0 }, new[] { 2d, 0, 0 }, new[] { 0d, 0, 0 } }), 3, "duplicate vertex triangle");
        Near(Geometry.PointToTriangleDistance(new[] { 0d, 3, 4 },
            new[] { new double[3], new double[3], new double[3] }), 5, "collapsed triangle");
        var closest = new Geometry2().ComputeClosest(new Vec3D(0, 0, 0), new Vec3D(2, 0, 0),
            new Vec3D(1, -1, 3), new Vec3D(1, 1, 3));
        Near(closest.Distance, 3, "skew segments");
        var cross = Vec3D.CrossProduct(new Vec3D(1, 0, 0), new Vec3D(0, 1, 0));
        Check(cross.Coor.SequenceEqual(new[] { 0d, 0, 1 }), "right-handed cross product");

        // Zero leading pivot requires row exchange during inversion.
        double[][] matrix = { new[] { 0d, 2, 1 }, new[] { 1d, 1, 0 }, new[] { 2d, 0, 1 } };
        double[][] product = Matrix.MatrixProduct(matrix, Matrix.MatrixInverse(matrix));
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++) Near(product[row][col], row == col ? 1 : 0, "matrix inverse");
        Check(matrix[0][0] == 0 && matrix[1][0] == 1, "inversion preserves input");

        var queue = new UniqueQueue<int>();
        queue.Enqueue(3); queue.Enqueue(2); queue.Enqueue(3);
        Check(queue.Count == 2 && queue.Dequeue() == 3, "unique FIFO");
        queue.Enqueue(3);
        Check(queue.SequenceEqual(new[] { 2, 3 }), "re-enqueue after removal");
        queue.Clear();
        Check(queue.Count == 0 && !queue.Contains(3), "queue clear");

        var dictionary = new CaeGlobals.OrderedDictionary<string, int>("parts");
        dictionary.Add("B", 2); dictionary.Add("A", 1); dictionary["B"] = 20;
        dictionary.Insert(1, "C", 3);
        Check(dictionary.Keys.SequenceEqual(new[] { "B", "C", "A" }), "insertion order");
        Check(dictionary.Replace("C", "D", 4), "rename entry");
        Check(dictionary.Keys.SequenceEqual(new[] { "B", "D", "A" }), "rename preserves position");
        Check(dictionary.Remove("D") && dictionary.Values.SequenceEqual(new[] { 20, 1 }), "ordered removal");
        Throws<CaeException>(() => { _ = dictionary["absent"]; }, "missing key domain exception");

        var key = new SortedIntKey(new[] { 9, 2, 9, 1 });
        var equivalent = new SortedIntKey(new[] { 1, 9, 2 });
        Check(key.Equals(equivalent) && key.GetHashCode() == equivalent.GetHashCode(), "unordered node key");
        string name = "  Part-À_1  ";
        NamedClass.CheckNameForErrors(ref name, null);
        Check(name == "Part-À_1", "Unicode name and trimming");
        Throws<CaeException>(() => { string invalid = "Missing"; NamedClass.CheckNameForErrors(ref invalid, null); }, "reserved name");

        var nodes = new NodesExchangeData { Ids = new[] { 7 }, Coor = new[] { new[] { 1d, 2, 3 } }, Values = new[] { 4f } };
        var copy = nodes.DeepCopy();
        copy.Ids[0] = 8; copy.Coor[0][0] = 9; copy.Values[0] = 10;
        Check(nodes.Ids[0] == 7 && nodes.Coor[0][0] == 1 && nodes.Values[0] == 4, "mesh transfer deep copy");

        var random = new Random(20260910);
        var points = Enumerable.Range(0, 256).Select(_ => new Point(
            random.NextDouble() * 200 - 100, random.NextDouble() * 200 - 100, random.NextDouble() * 200 - 100)).ToArray();
        var tree = new PointOctree<int>(2, new Point(0, 0, 0), 0.25);
        for (int i = 0; i < points.Length; i++) tree.Add(i, points[i]);
        Check(tree.Count == points.Length, "octree growth");
        for (int query = 0; query < 40; query++)
        {
            Point center = points[query];
            double radius = 1 + query * 2;
            int[] expected = Enumerable.Range(0, points.Length).Where(i =>
                Math.Pow(points[i].X - center.X, 2) + Math.Pow(points[i].Y - center.Y, 2) +
                Math.Pow(points[i].Z - center.Z, 2) <= radius * radius).ToArray();
            Check(tree.GetNearby(center, radius).OrderBy(i => i).SequenceEqual(expected), "octree versus brute force");
        }
        for (int i = 0; i < 128; i++) Check(tree.Remove(i, points[i]), "octree removal");
        Check(tree.GetAll().OrderBy(i => i).SequenceEqual(Enumerable.Range(128, 128)), "octree retained objects");

        foreach (string input in new[] { "", "Mesh à Ω\r\n1.25", string.Concat(Enumerable.Range(0, 10000).Select(i => $"Node-{i}: à Ω\n")) })
            Check(StringCompressor.DecompressString(StringCompressor.CompressString(input)) == input, "compressed UTF-8 round trip");
        foreach (int declaredLength in new[] { 2, 4 })
        {
            byte[] invalid = Convert.FromBase64String(StringCompressor.CompressString("abc"));
            BitConverter.GetBytes(declaredLength).CopyTo(invalid, 0);
            Throws<InvalidDataException>(() => StringCompressor.DecompressString(Convert.ToBase64String(invalid)), "compressed length mismatch");
        }
    }
    Console.WriteLine($"PASS: {checks} CaeGlobals characterization checks.");
}
finally { CultureInfo.CurrentCulture = original; }

void Check(bool condition, string message)
{
    if (!condition) throw new Exception($"FAIL ({CultureInfo.CurrentCulture.Name}): {message}");
    checks++;
}
void Near(double actual, double expected, string message) => Check(Math.Abs(actual - expected) < 1e-10, message);
void Throws<T>(Action action, string message) where T : Exception
{
    try { action(); }
    catch (T) { checks++; return; }
    throw new Exception("FAIL: " + message);
}

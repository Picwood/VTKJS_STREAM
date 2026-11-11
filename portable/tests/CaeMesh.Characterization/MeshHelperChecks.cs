using CaeMesh;

internal static class MeshHelperChecks
{
    public static void Run(Action<bool, string> check)
    {
        void Vector(double[] a, double[] b, string label) => check(a.Zip(b).All(p => Math.Abs(p.First - p.Second) < 1e-10), label);
        foreach (int item in new[] { 0, 1, 123, 20000 })
        foreach (int part in new[] { 1, 9900 })
        foreach (GeometryType type in new[] { GeometryType.Vertex, GeometryType.Edge, GeometryType.SolidSurface, GeometryType.Part })
        {
            int encoded = MeshGeometryIds.GetGeometryId(item, (int)type, part);
            check(encoded == item * 100000 + (int)type * 10000 + part, "geometry ID wire value");
            check(MeshGeometryIds.GetItemTypePartIdsFromGeometryId(encoded).SequenceEqual(new[] { item, (int)type, part }), "geometry ID components");
            check(MeshGeometryIds.GetItemIdFromGeometryId(encoded) == item && MeshGeometryIds.GetTypeIdFromGeometryId(encoded) == (int)type &&
                MeshGeometryIds.GetPartIdFromGeometryId(encoded) == part, "individual ID accessors");
            check(MeshGeometryIds.GetGeometryPartIdFromGeometryId(encoded) == 90000 + part, "part selection ID");
        }
        check(MeshGeometryIds.GetPartIdsFromGeometryIds(null) == null, "null geometry IDs");
        check(MeshGeometryIds.GetPartIdsFromGeometryIds(new[] { 120001, 230001, 390002 }).Order().SequenceEqual(new[] { 1, 2 }), "distinct geometry parts");
        check(GeometryType.ShellEdgeSurface.IsEdge() && !GeometryType.ShellEdgeSurface.IsSurface() &&
            GeometryType.ShellBackSurface.IsShellSurface() && GeometryType.SolidSurface.IsSurface(), "geometry classifications");
        var point = new[] { 2d, 3, 4 };
        MeshTransforms.TranslatePoint(point, new[] { 1d, -2, 3 });
        Vector(point, new[] { 3d, 1, 7 }, "point translation");
        MeshTransforms.ScalePoint(point, new[] { 1d, 1, 1 }, new[] { 2d, 3, -1 });
        Vector(point, new[] { 5d, 1, -5 }, "scale about center");
        point = new[] { 3d, 2, 3 };
        MeshTransforms.RotatePoint(point, new[] { 1d, 2, 3 }, new[] { 0d, 0, 2 }, Math.PI / 2);
        Vector(point, new[] { 1d, 4, 3 }, "rotation with normalized axis");
        MeshTransforms.MirrorPoint(point, new[] { 0d, 2, 0 }, new[] { 0d, 3, 0 });
        Vector(point, new[] { 1d, 0, 3 }, "single point reflection changes caller array");
        var points = new[] { new[] { 1d, 4, 3 }, new[] { 2d, 2, 3 } };
        MeshTransforms.MirrorPoints(points, new[] { 0d, 2, 0 }, new[] { 0d, 3, 0 });
        Vector(points[0], new[] { 1d, 0, 3 }, "batch reflection");
        Vector(points[1], new[] { 2d, 2, 3 }, "point on mirror plane");
    }
}

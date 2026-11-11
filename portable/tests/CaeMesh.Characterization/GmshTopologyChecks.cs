using CaeMesh;

internal static class GmshTopologyChecks
{
    public static void Run(Action<bool, string> check)
    {
        var triangle = new GmshSurface(1, new[] { 1, 2, 3 }, new[] { 10, 11, 12 },
            new[] { 2d, 2, 3 }, new[] { new[] { 1, 2 }, new[] { 2, 3 }, new[] { 3, 1 } });
        check(triangle.ThreeSided && triangle.Transfinite && !triangle.Collapsed && triangle.VertexIds[0] == 2,
            "triangle selects equal-edge corner");
        triangle.SetFirstVertexId(1);
        check(triangle.VertexIds.SequenceEqual(new[] { 1, 2, 3 }) && triangle.OppositeEdgesA.SequenceEqual(new[] { 12, 10 }),
            "triangle corner rotation preserves winding and edge pair");
        var collapsed = new GmshSurface(2, new[] { 1, 2, 3 }, new[] { 99, 10, 11, 12 },
            new[] { 0d, 2, 2, 3 }, new[] { new[] { 1, 1 }, new[] { 1, 2 }, new[] { 2, 3 }, new[] { 3, 1 } });
        check(collapsed.ThreeSided && collapsed.Transfinite && collapsed.EdgeIds.SequenceEqual(new[] { 10, 11, 12 }),
            "collapsed triangle exposes repaired edge IDs");
        collapsed.SetFirstVertexId(1);
        check(collapsed.OppositeEdgesA.SequenceEqual(new[] { 12, 10 }), "repaired triangle rotation uses repaired edge IDs");
        var quad = new GmshSurface(3, new[] { 1, 2, 3, 4 }, new[] { 10, 11, 12, 13 },
            new[] { 1d, 1, 1, 1 }, new[] { new[] { 1, 2 }, new[] { 2, 3 }, new[] { 3, 4 }, new[] { 4, 1 } });
        check(quad.FourSided && quad.Transfinite && !quad.HasHole && quad.OppositeEdgesA.SequenceEqual(new[] { 10, 12 }) &&
            quad.OppositeEdgesB.SequenceEqual(new[] { 11, 13 }), "quadrilateral opposite edge pairs");
        var edge = new GmshEdge(5, new[] { 1, 2 }, 3, 2.5);
        edge.SurfaceIds.Add(3); edge.SurfaceIds.Add(4);
        check(edge.SurfaceIds.Count == 2 && edge.Length == 2.5, "edge incident surfaces are unique");
        var volume = new GmshVolume(6, 3);
        volume.TriSurfIds.Add(1); volume.QuadSurfIds.Add(2);
        check(volume.NumTriSurfaces == 1 && volume.NumQuadSurfaces == 1 && !volume.Transfinite, "volume face classification");
        var surfaces = new[] { triangle, quad, collapsed }; Array.Sort(surfaces);
        check(surfaces.Select(s => s.Id).SequenceEqual(new[] { 3, 2, 1 }), "topology descending identifier ordering");
    }
}

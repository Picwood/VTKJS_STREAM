using PrePoMax.Render.Server;
using PrePoMax.Render.SyntheticData;

namespace PrePoMax.Render.Server.Tests;

public sealed class SectionGeometryBuilderTests
{
    [Fact]
    public void Build_IntersectsTetrahedronAndInterpolatesPointScalars()
    {
        var scene = SmallFixtureFactory.Create();
        var parent = scene.Manifest.Actors[0];
        var topology = new SelectionActorTopology(parent.Name, [10, 11, 12, 13],
            [0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1],
            [new SelectionCellTopology(99, 10, [0, 1, 2, 3])], [0, 4, 8, 12]);

        var result = SectionGeometryBuilder.Build(scene.Manifest, [topology], Request([0.25, 0, 0]));

        var actor = Assert.Single(result.Actors);
        Assert.Equal(0, result.SkippedCells);
        Assert.Equal(3, actor.Positions.Length / 3);
        Assert.Equal(1, actor.TriangleConnectivity.Length / 3);
        Assert.Equal(3, actor.LineConnectivity.Length / 2);
        Assert.Equal([99], actor.SourceElementIds);
        Assert.All(Enumerable.Range(0, 3), point =>
            Assert.Equal(0.25, actor.Positions[point * 3] + scene.Manifest.WorldOrigin[0], 6));
        Assert.Equal([1f, 7f, 10f], actor.PointScalars.Order().ToArray());
        Assert.False(parent.Material.FrontRgba.SequenceEqual(actor.Rgba));
    }

    [Fact]
    public void Build_AppliesActorOffsetWhenResolvingPlaneButReturnsLocalGeometry()
    {
        var scene = SmallFixtureFactory.Create();
        var parent = scene.Manifest.Actors[0];
        var topology = new SelectionActorTopology(parent.Name, [10, 11, 12, 13],
            [0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1],
            [new SelectionCellTopology(99, 10, [0, 1, 2, 3])]);
        var request = Request([1.25, 0, 0]) with
        {
            ActorOffsets = [new SectionActorOffset(parent.Name, [1, 0, 0])],
        };

        var actor = Assert.Single(SectionGeometryBuilder.Build(scene.Manifest, [topology], request).Actors);

        Assert.All(Enumerable.Range(0, 3), point =>
            Assert.Equal(0.25, actor.Positions[point * 3] + scene.Manifest.WorldOrigin[0], 6));
    }

    [Fact]
    public void Build_QuadraticTetraMatchesVtk58EightTetraSubdivision()
    {
        var scene = SmallFixtureFactory.Create();
        var parent = scene.Manifest.Actors[0];
        var origin = scene.Manifest.WorldOrigin;
        double[][] localPoints =
        [
            [0, 0, 0], [1, 0, 0], [0, 1, 0], [0, 0, 1],
            [0.5, 0, 0], [0.5, 0.5, 0], [0, 0.5, 0],
            [0, 0, 0.5], [0.5, 0, 0.5], [0, 0.5, 0.5],
        ];
        var coordinates = localPoints.SelectMany(point => new[]
        {
            origin[0] + point[0], origin[1] + point[1], origin[2] + point[2],
        }).ToArray();
        var scalars = localPoints.Select(point => (float)(point[1] + 2 * point[2])).ToArray();
        var topology = new SelectionActorTopology(parent.Name, Enumerable.Range(10, 10).ToArray(), coordinates,
            [new SelectionCellTopology(99, 24, Enumerable.Range(0, 10).ToArray())], scalars);

        var actor = Assert.Single(SectionGeometryBuilder.Build(scene.Manifest, [topology],
            Request([origin[0] + 0.25, origin[1], origin[2]])).Actors);

        Assert.Equal(10, actor.Positions.Length / 3);
        Assert.Equal(9, actor.TriangleConnectivity.Length / 3);
        Assert.Equal(3, actor.LineConnectivity.Length / 2);
        Assert.Equal(Enumerable.Repeat(99, 9), actor.SourceElementIds);
        Assert.Equal([0, 1, 2, 3, 10, 11, 12, 20, 21, 30], Enumerable.Range(0, 10).Select(point =>
            (int)Math.Round(actor.Positions[point * 3 + 1] * 4) * 10 +
            (int)Math.Round(actor.Positions[point * 3 + 2] * 4)).Order());
        Assert.Contains(Enumerable.Range(0, 10), point =>
            Math.Abs(actor.Positions[point * 3] - 0.25f) < 1e-6 &&
            Math.Abs(actor.Positions[point * 3 + 1] - 0.25f) < 1e-6 &&
            Math.Abs(actor.Positions[point * 3 + 2] - 0.25f) < 1e-6);
        for (var point = 0; point < 10; point++)
        {
            Assert.Equal(0.25f, actor.Positions[point * 3], 6);
            Assert.Equal(actor.Positions[point * 3 + 1] + 2 * actor.Positions[point * 3 + 2],
                actor.PointScalars[point], 6);
        }
        for (var triangle = 0; triangle < actor.TriangleConnectivity.Length; triangle += 3)
        {
            var first = actor.TriangleConnectivity[triangle] * 3;
            var second = actor.TriangleConnectivity[triangle + 1] * 3;
            var third = actor.TriangleConnectivity[triangle + 2] * 3;
            var normalX = (actor.Positions[second + 1] - actor.Positions[first + 1]) *
                          (actor.Positions[third + 2] - actor.Positions[first + 2]) -
                          (actor.Positions[second + 2] - actor.Positions[first + 2]) *
                          (actor.Positions[third + 1] - actor.Positions[first + 1]);
            Assert.True(normalX > 0);
        }
    }

    [Fact]
    public void Build_TriQuadraticHexahedronMatchesVtk58EightHexSubdivision()
    {
        var scene = SmallFixtureFactory.Create();
        var parent = scene.Manifest.Actors[0];
        var origin = scene.Manifest.WorldOrigin;
        double[][] localPoints =
        [
            [0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0],
            [0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1],
            [0.5, 0, 0], [1, 0.5, 0], [0.5, 1, 0], [0, 0.5, 0],
            [0.5, 0, 1], [1, 0.5, 1], [0.5, 1, 1], [0, 0.5, 1],
            [0, 0, 0.5], [1, 0, 0.5], [1, 1, 0.5], [0, 1, 0.5],
            [0, 0.5, 0.5], [1, 0.5, 0.5], [0.5, 0, 0.5],
            [0.5, 1, 0.5], [0.5, 0.5, 0], [0.5, 0.5, 1], [0.5, 0.5, 0.5],
        ];
        var coordinates = localPoints.SelectMany(point => new[]
        {
            origin[0] + point[0], origin[1] + point[1], origin[2] + point[2],
        }).ToArray();
        var scalars = localPoints.Select(point => (float)(point[1] + 2 * point[2])).ToArray();
        var topology = new SelectionActorTopology(parent.Name, Enumerable.Range(10, 27).ToArray(), coordinates,
            [new SelectionCellTopology(199, 29, Enumerable.Range(0, 27).ToArray())], scalars);

        var actor = Assert.Single(SectionGeometryBuilder.Build(scene.Manifest, [topology],
            Request([origin[0] + 0.25, origin[1], origin[2]])).Actors);

        Assert.Equal(9, actor.Positions.Length / 3);
        Assert.Equal(8, actor.TriangleConnectivity.Length / 3);
        Assert.Equal(8, actor.LineConnectivity.Length / 2);
        Assert.Equal(Enumerable.Repeat(199, 8), actor.SourceElementIds);
        Assert.Equal([0, 2, 4, 20, 22, 24, 40, 42, 44], Enumerable.Range(0, 9).Select(point =>
            (int)Math.Round(actor.Positions[point * 3 + 1] * 4) * 10 +
            (int)Math.Round(actor.Positions[point * 3 + 2] * 4)).Order());
        for (var point = 0; point < 9; point++)
        {
            Assert.Equal(0.25f, actor.Positions[point * 3], 6);
            Assert.Equal(actor.Positions[point * 3 + 1] + 2 * actor.Positions[point * 3 + 2],
                actor.PointScalars[point], 6);
        }
    }

    [Fact]
    public void Build_TriQuadraticHexahedronMatchesVtk58AmbiguousMarchingCubeCase()
    {
        var scene = SmallFixtureFactory.Create();
        var parent = scene.Manifest.Actors[0];
        var origin = scene.Manifest.WorldOrigin;
        double[][] localPoints =
        [
            [0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0],
            [0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1],
            [0.5, 0, 0], [1, 0.5, 0], [0.5, 1, 0], [0, 0.5, 0],
            [0.5, 0, 1], [1, 0.5, 1], [0.5, 1, 1], [0, 0.5, 1],
            [0, 0, 0.5], [1, 0, 0.5], [1, 1, 0.5], [0, 1, 0.5],
            [0, 0.5, 0.5], [1, 0.5, 0.5], [0.5, 0, 0.5],
            [0.5, 1, 0.5], [0.5, 0.5, 0], [0.5, 0.5, 1], [0.5, 0.5, 0.5],
        ];
        var firstSubcell = new[] { 0, 8, 24, 11, 16, 22, 26, 20 };
        var positiveVertices = new HashSet<int> { 1, 3, 4, 6 }; // VTK case 90.
        var signedX = Enumerable.Repeat(-1d, localPoints.Length).ToArray();
        for (var vertex = 0; vertex < firstSubcell.Length; vertex++)
            if (positiveVertices.Contains(vertex)) signedX[firstSubcell[vertex]] = 1;
        var coordinates = localPoints.SelectMany((point, index) => new[]
        {
            origin[0] + signedX[index], origin[1] + point[1] + index * 0.007,
            origin[2] + point[2] + index * index * 0.0003,
        }).ToArray();
        var scalars = localPoints.Select((point, index) =>
            (float)(point[1] + index * 0.007 + 2 * (point[2] + index * index * 0.0003))).ToArray();
        var topology = new SelectionActorTopology(parent.Name, Enumerable.Range(10, 27).ToArray(), coordinates,
            [new SelectionCellTopology(200, 29, Enumerable.Range(0, 27).ToArray())], scalars);

        var actor = Assert.Single(SectionGeometryBuilder.Build(scene.Manifest, [topology],
            Request([origin[0], origin[1], origin[2]])).Actors);

        Assert.Equal(18, actor.Positions.Length / 3);
        Assert.Equal(14, actor.TriangleConnectivity.Length / 3);
        Assert.Equal(Enumerable.Repeat(200, 14), actor.SourceElementIds);
        for (var point = 0; point < 18; point++)
        {
            Assert.Equal(0f, actor.Positions[point * 3], 6);
            Assert.Equal(actor.Positions[point * 3 + 1] + 2 * actor.Positions[point * 3 + 2],
                actor.PointScalars[point], 6);
        }
    }

    [Fact]
    public void Build_QuadraticHexahedronGeneratesVtk58FaceAndCenterSamples()
    {
        var scene = SmallFixtureFactory.Create();
        var parent = scene.Manifest.Actors[0];
        var origin = scene.Manifest.WorldOrigin;
        double[][] localPoints =
        [
            [0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0],
            [0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1],
            [0.5, 0, 0], [1, 0.5, 0], [0.5, 1, 0], [0, 0.5, 0],
            [0.5, 0, 1], [1, 0.5, 1], [0.5, 1, 1], [0, 0.5, 1],
            [0, 0, 0.5], [1, 0, 0.5], [1, 1, 0.5], [0, 1, 0.5],
        ];
        var coordinates = localPoints.SelectMany(point => new[]
        {
            origin[0] + point[0], origin[1] + point[1], origin[2] + point[2],
        }).ToArray();
        var scalars = localPoints.Select(point => (float)(point[1] + 2 * point[2])).ToArray();
        var topology = new SelectionActorTopology(parent.Name, Enumerable.Range(10, 20).ToArray(), coordinates,
            [new SelectionCellTopology(299, 25, Enumerable.Range(0, 20).ToArray())], scalars);

        var actor = Assert.Single(SectionGeometryBuilder.Build(scene.Manifest, [topology],
            Request([origin[0] + 0.25, origin[1], origin[2]])).Actors);

        Assert.Equal(9, actor.Positions.Length / 3);
        Assert.Equal(8, actor.TriangleConnectivity.Length / 3);
        Assert.Equal(8, actor.LineConnectivity.Length / 2);
        Assert.Equal(Enumerable.Repeat(299, 8), actor.SourceElementIds);
        Assert.Equal([0, 2, 4, 20, 22, 24, 40, 42, 44], Enumerable.Range(0, 9).Select(point =>
            (int)Math.Round(actor.Positions[point * 3 + 1] * 4) * 10 +
            (int)Math.Round(actor.Positions[point * 3 + 2] * 4)).Order());
        for (var point = 0; point < 9; point++)
        {
            Assert.Equal(0.25f, actor.Positions[point * 3], 6);
            Assert.Equal(actor.Positions[point * 3 + 1] + 2 * actor.Positions[point * 3 + 2],
                actor.PointScalars[point], 6);
        }
    }

    [Fact]
    public void Build_QuadraticHexahedraShareGeneratedFaceCutVertices()
    {
        var scene = SmallFixtureFactory.Create();
        var parent = scene.Manifest.Actors[0];
        var origin = scene.Manifest.WorldOrigin;
        var points = new List<double[]>();
        var pointIndices = new Dictionary<(int X, int Y, int Z), int>();
        int[] AddCell(int yOffset)
        {
            double[][] cellPoints =
            [
                [0,0,0], [1,0,0], [1,1,0], [0,1,0], [0,0,1], [1,0,1], [1,1,1], [0,1,1],
                [0.5,0,0], [1,0.5,0], [0.5,1,0], [0,0.5,0],
                [0.5,0,1], [1,0.5,1], [0.5,1,1], [0,0.5,1],
                [0,0,0.5], [1,0,0.5], [1,1,0.5], [0,1,0.5],
            ];
            return cellPoints.Select(point =>
            {
                var shifted = new[] { point[0], point[1] + yOffset, point[2] };
                var key = ((int)Math.Round(shifted[0] * 2), (int)Math.Round(shifted[1] * 2),
                    (int)Math.Round(shifted[2] * 2));
                if (pointIndices.TryGetValue(key, out var existing)) return existing;
                var index = points.Count;
                points.Add(shifted);
                pointIndices.Add(key, index);
                return index;
            }).ToArray();
        }
        var first = AddCell(0);
        var second = AddCell(1);
        var coordinates = points.SelectMany(point => new[]
        {
            origin[0] + point[0], origin[1] + point[1], origin[2] + point[2],
        }).ToArray();
        var scalars = points.Select(point => (float)(point[1] + 2 * point[2])).ToArray();
        var topology = new SelectionActorTopology(parent.Name, Enumerable.Range(10, points.Count).ToArray(), coordinates,
            [new SelectionCellTopology(300, 25, first), new SelectionCellTopology(301, 25, second)], scalars);

        var actor = Assert.Single(SectionGeometryBuilder.Build(scene.Manifest, [topology],
            Request([origin[0] + 0.25, origin[1], origin[2]])).Actors);

        Assert.Equal(15, actor.Positions.Length / 3);
        Assert.Equal(16, actor.TriangleConnectivity.Length / 3);
        Assert.Equal(14, actor.LineConnectivity.Length / 2);
        Assert.Equal(8, actor.SourceElementIds.Count(id => id == 300));
        Assert.Equal(8, actor.SourceElementIds.Count(id => id == 301));
    }

    [Fact]
    public void Build_QuadraticWedgeGeneratesVtk58FaceSamplesAndSubdivision()
    {
        var scene = SmallFixtureFactory.Create();
        var parent = scene.Manifest.Actors[0];
        var origin = scene.Manifest.WorldOrigin;
        double[][] localPoints =
        [
            [0,0,0], [1,0,0], [0,1,0], [0,0,1], [1,0,1], [0,1,1],
            [0.5,0,0], [0.5,0.5,0], [0,0.5,0],
            [0.5,0,1], [0.5,0.5,1], [0,0.5,1],
            [0,0,0.5], [1,0,0.5], [0,1,0.5],
        ];
        var coordinates = localPoints.SelectMany(point => new[]
        {
            origin[0] + point[0], origin[1] + point[1], origin[2] + point[2],
        }).ToArray();
        var scalars = localPoints.Select(point => (float)(point[1] + 2 * point[2])).ToArray();
        var topology = new SelectionActorTopology(parent.Name, Enumerable.Range(10, 15).ToArray(), coordinates,
            [new SelectionCellTopology(400, 26, Enumerable.Range(0, 15).ToArray())], scalars);

        var actor = Assert.Single(SectionGeometryBuilder.Build(scene.Manifest, [topology],
            Request([origin[0] + 0.25, origin[1], origin[2]])).Actors);

        Assert.Equal(12, actor.Positions.Length / 3);
        Assert.Equal(12, actor.TriangleConnectivity.Length / 3);
        Assert.Equal(10, actor.LineConnectivity.Length / 2);
        Assert.Equal(Enumerable.Repeat(400, 12), actor.SourceElementIds);
        Assert.Equal([0, 1, 2, 10, 11, 12, 20, 21, 22, 30, 31, 32],
            Enumerable.Range(0, 12).Select(point =>
                (int)Math.Round(actor.Positions[point * 3 + 1] * 4) * 10 +
                (int)Math.Round(actor.Positions[point * 3 + 2] * 2)).Order());
        for (var point = 0; point < 12; point++)
        {
            Assert.Equal(0.25f, actor.Positions[point * 3], 6);
            Assert.Equal(actor.Positions[point * 3 + 1] + 2 * actor.Positions[point * 3 + 2],
                actor.PointScalars[point], 6);
        }
        for (var triangle = 0; triangle < actor.TriangleConnectivity.Length; triangle += 3)
        {
            var first = actor.TriangleConnectivity[triangle] * 3;
            var second = actor.TriangleConnectivity[triangle + 1] * 3;
            var third = actor.TriangleConnectivity[triangle + 2] * 3;
            var normalX = (actor.Positions[second + 1] - actor.Positions[first + 1]) *
                          (actor.Positions[third + 2] - actor.Positions[first + 2]) -
                          (actor.Positions[second + 2] - actor.Positions[first + 2]) *
                          (actor.Positions[third + 1] - actor.Positions[first + 1]);
            Assert.True(normalX > 0);
        }
    }

    [Fact]
    public void Build_QuadraticWedgeMatchesVtk58GeneratedAmbiguousCase()
    {
        var scene = SmallFixtureFactory.Create();
        var parent = scene.Manifest.Actors[0];
        var origin = scene.Manifest.WorldOrigin;
        double[][] localPoints =
        [
            [0,0,0], [1,0,0], [0,1,0], [0,0,1], [1,0,1], [0,1,1],
            [0.5,0,0], [0.5,0.5,0], [0,0.5,0],
            [0.5,0,1], [0.5,0.5,1], [0,0.5,1],
            [0,0,0.5], [1,0,0.5], [0,1,0.5],
        ];
        double[] signedX = [1, 1, 1, 1, 1, 1, 1, -1, -1, -10, -1, 10, 1, -10, 10];
        var coordinates = localPoints.SelectMany((point, index) => new[]
        {
            origin[0] + signedX[index], origin[1] + point[1] + index * 0.007,
            origin[2] + point[2] + index * index * 0.0003,
        }).ToArray();
        var scalars = localPoints.Select((point, index) =>
            (float)(point[1] + index * 0.007 + 2 * (point[2] + index * index * 0.0003))).ToArray();
        var topology = new SelectionActorTopology(parent.Name, Enumerable.Range(10, 15).ToArray(), coordinates,
            [new SelectionCellTopology(401, 26, Enumerable.Range(0, 15).ToArray())], scalars);

        var actor = Assert.Single(SectionGeometryBuilder.Build(scene.Manifest, [topology],
            Request([origin[0], origin[1], origin[2]])).Actors);

        Assert.Equal(20, actor.Positions.Length / 3);
        Assert.Equal(18, actor.TriangleConnectivity.Length / 3);
        Assert.Equal(Enumerable.Repeat(401, 18), actor.SourceElementIds);
        for (var point = 0; point < 20; point++)
        {
            Assert.Equal(0f, actor.Positions[point * 3], 6);
            Assert.Equal(actor.Positions[point * 3 + 1] + 2 * actor.Positions[point * 3 + 2],
                actor.PointScalars[point], 5);
        }
    }

    [Fact]
    public void Build_QuadraticWedgesShareGeneratedRectangularFaceVertices()
    {
        var scene = SmallFixtureFactory.Create();
        var parent = scene.Manifest.Actors[0];
        var origin = scene.Manifest.WorldOrigin;
        double[][] firstPoints =
        [
            [0,0,0], [1,0,0], [0,1,0], [0,0,1], [1,0,1], [0,1,1],
            [0.5,0,0], [0.5,0.5,0], [0,0.5,0], [0.5,0,1], [0.5,0.5,1], [0,0.5,1],
            [0,0,0.5], [1,0,0.5], [0,1,0.5],
        ];
        double[][] secondPoints =
        [
            [0,0,0], [0,1,0], [-1,0,0], [0,0,1], [0,1,1], [-1,0,1],
            [0,0.5,0], [-0.5,0.5,0], [-0.5,0,0], [0,0.5,1], [-0.5,0.5,1], [-0.5,0,1],
            [0,0,0.5], [0,1,0.5], [-1,0,0.5],
        ];
        var points = new List<double[]>();
        var pointIndices = new Dictionary<(int X, int Y, int Z), int>();
        int[] AddCell(IEnumerable<double[]> cellPoints) => cellPoints.Select(point =>
        {
            var key = ((int)Math.Round(point[0] * 2), (int)Math.Round(point[1] * 2),
                (int)Math.Round(point[2] * 2));
            if (pointIndices.TryGetValue(key, out var existing)) return existing;
            var index = points.Count;
            points.Add(point);
            pointIndices.Add(key, index);
            return index;
        }).ToArray();
        var first = AddCell(firstPoints);
        var second = AddCell(secondPoints);
        var coordinates = points.SelectMany(point => new[]
        {
            origin[0] + point[0], origin[1] + point[1], origin[2] + point[2],
        }).ToArray();
        var scalars = points.Select(point => (float)(point[0] + 2 * point[2])).ToArray();
        var topology = new SelectionActorTopology(parent.Name, Enumerable.Range(10, points.Count).ToArray(), coordinates,
            [new SelectionCellTopology(402, 26, first), new SelectionCellTopology(403, 26, second)], scalars);

        var request = Request([origin[0], origin[1] + 0.25, origin[2]]) with { Normal = [0, 1, 0] };
        var actor = Assert.Single(SectionGeometryBuilder.Build(scene.Manifest, [topology], request).Actors);

        Assert.Equal(21, actor.Positions.Length / 3);
        Assert.Equal(24, actor.TriangleConnectivity.Length / 3);
        Assert.Equal(18, actor.LineConnectivity.Length / 2);
        Assert.Equal(12, actor.SourceElementIds.Count(id => id == 402));
        Assert.Equal(12, actor.SourceElementIds.Count(id => id == 403));
    }

    [Fact]
    public void Build_QuadraticPyramidGeneratesVtk58BaseSampleAndMixedSubdivision()
    {
        var scene = SmallFixtureFactory.Create();
        var parent = scene.Manifest.Actors[0];
        var origin = scene.Manifest.WorldOrigin;
        double[][] localPoints =
        [
            [0,0,0], [1,0,0], [1,1,0], [0,1,0], [0.5,0.5,1],
            [0.5,0,0], [1,0.5,0], [0.5,1,0], [0,0.5,0],
            [0.25,0.25,0.5], [0.75,0.25,0.5], [0.75,0.75,0.5], [0.25,0.75,0.5],
        ];
        var coordinates = localPoints.SelectMany(point => new[]
        {
            origin[0] + point[0], origin[1] + point[1], origin[2] + point[2],
        }).ToArray();
        var scalars = localPoints.Select(point => (float)(point[1] + 2 * point[2])).ToArray();
        var topology = new SelectionActorTopology(parent.Name, Enumerable.Range(10, 13).ToArray(), coordinates,
            [new SelectionCellTopology(500, 27, Enumerable.Range(0, 13).ToArray())], scalars);

        var actor = Assert.Single(SectionGeometryBuilder.Build(scene.Manifest, [topology],
            Request([origin[0] + 0.25, origin[1], origin[2]])).Actors);

        Assert.Equal(5, actor.Positions.Length / 3);
        Assert.Equal(3, actor.TriangleConnectivity.Length / 3);
        Assert.Equal(5, actor.LineConnectivity.Length / 2);
        Assert.Equal(Enumerable.Repeat(500, 3), actor.SourceElementIds);
        Assert.Equal([0, 11, 20, 31, 40], Enumerable.Range(0, 5).Select(point =>
            (int)Math.Round(actor.Positions[point * 3 + 1] * 4) * 10 +
            (int)Math.Round(actor.Positions[point * 3 + 2] * 2)).Order());
        for (var point = 0; point < 5; point++)
        {
            Assert.Equal(0.25f, actor.Positions[point * 3], 6);
            Assert.Equal(actor.Positions[point * 3 + 1] + 2 * actor.Positions[point * 3 + 2],
                actor.PointScalars[point], 6);
        }
        for (var triangle = 0; triangle < actor.TriangleConnectivity.Length; triangle += 3)
        {
            var first = actor.TriangleConnectivity[triangle] * 3;
            var second = actor.TriangleConnectivity[triangle + 1] * 3;
            var third = actor.TriangleConnectivity[triangle + 2] * 3;
            var normalX = (actor.Positions[second + 1] - actor.Positions[first + 1]) *
                          (actor.Positions[third + 2] - actor.Positions[first + 2]) -
                          (actor.Positions[second + 2] - actor.Positions[first + 2]) *
                          (actor.Positions[third + 1] - actor.Positions[first + 1]);
            Assert.True(normalX > 0);
        }
    }

    [Fact]
    public void Build_QuadraticPyramidMatchesVtk58GeneratedAmbiguousCase()
    {
        var scene = SmallFixtureFactory.Create();
        var parent = scene.Manifest.Actors[0];
        var origin = scene.Manifest.WorldOrigin;
        double[][] localPoints =
        [
            [0,0,0], [1,0,0], [1,1,0], [0,1,0], [0.5,0.5,1],
            [0.5,0,0], [1,0.5,0], [0.5,1,0], [0,0.5,0],
            [0.25,0.25,0.5], [0.75,0.25,0.5], [0.75,0.75,0.5], [0.25,0.75,0.5],
        ];
        double[] signedX = [1, 1, 1, 1, 1, -1, 10, 10, -1, 1, 1, 1, 1];
        var coordinates = localPoints.SelectMany((point, index) => new[]
        {
            origin[0] + signedX[index], origin[1] + point[1] + index * 0.007,
            origin[2] + point[2] + index * index * 0.0003,
        }).ToArray();
        var scalars = localPoints.Select((point, index) =>
            (float)(point[1] + index * 0.007 + 2 * (point[2] + index * index * 0.0003))).ToArray();
        var topology = new SelectionActorTopology(parent.Name, Enumerable.Range(10, 13).ToArray(), coordinates,
            [new SelectionCellTopology(501, 27, Enumerable.Range(0, 13).ToArray())], scalars);

        var actor = Assert.Single(SectionGeometryBuilder.Build(scene.Manifest, [topology],
            Request([origin[0], origin[1], origin[2]])).Actors);

        Assert.Equal(10, actor.Positions.Length / 3);
        Assert.Equal(8, actor.TriangleConnectivity.Length / 3);
        Assert.Equal(Enumerable.Repeat(501, 8), actor.SourceElementIds);
        for (var point = 0; point < 10; point++)
        {
            Assert.Equal(0f, actor.Positions[point * 3], 6);
            Assert.Equal(actor.Positions[point * 3 + 1] + 2 * actor.Positions[point * 3 + 2],
                actor.PointScalars[point], 5);
        }
    }

    [Fact]
    public void Build_QuadraticPyramidsShareGeneratedBaseFaceVertices()
    {
        var scene = SmallFixtureFactory.Create();
        var parent = scene.Manifest.Actors[0];
        var origin = scene.Manifest.WorldOrigin;
        double[][] firstPoints =
        [
            [0,0,0], [1,0,0], [1,1,0], [0,1,0], [0.5,0.5,1],
            [0.5,0,0], [1,0.5,0], [0.5,1,0], [0,0.5,0],
            [0.25,0.25,0.5], [0.75,0.25,0.5], [0.75,0.75,0.5], [0.25,0.75,0.5],
        ];
        double[][] secondPoints =
        [
            [0,0,0], [1,0,0], [1,1,0], [0,1,0], [0.5,0.5,-1],
            [0.5,0,0], [1,0.5,0], [0.5,1,0], [0,0.5,0],
            [0.25,0.25,-0.5], [0.75,0.25,-0.5], [0.75,0.75,-0.5], [0.25,0.75,-0.5],
        ];
        var points = new List<double[]>();
        var pointIndices = new Dictionary<(int X, int Y, int Z), int>();
        int[] AddCell(IEnumerable<double[]> cellPoints) => cellPoints.Select(point =>
        {
            var key = ((int)Math.Round(point[0] * 4), (int)Math.Round(point[1] * 4),
                (int)Math.Round(point[2] * 4));
            if (pointIndices.TryGetValue(key, out var existing)) return existing;
            var index = points.Count;
            points.Add(point);
            pointIndices.Add(key, index);
            return index;
        }).ToArray();
        var first = AddCell(firstPoints);
        var second = AddCell(secondPoints);
        var coordinates = points.SelectMany(point => new[]
        {
            origin[0] + point[0], origin[1] + point[1], origin[2] + point[2],
        }).ToArray();
        var scalars = points.Select(point => (float)(point[1] + 2 * point[2])).ToArray();
        var topology = new SelectionActorTopology(parent.Name, Enumerable.Range(10, points.Count).ToArray(), coordinates,
            [new SelectionCellTopology(502, 27, first), new SelectionCellTopology(503, 27, second)], scalars);

        var actor = Assert.Single(SectionGeometryBuilder.Build(scene.Manifest, [topology],
            Request([origin[0] + 0.25, origin[1], origin[2]])).Actors);

        Assert.Equal(7, actor.Positions.Length / 3);
        Assert.Equal(6, actor.TriangleConnectivity.Length / 3);
        Assert.Equal(8, actor.LineConnectivity.Length / 2);
        Assert.Equal(3, actor.SourceElementIds.Count(id => id == 502));
        Assert.Equal(3, actor.SourceElementIds.Count(id => id == 503));
    }

    [Fact]
    public void Build_AdjacentTetrahedraEmitCoincidentSharedFaceOnce()
    {
        var scene = SmallFixtureFactory.Create();
        var parent = scene.Manifest.Actors[0];
        var origin = scene.Manifest.WorldOrigin;
        double[][] points =
        [
            [0,0,0], [1,0,0], [0,1,0], [0,0,1], [0,0,-1],
        ];
        var coordinates = points.SelectMany(point => new[]
        {
            origin[0] + point[0], origin[1] + point[1], origin[2] + point[2],
        }).ToArray();
        var topology = new SelectionActorTopology(parent.Name, Enumerable.Range(10, points.Length).ToArray(), coordinates,
            [new SelectionCellTopology(600, 10, [0,1,2,3]),
             new SelectionCellTopology(601, 10, [0,2,1,4])]);
        var request = Request([origin[0], origin[1], origin[2]]) with { Normal = [0,0,1] };

        var actor = Assert.Single(SectionGeometryBuilder.Build(scene.Manifest, [topology], request).Actors);

        Assert.Equal(3, actor.Positions.Length / 3);
        Assert.Equal(1, actor.TriangleConnectivity.Length / 3);
        Assert.Equal([601], actor.SourceElementIds);
    }

    [Fact]
    public void Build_AdjacentHexahedraUseVtkMarchingDiagonalThroughSharedNodes()
    {
        var scene = SmallFixtureFactory.Create();
        var parent = scene.Manifest.Actors[0];
        var origin = scene.Manifest.WorldOrigin;
        double[][] points =
        [
            [0,0,0], [0,1,0], [0,1,1], [0,0,1],
            [1,0,0], [1,1,0], [1,1,1], [1,0,1],
            [2,0,0], [2,1,0], [2,1,1], [2,0,1],
        ];
        var coordinates = points.SelectMany(point => new[]
        {
            origin[0] + point[0], origin[1] + point[1], origin[2] + point[2],
        }).ToArray();
        var topology = new SelectionActorTopology(parent.Name, Enumerable.Range(10, points.Length).ToArray(), coordinates,
            [new SelectionCellTopology(610, 12, [0,4,5,1,3,7,6,2]),
             new SelectionCellTopology(611, 12, [4,8,9,5,7,11,10,6])]);
        var request = Request([origin[0], origin[1], origin[2]]) with { Normal = [0,1,-1] };

        var actor = Assert.Single(SectionGeometryBuilder.Build(scene.Manifest, [topology], request).Actors);

        Assert.Equal(6, actor.Positions.Length / 3);
        Assert.Equal(4, actor.TriangleConnectivity.Length / 3);
        Assert.Equal(2, actor.SourceElementIds.Count(id => id == 610));
        Assert.Equal(2, actor.SourceElementIds.Count(id => id == 611));
        Assert.Contains(Enumerable.Range(0, 6), point =>
            actor.Positions[point * 3] == 1 && actor.Positions[point * 3 + 1] == 0 &&
            actor.Positions[point * 3 + 2] == 0);
    }

    [Fact]
    public void Build_GeometricallyMergesCoincidentInterfacePointsWithDistinctSourceIds()
    {
        var scene = SmallFixtureFactory.Create();
        var parent = scene.Manifest.Actors[0];
        var origin = scene.Manifest.WorldOrigin;
        double[][] first =
        [
            [0,0,0], [1,0,0], [1,1,0], [0,1,0],
            [0,0,1], [1,0,1], [1,1,1], [0,1,1],
        ];
        double[][] second =
        [
            [1,0,0], [2,0,0], [2,1,0], [1,1,0],
            [1,0,1], [2,0,1], [2,1,1], [1,1,1],
        ];
        var points = first.Concat(second).ToArray();
        var coordinates = points.SelectMany(point => new[]
        {
            origin[0] + point[0], origin[1] + point[1], origin[2] + point[2],
        }).ToArray();
        var topology = new SelectionActorTopology(parent.Name, Enumerable.Range(10, points.Length).ToArray(), coordinates,
            [new SelectionCellTopology(620, 12, Enumerable.Range(0, 8).ToArray()),
             new SelectionCellTopology(621, 12, Enumerable.Range(8, 8).ToArray())]);
        var request = Request([origin[0], origin[1] + 0.5, origin[2]]) with { Normal = [0,1,0] };

        var actor = Assert.Single(SectionGeometryBuilder.Build(scene.Manifest, [topology], request).Actors);

        Assert.Equal(6, actor.Positions.Length / 3);
        Assert.Equal(4, actor.TriangleConnectivity.Length / 3);
        Assert.Equal(6, Enumerable.Range(0, 6).Select(point => (
            actor.Positions[point * 3], actor.Positions[point * 3 + 1], actor.Positions[point * 3 + 2])).Distinct().Count());
    }

    [Fact]
    public void Build_ProducesCutLineForShellCell()
    {
        var scene = SmallFixtureFactory.Create();
        var parent = scene.Manifest.Actors[0];
        var topology = new SelectionActorTopology(parent.Name, [10, 11, 12],
            [0, 0, 0, 1, 0, 0, 0, 1, 0],
            [new SelectionCellTopology(99, 5, [0, 1, 2])]);

        var actor = Assert.Single(SectionGeometryBuilder.Build(scene.Manifest, [topology], Request([0.25, 0, 0])).Actors);

        Assert.Empty(actor.TriangleConnectivity);
        Assert.Equal(1, actor.LineConnectivity.Length / 2);
    }

    [Fact]
    public void Build_RejectsDuplicateOffsetActors()
    {
        var scene = SmallFixtureFactory.Create();
        var request = Request([0, 0, 0]) with
        {
            ActorOffsets = [new SectionActorOffset("part", [0, 0, 0]), new SectionActorOffset("part", [1, 0, 0])],
        };

        var exception = Assert.Throws<SectionGeometryException>(() =>
            SectionGeometryBuilder.Build(scene.Manifest, [], request));

        Assert.Equal("section-invalid", exception.Code);
    }

    [Fact]
    public void Build_DoesNotGenerateCapsForHiddenParent()
    {
        var scene = SmallFixtureFactory.Create();
        var parent = scene.Manifest.Actors[0];
        var hiddenManifest = scene.Manifest with
        {
            Actors = scene.Manifest.Actors.Select(actor => actor.ActorId == parent.ActorId
                ? actor with { Visible = false }
                : actor).ToArray(),
        };
        var topology = new SelectionActorTopology(parent.Name, [10, 11, 12, 13],
            [0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1],
            [new SelectionCellTopology(99, 10, [0, 1, 2, 3])]);

        var result = SectionGeometryBuilder.Build(hiddenManifest, [topology], Request([0.25, 0, 0]));

        Assert.Empty(result.Actors);
        Assert.Equal(0, result.SkippedCells);
    }

    private static SectionResolveRequest Request(IReadOnlyList<double> point) =>
        new(point, [1, 0, 0], true, [0, 0, 0, 0]);
}

using System.Text.Json;
using PrePoMax.Render.SyntheticData;

namespace PrePoMax.Render.Server.Tests;

public sealed class FrustumParityCorpusTests
{
    private static readonly SelectionSpatialIndex.Plane[] UnitBoxPlanes =
    [
        new(1, 0, 0, 1), new(-1, 0, 0, 1),
        new(0, 1, 0, 1), new(0, -1, 0, 1),
        new(0, 0, 1, 1), new(0, 0, -1, 1),
    ];

    public static TheoryData<FrustumParityCase> Cases()
    {
        var path = Environment.GetEnvironmentVariable("VTKJS_FRUSTUM_CORPUS_PATH");
        if (string.IsNullOrWhiteSpace(path))
            path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "frustum-parity-corpus.json");
        var cases = JsonSerializer.Deserialize<FrustumParityCase[]>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var data = new TheoryData<FrustumParityCase>();
        foreach (var item in cases) data.Add(item);
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ServerSelector_MatchesCapturedActiViz58Result(FrustumParityCase item)
    {
        var coordinates = item.Points.SelectMany(point => point).ToArray();
        var localPoints = Enumerable.Range(0, item.Points.Length).ToArray();
        var bounds = new[]
        {
            item.Points.Min(point => point[0]), item.Points.Max(point => point[0]),
            item.Points.Min(point => point[1]), item.Points.Max(point => point[1]),
            item.Points.Min(point => point[2]), item.Points.Max(point => point[2]),
        };
        var overallIntersects = SelectionSpatialIndex.IntersectsBoundsAsVoxel(UnitBoxPlanes, bounds);
        var selectedPoints = overallIntersects
            ? localPoints.Where(point => SelectionSpatialIndex.ContainsPoint(UnitBoxPlanes, coordinates, point)).ToArray()
            : [];
        var cell = new SelectionCellTopology(501, item.VtkCellType, localPoints);

        Assert.Equal(item.LegacyPointIndices, selectedPoints);
        Assert.Equal(item.LegacyCellIntersects, overallIntersects &&
            SelectionSpatialIndex.IntersectsCell(UnitBoxPlanes, coordinates, cell));
    }

    public sealed record FrustumParityCase(
        string Name,
        int VtkCellType,
        double[][] Points,
        int[] LegacyPointIndices,
        bool LegacyCellIntersects)
    {
        public override string ToString() => Name;
    }
}

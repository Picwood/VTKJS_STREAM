using System.Globalization;
using System.Text.Json;
using PrePoMax.Render.Server;
using PrePoMax.Render.SyntheticData;

if (args.Length != 2)
    throw new ArgumentException("Usage: PrePoMax.Render.SectionOracle <corpus.json> <output.json>");

var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var cases = JsonSerializer.Deserialize<OracleCase[]>(File.ReadAllText(args[0]), options)
    ?? throw new InvalidDataException("The section corpus is empty.");
var results = cases.Select(Build).ToArray();
File.WriteAllText(args[1], JsonSerializer.Serialize(results,
    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

static OracleResult Build(OracleCase item)
{
    if (item.Coordinates.Length == 0 || item.Coordinates.Any(point => point.Length != 3) ||
        item.PlanePoint.Length != 3 || item.PlaneNormal.Length != 3)
        throw new InvalidDataException($"Corpus case '{item.Name}' is malformed.");

    var cells = item.Cells is { Length: > 0 }
        ? item.Cells
        : [new OracleCell(1, item.VtkCellType, Enumerable.Range(0, item.Coordinates.Length).ToArray())];
    if (cells.Any(cell => cell.LocalPointIndices.Length == 0 ||
                          cell.LocalPointIndices.Any(point => point < 0 || point >= item.Coordinates.Length)))
        throw new InvalidDataException($"Corpus case '{item.Name}' contains invalid cell connectivity.");

    var fixture = SmallFixtureFactory.Create();
    var parent = fixture.Manifest.Actors[0];
    var origin = fixture.Manifest.WorldOrigin;
    var coordinates = item.Coordinates.SelectMany(point => new[]
    {
        origin[0] + point[0], origin[1] + point[1], origin[2] + point[2],
    }).ToArray();
    var scalars = item.Coordinates.Select(point => (float)(point[0] + 2 * point[1] + 3 * point[2])).ToArray();
    var topology = new SelectionActorTopology(parent.Name,
        Enumerable.Range(1, item.Coordinates.Length).ToArray(), coordinates,
        cells.Select(cell => new SelectionCellTopology(
            cell.SourceElementId, cell.VtkCellType, cell.LocalPointIndices)).ToArray(),
        scalars);
    var request = new SectionResolveRequest(
        [origin[0] + item.PlanePoint[0], origin[1] + item.PlanePoint[1], origin[2] + item.PlanePoint[2]],
        item.PlaneNormal, false, [0, 0, 0, 0]);
    var actor = SectionGeometryBuilder.Build(fixture.Manifest, [topology], request).Actors.SingleOrDefault();
    if (actor is null) return new OracleResult(item.Name, 0, [], []);

    var points = new string[actor.Positions.Length / 3];
    for (var point = 0; point < points.Length; point++)
        points[point] = string.Join(',',
            QuantizePosition(actor.Positions[point * 3]),
            QuantizePosition(actor.Positions[point * 3 + 1]),
            QuantizePosition(actor.Positions[point * 3 + 2]),
            QuantizeScalar(actor.PointScalars[point]));
    Array.Sort(points, StringComparer.Ordinal);

    var triangles = new string[actor.TriangleConnectivity.Length / 3];
    for (var triangle = 0; triangle < triangles.Length; triangle++)
    {
        var vertices = new string[3];
        for (var vertex = 0; vertex < 3; vertex++)
        {
            var point = checked((int)actor.TriangleConnectivity[triangle * 3 + vertex]);
            vertices[vertex] = string.Join(',',
                QuantizePosition(actor.Positions[point * 3]),
                QuantizePosition(actor.Positions[point * 3 + 1]),
                QuantizePosition(actor.Positions[point * 3 + 2]),
                QuantizeScalar(actor.PointScalars[point]));
        }
        Array.Sort(vertices, StringComparer.Ordinal);
        triangles[triangle] = string.Join('|', vertices);
    }
    Array.Sort(triangles, StringComparer.Ordinal);
    return new OracleResult(item.Name, actor.Positions.Length / 3, points, triangles);
}

static long QuantizePosition(double value) => checked((long)Math.Round(value * 100_000));
static long QuantizeScalar(double value) => checked((long)Math.Round(value * 10_000));

internal sealed record OracleCase(
    string Name,
    int VtkCellType,
    double[][] Coordinates,
    double[] PlanePoint,
    double[] PlaneNormal,
    OracleCell[]? Cells = null);

internal sealed record OracleCell(int SourceElementId, int VtkCellType, int[] LocalPointIndices);

internal sealed record OracleResult(string Name, int PointCount, string[] Points, string[] Triangles);

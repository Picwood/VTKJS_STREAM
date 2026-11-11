using CaeGlobals;
using CaeResults;

internal static class MeshInterpolationChecks
{
    public static void Run(Action<bool, string> check)
    {
        var source = new PartExchangeData();
        source.Nodes.Ids = new[] { 0, 1, 2 };
        source.Nodes.Coor = new[] { new[] { 0d, 0, 0 }, new[] { 1d, 0, 0 }, new[] { 0d, 1, 0 } };
        source.Nodes.Values = new[] { 0f, 2, 4 };
        source.Cells.Ids = new[] { 0 };
        source.Cells.Types = new[] { 5 };
        source.Cells.CellNodeIds = new[] { new[] { 0, 1, 2 } };
        var interpolator = new ResultsInterpolator(source);
        interpolator.InterpolateAt(new[] { .25, .25, 0 }, InterpolatorEnum.ClosestPoint, out var distance, out var value);
        check(Math.Abs(value - 1.5) < 1e-12 && distance.All(x => Math.Abs(x) < 1e-12),
              "surface interpolation reproduces affine triangle field");
        interpolator.InterpolateAt(new[] { .25, .25, 2 }, InterpolatorEnum.ClosestPoint, out distance, out value);
        check(Math.Abs(value - 1.5) < 1e-12 && Math.Abs(Math.Sqrt(distance.Sum(x => x * x)) - 2) < 1e-12,
              "surface projection preserves field and normal distance");
        interpolator.InterpolateAt(new[] { .9, .05, 0 }, InterpolatorEnum.ClosestNode, out _, out value);
        check(value == 2, "surface closest-node interpolation");
        var converted = interpolator.ConvertSourceToTriangularFaces(source);
        converted.Nodes.Values[0] = 99;
        check(source.Nodes.Values[0] == 0, "surface conversion owns copied values");
    }
}

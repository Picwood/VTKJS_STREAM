using CaeResults;

internal static class CloudChecks
{
    public static void Run(Action<bool, string> check)
    {
        var singlePoint = new CloudPoint { Coor = new[] { 0d, 0, 0 }, Values = new[] { 3d, -2 } };
        var single = new CloudInterpolator(new[] { singlePoint });
        check(single.NumOfValues == 2, "single-point cloud component count");
        foreach (var method in new[] { CloudInterpolatorEnum.ClosestPoint, CloudInterpolatorEnum.Gauss, CloudInterpolatorEnum.Shepard })
        {
            single.InterpolateAt(new double[3], method, 1, out _, out var value);
            check(value.SequenceEqual(singlePoint.Values), "single-point cloud interpolates all components");
        }
        foreach (var invalid in new[]
        {
            Array.Empty<CloudPoint>(), null,
            new CloudPoint[] { null },
            new[] { new CloudPoint { Coor = new[] { 0d, 0 }, Values = new[] { 1d } } },
            new[] { new CloudPoint { Coor = new double[3], Values = null } },
            new[] { new CloudPoint { Coor = new double[3], Values = Array.Empty<double>() } },
            new[] { singlePoint, new CloudPoint { Coor = new[] { 1d, 0, 0 }, Values = new[] { 4d } } }
        })
        {
            bool rejected = false;
            try { new CloudInterpolator(invalid); }
            catch (ArgumentException) { rejected = true; }
            catch (CaeGlobals.CaeException) { rejected = true; }
            check(rejected, "invalid cloud rejected before spatial indexing");
        }
        foreach (double invalidCoordinate in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            for (int axis = 0; axis < 3; axis++)
            {
                foreach (bool first in new[] { true, false })
                {
                    var coordinates = new double[3];
                    coordinates[axis] = invalidCoordinate;
                    var invalidPoint = new CloudPoint { Coor = coordinates, Values = new[] { 3d, -2 } };
                    bool rejected = false;
                    try { new CloudInterpolator(first ? new[] { invalidPoint, singlePoint } : new[] { singlePoint, invalidPoint }); }
                    catch (ArgumentException) { rejected = true; }
                    check(rejected, "cloud rejects nonfinite source coordinate before indexing");
                }
            }
        }
        foreach (double radius in new[] { 0d, -1, double.NaN, double.PositiveInfinity })
        {
            foreach (var method in new[] { CloudInterpolatorEnum.Gauss, CloudInterpolatorEnum.Shepard })
            {
                bool rejected = false;
                try { single.InterpolateAt(new double[3], method, radius, out _, out _); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                check(rejected, "weighted cloud query rejects invalid radius");
            }
        }
        foreach (var query in new[] { null, new double[2], new[] { double.NaN, 0, 0 }, new[] { 0d, double.PositiveInfinity, 0 } })
        {
            bool rejected = false;
            try { single.InterpolateByClosestPoint(query, out _, out _); }
            catch (ArgumentException) { rejected = true; }
            check(rejected, "cloud query rejects invalid coordinates");
        }
        var radiusCloud = new CloudInterpolator(new[]
        {
            new CloudPoint { Coor = new[] { .1, 0, 0 }, Values = new[] { 7d } },
            new CloudPoint { Coor = new[] { .8, .8, 0 }, Values = new[] { 100d } }
        });
        foreach (var method in new[] { CloudInterpolatorEnum.Gauss, CloudInterpolatorEnum.Shepard })
        {
            radiusCloud.InterpolateAt(new double[3], method, 1, out _, out var bounded);
            check(Math.Abs(bounded[0] - 7) < 1e-12, "cloud radius excludes diagonal points outside sphere");
        }
        var points = new[]
        {
            new CloudPoint { Coor = new[] { 0d, 0, 0 }, Values = new[] { 0d, 4 } },
            new CloudPoint { Coor = new[] { 1d, 0, 0 }, Values = new[] { 10d, 4 } }
        };
        var cloud = new CloudInterpolator(points);
        cloud.InterpolateAt(new[] { .25, 0, 0 }, CloudInterpolatorEnum.ClosestPoint, 2, out _, out var nearest);
        check(nearest.SequenceEqual(new[] { 0d, 4 }), "cloud nearest-point values");
        nearest[0] = 99;
        check(points[0].Values[0] == 0, "cloud dispatch returns owned values");
        cloud.InterpolateAt(new double[3], CloudInterpolatorEnum.Shepard, 2, out _, out var exact);
        check(exact.SequenceEqual(points[0].Values), "Shepard reproduces exact source point");
        cloud.InterpolateAt(new[] { .25, 0, 0 }, CloudInterpolatorEnum.Shepard, 2, out _, out var shepard);
        check(Math.Abs(shepard[0] - 1) < 1e-12 && Math.Abs(shepard[1] - 4) < 1e-12,
              "Shepard inverse-square interpolation");
        cloud.InterpolateAt(new[] { .25, 0, 0 }, CloudInterpolatorEnum.Gauss, 2, out _, out var gauss);
        double nearWeight = Math.Exp(-Math.Pow(.25 / 2, 2));
        double farWeight = Math.Exp(-Math.Pow(.75 / 2, 2));
        check(Math.Abs(gauss[0] - 10 * farWeight / (nearWeight + farWeight)) < 1e-12,
              "Gaussian interpolation decreases weight with distance");
        check(Math.Abs(gauss[1] - 4) < 1e-12, "Gaussian preserves constant component");
    }
}

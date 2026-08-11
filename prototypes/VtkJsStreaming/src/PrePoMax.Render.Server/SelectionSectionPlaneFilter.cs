using PrePoMax.Render.Contracts;

namespace PrePoMax.Render.Server;

internal readonly record struct SelectionSectionHalfSpace(double X, double Y, double Z, double Offset)
{
    public double Evaluate(double x, double y, double z) => X * x + Y * y + Z * z + Offset;
    public bool Contains(double x, double y, double z) => Evaluate(x, y, z) >= 0;
}

internal static class SelectionSectionPlaneFilter
{
    public static bool TryCreate(SelectionSectionPlane? plane, out SelectionSectionHalfSpace halfSpace)
    {
        halfSpace = default;
        if (plane is null) return true;
        if (plane.Point is not { Count: 3 } || plane.Normal is not { Count: 3 } ||
            plane.Point.Any(value => !double.IsFinite(value)) ||
            plane.Normal.Any(value => !double.IsFinite(value))) return false;

        var length = Math.Sqrt(plane.Normal.Sum(value => value * value));
        if (!double.IsFinite(length) || length <= 1e-12) return false;
        var x = plane.Normal[0] / length;
        var y = plane.Normal[1] / length;
        var z = plane.Normal[2] / length;
        halfSpace = new SelectionSectionHalfSpace(x, y, z,
            -(x * plane.Point[0] + y * plane.Point[1] + z * plane.Point[2]));
        return double.IsFinite(halfSpace.Offset);
    }
}

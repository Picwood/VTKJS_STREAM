using CaeResults;

internal static class ResultDefinitionChecks
{
    public static void Run(Action<bool, string> check)
    {
        var linear = new LinearPattern("copies", new[] { 1d, 2, 3 }, new[] { 4d, 6, 3 }, 0);
        check(linear.Displacement.SequenceEqual(new[] { 3d, 4, 0 }) && linear.DisplacementLength == 5 &&
              linear.NumberOfItems == 2, "linear result pattern geometry and minimum copies");
        var circular = new CircularPattern("rotation", new[] { 1d, 2, 3 }, new[] { 1d, 2, 8 }, 90, 4);
        check(circular.AxisNormal.SequenceEqual(new[] { 0d, 0, 1 }) && circular.AxisNormalLength == 5,
              "circular result pattern normalized axis");
        circular.AxisSecondPoint = null;
        check(circular.AxisNormal.SequenceEqual(new double[3]) && circular.AxisNormalLength == 0,
              "incomplete circular axis remains unavailable");
        var limit = new ResultFieldOutputLimit("limit", "STRESS", "MISES");
        var envelope = new ResultFieldOutputEnvelope("envelope", "STRESS", "MISES");
        var equation = new ResultFieldOutputEquation("derived", "[STRESS.MISES] / 2");
        equation.SetParentNames(new[] { "STRESS" });
        ResultFieldOutput[] definitions = { limit, envelope, equation };
        check(definitions.All(d => d.GetParentNames().SequenceEqual(new[] { "STRESS" })),
              "derived result definitions retain dependency names");
        check(limit.GetComponentNames().SequenceEqual(new[] { FOComponentNames.Ratio, FOComponentNames.SafetyFactor }) &&
              envelope.GetComponentNames().SequenceEqual(new[] { FOComponentNames.Max, FOComponentNames.Min, FOComponentNames.Average }) &&
              equation.GetComponentNames().SequenceEqual(new[] { "VALUE" }), "derived output component contracts");
        limit.Filter1.Type = FieldResultFilterTypeEnum.Smooth;
        limit.Filter1.NumberOfIterations = 0;
        check(limit.Filter1.NumberOfIterations == 1 && limit.Filter2.Type == FieldResultFilterTypeEnum.None &&
              envelope.Filter1.Type == FieldResultFilterTypeEnum.None, "derived output filters are independent and bounded");
    }
}

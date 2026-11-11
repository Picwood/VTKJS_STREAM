using CaeModel;

internal static class StepCollectionChecks
{
    public static void Run(Action<bool, string> check)
    {
        var steps = new StepCollection();
        var first = new StaticStep("first") { TimePeriod = .5 };
        var second = new StaticStep("second") { TimePeriod = 2 };
        steps.AddStep(first);
        steps.AddStep(second);
        check(steps.GetStepNames().SequenceEqual(new[] { "first", "second" }), "collection preserves step order");
        check(steps.GetNextStepNames("first").SequenceEqual(new[] { "second" }), "collection resolves following steps");
        var durations = steps.GetStepIdDuration();
        check(durations[1] == .5 && durations[2] == 2, "collection assigns solver IDs and durations");
        steps.SetCheckModel();
        check(steps.OnlyCheckModel && !first.RunAnalysis && !second.RunAnalysis, "collection propagates check-model mode");
        steps.SetRunAnalysis();
        check(!steps.OnlyCheckModel && first.RunAnalysis && second.RunAnalysis, "collection restores analysis mode");
        var replacement = new StaticStep("replacement");
        steps.ReplaceStep("first", replacement);
        check(ReferenceEquals(steps.GetStep("replacement"), replacement) && steps.GetStepNames()[1] == "second",
            "replacement preserves collection ordering");
        check(ReferenceEquals(steps.RemoveStep("second"), second) && steps.StepsList.Count == 1,
            "removal returns the removed step");
        bool rejected = false;
        try { steps.AddStep(null); } catch (ArgumentNullException) { rejected = true; }
        check(rejected && steps.StepsList.Count == 1, "null step rejected without changing collection");
    }
}

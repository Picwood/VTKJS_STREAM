using CaeResults;

internal static class FieldChecks
{
    public static void Run(Action<bool, string> check)
    {
        var partial = new FieldComponent("U1", new[] { float.NaN, 3f, -2f, float.NaN });
        check(partial.Min.Value == -2 && partial.Min.Id == 2 && partial.Max.Value == 3 && partial.Max.Id == 1,
              "extrema ignore missing leading and trailing values");
        foreach (var values in new[] { new[] { float.NaN, float.NaN }, Array.Empty<float>(), null })
        {
            var unavailable = new FieldComponent("U1", values);
            check(float.IsNaN(unavailable.Min.Value) && float.IsNaN(unavailable.Max.Value) &&
                  unavailable.Min.Id == -1 && unavailable.Max.Id == -1, "unavailable extrema have no index");
        }
        partial.Values = new[] { 4f, float.NaN, 4f };
        partial.UpdateMaxMin();
        check(partial.Min.Id == 0 && partial.Max.Id == 0 && partial.Min.Value == 4 && partial.Max.Value == 4,
              "extrema ties retain first valid index");
        partial.Values = new[] { float.NaN };
        partial.UpdateMaxMin();
        check(partial.Min.Id == -1 && partial.Max.Id == -1 && float.IsNaN(partial.Min.Value),
              "extrema reset when field becomes unavailable");
        var reset = new Field("DISP");
        reset.AddComponent("U1", new[] { -3f, 7f });
        reset.SetComponentValuesToZero();
        check(reset.GetComponentValues("U1").All(v => v == 0) &&
              reset.GetComponentMin("U1") == 0 && reset.GetComponentMax("U1") == 0,
              "zeroing field updates cached extrema");
        reset.SetComponentValuesTo(5);
        check(reset.GetComponentMin("U1") == 5 && reset.GetComponentMax("U1") == 5 &&
              reset.GetComponentMinId("U1") == 0 && reset.GetComponentMaxId("U1") == 0,
              "constant field resets extrema and indices");
        reset.SetComponentValuesTo(float.NaN);
        check(float.IsNaN(reset.GetComponentMin("U1")) && float.IsNaN(reset.GetComponentMax("U1")) &&
              reset.GetComponentMinId("U1") == -1 && reset.GetComponentMaxId("U1") == -1,
              "setting missing values invalidates cached extrema");
        reset.SetComponentValuesToZero();
        check(reset.GetComponentMin("U1") == 0 && reset.GetComponentMax("U1") == 0 &&
              reset.GetComponentMinId("U1") == 0 && reset.GetComponentMaxId("U1") == 0,
              "zeroing missing field restores valid extrema");
        var vector = new Field("DISP");
        vector.AddComponent("U1", new[] { 3f, 0 }); vector.AddComponent("U2", new[] { 4f, 0 }); vector.AddComponent("U3", new[] { 0f, 2 });
        vector.ComputeInvariants();
        check(vector.GetComponentValues("ALL").SequenceEqual(new[] { 5f, 2 }), "vector magnitudes");
        check(vector.GetComponentMax("ALL") == 5 && vector.GetComponentMin("ALL") == 2, "vector magnitude extrema");
        var copy = new Field(vector);
        copy.GetComponentValues("U1")[0] = 99;
        check(vector.GetComponentValues("U1")[0] == 3, "field copy owns components");
        foreach (var components in new[] { new[] { 10f, 0, 0, 0, 0, 0 }, new[] { 5f, 5, 5, 0, 0, 0 }, new[] { 0f, 0, 0, 2, 0, 0 } })
        {
            var stress = new Field("STRESS");
            var names = new[] { "S11", "S22", "S33", "S12", "S23", "S13" };
            for (int i = 0; i < 6; i++) stress.AddComponent(names[i], new[] { components[i] });
            stress.ComputeInvariants();
            double expected = components[0] == 10 ? 10 : components[0] == 5 ? 0 : Math.Sqrt(12);
            check(Math.Abs(stress.GetComponentValues("MISES")[0] - expected) < 1e-5, "analytic stress Mises invariant");
            double tresca = components[0] == 10 ? 10 : components[0] == 5 ? 0 : 4;
            check(Math.Abs(stress.GetComponentValues("TRESCA")[0] - tresca) < 1e-4, "analytic stress Tresca invariant");
            stress.ComputeInvariants();
            check(Math.Abs(stress.GetComponentValues("MISES")[0] - expected) < 1e-5, "repeat invariant computation");
        }
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) Field.WriteToBinaryWriter(vector, writer);
        stream.Position = 0;
        Field restored;
        using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true)) restored = Field.ReadFromBinaryReader(reader, 1_004_000);
        check(restored.GetComponentValues("ALL").SequenceEqual(new[] { 5f, 2 }) && restored.IsComponentInvariant("ALL"), "field binary round trip");
        check(stream.Position == stream.Length, "field binary payload consumed");
        var data = new FieldData("DISP") { Time = .5f, StepId = 2, StepIncrementId = 3, Component = "U1" };
        var dataCopy = new FieldData(data);
        check(dataCopy.Time == .5f && dataCopy.StepId == 2 && dataCopy.StepIncrementId == 3 && dataCopy.Component == "U1", "field step metadata copy");
    }
}

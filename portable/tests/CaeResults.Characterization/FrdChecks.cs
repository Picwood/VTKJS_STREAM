using CaeResults;

internal static class FrdChecks
{
    public static void Cube(string file)
    {
        var frd = FrdFileReader.ReadData(file);
        if (frd == null || frd.Mesh.Nodes.Count != 8 || frd.Mesh.Elements.Count != 1)
            throw new Exception("FRD cube mesh mismatch.");
        if (!frd.Mesh.Elements[1].NodeIds.SequenceEqual(Enumerable.Range(1, 8)))
            throw new Exception("FRD cube connectivity mismatch.");
        foreach (var spec in new[] { (Name: "STRESS", Prefix: "S", Axial: 1.0),
                                     (Name: "TOSTRAIN", Prefix: "E", Axial: .001) })
        {
            var entry = frd.Fields.Single(x => x.Key.Name == spec.Name);
            if (entry.Key.Time != 1 || entry.Key.StepId != 1)
                throw new Exception($"FRD {spec.Name} metadata mismatch.");
            foreach (string suffix in new[] { "11", "22", "33", "12", "23", "13" })
                CheckValues(entry.Value, spec.Prefix + suffix, suffix == "11" ? spec.Axial : 0, spec.Axial * 1e-4);
            if (spec.Name == "STRESS")
            {
                entry.Value.ComputeInvariants();
                foreach (string component in new[] { "MISES", "TRESCA", "PRINCIPAL_MAX" })
                    CheckValues(entry.Value, component, 1, 1e-4);
                foreach (string component in new[] { "PRINCIPAL_MID", "PRINCIPAL_MIN" })
                    CheckValues(entry.Value, component, 0, 1e-5);
            }
        }
        var displacement = frd.Fields.Single(x => x.Key.Name == "DISP");
        displacement.Value.ComputeInvariants();
        foreach (var node in frd.Mesh.Nodes.Values)
        {
            int index = frd.NodeIdsLookUp[node.Id];
            double expected = node.X == 1 ? .001 : 0;
            foreach (string component in new[] { "U1", "ALL", "U2", "U3" })
            {
                double value = displacement.Value.GetComponentValues(component)[index];
                double target = component == "U1" || component == "ALL" ? expected : 0;
                if (!double.IsFinite(value) || Math.Abs(value - target) > 1e-8)
                    throw new Exception("FRD cube displacement mismatch.");
            }
        }
        if (displacement.Key.Time != 1 || displacement.Key.StepId != 1)
            throw new Exception("FRD step metadata mismatch.");
    }
    public static void RepeatedBeamImports(string directory, Action<bool, string> check)
    {
        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "fixtures", "cube.frd"));
        int elementStart = Array.FindIndex(lines, line => line.StartsWith("    3C"));
        var beamLines = lines.Take(elementStart).Concat(new[]
        {
            "    3C                             2                                     1",
            " -1         1 11 0 1",
            " -2         2         1",
            " -1         2 11 0 1",
            " -2         1         2",
            " -3", " 9999"
        });
        string diagnostic = Path.Combine(directory, "ResultsForLastIterations.frd");
        string ordinary = Path.Combine(directory, "beams.frd");
        File.WriteAllLines(diagnostic, beamLines);
        File.Copy(diagnostic, ordinary, true);
        var first = FrdFileReader.ReadData(diagnostic);
        check(first.Mesh.Elements.Count == 1, "diagnostic beam duplicates removed");
        var normal = FrdFileReader.ReadData(ordinary);
        check(normal.Mesh.Elements.Count == 2, "diagnostic deduplication does not leak into ordinary import");
        check(first.Mesh.Elements[1].NodeIds.SequenceEqual(new[] { 2, 1 }), "diagnostic beam orientation preserved");
        check(normal.Mesh.Elements[1].NodeIds.SequenceEqual(new[] { 2, 1 }), "ordinary beam orientation preserved");
        var second = FrdFileReader.ReadData(diagnostic);
        check(second.Mesh.Elements.Count == 1, "repeated diagnostic import owns its deduplication state");
        check(first.Mesh.Elements[1].NodeIds.SequenceEqual(second.Mesh.Elements[1].NodeIds), "repeated diagnostic connectivity stable");
    }

    public static void PartialFields(string directory, Action<bool, string> check)
    {
        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "fixtures", "cube.frd"));
        int header = Array.FindIndex(lines, line => line.StartsWith(" -4  DISP"));
        int first = Array.FindIndex(lines, header, line => line.StartsWith(" -1"));
        // Remove one displacement record; keep the other field datasets intact.
        var partial = lines.Take(first + 1).Concat(lines.Skip(first + 2)).ToArray();
        int countPosition = 33;
        if (lines[header - 1].Substring(countPosition, 3).Trim() != "8")
            throw new Exception("Unexpected FRD fixture count layout.");
        partial[header - 1] = partial[header - 1].Remove(countPosition, 3).Insert(countPosition, "  7");
        string path = Path.Combine(directory, "partial.frd");
        File.WriteAllLines(path, partial);
        var data = FrdFileReader.ReadData(path);
        var field = data.Fields.Single(x => x.Key.Name == "DISP").Value;
        int missing = data.NodeIdsLookUp[2];
        check(new[] { "U1", "U2", "U3" }.All(c => float.IsNaN(field.GetComponentValues(c)[missing])),
              "missing FRD node values remain unknown");
        check(field.GetComponentValues("U1")[data.NodeIdsLookUp[1]] == 0, "explicit FRD zero preserved");
        check(Math.Abs(field.GetComponentValues("U1")[data.NodeIdsLookUp[3]] - .001) < 1e-8,
              "partial FRD values retain node mapping");
        field.ComputeInvariants();
        check(float.IsNaN(field.GetComponentValues("ALL")[missing]), "missing vector propagates to magnitude");
    }

    public static void TwoStep(string file)
    {
        var data = FrdFileReader.ReadData(file);
        foreach (string name in new[] { "DISP", "STRESS", "TOSTRAIN" })
        {
            var fields = data.Fields.Where(x => x.Key.Name == name).OrderBy(x => x.Key.StepId).ToArray();
            if (fields.Length != 2) throw new Exception($"Expected two {name} steps.");
            for (int i = 0; i < 2; i++)
            {
                var entry = fields[i];
                if (entry.Key.StepId != i + 1 || entry.Key.StepIncrementId != 1 || entry.Key.Time != i + 1)
                    throw new Exception($"Two-step {name} metadata mismatch.");
                if (name == "DISP")
                {
                    foreach (var node in data.Mesh.Nodes.Values)
                    {
                        double actual = entry.Value.GetComponentValues("U1")[data.NodeIdsLookUp[node.Id]];
                        double expected = node.X == 1 ? .001 * (i + 1) : 0;
                        if (!double.IsFinite(actual) || Math.Abs(actual - expected) > 1e-8)
                            throw new Exception("Two-step displacement mismatch.");
                    }
                }
                else CheckValues(entry.Value, name == "STRESS" ? "S11" : "E11",
                                 (name == "STRESS" ? 1 : .001) * (i + 1),
                                 (name == "STRESS" ? 1 : .001) * (i + 1) * 1e-4);
            }
            if (ReferenceEquals(fields[0].Value, fields[1].Value)) throw new Exception("Steps share field storage.");
        }
    }

    public static void Continuations(string directory, Action<bool, string> check)
    {
        var source = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "fixtures", "cube.frd"));
        foreach (bool allNodes in new[] { true, false })
        {
            var lines = new List<string>();
            bool stress = false;
            int node = 0;
            foreach (string line in source)
            {
                if (line.StartsWith(" -4")) stress = line.StartsWith(" -4  STRESS");
                if (stress && line.StartsWith(" -1") && (allNodes || node++ == 0))
                {
                    lines.Add(line.Substring(0, 49));
                    lines.Add(" -2          " + line.Substring(49));
                }
                else lines.Add(line);
            }
            string file = Path.Combine(directory, allNodes ? "continued.frd" : "mixed-continuation.frd");
            File.WriteAllLines(file, lines);
            Cube(file);
            check(true, allNodes ? "uniform FRD continuation records" : "variable FRD continuation records");
        }
    }

    public static void TruncatedContinuations(string directory, Action<bool, string> check)
    {
        var source = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "fixtures", "cube.frd"));
        int header = Array.FindIndex(source, line => line.StartsWith(" -4  STRESS"));
        int first = Array.FindIndex(source, header, line => line.StartsWith(" -1"));
        foreach (string tail in new[] { " -2", " -2           1.00" })
        {
            var lines = source.Take(first).Concat(new[] { source[first].Substring(0, 49), tail })
                              .Concat(source.Skip(first + 1));
            string file = Path.Combine(directory, "truncated-continuation.frd");
            File.WriteAllLines(file, lines);
            var data = FrdFileReader.ReadData(file);
            var field = data.Fields.Single(x => x.Key.Name == "STRESS").Value;
            int missing = data.NodeIdsLookUp[1];
            check(new[] { "S12", "S23", "S13" }.All(c => float.IsNaN(field.GetComponentValues(c)[missing])),
                  "truncated continuation leaves missing components unknown");
            check(Math.Abs(field.GetComponentValues("S11")[missing] - 1) < 1e-4 &&
                  Math.Abs(field.GetComponentValues("S11")[data.NodeIdsLookUp[2]] - 1) < 1e-4,
                  "truncated continuation preserves preceding values and next node");
        }
    }

    private static void CheckValues(Field field, string component, double expected, double tolerance)
    {
        var values = field.GetComponentValues(component);
        if (values == null || values.Length != 8 ||
            values.Any(value => !float.IsFinite(value) || Math.Abs(value - expected) > tolerance))
            throw new Exception($"FRD {field.Name}/{component}: expected {expected} at all eight nodes.");
    }

}

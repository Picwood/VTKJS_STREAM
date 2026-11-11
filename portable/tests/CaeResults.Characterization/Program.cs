using CaeResults;
using System.Globalization;
if (args.Length == 2 && args[0] == "--check-two-step")
{
    foreach (string culture in new[] { "en-US", "it-IT" })
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        FrdChecks.TwoStep(args[1]);
        DerivedHistoryChecks.Run(args[1], (ok, message) => { if (!ok) throw new Exception(message); });
        AggregateChecks.Run(args[1], (ok, message) => { if (!ok) throw new Exception(message); });
        Console.WriteLine($"PASS: native two-step FRD fields and times ({culture}).");
    }
    return;
}
if (args.Length == 2 && args[0] == "--check-cube")
{
    foreach (string culture in new[] { "en-US", "it-IT" })
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        var history = DatFileReader.Read(args[1], out var diagnostics);
        if (history == null || diagnostics is { Length: > 0 }) throw new Exception("Solver DAT parse failed.");
        var field = history.Sets.Values.SelectMany(s => s.Fields.Values).Single(f => f.Name == "DISPLACEMENTS");
        var axial = field.Components["U1"].Entries;
        if (!axial.Keys.ToHashSet().SetEquals(new[] { "2", "3", "6", "7" }) ||
            axial.Values.Any(e => e.Values.Count != 1 || !double.IsFinite(e.Values[0]) || Math.Abs(e.Values[0] - .001) > 1e-8 || e.Time.Single() != 1))
            throw new Exception("Solver displacement history mismatch.");
        foreach (string component in new[] { "U2", "U3" })
            if (field.Components[component].Entries.Values.Any(e => e.Values.Count != 1 || !double.IsFinite(e.Values[0]) || Math.Abs(e.Values[0]) > 1e-10))
                throw new Exception("Solver transverse displacement mismatch.");
        FrdChecks.Cube(Path.ChangeExtension(args[1], ".frd"));
        HistoryExportChecks.NativeCube(args[1]);
        Console.WriteLine($"PASS: production FRD reader validates cube mesh, displacement, stress and strain ({culture}).");
        Console.WriteLine($"PASS: production DAT reader validates cube solve ({culture}).");
    }
    return;
}
int checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
string directory = Path.Combine(Path.GetTempPath(), "prepomax results é " + Guid.NewGuid());
Directory.CreateDirectory(directory);
var original = CultureInfo.CurrentCulture;
try
{
    foreach (string culture in new[] { "en-US", "it-IT" })
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        FieldChecks.Run(Check);
        DerivedHistoryChecks.Run(Path.Combine(AppContext.BaseDirectory, "fixtures", "cube-two-step.frd"), Check);
        HistoryExportChecks.Run(directory, Check);
        AggregatePersistenceChecks.Run(directory, Check);
        AggregateChecks.Run(Path.Combine(AppContext.BaseDirectory, "fixtures", "cube-two-step.frd"), Check);
        MeshInterpolationChecks.Run(Check);
        CloudReaderChecks.Run(directory, Check);
        CloudChecks.Run(Check);
        ResultDefinitionChecks.Run(Check);
        HistoryStreamChecks.Run(directory, Check);
        FrdChecks.TruncatedContinuations(directory, Check);
        FrdChecks.Continuations(directory, Check);
        FrdChecks.TwoStep(Path.Combine(AppContext.BaseDirectory, "fixtures", "cube-two-step.frd"));
        Check(true, "two-step FRD values and metadata");
        FrdChecks.PartialFields(directory, Check);
        FrdChecks.RepeatedBeamImports(directory, Check);
        FrdChecks.Cube(Path.Combine(AppContext.BaseDirectory, "fixtures", "cube.frd"));
        Check(true, "FRD analytic cube mesh, fields and metadata");
        string reordered = Path.Combine(directory, "reordered.frd");
        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "fixtures", "cube.frd"));
        // Reorder only node records; field records keep their original order.
        int start = Array.FindIndex(lines, line => line.StartsWith("    2C")) + 1;
        (lines[start], lines[start + 1]) = (lines[start + 1], lines[start]);
        File.WriteAllLines(reordered, lines);
        FrdChecks.Cube(reordered);
        Check(true, "FRD fields follow node labels after node-record reordering");
        Check(FrdFileReader.ReadData(Path.Combine(directory, "missing.frd")) == null, "FRD missing-file contract");
        string file = Path.Combine(directory, "cube.dat");
        File.WriteAllText(file, "\n displacements (vx,vy,vz) for set RIGHT and time  0.5000000E+00\n\n 2 1.000000E-03 0.000000E+00 -2.500000-04\n 3 1.000000E-03 0.000000E+00 -2.500000-04\n\n");
        var results = DatFileReader.Read(file, out var errors);
        Check(results != null && (errors == null || errors.Length == 0), "DAT parses without errors");
        var fields = results.Sets.Values.SelectMany(s => s.Fields.Values).ToArray();
        var displacement = fields.Single(f => f.Name == "DISPLACEMENTS");
        var entries = displacement.Components["U1"].Entries.Values.ToArray();
        Check(entries.Length == 2, "DAT node history entries");
        Check(entries.All(e => e.Values.Count == 1 && Math.Abs(e.Values[0] - .001) < 1e-12), "DAT displacement values");
        Check(entries.All(e => e.Time.Single() == .5), "DAT time values");
        Check(displacement.Components["U3"].Entries.Values.All(e => e.Values.Single() == -.00025), "DAT signed exponent values");
        using var memory = new MemoryStream();
        using (var writer = new BinaryWriter(memory, System.Text.Encoding.UTF8, true)) HistoryResults.WriteToBinaryWriter(results, writer);
        memory.Position = 0;
        HistoryResults restored;
        using (var reader = new BinaryReader(memory, System.Text.Encoding.UTF8, true)) restored = HistoryResults.ReadFromBinaryReader(reader, -1);
        var restoredEntries = restored.Sets.Values.SelectMany(x => x.Fields.Values).Single(x => x.Name == "DISPLACEMENTS").Components["U1"].Entries.Values;
        Check(restoredEntries.All(e => e.Time.Single() == .5 && e.Values.Single() == .001), "history binary round trip");
        Check(memory.Position == memory.Length, "history binary payload consumed");
        Check(DatFileReader.Read(Path.Combine(directory, "missing.dat"), out _) == null, "DAT missing-file contract");
    }
    Console.WriteLine($"PASS: {checks} result history characterization checks.");
}
finally { CultureInfo.CurrentCulture = original; Directory.Delete(directory, true); }

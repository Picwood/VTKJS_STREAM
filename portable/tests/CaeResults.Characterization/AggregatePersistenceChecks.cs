using CaeResults;
using System.IO.Compression;

internal static class AggregatePersistenceChecks
{
    private sealed class ChunkedFile : FileStream
    {
        public ChunkedFile(string path) : base(path, FileMode.Open, FileAccess.Read) { }
        public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, 3));
    }
    private sealed class FailingFile : FileStream
    {
        private readonly long _limit;
        public FailingFile(string path, long limit) : base(path, FileMode.Create, FileAccess.Write) { _limit = limit; }
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (Position + count > _limit) throw new IOException("Injected result-save failure.");
            base.Write(buffer, offset, count);
        }
    }
    public static void Run(string directory, Action<bool, string> check)
    {
        var source = FrdFileReader.Read(Path.Combine(AppContext.BaseDirectory, "fixtures", "cube-two-step.frd"));
        string dat = Path.Combine(directory, "persisted-history.dat");
        File.WriteAllText(dat, "\n displacements (vx,vy,vz) for set RIGHT and time  0.5000000E+00\n\n 2 1.000000E-03 0.000000E+00 -2.500000E-04\n\n");
        source.SetHistory(DatFileReader.Read(dat, out _));
        source.SetMeshDeformation(10, 2, 1);
        string path = Path.Combine(directory, "aggregate.bin");
        using (var file = File.Create(path)) FeResults.WriteToFileStream(source, file, CompressionLevel.Optimal);
        check(Math.Abs(source.Mesh.Nodes[2].X - 1.02) < 1e-7, "aggregate save preserves displayed deformation");
        var displacement = source.GetField(source.GetFieldData("DISP", "ALL", 1, 1));
        var expectedMagnitude = displacement.GetComponentValues("ALL").ToArray();
        byte[] bytes = File.ReadAllBytes(path);
        // Fail as the first compressed field payload is written, after its header.
        long fieldStart = 20L + BitConverter.ToInt32(bytes, 4);
        bool failed = false;
        try
        {
            using var file = new FailingFile(Path.Combine(directory, "failed-save.bin"), fieldStart);
            FeResults.WriteToFileStream(source, file, CompressionLevel.Optimal);
        }
        catch (IOException) { failed = true; }
        check(failed, "aggregate save propagates injected I/O failure");
        check(displacement.ContainsComponent("ALL") && displacement.GetComponentValues("ALL").SequenceEqual(expectedMagnitude),
              "failed aggregate save restores field invariants");
        check(Math.Abs(source.Mesh.Nodes[2].X - 1.02) < 1e-7, "failed save preserves displayed geometry");
        // Keep valid compression and framing, but replace the first field's
        // decompressed contents with an incomplete integer marker.
        int firstFieldLengthOffset = 16 + BitConverter.ToInt32(bytes, 4);
        int oldLength = BitConverter.ToInt32(bytes, firstFieldLengthOffset);
        byte[] invalidPayload;
        using (var input = new MemoryStream(new byte[] { 1 }))
            invalidPayload = CaeGlobals.Tools.Compress(input, CompressionLevel.Optimal);
        string corruptPath = Path.Combine(directory, "corrupt-field.bin");
        using (var output = new BinaryWriter(File.Create(corruptPath)))
        {
            output.Write(bytes, 0, firstFieldLengthOffset);
            output.Write(invalidPayload.Length);
            output.Write(invalidPayload);
            int suffix = firstFieldLengthOffset + 4 + oldLength;
            output.Write(bytes, suffix, bytes.Length - suffix);
        }
        var corruptTarget = new FeResults("corrupt", source.UnitSystem);
        corruptTarget.Mesh = new CaeMesh.FeMesh(source.Mesh, source.Mesh.Parts.Keys.ToArray());
        bool corruptRejected = false;
        try
        {
            using var file = File.OpenRead(corruptPath);
            FeResults.ReadFromFileStream(corruptTarget, file, 2_004_004);
        }
        catch (InvalidDataException ex)
        {
            corruptRejected = ex.InnerException is EndOfStreamException && ex.Message.Contains("field 1");
        }
        check(corruptRejected, "corrupt binary field fails explicitly with record context");
        // The binary payload supplements mesh metadata, which the application stores separately.
        var restored = new FeResults("restored", source.UnitSystem);
        restored.Mesh = new CaeMesh.FeMesh(source.Mesh, source.Mesh.Parts.Keys.ToArray());
        using (var file = new ChunkedFile(path))
        {
            FeResults.ReadFromFileStream(restored, file, 2_004_004);
            check(file.Position == file.Length, "aggregate payload consumed through short reads");
        }
        check(restored.Mesh.Nodes[2].X == 1 && restored.GetAllStepIds().SequenceEqual(new[] { 1, 2 }),
              "aggregate payload restores undeformed geometry and steps");
        var field = restored.GetField(restored.GetFieldData("STRESS", "MISES", 2, 1));
        check(field.GetComponentValues("MISES").All(x => float.IsFinite(x) && Math.Abs(x - 2) < .0002),
              "aggregate payload rebuilds stress invariants");
        var historyField = restored.GetHistory().Sets.Values.Single().Fields.Values.Single();
        var axial = historyField.Components["U1"].Entries.Values.Single();
        check(axial.Time.SequenceEqual(new[] { .5 }) && axial.Values.SequenceEqual(new[] { .001 }),
              "aggregate payload restores history samples");
        check(historyField.Components["U3"].Entries.Values.Single().Values.Single() == -.00025,
              "aggregate payload preserves signed history values");
        restored.SetMeshDeformation(10, 1, 1);
        check(Math.Abs(restored.Mesh.Nodes[2].X - 1.01) < 1e-7, "restored aggregate deforms from persisted field values");
    }
}

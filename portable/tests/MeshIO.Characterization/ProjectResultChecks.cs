using System.Buffers.Binary;
using System.IO.Compression;
using CaeResults;
using PrePoMax.Application;
using PrePoMax.Persistence;
using PrePoMax.Render.Server;

internal static class ProjectResultChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "result contours " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var model = new StaticModelWorkflow(2).Model;
            byte[] frd = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures/archive-cube.frd"));
            string project = Path.Combine(root, "model.pma");
            StaticProjectArchive.Save(project, model);
            check(ProjectResults.List(project).Length == 0, "unsolved project has no fields");
            StaticProjectArchive.Save(project, model, frd);
            byte[] original = File.ReadAllBytes(project);
            var fields = ProjectResults.List(project).Where(f => f.Name == "DISP" && f.Component == "U1").ToArray();
            check(fields.Length == 2 && fields.Select(f => f.Step).Distinct().Count() == 2, "FRD field catalog retains both steps");
            var frdFingerprint = ProjectResults.Fingerprint(project, fields[0]);
            check(frdFingerprint.ValueCount == 8 && frdFingerprint.Minimum == 0 && frdFingerprint.MinimumNodeId == 1 &&
                  Math.Abs(frdFingerprint.Maximum - .001) < 1e-8 && frdFingerprint.MaximumNodeId == 2 &&
                  Math.Abs(frdFingerprint.Mean - .0005) < 1e-8 && frdFingerprint.Sha256.Length == 64 &&
                  frdFingerprint.Sha256 == "AD20016DF0BCD1AA7098F1F50A23F867EF8959443315EC4710304D937150419D" &&
                  frdFingerprint.Samples.Length == 5, "result fingerprint reports deterministic field statistics and samples");
            var repeatedFingerprint = ProjectResults.Fingerprint(project, fields[0]);
            ResultSessionChecks.Run(check, project, fields);
            check(repeatedFingerprint.Sha256 == frdFingerprint.Sha256 &&
                  repeatedFingerprint.Samples.SequenceEqual(frdFingerprint.Samples),
                  "result fingerprint is stable across independent reads");
            foreach (var field in fields)
            {
                string scenePath = Path.Combine(root, "step" + field.Step + ".bin");
                ProjectScene.Export(project, scenePath, field);
                using var stream = File.OpenRead(scenePath);
                var scene = HostSceneUploadReader.ReadAsync(stream, stream.Length, HostSceneUploadReader.DefaultMaximumBytes, default).GetAwaiter().GetResult();
                int[] Ints(string semantic)
                {
                    var resource = scene.Resources.Values.Single(r => r.Descriptor.Semantic == semantic);
                    byte[] bytes = new byte[(int)resource.Descriptor.ByteLength];
                    resource.Read(0, bytes);
                    return Enumerable.Range(0, bytes.Length / 4).Select(i => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(i * 4, 4))).ToArray();
                }
                var ids = Ints("sourcePointIds");
                var values = Ints("pointScalars").Select(BitConverter.Int32BitsToSingle).ToArray();
                check(ids.Select((id, i) => Math.Abs(values[i] - (new[] { 2, 3, 6, 7 }.Contains(id) ? field.Step * .001 : 0)) < 1e-8).All(v => v),
                    "result scene maps analytical displacement by source node ID");
            }
            foreach (var field in fields)
            {
                foreach (double scale in new[] { 100d, -100d, 0d })
                {
                    string output = Path.Combine(root, $"deformed-{field.Step}-{scale}.bin");
                    ProjectScene.Export(project, output, field, scale);
                    using var reader = new BinaryReader(File.OpenRead(output));
                    reader.ReadBytes(8); reader.ReadInt32();
                    reader.ReadBytes(reader.ReadInt32()); reader.ReadBytes(32);
                    int count = reader.ReadInt32(); reader.ReadInt32();
                    bool valid = count == 8;
                    for (int i = 0; i < count; i++)
                    {
                        int id = reader.ReadInt32();
                        double x = reader.ReadDouble(), y = reader.ReadDouble(), z = reader.ReadDouble();
                        float scalar = reader.ReadSingle();
                        var node = model.Mesh.Nodes[id];
                        double u = new[] { 2, 3, 6, 7 }.Contains(id) ? field.Step * .001 : 0;
                        valid &= Math.Abs(x - node.X - scale * u) < 1e-7 && y == node.Y && z == node.Z && Math.Abs(scalar - u) < 1e-8;
                    }
                    check(valid, "scaled displacement preserves scalar values and frame-specific coordinates");
                    if (scale == 0) check(File.ReadAllBytes(output).SequenceEqual(File.ReadAllBytes(Path.Combine(root, "step" + field.Step + ".bin"))),
                        "zero displacement restores exact reference scene bytes");
                }
            }
            foreach (double scale in new[] { double.NaN, 1000001d })
            {
                bool badScale = false;
                try { ProjectScene.Export(project, Path.Combine(root, "invalid-scale.bin"), fields[0], scale); }
                catch (ArgumentOutOfRangeException) { badScale = true; }
                check(badScale && !File.Exists(Path.Combine(root, "invalid-scale.bin")), "invalid displacement scale rejected without output");
            }
            bool rejected = false;
            try { ProjectScene.Export(project, Path.Combine(root, "unknown.bin"), fields[0] with { Component = "unknown" }); }
            catch (InvalidDataException) { rejected = true; }
            check(rejected && !File.Exists(Path.Combine(root, "unknown.bin")), "unknown field fails before export");
            check(File.ReadAllBytes(project).SequenceEqual(original), "contour export preserves archive");
            string frdPath = Path.Combine(root, "portable-source.frd");
            File.WriteAllBytes(frdPath, frd);
            var binarySource = FrdFileReader.Read(frdPath) ?? throw new InvalidDataException("Fixture FRD failed to load.");
            var collection = new ResultsCollection();
            collection.Add("Fixture results", binarySource);
            string tailPath = Path.Combine(root, "portable-results.bin");
            using (var stream = new FileStream(tailPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                ResultsCollection.WriteToFileStream(collection, stream, CompressionLevel.Optimal);
            byte[] binaryPayload = ResultPayload.Create(2_005_001, ["Fixture results"], File.ReadAllBytes(tailPath));
            using (var envelope = new MemoryStream(binaryPayload, writable: false))
            {
                var header = ResultPayload.ReadHeader(envelope, envelope.Length);
                check(header.BinaryVersion == 2_005_001 && header.ResultNames.SequenceEqual(new[] { "Fixture results" }) &&
                      header.BinaryLength == envelope.Length - envelope.Position,
                      "result envelope header streams without materializing its binary data");
            }
            StaticProjectArchive.Save(project, model, binaryResults: binaryPayload);
            var reopenedBinary = StaticProjectArchive.Open(project);
            check(reopenedBinary.Frd == null && reopenedBinary.BinaryResults!.SequenceEqual(binaryPayload),
                "archive v4 preserves checksummed binary results");
            var binaryFields = ProjectResults.List(project).Where(f => f.Name == "DISP" && f.Component == "U1").ToArray();
            check(binaryFields.SequenceEqual(fields), "binary result payload restores the FRD field catalog");
            ResultSessionChecks.Run(check, project, binaryFields);
            IndexedResultChecks.Run(check, project);
            var binaryFingerprint = ProjectResults.Fingerprint(project, binaryFields[0]);
            check(binaryFingerprint.Sha256 == frdFingerprint.Sha256 && binaryFingerprint.ValueCount == frdFingerprint.ValueCount &&
                  binaryFingerprint.Minimum == frdFingerprint.Minimum && binaryFingerprint.Maximum == frdFingerprint.Maximum &&
                  binaryFingerprint.Mean == frdFingerprint.Mean && binaryFingerprint.Samples.SequenceEqual(frdFingerprint.Samples),
                  "FRD and binary payloads produce the same result fingerprint");
            string binaryScene = Path.Combine(root, "binary-results-scene.bin");
            ProjectScene.Export(project, binaryScene, binaryFields[0], 10);
            check(File.Exists(binaryScene) && new FileInfo(binaryScene).Length > 0,
                "binary result payload drives contour and deformation scene export");
            bool malformedPayload = false;
            byte[] corrupt = binaryPayload.ToArray(); corrupt[0] ^= 0xff;
            try { ResultPayload.Read(corrupt); } catch (InvalidDataException) { malformedPayload = true; }
            check(malformedPayload, "binary result envelope rejects a corrupt signature");
            var moved = model.Mesh.Nodes[1]; moved.X = .1; model.Mesh.Nodes[1] = moved;
            StaticProjectArchive.Save(project, model, frd);
            rejected = false;
            try { ProjectResults.List(project); } catch (InvalidDataException) { rejected = true; }
            check(rejected, "result display rejects mismatched mesh coordinates");
        }
        finally { Directory.Delete(root, true); }
    }
}

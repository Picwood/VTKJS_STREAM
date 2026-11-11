using System.IO.Compression;
using CaeResults;
using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class IndexedResultChecks
{
    public static void Run(Action<bool, string> check, string project)
    {
        string root = Directory.CreateTempSubdirectory("indexed-result-checks-").FullName;
        string[] Temps() => Directory.GetDirectories(Path.GetTempPath(), "prepomax-result-index-*").Order().ToArray();
        var before = Temps();
        byte[] original = File.ReadAllBytes(project);
        var saved = StaticProjectArchive.Open(project);
        var envelope = ResultPayload.Read(saved.BinaryResults!);
        byte[] raw = envelope.BinaryData.ToArray();
        void Save(byte[] binary, int version = 2_005_001) => StaticProjectArchive.Save(project, saved.Model,
            binaryResults: ResultPayload.Create(version, envelope.ResultNames, binary));
        try
        {
            var catalog = ProjectResults.List(project);
            using var indexed = new ProjectResultSession();
            check(indexed.List(project).SequenceEqual(catalog) && indexed.LastAccess is
                  { Indexed: true, Retained: true, CacheHit: false, DecodedFieldRecords: 0, IndexMetadataBytes: > 0 },
                  "binary catalog indexes records without materializing nodal fields");
            int records = indexed.LastAccess!.IndexedFieldRecords;
            check(records == catalog.Select(f => (f.Name, f.Step, f.Increment)).Distinct().Count() && records > 2,
                  "index counts field records independently of raw and derived components");
            check(Temps().Length == before.Length + 1, "retained index owns one private disk payload");
            var returned = indexed.List(project); returned[0] = returned[0] with { Component = "mutated" };
            check(indexed.List(project).SequenceEqual(catalog) && indexed.LastAccess is { CacheHit: true, DecodedFieldRecords: 0 },
                  "warm indexed catalog returns independent metadata without field decoding");
            foreach (var field in catalog)
            {
                var expected = ProjectResults.Fingerprint(project, field);
                var actual = indexed.Fingerprint(project, field);
                check(expected == actual with { Samples = expected.Samples } && expected.Samples.SequenceEqual(actual.Samples),
                      "indexed raw/derived component fingerprint, units, extrema and samples equal eager reader");
                check(indexed.LastAccess is { CacheHit: true, Indexed: true, DecodedFieldRecords: 1 },
                      "selected component decodes only its field record and frame");
            }
            foreach (var field in catalog.Where(f => f.Component is "MISES" or "U1"))
            foreach (double scale in new[] { 0d, 100d, -100d })
            {
                string baseline = Path.Combine(root, "eager.bin"), selection = Path.Combine(root, "indexed.bin");
                ProjectScene.Export(project, baseline, field, scale);
                ProjectScene.Export(project, selection, field, scale, resultSession: indexed);
                check(File.ReadAllBytes(baseline).SequenceEqual(File.ReadAllBytes(selection)),
                      "indexed scene equals eager scene at zero, positive and negative displacement");
                check(indexed.LastAccess!.DecodedFieldRecords == (scale != 0 && field.Name != "DISP" ? 2 : 1),
                      "deformation decodes only the selected field and matching displacement record");
                File.Delete(baseline); File.Delete(selection);
            }
            string copy = Path.Combine(root, "same-content.pma"); File.WriteAllBytes(copy, original);
            indexed.List(copy);
            check(indexed.LastAccess!.CacheHit && indexed.LoadCount == 1, "index identity follows content across paths");
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                bool rejected = false;
                try { indexed.List(project, cancel.Token); } catch (OperationCanceledException) { rejected = true; }
                check(rejected && indexed.LoadCount == 1, "cancelled index access starts no loading");
            }
            indexed.Clear();
            check(before.SequenceEqual(Temps()) && indexed.RetainedEntries == 0, "clear removes the retained disk index");
            using (var tiny = new ProjectResultSession(1))
            {
                check(tiny.List(project).SequenceEqual(catalog) && tiny.LastAccess is { Indexed: true, Retained: false },
                      "index metadata admission budget is enforced");
                check(before.SequenceEqual(Temps()), "non-admitted catalog removes its disk payload");
                check(tiny.Fingerprint(project, catalog[0]).Sha256 == ProjectResults.Fingerprint(project, catalog[0]).Sha256 &&
                      before.SequenceEqual(Temps()), "non-admitted selection completes before removing its disk payload");
            }
            // Metadata admission is independent of the larger transient mesh/index-build allocations.
            using (var bounded = new ProjectResultSession(64 * 1024))
            {
                bounded.List(project);
                check(bounded.LastAccess is { Indexed: true, Retained: true } && bounded.LastAccess.LoadAllocatedBytes > 64 * 1024,
                      "index can be retained without admitting transient mesh loading allocations");
            }
            // A legal ZIP comment increases source size without changing any result or manifest.
            byte[] padded = new byte[original.Length + ushort.MaxValue]; original.CopyTo(padded, 0);
            BitConverter.GetBytes(ushort.MaxValue).CopyTo(padded, original.Length - 2);
            File.WriteAllBytes(copy, padded);
            using (var bounded = new ProjectResultSession(32 * 1024))
                check(new FileInfo(copy).Length > 32 * 1024 && bounded.List(copy).SequenceEqual(catalog) &&
                      bounded.LastAccess is { Indexed: true, Retained: true },
                      "archive size above the budget does not prevent bounded index retention");
            using (var disabled = new ProjectResultSession(0))
            {
                check(disabled.List(project).SequenceEqual(catalog) && disabled.LastAccess is { Indexed: false, Retained: false },
                      "zero budget preserves eager uncached access");
            }
            Save(raw, version: 2_005_002);
            check(indexed.List(project).SequenceEqual(ProjectResults.List(project)) && !indexed.LastAccess!.Indexed && before.SequenceEqual(Temps()),
                  "unindexed binary versions use the production eager reader and clean index attempts");
            File.WriteAllBytes(project, original);
            indexed.List(project);
            StaticProjectArchive.Save(project, saved.Model);
            check(indexed.List(project).Length == 0 && !indexed.LastAccess!.Indexed && before.SequenceEqual(Temps()),
                  "switching to an unsolved project evicts the previous disk payload");

            // Locate blocks independently in the writer's fixture. Save recomputes archive checksums,
            // so these cases exercise the binary index/production validators, not just ZIP integrity.
            using var input = new MemoryStream(raw);
            using var reader = new BinaryReader(input);
            reader.ReadInt32(); int nameBytes = reader.ReadInt32(); input.Position += nameBytes;
            reader.ReadInt32(); int meshOffset = (int)input.Position;
            int meshLength = reader.ReadInt32(); input.Position += meshLength;
            reader.ReadInt32(); int count = reader.ReadInt32();
            var offsets = new List<int>();
            for (int i = 0; i < count; i++) { offsets.Add((int)input.Position); int length = reader.ReadInt32(); input.Position += length; }
            int historyOffset = (int)input.Position;
            foreach (var (offset, label) in new[] { (meshOffset, "mesh"), (offsets[^1], "unselected field"), (historyOffset, "history") })
            {
                File.WriteAllBytes(project, original); indexed.List(project);
                byte[] corrupt = raw.ToArray(); BitConverter.GetBytes(int.MaxValue).CopyTo(corrupt, offset);
                Save(corrupt);
                bool rejected = false;
                try { indexed.List(project); } catch (InvalidDataException) { rejected = true; }
                check(rejected && indexed.RetainedEntries == 0 && before.SequenceEqual(Temps()),
                      "index rejects invalid " + label + " after valid archive hashes and removes failed/previous payloads");
            }
            // Truncated unselected field after successful decompression must fail during catalog opening.
            byte[] ReplaceRecord(int offset, byte[] plain)
            {
                using var compressed = new MemoryStream();
                using (var deflate = new DeflateStream(compressed, CompressionLevel.Optimal, true)) deflate.Write(plain);
                int oldLength = BitConverter.ToInt32(raw, offset);
                return raw[..offset].Concat(BitConverter.GetBytes((int)compressed.Length)).Concat(compressed.ToArray())
                    .Concat(raw[(offset + 4 + oldLength)..]).ToArray();
            }
            Save(ReplaceRecord(offsets[^1], [1, 0, 0, 0]));
            bool truncated = false;
            try { indexed.List(project); } catch (IOException) { truncated = true; }
            check(truncated && before.SequenceEqual(Temps()), "catalog rejects truncated metadata in an unselected field");
            Save(ReplaceRecord(meshOffset, [1, 0, 0, 0]));
            truncated = false;
            try { indexed.List(project); } catch (IOException) { truncated = true; }
            check(truncated && before.SequenceEqual(Temps()), "catalog uses the production reader to reject malformed inflated mesh data");

            // A well-formed non-static record is deliberately delegated to the production reader.
            int first = offsets[0];
            using var block = new MemoryStream(raw, first + 4, BitConverter.ToInt32(raw, first));
            using var inflater = new DeflateStream(block, CompressionMode.Decompress);
            using var fieldReader = new BinaryReader(inflater);
            var data = FieldData.ReadFromBinaryReader(fieldReader, 2_005_001);
            var contents = Field.ReadFromBinaryReader(fieldReader, 2_005_001);
            data.StepType = StepTypeEnum.Frequency;
            using var changed = new MemoryStream();
            using (var writer = new BinaryWriter(changed, System.Text.Encoding.UTF8, true))
            { FieldData.WriteToBinaryWriter(data, writer); Field.WriteToBinaryWriter(contents, writer); }
            Save(ReplaceRecord(first, changed.ToArray()));
            check(indexed.List(project).SequenceEqual(ProjectResults.List(project)) && !indexed.LastAccess!.Indexed,
                  "non-static records fall back without changing the supported static catalog");
            data.StepType = StepTypeEnum.Static;
            contents.Complex = true;
            changed.SetLength(0);
            using (var writer = new BinaryWriter(changed, System.Text.Encoding.UTF8, true))
            { FieldData.WriteToBinaryWriter(data, writer); Field.WriteToBinaryWriter(contents, writer); }
            Save(ReplaceRecord(first, changed.ToArray()));
            check(indexed.List(project).SequenceEqual(ProjectResults.List(project)) && !indexed.LastAccess!.Indexed && before.SequenceEqual(Temps()),
                  "complex field state falls back to production semantics without retaining an index");
            File.WriteAllBytes(project, original); indexed.List(project);
            indexed.Dispose();
            check(before.SequenceEqual(Temps()), "disposing an indexed session removes its private payload");
        }
        finally { File.WriteAllBytes(project, original); Directory.Delete(root, true); }
    }
}

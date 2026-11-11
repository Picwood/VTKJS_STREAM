using System.IO.Compression;
using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class ResultSessionChecks
{
    public static void Run(Action<bool, string> check, string project, ResultField[] fields)
    {
        string root = Path.Combine(Path.GetTempPath(), "result-session-checks-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        byte[] original = File.ReadAllBytes(project);
        var temporaryBefore = Directory.GetDirectories(Path.GetTempPath(), "prepomax-result-session-*").Order().ToArray();
        try
        {
            using var session = new ProjectResultSession(useBinaryIndex: false);
            var catalog = session.List(project);
            check(catalog.SequenceEqual(ProjectResults.List(project)), "session preserves complete field catalog");
            check(session.LoadCount == 1 && session.HitCount == 0 && session.RetainedEntries == 1 &&
                  session.LastAccess is { CacheHit: false, Retained: true, LoadAllocatedBytes: > 0 },
                  "first session access loads and admits one validated result");
            catalog[0] = catalog[0] with { Component = "mutated" };
            check(session.List(project)[0].Component != "mutated", "session catalog does not expose cached mutable array");
            foreach (var field in fields)
            {
                var expected = ProjectResults.Fingerprint(project, field);
                var actual = session.Fingerprint(project, field);
                check(expected.Sha256 == actual.Sha256 && expected.Units == actual.Units && expected.Minimum == actual.Minimum &&
                      expected.Maximum == actual.Maximum && expected.Mean == actual.Mean && expected.Samples.SequenceEqual(actual.Samples),
                      "retained result preserves field fingerprint, units and statistics");
                foreach (double scale in new[] { 100d, -100d, 0d })
                {
                    string baseline = Path.Combine(root, Guid.NewGuid() + "-baseline.bin");
                    string retained = Path.Combine(root, Guid.NewGuid() + "-session.bin");
                    ProjectScene.Export(project, baseline, field, scale);
                    ProjectScene.Export(project, retained, field, scale, resultSession: session);
                    check(File.ReadAllBytes(baseline).SequenceEqual(File.ReadAllBytes(retained)),
                          "session scene equals independent export at positive, negative and zero displacement");
                }
            }
            for (int i = 0; i < 20; i++) session.Fingerprint(project, fields[i % fields.Length]);
            check(session.LoadCount == 1 && session.RetainedEntries == 1 && session.LastAccess!.Load.DecodeMilliseconds == 0,
                  "repeated frame access keeps one entry and does not decode again");
            string copy = Path.Combine(root, "same-content.pma");
            File.WriteAllBytes(copy, original);
            session.List(copy);
            check(session.LastAccess!.CacheHit && session.LoadCount == 1, "result cache identity is content, not pathname");

            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                bool rejected = false;
                try { session.List(project, cancel.Token); } catch (OperationCanceledException) { rejected = true; }
                check(rejected && session.LoadCount == 1, "cancelled result access does not decode");
                string output = Path.Combine(root, "cancelled.bin");
                rejected = false;
                try { ProjectScene.Export(project, output, fields[0], resultSession: session, cancellationToken: cancel.Token); }
                catch (OperationCanceledException) { rejected = true; }
                check(rejected && !File.Exists(output), "cancelled result export publishes no file");
            }

            DateTime stamp = File.GetLastWriteTimeUtc(project);
            byte[] corrupt = original.ToArray();
            // Archives written by our writer have a 22-byte EOCD, with no ZIP comment.
            check(BitConverter.ToUInt32(corrupt, corrupt.Length - 22) == 0x06054b50, "corruption fixture locates ZIP end record");
            corrupt[^22] ^= 0xff;
            File.WriteAllBytes(project, corrupt);
            File.SetLastWriteTimeUtc(project, stamp);
            bool invalid = false;
            try { session.List(project); } catch (InvalidDataException) { invalid = true; }
            check(invalid && session.RetainedEntries == 0 && new FileInfo(project).Length == original.Length &&
                  File.GetLastWriteTimeUtc(project) == stamp,
                  "same-size same-timestamp corruption cannot reuse cached results");
            File.WriteAllBytes(project, original);
            session.List(project);
            check(session.LoadCount == 2 && !session.LastAccess!.CacheHit, "valid archive recovers after rejected replacement");

            // Corrupt an entry unused by result rendering, retaining its old manifest hash.
            using (var zip = ZipFile.Open(project, ZipArchiveMode.Update))
            {
                var entry = zip.GetEntry("analysis.inp")!;
                byte[] input;
                using (var stream = entry.Open()) { using var data = new MemoryStream(); stream.CopyTo(data); input = data.ToArray(); }
                entry.Delete(); input[0] ^= 1;
                using var output = zip.CreateEntry("analysis.inp").Open(); output.Write(input);
            }
            invalid = false;
            try { session.List(project); } catch (InvalidDataException) { invalid = true; }
            check(invalid && session.RetainedEntries == 0, "session validates hashes of unselected archive entries");
            File.WriteAllBytes(project, original);
            session.List(project);
            long loads = session.LoadCount;
            session.Clear();
            check(session.RetainedEntries == 0 && session.LastAccess == null, "explicit clear releases result entry and diagnostics");
            session.List(project);
            check(session.LoadCount == loads + 1 && !session.LastAccess!.CacheHit, "access after clear reloads results");
            session.Dispose();
            bool disposed = false;
            try { session.List(project); } catch (ObjectDisposedException) { disposed = true; }
            check(disposed && session.RetainedEntries == 0, "disposed session releases entry and rejects reuse");

            using var bounded = new ProjectResultSession(retentionBudgetBytes: 1, useBinaryIndex: false);
            check(bounded.List(project).SequenceEqual(ProjectResults.List(project)) && bounded.RetainedEntries == 0,
                  "over-budget load returns correct catalog without retaining collection");
            bounded.List(project);
            check(bounded.LoadCount == 2 && bounded.HitCount == 0 && bounded.LastAccess is { Retained: false },
                  "over-budget access falls back to reloading");
            check(bounded.LastAccess!.IdentityMilliseconds == 0 && bounded.LastAccess.SnapshotMilliseconds == 0,
                  "archive-size budget fallback avoids extra hashing and snapshot copying");
            using var allocationLimited = new ProjectResultSession(retentionBudgetBytes: original.Length, useBinaryIndex: false);
            allocationLimited.List(project);
            check(allocationLimited.LastAccess!.LoadAllocatedBytes > original.Length && allocationLimited.RetainedEntries == 0,
                  "load allocation budget also rejects small compressed archives that expand beyond the budget");
            using var disabled = new ProjectResultSession(retentionBudgetBytes: 0);
            disabled.List(project); disabled.List(project);
            check(disabled.LoadCount == 2 && disabled.RetainedEntries == 0, "zero budget disables retention");

            using var switching = new ProjectResultSession(useBinaryIndex: false);
            switching.List(project);
            StaticProjectArchive.Save(copy, StaticProjectArchive.Open(project).Model);
            File.WriteAllBytes(project, File.ReadAllBytes(copy));
            check(switching.List(project).Length == 0 && switching.LoadCount == 2 && switching.RetainedEntries == 1,
                  "valid replacement at the same path replaces the result catalog with an unsolved project");
            File.WriteAllBytes(project, original);
            check(switching.List(project).SequenceEqual(ProjectResults.List(project)) && switching.LoadCount == 3,
                  "returning to an evicted result project decodes again");

            string oversized = Path.Combine(root, "oversized.pma");
            using (var stream = File.Create(oversized)) stream.SetLength(ProjectResultSession.MaximumArchiveBytes + 1);
            invalid = false;
            try { bounded.List(oversized); } catch (InvalidDataException) { invalid = true; }
            check(invalid, "session rejects oversized source before copying or decoding");
            check(temporaryBefore.SequenceEqual(Directory.GetDirectories(Path.GetTempPath(), "prepomax-result-session-*").Order()),
                  "successful, failed and over-budget accesses clean private snapshots");
        }
        finally
        {
            File.WriteAllBytes(project, original);
            Directory.Delete(root, true);
        }
    }
}

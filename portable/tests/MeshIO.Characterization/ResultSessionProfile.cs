using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using PrePoMax.Application;

internal static class ResultSessionProfile
{
    public static void Run(string project, long budget)
    {
        var rows = new List<object>();
        using var session = new ProjectResultSession(budget);
        var watch = Stopwatch.StartNew();
        var fields = session.List(project);
        rows.Add(new { Operation = "session-catalog-cold", Milliseconds = watch.Elapsed.TotalMilliseconds, Access = session.LastAccess });
        watch.Restart();
        if (!session.List(project).SequenceEqual(fields)) throw new InvalidDataException("Warm catalog differs.");
        rows.Add(new { Operation = "session-catalog-warm", Milliseconds = watch.Elapsed.TotalMilliseconds, Access = session.LastAccess });
        var field = fields.FirstOrDefault(f => f.Name == "STRESS" && f.Component == "MISES") ?? fields.First();
        string? fingerprint = null;
        for (int i = 0; i < 3; i++)
        {
            long before = GC.GetTotalAllocatedBytes(precise: true);
            watch.Restart();
            var value = session.Fingerprint(project, field);
            fingerprint ??= value.Sha256;
            if (value.Sha256 != fingerprint) throw new InvalidDataException("Session fingerprint changed.");
            rows.Add(new { Operation = "session-fingerprint", Iteration = i + 1, Milliseconds = watch.Elapsed.TotalMilliseconds,
                AllocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - before, Access = session.LastAccess });
        }
        // Release the cached collection before profiling independent reads of the same source.
        session.Clear();
        for (int i = 0; i < 3; i++)
        {
            long before = GC.GetTotalAllocatedBytes(precise: true);
            watch.Restart();
            var value = ProjectResults.Fingerprint(project, field);
            if (value.Sha256 != fingerprint) throw new InvalidDataException("Independent fingerprint differs.");
            rows.Add(new { Operation = "independent-fingerprint", Iteration = i + 1, Milliseconds = watch.Elapsed.TotalMilliseconds,
                AllocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - before });
        }
        using var source = File.OpenRead(project);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Runtime = Environment.Version.ToString(), ArchiveBytes = source.Length,
            ArchiveSha256 = Convert.ToHexString(SHA256.HashData(source)),
            RetentionBudgetBytes = budget, Field = field, Fingerprint = fingerprint,
            session.LoadCount, session.HitCount, Measurements = rows,
            // Cumulative for this process, not a per-operation RSS comparison or a GPU measurement.
            ProcessPeakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}

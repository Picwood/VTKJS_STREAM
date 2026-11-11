using System.Diagnostics;
using System.Security.Cryptography;
using CaeResults;

namespace PrePoMax.Application;

public sealed record ResultSessionAccess(bool CacheHit, bool Retained, long LoadAllocatedBytes,
    double IdentityMilliseconds, double SnapshotMilliseconds, ResultLoadTimings Load)
{
    public bool Indexed { get; init; }
    public int IndexedFieldRecords { get; init; }
    public long IndexMetadataBytes { get; init; }
    public int DecodedFieldRecords { get; init; }
    public double SelectedDecodeMilliseconds { get; init; }
}

/// <summary>
/// Serial-use, single-entry result session. Eligible binary results retain a disk payload and metadata
/// index; other formats retain a decoded collection within a load-allocation budget. Every retained
/// access verifies source content. The admission budget is not a process RSS or peak allocation limit.
/// </summary>
public sealed class ProjectResultSession : IDisposable
{
    public const long DefaultRetentionBudgetBytes = 512L * 1024 * 1024;
    public const long MaximumArchiveBytes = 2L * 1024 * 1024 * 1024;
    private sealed record Entry(string Hash, FeResults? Results, ResultField[] Fields, long AllocatedBytes,
                                BinaryResultIndex? Index = null) : IDisposable
    {
        public void Dispose() => Index?.Dispose();
    }
    private readonly long _retentionBudget;
    private readonly bool _useBinaryIndex;
    private Entry? _entry;
    private bool _disposed;
    public ResultSessionAccess? LastAccess { get; private set; }
    public int RetainedEntries => _entry == null ? 0 : 1;
    public long LoadCount { get; private set; }
    public long HitCount { get; private set; }

    public ProjectResultSession(long retentionBudgetBytes = DefaultRetentionBudgetBytes, bool useBinaryIndex = true)
    {
        if (retentionBudgetBytes < 0) throw new ArgumentOutOfRangeException(nameof(retentionBudgetBytes));
        _retentionBudget = retentionBudgetBytes;
        _useBinaryIndex = useBinaryIndex;
    }

    public ResultField[] List(string path, CancellationToken cancellationToken = default)
        => Use(path, cancellationToken, entry => (ResultField[])entry.Fields.Clone());

    public ResultFingerprint Fingerprint(string path, ResultField selected, CancellationToken cancellationToken = default)
        => ProjectResults.Fingerprint(Values(path, selected, false, cancellationToken), selected);

    internal ResultValues Values(string path, ResultField selected, bool displacement, CancellationToken cancellationToken)
        => Use(path, cancellationToken, entry =>
        {
            var watch = Stopwatch.StartNew();
            int decoded = 0;
            var values = entry.Index is { } index
                ? index.Values(selected, displacement, cancellationToken, out decoded)
                : ProjectResults.Values(entry.Results ?? throw new InvalidDataException("Project contains no results."), selected, displacement);
            cancellationToken.ThrowIfCancellationRequested();
            LastAccess = LastAccess! with { DecodedFieldRecords = decoded, SelectedDecodeMilliseconds = watch.Elapsed.TotalMilliseconds };
            return values;
        });

    private T Use<T>(string path, CancellationToken stop, Func<Entry, T> action)
    {
        var entry = Get(path, stop);
        try { return action(entry); }
        finally { if (!ReferenceEquals(entry, _entry)) entry.Dispose(); }
    }

    private Entry Get(string path, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        LastAccess = null;
        cancellationToken.ThrowIfCancellationRequested();
        // Keep the same file handle through hashing and snapshot creation. Replacement of the path
        // cannot redirect the second pass; in-place changes between passes fail the hash comparison.
        using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (source.Length > MaximumArchiveBytes) throw new InvalidDataException("Result session archive exceeds 2 GiB.");
        if (_retentionBudget == 0 || (!_useBinaryIndex && source.Length > _retentionBudget))
        {
            // Do not add hashing/copying overhead to large archives we will not retain.
            Evict();
            var uncached = Decode(path, "", cancellationToken, out var loadTimings);
            LoadCount++;
            LastAccess = new(false, false, uncached.AllocatedBytes, 0, 0, loadTimings);
            return uncached;
        }
        var watch = Stopwatch.StartNew();
        string identity = CopyAndHash(source, null, cancellationToken);
        double identityMilliseconds = watch.Elapsed.TotalMilliseconds;
        if (_entry is { } cached && cached.Hash == identity)
        {
            HitCount++;
            LastAccess = Access(cached, true, true, identityMilliseconds, 0, new(0, 0));
            return cached;
        }

        // Drop the previous decoded collection before loading another; failed loads never cache.
        Evict();
        string directory = Directory.CreateTempSubdirectory("prepomax-result-session-").FullName;
        try
        {
            string snapshot = Path.Combine(directory, "snapshot.pma");
            watch.Restart();
            source.Position = 0;
            using (var destination = new FileStream(snapshot, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                if (CopyAndHash(source, destination, cancellationToken) != identity)
                    throw new IOException("Project changed while creating the result snapshot.");
            double snapshotMilliseconds = watch.Elapsed.TotalMilliseconds;
            var entry = LoadEntry(snapshot, identity, cancellationToken, out var timings);
            bool retain = entry.Index is { } index ? index.MetadataBytes <= _retentionBudget
                : source.Length <= _retentionBudget && entry.AllocatedBytes <= _retentionBudget;
            if (retain) _entry = entry;
            LoadCount++;
            LastAccess = Access(entry, false, retain, identityMilliseconds, snapshotMilliseconds, timings);
            return entry;
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static ResultSessionAccess Access(Entry entry, bool hit, bool retained, double identity, double snapshot, ResultLoadTimings timings)
        => new(hit, retained, entry.AllocatedBytes, identity, snapshot, timings)
        {
            Indexed = entry.Index != null,
            IndexedFieldRecords = entry.Index?.RecordCount ?? 0,
            IndexMetadataBytes = entry.Index?.MetadataBytes ?? 0
        };

    private Entry LoadEntry(string path, string identity, CancellationToken stop, out ResultLoadTimings timings)
    {
        long before = GC.GetTotalAllocatedBytes(precise: true);
        ResultLoadTimings attempt = new(0, 0);
        if (_useBinaryIndex && BinaryResultIndex.TryOpen(path, stop, out attempt) is { } index)
        {
            timings = attempt;
            return new(identity, null, index.Fields, Math.Max(0, GC.GetTotalAllocatedBytes(precise: true) - before), index);
        }
        var eager = Decode(path, identity, stop, out var loaded);
        timings = new(attempt.ArchiveMilliseconds + loaded.ArchiveMilliseconds, attempt.DecodeMilliseconds + loaded.DecodeMilliseconds);
        return eager with { AllocatedBytes = Math.Max(0, GC.GetTotalAllocatedBytes(precise: true) - before) };
    }

    private static Entry Decode(string path, string identity, CancellationToken cancellationToken, out ResultLoadTimings timings)
    {
        long before = GC.GetTotalAllocatedBytes(precise: true);
        var results = ProjectResults.Load(path, cancellationToken, out timings);
        var fields = results == null ? [] : ProjectResults.Fields(results);
        // Process-wide allocation delta deliberately overestimates admission if other work runs.
        long allocated = Math.Max(0, GC.GetTotalAllocatedBytes(precise: true) - before);
        cancellationToken.ThrowIfCancellationRequested();
        return new(identity, results, fields, allocated);
    }

    private static string CopyAndHash(Stream source, Stream? destination, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[128 * 1024];
        long total = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int read = source.Read(buffer, 0, buffer.Length);
            if (read == 0) break;
            total += read;
            if (total > MaximumArchiveBytes) throw new InvalidDataException("Result session archive exceeds 2 GiB.");
            hash.AppendData(buffer, 0, read);
            destination?.Write(buffer, 0, read);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Evict();
        LastAccess = null;
    }

    public void Dispose()
    {
        Evict();
        LastAccess = null;
        _disposed = true;
    }

    private void Evict()
    {
        var previous = _entry;
        _entry = null;
        previous?.Dispose();
    }
}

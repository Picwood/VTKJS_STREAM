using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using CaeGlobals;
using CaeResults;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

/// <summary>
/// Index of a verified, privately owned PMA-v4 payload. Only the known 2.5.1 static-real
/// layout is indexed. Cold opening validates mesh/history with the production reader and
/// scans every field record; subsequent selections materialize only requested field records.
/// </summary>
internal sealed class BinaryResultIndex : IDisposable
{
    private sealed record Record(long Offset, int Length, ResultField[] Selections);
    private sealed class UnsupportedIndex : Exception { }
    private readonly string _directory, _payload;
    private readonly ResultPayloadHeader _header;
    private readonly UnitSystem _units;
    private readonly long _binaryStart, _prefixLength, _historyStart;
    private readonly Record[] _records;
    internal ResultField[] Fields { get; }
    internal int RecordCount => _records.Length;
    // Conservative metadata accounting, separate from transient mesh/decoder allocation.
    internal long MetadataBytes { get; }

    private BinaryResultIndex(string directory, ResultPayloadHeader header, UnitSystem units,
                              long binaryStart, long prefixLength, long historyStart, Record[] records)
    {
        _directory = directory; _payload = Path.Combine(directory, "results.bin");
        _header = header; _units = units; _binaryStart = binaryStart;
        _prefixLength = prefixLength; _historyStart = historyStart; _records = records;
        Fields = records.SelectMany(r => r.Selections).ToArray();
        MetadataBytes = 4096L + records.Length * 128L + Fields.Sum(f => 128L + 2L * (f.Name.Length + f.Component.Length));
    }

    internal static BinaryResultIndex? TryOpen(string archive, CancellationToken stop, out ResultLoadTimings timings)
    {
        timings = new(0, 0);
        if (!AnalysisArchive.Describe(archive).Payloads.Contains("results.bin", StringComparer.Ordinal)) return null;
        string directory = Directory.CreateTempSubdirectory("prepomax-result-index-").FullName;
        bool keep = false;
        var watch = Stopwatch.StartNew();
        double archiveMilliseconds = 0;
        try
        {
            using var metadata = new MemoryStream();
            string payload = Path.Combine(directory, "results.bin");
            using (var output = File.Create(payload))
                AnalysisArchive.CopyPayloads(archive, new Dictionary<string, Stream>
                    { ["model.json"] = metadata, ["results.bin"] = output }, stop);
            var units = StaticModelMetadata.ReadUnitSystem(metadata.ToArray());
            archiveMilliseconds = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            using var input = File.OpenRead(payload);
            var header = ResultPayload.ReadHeader(input, input.Length);
            if (header.BinaryVersion != 2_005_001 || header.ResultNames.Length != 1) return null;
            long binaryStart = input.Position;
            if (Tools.ReadIntFromFileStream(input) != 1 ||
                !string.Equals(Tools.ReadStringFromFileStream(input), header.ResultNames[0], StringComparison.OrdinalIgnoreCase) ||
                Tools.ReadIntFromFileStream(input) != 1)
                throw new InvalidDataException("Invalid indexed result collection.");
            int meshBytes = ReadBlockLength(input);
            input.Position += meshBytes;
            long prefixLength = input.Position - binaryStart;
            int state = Tools.ReadIntFromFileStream(input);
            if (state is not (-1 or 1)) throw new InvalidDataException("Invalid indexed field state.");
            int count = state == 1 ? Tools.ReadIntFromFileStream(input) : 0;
            if (count is < 0 or > 100_000) throw new InvalidDataException("Invalid indexed field count.");
            var records = new List<Record>();
            var hashes = new HashSet<string>(StringComparer.Ordinal);
            long metadataBytes = 4096;
            for (int i = 0; i < count; i++)
            {
                stop.ThrowIfCancellationRequested();
                long offset = input.Position;
                int compressed = ReadBlockLength(input);
                using var segment = new ReadLimitStream(input, compressed, false, stop);
                using var inflater = new DeflateStream(segment, CompressionMode.Decompress, leaveOpen: true);
                using var bounded = new ReadLimitStream(inflater, 1024L * 1024 * 1024, true, stop);
                using var reader = new IndexReader(bounded);
                var data = FieldData.ReadFromBinaryReader(reader, header.BinaryVersion)
                    ?? throw new InvalidDataException("Missing indexed field metadata.");
                var field = ReadFieldMetadata(reader);
                if (reader.BaseStream.ReadByte() != -1) throw new InvalidDataException("Trailing indexed field data.");
                // No projection of complex or deferred-computation semantics into a static index.
                if (data.StepType != StepTypeEnum.Static || data.StepId < 1 || data.StepIncrementId < 1 ||
                    data.Name != field.Name || field.Complex || field.DataState != DataStateEnum.OK ||
                    FeResults.ComplexFieldNames.Any(names => names.Skip(1).Contains(data.Name, StringComparer.OrdinalIgnoreCase)))
                    throw new UnsupportedIndex();
                if (!hashes.Add(data.GetHashKey())) throw new InvalidDataException("Duplicate indexed result field.");
                field.ComputeInvariants(); // one dummy value: production component ordering, no engineering data
                var selections = field.GetComponentNames().Select(c =>
                    new ResultField(data.Name, c, data.StepId, data.StepIncrementId, data.Time)).ToArray();
                metadataBytes += 128 + selections.Sum(f => 128L + 2L * (f.Name.Length + f.Component.Length));
                if (metadataBytes > 128L * 1024 * 1024) throw new UnsupportedIndex();
                records.Add(new(offset, checked(compressed + 4), selections));
                input.Position = offset + 4L + compressed;
            }
            long historyStart = input.Position;
            var index = new BinaryResultIndex(directory, header, units, binaryStart, prefixLength, historyStart, records.ToArray());
            // Validate the entire mesh and history with existing version-aware production code.
            // These objects are discarded; a catalog does not retain an editable or result mesh.
            index.ReadSlice([], includeHistory: true, stop);
            stop.ThrowIfCancellationRequested();
            keep = true;
            return index;
        }
        catch (UnsupportedIndex) { return null; }
        finally
        {
            timings = new(archiveMilliseconds, watch.Elapsed.TotalMilliseconds);
            if (!keep) Directory.Delete(directory, recursive: true);
        }
    }

    internal ResultValues Values(ResultField selected, bool displacement, CancellationToken stop, out int decodedRecords)
    {
        if (!Fields.Contains(selected)) throw new InvalidDataException("Unknown static result field or frame.");
        var selectedRecords = _records.Where(r => r.Selections.Contains(selected)).ToList();
        if (displacement)
        {
            var required = new[] { "U1", "U2", "U3" }.Select(c => new ResultField("DISP", c, selected.Step, selected.Increment, selected.Time)).ToArray();
            var vector = _records.SingleOrDefault(r => required.All(r.Selections.Contains))
                ?? throw new InvalidDataException("Selected frame has no complete displacement vector.");
            if (!selectedRecords.Contains(vector)) selectedRecords.Add(vector);
        }
        // Preserve source field order, including when DISP precedes STRESS.
        var records = _records.Where(selectedRecords.Contains).ToArray();
        var result = ReadSlice(records, includeHistory: false, stop);
        stop.ThrowIfCancellationRequested();
        var values = ProjectResults.Values(result, selected, displacement);
        stop.ThrowIfCancellationRequested();
        decodedRecords = records.Length;
        return values;
    }

    private FeResults ReadSlice(Record[] records, bool includeHistory, CancellationToken stop)
    {
        string slice = Path.Combine(_directory, "selection-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            using (var source = File.OpenRead(_payload))
            using (var output = File.Create(slice))
            {
                Copy(source, output, _binaryStart, _prefixLength, stop);
                Tools.WriteIntToFileStream(output, 1);
                Tools.WriteIntToFileStream(output, records.Length);
                foreach (var record in records) Copy(source, output, record.Offset, record.Length, stop);
                if (includeHistory) Copy(source, output, _historyStart, source.Length - _historyStart, stop);
                else Tools.WriteIntToFileStream(output, -1);
            }
            stop.ThrowIfCancellationRequested();
            return ProjectResults.ReadBinaryCollection(slice, _header, _units);
        }
        finally { if (File.Exists(slice)) File.Delete(slice); }
    }

    private static int ReadBlockLength(FileStream input)
    {
        int length = Tools.ReadIntFromFileStream(input);
        if (length is <= 0 or > 512 * 1024 * 1024 || length > input.Length - input.Position)
            throw new InvalidDataException("Invalid indexed compressed block length.");
        return length;
    }

    // Version-pinned schema projection: consume every value byte but allocate no nodal arrays.
    private static Field ReadFieldMetadata(BinaryReader reader)
    {
        if (reader.ReadInt32() != 1) throw new InvalidDataException("Missing indexed field.");
        var field = new Field(reader.ReadString());
        int state = reader.ReadInt32();
        if (state is not (0 or 1)) throw new InvalidDataException("Invalid indexed component state.");
        int count = state == 1 ? reader.ReadInt32() : 0;
        if (count is < 0 or > 1024) throw new InvalidDataException("Invalid indexed component count.");
        int? commonCount = null;
        byte[] buffer = new byte[64 * 1024];
        for (int i = 0; i < count; i++)
        {
            string name = reader.ReadString();
            bool invariant = reader.ReadBoolean();
            int values = reader.ReadInt32();
            if (values is < 0 or > 100_000_000) throw new InvalidDataException("Invalid indexed component value count.");
            if (commonCount is { } previous && previous != values) throw new UnsupportedIndex();
            commonCount = values;
            long remaining = values * 4L;
            while (remaining > 0)
            {
                int bytes = (int)Math.Min(remaining, buffer.Length);
                reader.BaseStream.ReadExactly(buffer.AsSpan(0, bytes));
                remaining -= bytes;
            }
            if (field.ContainsComponent(name)) throw new InvalidDataException("Duplicate indexed component.");
            field.AddComponent(name, values == 0 ? [] : [0f], invariant);
        }
        field.Complex = reader.ReadBoolean();
        field.DataState = (DataStateEnum)reader.ReadInt32();
        field.Active = reader.ReadBoolean(); field.Visible = reader.ReadBoolean();
        field.Valid = reader.ReadBoolean(); field.Internal = reader.ReadBoolean();
        return field;
    }

    private static void Copy(FileStream source, Stream destination, long offset, long count, CancellationToken stop)
    {
        source.Position = offset;
        byte[] buffer = new byte[128 * 1024];
        while (count > 0)
        {
            stop.ThrowIfCancellationRequested();
            int read = source.Read(buffer, 0, (int)Math.Min(count, buffer.Length));
            if (read == 0) throw new EndOfStreamException("Truncated indexed result payload.");
            destination.Write(buffer, 0, read); count -= read;
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private sealed class IndexReader(Stream stream) : BinaryReader(stream, Encoding.UTF8, leaveOpen: true)
    {
        public override string ReadString()
        {
            int bytes = Read7BitEncodedInt();
            if (bytes is < 0 or > 4096) throw new InvalidDataException("Indexed result name exceeds limit.");
            byte[] data = new byte[bytes]; BaseStream.ReadExactly(data);
            return Encoding.UTF8.GetString(data);
        }
    }

    private sealed class ReadLimitStream(Stream source, long limit, bool checkOverflow, CancellationToken stop) : Stream
    {
        private long _read;
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            stop.ThrowIfCancellationRequested();
            if (buffer.Length == 0) return 0;
            if (_read == limit)
            {
                if (checkOverflow && source.ReadByte() != -1) throw new InvalidDataException("Indexed block exceeds inflation limit.");
                return 0;
            }
            int read = source.Read(buffer[..(int)Math.Min(buffer.Length, limit - _read)]);
            _read += read; return read;
        }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _read; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

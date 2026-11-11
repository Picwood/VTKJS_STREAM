using System.Formats.Nrbf;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using CaeResults;

internal static class LegacyPmxResultsReader
{
    sealed record BlockInventory(int CompressedBytes, long InflatedBytes, string Sha256);
    const int MaxResults = 1024;
    const int MaxFields = 100000;
    const int MaxHistoryItems = 1000000;
    const int MaxNameBytes = 4096;
    const int MaxCompressedBlock = 512 * 1024 * 1024;
    const long MaxTailBytes = 1024L * 1024 * 1024;
    const long MaxInflatedBlock = 2L * 1024 * 1024 * 1024;
    static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static object Read(FileStream file, ClassRecord controller, Version version)
    {
        string[] expected = ExpectedResultNames(controller);
        long start = file.Position;
        if (file.Length - start is < 4 or > MaxTailBytes)
            throw new InvalidDataException("Invalid results tail size or results exceed 1 GiB.");
        int count = ReadCount(file, MaxResults, "result");
        if (count != expected.Length)
            throw new InvalidDataException($"Results tail contains {count} result sets but metadata declares {expected.Length}.");
        var results = new List<object>(count);
        for (int i = 0; i < count; i++)
        {
            string name = ReadRequiredString(file, "result name");
            if (!StringComparer.OrdinalIgnoreCase.Equals(name, expected[i]))
                throw new InvalidDataException($"Result set {i + 1} is '{name}' but metadata declares '{expected[i]}'.");
            int state = ReadState(file, -1, 1, "result");
            if (state == -1)
            {
                results.Add(new { name, present = false });
                continue;
            }
            var mesh = ReadDeflatedBlock(file, "result mesh", null);
            int fieldsState = ReadState(file, -1, 1, "result fields");
            var fields = new List<object>();
            if (fieldsState == 1)
            {
                int fieldCount = ReadCount(file, MaxFields, "result field");
                for (int j = 0; j < fieldCount; j++)
                {
                    object? detail = null;
                    var block = ReadDeflatedBlock(file, $"result field {j + 1}", reader =>
                    {
                        FieldData data = FieldData.ReadFromBinaryReader(reader, VersionNumber(version))
                            ?? throw new InvalidDataException("Missing result field metadata.");
                        Field field = Field.ReadFromBinaryReader(reader, VersionNumber(version))
                            ?? throw new InvalidDataException("Missing result field values.");
                        string[] componentNames = field.GetComponentNames();
                        var components = componentNames.Select(component =>
                        {
                            float[] values = field.GetComponentValues(component)
                                ?? throw new InvalidDataException("Missing result component values.");
                            return new { name = component, values = values.Length,
                                finiteValues = values.LongCount(float.IsFinite), nanValues = values.LongCount(float.IsNaN),
                                positiveInfinityValues = values.LongCount(float.IsPositiveInfinity),
                                negativeInfinityValues = values.LongCount(float.IsNegativeInfinity) };
                        }).ToArray();
                        detail = new { name = data.Name, data.Component, data.GlobalIncrementId, data.MethodId,
                            stepType = data.StepType.ToString(), data.Time, data.StepId, data.StepIncrementId,
                            data.Unit, data.Active, data.Visible, data.Valid, data.Internal,
                            fieldComplex = field.Complex, dataType = field.DataType.ToString(), components };
                    });
                    fields.Add(new { block.CompressedBytes, block.InflatedBytes, block.Sha256, detail });
                }
            }
            object? history = null;
            if (VersionNumber(version) > 2_003_005)
            {
                int historyState = ReadState(file, -1, 1, "result history");
                if (historyState == 1) history = ReadHistory(file, version);
            }
            results.Add(new { name, present = true, mesh, fieldsPresent = fieldsState == 1,
                fieldCount = fields.Count, fields, history });
        }
        if (file.Position != file.Length)
            throw new InvalidDataException($"Results tail has {file.Length - file.Position} trailing bytes.");
        return new { status = "decoded-results-inventory", metadataMatched = true,
            resultCount = results.Count, tailBytes = file.Position - start, results,
            scope = "Every result field and history entry was decoded with the production version-aware binary readers. The result mesh Deflate block was integrity-checked and bounded but not materialized." };
    }

    internal static string[] ExpectedResultNames(ClassRecord controller)
    {
        ClassRecord? all = controller.HasMember("_allResults") ? controller.GetClassRecord("_allResults") : null;
        if (all == null) return [];
        if (all.TypeName.FullName != "CaeResults.ResultsCollection" || !all.HasMember("_results"))
            throw new InvalidDataException("Unsupported results metadata layout.");
        return ActiveModelInventory.DictionaryPairs(all.GetClassRecord("_results"))
            .Select(pair =>
            {
                string name = pair.GetString("key") ?? throw new InvalidDataException("Missing result metadata name.");
                ClassRecord value = pair.GetClassRecord("value") ?? throw new InvalidDataException("Missing result metadata value.");
                if (value.TypeName.FullName != "CaeResults.FeResults")
                    throw new InvalidDataException("Unsupported result metadata type: " + value.TypeName.FullName);
                return name;
            }).ToArray();
    }

    static object ReadHistory(FileStream file, Version version)
    {
        if (ReadState(file, 0, 1, "history") == 0) return new { present = false };
        string name = ReadRequiredString(file, "history name");
        int setsState = ReadState(file, 0, 1, "history sets");
        var sets = new List<object>();
        if (setsState == 1)
        {
            int setCount = ReadCount(file, MaxHistoryItems, "history set");
            for (int i = 0; i < setCount; i++) sets.Add(ReadHistorySet(file, version));
        }
        return new { present = true, name, setCount = sets.Count, sets };
    }

    static object ReadHistorySet(FileStream file, Version version)
    {
        RequirePresent(file, "history set");
        string name = ReadRequiredString(file, "history set name");
        bool harmonic = ReadBool(file);
        int fieldsState = ReadState(file, 0, 1, "history fields");
        var fields = new List<object>();
        if (fieldsState == 1)
        {
            int count = ReadCount(file, MaxHistoryItems, "history field");
            for (int i = 0; i < count; i++) fields.Add(ReadHistoryField(file, version));
        }
        string? baseSetName = ReadString(file);
        return new { name, harmonic, baseSetName, fieldCount = fields.Count, fields };
    }

    static object ReadHistoryField(FileStream file, Version version)
    {
        RequirePresent(file, "history field");
        string name = ReadRequiredString(file, "history field name");
        int componentsState = ReadState(file, 0, 1, "history components");
        var components = new List<object>();
        if (componentsState == 1)
        {
            int count = ReadCount(file, MaxHistoryItems, "history component");
            for (int i = 0; i < count; i++) components.Add(ReadHistoryComponent(file, version));
        }
        return new { name, componentCount = components.Count, components };
    }

    static object ReadHistoryComponent(FileStream file, Version version)
    {
        RequirePresent(file, "history component");
        string name = ReadRequiredString(file, "history component name");
        string? unit = ReadString(file);
        int entriesState = ReadState(file, 0, 1, "history entries");
        var entries = new List<object>();
        if (entriesState == 1)
        {
            int count = ReadCount(file, MaxHistoryItems, "history entry");
            for (int i = 0; i < count; i++)
            {
                RequirePresent(file, "history entry");
                object? detail = null;
                var block = ReadDeflatedBlock(file, $"history entry {i + 1}", reader =>
                {
                    HistoryResultEntries value = HistoryResultEntries.ReadFromBinaryReader(reader, VersionNumber(version))
                        ?? throw new InvalidDataException("Missing history entry values.");
                    detail = new { value.Name, timeValues = value.Time.Count, values = value.Values.Count,
                        counts = value.Count.Count, value.Local, value.Unit };
                });
                entries.Add(new { block.CompressedBytes, block.InflatedBytes, block.Sha256, detail });
            }
        }
        return new { name, unit, entryCount = entries.Count, entries };
    }

    static BlockInventory ReadDeflatedBlock(
        FileStream file, string description, Action<BinaryReader>? decode)
    {
        int length = ReadCount(file, MaxCompressedBlock, description + " compressed byte");
        if (length == 0) throw new InvalidDataException("Empty " + description + " block.");
        byte[] compressed = new byte[length];
        file.ReadExactly(compressed);
        using var input = new MemoryStream(compressed, writable: false);
        using var inflate = new DeflateStream(input, CompressionMode.Decompress);
        using var bounded = new BoundedReadStream(inflate, MaxInflatedBlock, description);
        if (decode == null) bounded.CopyTo(Stream.Null);
        else
        {
            using var reader = new BinaryReader(bounded, Encoding.UTF8, leaveOpen: true);
            decode(reader);
            if (bounded.ReadByte() != -1) throw new InvalidDataException("Trailing decompressed data in " + description + ".");
        }
        return new(length, bounded.BytesRead, Convert.ToHexString(SHA256.HashData(compressed)));
    }

    static void RequirePresent(FileStream file, string description)
    {
        if (ReadState(file, 0, 1, description) != 1) throw new InvalidDataException("Missing " + description + ".");
    }

    static int ReadState(FileStream file, int absent, int present, string description)
    {
        int value = ReadInt(file);
        if (value != absent && value != present) throw new InvalidDataException("Invalid " + description + " state.");
        return value;
    }

    static int ReadCount(FileStream file, int maximum, string description)
    {
        int value = ReadInt(file);
        if (value < 0 || value > maximum) throw new InvalidDataException("Invalid " + description + " count.");
        return value;
    }

    static int ReadInt(FileStream file)
    {
        Span<byte> bytes = stackalloc byte[4];
        file.ReadExactly(bytes);
        return BitConverter.ToInt32(bytes);
    }

    static bool ReadBool(FileStream file)
    {
        int value = file.ReadByte();
        if (value is not (0 or 1)) throw new InvalidDataException("Invalid Boolean value.");
        return value == 1;
    }

    static string? ReadString(FileStream file)
    {
        int length = ReadCount(file, MaxNameBytes, "string byte");
        byte[] bytes = new byte[length];
        file.ReadExactly(bytes);
        string value = StrictUtf8.GetString(bytes);
        return value == "@null#" ? null : value;
    }

    static string ReadRequiredString(FileStream file, string description) =>
        ReadString(file) ?? throw new InvalidDataException("Missing " + description + ".");

    static int VersionNumber(Version value) => value.Major * 1_000_000 + value.Minor * 1000 + Math.Max(value.Build, 0);

    sealed class BoundedReadStream(Stream inner, long maximum, string description) : Stream
    {
        public long BytesRead { get; private set; }
        public override int Read(byte[] buffer, int offset, int count) => Account(inner.Read(buffer, offset, count));
        public override int Read(Span<byte> buffer) => Account(inner.Read(buffer));
        public override int ReadByte()
        {
            int value = inner.ReadByte();
            if (value >= 0) Account(1);
            return value;
        }
        int Account(int count)
        {
            BytesRead += count;
            if (BytesRead > maximum) throw new InvalidDataException(description + " exceeds the inflated-data limit.");
            return count;
        }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => BytesRead; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

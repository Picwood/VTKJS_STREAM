using System.Buffers.Binary;
using System.Text;

namespace PrePoMax.Persistence;

public sealed record ResultPayloadData(int BinaryVersion, string[] ResultNames, ReadOnlyMemory<byte> BinaryData);
public sealed record ResultPayloadHeader(int BinaryVersion, string[] ResultNames, long BinaryLength);

/// <summary>Self-describing envelope for the versioned CaeResults binary stream.</summary>
public static class ResultPayload
{
    static readonly byte[] Magic = "PMXRSLT1"u8.ToArray();
    static readonly UTF8Encoding StrictUtf8 = new(false, true);
    const int FormatVersion = 1;
    const int MaxResults = 1024;
    const int MaxNameBytes = 4096;
    public const int MaxBytes = 768 * 1024 * 1024;

    public static byte[] Create(int binaryVersion, IReadOnlyList<string> resultNames, byte[] binaryData)
    {
        ArgumentNullException.ThrowIfNull(binaryData);
        using var input = new MemoryStream(binaryData, writable: false);
        return Create(binaryVersion, resultNames, input, binaryData.LongLength);
    }

    public static byte[] Create(int binaryVersion, IReadOnlyList<string> resultNames, Stream binaryData, long binaryLength)
    {
        ArgumentNullException.ThrowIfNull(resultNames);
        ArgumentNullException.ThrowIfNull(binaryData);
        if (binaryVersion < 2_000_006 || binaryVersion > 999_999_999)
            throw new ArgumentOutOfRangeException(nameof(binaryVersion));
        if (resultNames.Count is < 1 or > MaxResults)
            throw new InvalidDataException("A result payload requires 1–1024 result names.");
        if (binaryLength is < 4 or > MaxBytes)
            throw new InvalidDataException("Invalid result binary size or result binary exceeds 768 MiB.");
        var names = resultNames.Select(name =>
        {
            if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("A result name is required.");
            byte[] value = StrictUtf8.GetBytes(name);
            if (value.Length > MaxNameBytes) throw new InvalidDataException("Result name exceeds 4096 UTF-8 bytes.");
            return value;
        }).ToArray();
        long headerLength = 8 + 4 + 4 + 4 + 8 + names.Sum(name => 4L + name.Length);
        int totalLength = checked((int)(headerLength + binaryLength));
        byte[] output = new byte[totalLength];
        int offset = 0;
        Magic.CopyTo(output, offset); offset += Magic.Length;
        WriteInt(output, ref offset, FormatVersion);
        WriteInt(output, ref offset, binaryVersion);
        WriteInt(output, ref offset, names.Length);
        foreach (byte[] name in names)
        {
            WriteInt(output, ref offset, name.Length);
            name.CopyTo(output, offset); offset += name.Length;
        }
        BinaryPrimitives.WriteInt64LittleEndian(output.AsSpan(offset, 8), binaryLength); offset += 8;
        binaryData.ReadExactly(output.AsSpan(offset, checked((int)binaryLength)));
        if (binaryData.ReadByte() != -1) throw new InvalidDataException("Result binary exceeds its declared length.");
        return output;
    }

    public static ResultPayloadData Read(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        using var stream = new MemoryStream(payload, writable: false);
        ResultPayloadHeader header = ReadHeader(stream, payload.LongLength);
        return new(header.BinaryVersion, header.ResultNames,
            payload.AsMemory(checked((int)stream.Position), checked((int)header.BinaryLength)));
    }

    /// <summary>Reads and validates the envelope header, leaving the stream at the binary data.</summary>
    public static ResultPayloadHeader ReadHeader(Stream payload, long payloadLength)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (!payload.CanRead || payloadLength is < 32 or > MaxBytes + 4L * 1024 * 1024)
            throw new InvalidDataException("Invalid result payload size.");
        long consumed = 0;
        byte[] magic = new byte[Magic.Length];
        payload.ReadExactly(magic); consumed += magic.Length;
        if (!magic.SequenceEqual(Magic)) throw new InvalidDataException("Invalid result payload signature.");
        if (ReadInt(payload, ref consumed) != FormatVersion) throw new InvalidDataException("Unsupported result payload version.");
        int binaryVersion = ReadInt(payload, ref consumed);
        if (binaryVersion < 2_000_006 || binaryVersion > 999_999_999)
            throw new InvalidDataException("Invalid result binary version.");
        int count = ReadInt(payload, ref consumed);
        if (count is < 1 or > MaxResults) throw new InvalidDataException("Invalid result-name count.");
        string[] names = new string[count];
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < count; i++)
        {
            int length = ReadInt(payload, ref consumed);
            if (length is < 1 or > MaxNameBytes || length > payloadLength - consumed)
                throw new InvalidDataException("Invalid result name length.");
            byte[] encoded = new byte[length];
            payload.ReadExactly(encoded); consumed += length;
            names[i] = StrictUtf8.GetString(encoded);
            if (string.IsNullOrWhiteSpace(names[i]) || !seen.Add(names[i]))
                throw new InvalidDataException("Invalid or duplicate result name.");
        }
        if (payloadLength - consumed < 8) throw new InvalidDataException("Missing result binary length.");
        Span<byte> lengthBytes = stackalloc byte[8];
        payload.ReadExactly(lengthBytes); consumed += 8;
        long binaryLength = BinaryPrimitives.ReadInt64LittleEndian(lengthBytes);
        if (binaryLength is < 4 or > MaxBytes || binaryLength != payloadLength - consumed)
            throw new InvalidDataException("Invalid result binary length.");
        return new(binaryVersion, names, binaryLength);
    }

    static void WriteInt(byte[] data, ref int offset, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset, 4), value); offset += 4;
    }

    static int ReadInt(Stream stream, ref long consumed)
    {
        Span<byte> data = stackalloc byte[4];
        stream.ReadExactly(data); consumed += 4;
        return BinaryPrimitives.ReadInt32LittleEndian(data);
    }
}

using System.Buffers.Binary;

namespace PrePoMax.Render.SyntheticData;

public static class LittleEndianArrayWriter
{
    public static byte[] Write(ReadOnlySpan<float> values)
    {
        var bytes = new byte[checked(values.Length * sizeof(float))];
        for (var index = 0; index < values.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(index * sizeof(float)), BitConverter.SingleToInt32Bits(values[index]));
        }

        return bytes;
    }

    public static byte[] Write(ReadOnlySpan<int> values)
    {
        var bytes = new byte[checked(values.Length * sizeof(int))];
        for (var index = 0; index < values.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(index * sizeof(int)), values[index]);
        }

        return bytes;
    }

    public static byte[] Write(ReadOnlySpan<uint> values)
    {
        var bytes = new byte[checked(values.Length * sizeof(uint))];
        for (var index = 0; index < values.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * sizeof(uint)), values[index]);
        }

        return bytes;
    }
}

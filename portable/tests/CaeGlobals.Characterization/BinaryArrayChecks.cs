using CaeGlobals;
using System.Text;

internal static class BinaryArrayChecks
{
    public static void Run(Action<bool, string> check)
    {
        // Literal little-endian fixtures specify the format independently of its writer.
        byte[] golden = Convert.FromHexString("03000000FFFFFFFF000000000200000001000000FEFFFFFF");
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        int[][] values = { null, Array.Empty<int>(), new[] { 1, -2 } };
        ReadWrite.WriteToBinaryStream(values, writer);
        check(stream.ToArray().SequenceEqual(golden), "jagged binary golden bytes");
        using var reader = new BinaryReader(new ShortReadStream(golden));
        ReadWrite.ReadFromBinaryStream(out int[][] restored, reader);
        check(restored.Length == 3 && restored[0] == null && restored[1].Length == 0 && restored[2].SequenceEqual(new[] { 1, -2 }), "jagged binary short reads");
        foreach (int[] integers in new[] { null, Array.Empty<int>(), new[] { int.MinValue, 0, int.MaxValue } })
        {
            using var buffer = new MemoryStream();
            using var bw = new BinaryWriter(buffer, Encoding.UTF8, true);
            ReadWrite.WriteToBinaryStream(integers, bw);
            buffer.Position = 0;
            using var br = new BinaryReader(buffer, Encoding.UTF8, true);
            ReadWrite.ReadFromBinaryStream(out int[] result, br);
            check(integers == null ? result == null : result.SequenceEqual(integers), "integer binary round trip");
            check(buffer.Position == buffer.Length && buffer.CanRead && buffer.CanWrite, "binary consumes record and leaves stream open");
        }
        using var doublesBuffer = new MemoryStream();
        using var doubleWriter = new BinaryWriter(doublesBuffer, Encoding.UTF8, true);
        ReadWrite.WriteToBinaryStream(new[] { 1d, -2.5 }, doubleWriter);
        check(doublesBuffer.ToArray().SequenceEqual(Convert.FromHexString("02000000000000000000F03F00000000000004C0")), "double binary golden bytes");
        foreach (double[] input in new[] { null, Array.Empty<double>(), new[] { -0d, double.PositiveInfinity, double.NegativeInfinity, BitConverter.Int64BitsToDouble(0x7ff8000000000042) } })
        {
            using var buffer = new MemoryStream();
            using var bw = new BinaryWriter(buffer, Encoding.UTF8, true);
            ReadWrite.WriteToBinaryStream(input, bw);
            using var br = new BinaryReader(new ShortReadStream(buffer.ToArray()));
            ReadWrite.ReadFromBinaryStream(out double[] result, br);
            check(input == null ? result == null : input.Select(BitConverter.DoubleToInt64Bits).SequenceEqual(result.Select(BitConverter.DoubleToInt64Bits)), "double binary bit preservation");
        }
        foreach (byte[] truncated in new[] { Array.Empty<byte>(), golden.Take(golden.Length-1).ToArray() })
        {
            bool rejected = false;
            try
            {
                using var br = new BinaryReader(new ShortReadStream(truncated));
                ReadWrite.ReadFromBinaryStream(out int[][] unused, br);
            }
            catch (EndOfStreamException) { rejected = true; }
            check(rejected, "truncated binary record rejected");
        }
    }
    private sealed class ShortReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, 1));
        public override int Read(Span<byte> buffer) => base.Read(buffer.Slice(0, Math.Min(buffer.Length, 1)));
    }
}

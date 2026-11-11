using CaeResults;
using System.IO.Compression;

internal static class HistoryStreamChecks
{
    private sealed class ChunkedFile : FileStream
    {
        public ChunkedFile(string path) : base(path, FileMode.Open, FileAccess.Read) { }
        public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, 1));
    }

    public static void Run(string directory, Action<bool, string> check)
    {
        string path = Path.Combine(directory, "history.bin");
        var entry = new HistoryResultEntries("node é", true) { Unit = "mm" };
        entry.Add(.5, -.001);
        entry.Add(1, .002);
        using (var file = File.Create(path))
            HistoryResultEntries.WriteToFileStream(entry, file, CompressionLevel.Optimal);
        using (var file = new ChunkedFile(path))
        {
            var restored = HistoryResultEntries.ReadFromFileStream(file, -1);
            check(restored.Name == entry.Name && restored.Local && restored.Unit == "mm" &&
                  restored.Time.SequenceEqual(entry.Time) && restored.Values.SequenceEqual(entry.Values) &&
                  restored.Count.SequenceEqual(entry.Count), "compressed history survives short reads");
            check(file.Position == file.Length, "short-read history consumes complete payload");
        }
        var bytes = File.ReadAllBytes(path);
        foreach (int length in new[] { 0, 2, 6, bytes.Length - 1 })
        {
            File.WriteAllBytes(path, bytes.Take(length).ToArray());
            bool rejected = false;
            try
            {
                using var file = new ChunkedFile(path);
                HistoryResultEntries.ReadFromFileStream(file, -1);
            }
            catch (EndOfStreamException) { rejected = true; }
            check(rejected, "truncated compressed history reports end of stream");
        }
    }
}

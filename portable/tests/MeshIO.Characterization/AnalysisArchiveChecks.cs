using System.IO.Compression;
using System.Text;
using CaeResults;
using PrePoMax.Persistence;

internal static class AnalysisArchiveChecks
{
    public static void Run(Action<bool, string> check)
    {
        string directory = Path.Combine(Path.GetTempPath(), "analysis è " + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "saved.pma");
            byte[] input = Encoding.UTF8.GetBytes(new StaticModelWorkflow(2).Export());
            byte[] frd = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "archive-cube.frd"));
            byte[] dat = Encoding.UTF8.GetBytes("\n displacements (vx,vy,vz) for set RIGHT and time  0.5000000E+00\n\n 2 1.000000E-03 0.000000E+00 -2.500000E-04\n\n");
            var source = new AnalysisSnapshot("Prova è", input, frd, dat);
            AnalysisArchive.Save(path, source);
            var restored = AnalysisArchive.Open(path);
            check(restored.Name == source.Name && restored.Input.SequenceEqual(input), "analysis archive preserves name and complete input");
            check(restored.Frd.SequenceEqual(frd) && restored.Dat.SequenceEqual(dat), "analysis archive preserves result bytes");
            var described = AnalysisArchive.Describe(path);
            check(described.Name == source.Name && described.Version == 1 &&
                  described.Payloads.SequenceEqual(new[] { "analysis.inp", "history.dat", "results.frd" }),
                  "archive descriptor validates layout without materializing payloads");
            using (var copied = new MemoryStream())
            {
                var streamed = AnalysisArchive.CopyPayloads(path, new Dictionary<string, Stream> { ["results.frd"] = copied });
                check(streamed.Name == described.Name && streamed.Version == described.Version &&
                      streamed.Payloads.SequenceEqual(described.Payloads) && copied.ToArray().SequenceEqual(frd),
                    "streaming archive validation copies only the requested payload");
            }
            using (var stop = new CancellationTokenSource())
            using (var copied = new CancelOnWriteStream(stop))
            {
                bool cancelled = false;
                try { AnalysisArchive.CopyPayloads(path, new Dictionary<string, Stream> { ["analysis.inp"] = copied }, stop.Token); }
                catch (OperationCanceledException) { cancelled = true; }
                check(cancelled && copied.Length > 0, "archive copying observes cancellation during payload streaming");
            }
            string resultPath = Path.Combine(directory, "restored.frd");
            File.WriteAllBytes(resultPath, restored.Frd);
            var results = FrdFileReader.Read(resultPath);
            check(results.Mesh.Nodes.Count == 8 && results.Mesh.Elements.Count == 1, "production FRD reader reopens archived result mesh");
            string historyPath = Path.Combine(directory, "restored.dat");
            File.WriteAllBytes(historyPath, restored.Dat);
            check(DatFileReader.Read(historyPath, out _) != null, "production DAT reader reopens archived history");
            AnalysisArchive.Save(path, source with { Frd = null, Dat = null });
            restored = AnalysisArchive.Open(path);
            check(restored.Frd == null && restored.Dat == null, "unsolved analysis archive omits results");
            byte[] before = File.ReadAllBytes(path);
            bool rejected = false;
            try { AnalysisArchive.Save(path, source with { Input = Array.Empty<byte>() }); }
            catch (InvalidDataException) { rejected = true; }
            check(rejected && File.ReadAllBytes(path).SequenceEqual(before), "invalid save preserves existing archive");
            rejected = false;
            try
            {
                AnalysisArchive.Save(path, source with
                {
                    ModelMetadata = Encoding.UTF8.GetBytes("{}"), GeometrySource = [1], GeometryFileName = "../source.step"
                });
            }
            catch (InvalidDataException) { rejected = true; }
            check(rejected && File.ReadAllBytes(path).SequenceEqual(before), "archive rejects unsafe retained geometry names");
            check(Directory.GetFiles(directory, "*.tmp").Length == 0, "archive save leaves no temporary files");

            string blocked = Path.Combine(directory, "destination-directory");
            Directory.CreateDirectory(blocked);
            rejected = false;
            try { AnalysisArchive.Save(blocked, source); }
            catch (IOException) { rejected = true; }
            check(rejected && Directory.Exists(blocked), "failed archive replacement preserves destination directory");
            check(Directory.GetFiles(directory, "*.tmp").Length == 0, "failed archive replacement cleans temporary file");

            void RejectMutation(Action<ZipArchive> mutate, string name)
            {
                AnalysisArchive.Save(path, source);
                using (var archive = ZipFile.Open(path, ZipArchiveMode.Update)) mutate(archive);
                bool failed = false, streamingFailed = false;
                try { AnalysisArchive.Open(path); }
                catch (InvalidDataException) { failed = true; }
                try { AnalysisArchive.CopyPayloads(path, new Dictionary<string, Stream>()); }
                catch (InvalidDataException) { streamingFailed = true; }
                check(failed && streamingFailed, name);
            }
            void Replace(ZipArchive archive, string entryName, string text)
            {
                archive.GetEntry(entryName)?.Delete();
                using var writer = new StreamWriter(archive.CreateEntry(entryName).Open());
                writer.Write(text);
            }
            RejectMutation(zip => Replace(zip, "analysis.inp", new string('x', input.Length)), "archive rejects payload corruption");
            RejectMutation(zip => {
                string text;
                using (var reader = new StreamReader(zip.GetEntry("manifest.json").Open())) text = reader.ReadToEnd();
                Replace(zip, "manifest.json", text.Replace("\"Version\": 1", "\"Version\": 2"));
            }, "archive rejects future schema versions");
            RejectMutation(zip => zip.GetEntry("analysis.inp").Delete(), "archive rejects missing input");
            RejectMutation(zip => { zip.GetEntry("history.dat").Delete(); zip.CreateEntry("../outside"); }, "archive rejects unexpected paths without extraction");
            RejectMutation(zip => { zip.GetEntry("history.dat").Delete(); zip.CreateEntry("analysis.inp"); }, "archive rejects duplicate entries");
            RejectMutation(zip => Replace(zip, "manifest.json", "{"), "archive rejects malformed metadata");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private sealed class CancelOnWriteStream(CancellationTokenSource stop) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            base.Write(buffer, offset, count);
            stop.Cancel();
        }
    }
}

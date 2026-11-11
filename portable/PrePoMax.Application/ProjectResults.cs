using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Diagnostics;
using CaeResults;
using CaeMesh;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

public sealed record ResultField(string Name, string Component, int Step, int Increment, float Time);
public sealed record ResultSample(int NodeId, float Value);
public sealed record ResultLoadTimings(double ArchiveMilliseconds, double DecodeMilliseconds);
public sealed record ResultFingerprint(ResultField Field, int ValueCount, string Units,
                                       float Minimum, int MinimumNodeId, float Maximum, int MaximumNodeId,
                                       double Mean, string Sha256, ResultSample[] Samples);
internal sealed record ResultValues(Dictionary<int, float> Values, string Units,
                                    Dictionary<int, double[]>? Displacements, FeMesh Mesh);

/// <summary>Static real-valued nodal fields from an editable archive's FRD or versioned binary result payload.</summary>
public static class ProjectResults
{
    public static ResultField[] List(string path)
    {
        var results = Load(path, default, out _);
        return results == null ? [] : Fields(results);
    }

    /// <summary>Deterministic node/value evidence for comparison with another result reader.</summary>
    public static ResultFingerprint Fingerprint(string path, ResultField selected)
        => Fingerprint(Values(path, selected), selected);

    internal static ResultFingerprint Fingerprint(ResultValues values, ResultField selected)
    {
        int[] ids = values.Values.Keys.Order().ToArray();
        if (ids.Length == 0) throw new InvalidDataException("The selected result field is empty.");
        float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
        int minimumId = 0, maximumId = 0;
        double sum = 0, correction = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> pair = stackalloc byte[8];
        foreach (int id in ids)
        {
            float value = values.Values[id];
            if (value < minimum) { minimum = value; minimumId = id; }
            if (value > maximum) { maximum = value; maximumId = id; }
            double adjusted = value - correction;
            double next = sum + adjusted;
            correction = (next - sum) - adjusted;
            sum = next;
            BinaryPrimitives.WriteInt32LittleEndian(pair, id);
            BinaryPrimitives.WriteInt32LittleEndian(pair[4..], BitConverter.SingleToInt32Bits(value));
            hash.AppendData(pair);
        }
        int[] sampleIndexes = [0, (ids.Length - 1) / 4, (ids.Length - 1) / 2,
                               3 * (ids.Length - 1) / 4, ids.Length - 1];
        var samples = sampleIndexes.Distinct().Select(index =>
            new ResultSample(ids[index], values.Values[ids[index]])).ToArray();
        return new(selected, ids.Length, values.Units, minimum, minimumId, maximum, maximumId,
            sum / ids.Length, Convert.ToHexString(hash.GetHashAndReset()), samples);
    }

    internal static ResultField[] Fields(FeResults results) => results.GetAllFieldData()
        .Where(data => data.StepType == StepTypeEnum.Static)
        .SelectMany(data => results.GetField(data, false).GetComponentNames()
            .Select(component => new ResultField(data.Name, component, data.StepId, data.StepIncrementId, data.Time)))
        .ToArray();

    internal static ResultValues Values(ReopenedStaticProject project, ResultField selected, bool displacement = false)
        => Values(Read(project), selected, displacement);

    internal static ResultValues Values(string path, ResultField selected, bool displacement = false)
        => Values(Load(path, default, out _) ?? throw new InvalidDataException("Project contains no results."), selected, displacement);

    internal static ResultValues Values(FeResults results, ResultField selected, bool displacement)
    {
        if (!Fields(results).Contains(selected)) throw new InvalidDataException("Unknown static result field or frame.");
        var data = results.GetFieldData(selected.Name, selected.Component, selected.Step, selected.Increment, true);
        int[] ids = results.Mesh.Nodes.Keys.ToArray();
        var values = results.GetValues(data, ids);
        if (values == null || values.Length != ids.Length || values.Any(value => !float.IsFinite(value)))
            throw new InvalidDataException("The selected field has missing or non-finite nodal values.");
        Dictionary<int, double[]>? vectors = null;
        if (displacement)
        {
            var catalog = Fields(results);
            var components = new List<float[]>();
            foreach (string component in new[] { "U1", "U2", "U3" })
            {
                var frame = new ResultField("DISP", component, selected.Step, selected.Increment, selected.Time);
                if (!catalog.Contains(frame)) throw new InvalidDataException("Selected frame has no complete displacement vector.");
                var vectorData = results.GetFieldData("DISP", component, selected.Step, selected.Increment, true);
                var valuesForComponent = results.GetValues(vectorData, ids);
                if (valuesForComponent == null || valuesForComponent.Length != ids.Length || valuesForComponent.Any(v => !float.IsFinite(v)))
                    throw new InvalidDataException("Displacement contains missing or non-finite values.");
                components.Add(valuesForComponent);
            }
            vectors = ids.Select((id, index) => (id, vector: components.Select(c => (double)c[index]).ToArray()))
                .ToDictionary(pair => pair.id, pair => pair.vector);
        }
        return new(ids.Select((id, index) => (id, value: values[index])).ToDictionary(x => x.id, x => x.value),
            results.GetFieldUnitAbbreviation(data), vectors, results.Mesh);
    }

    private static FeResults Read(ReopenedStaticProject project)
    {
        if (project.Frd == null)
        {
            if (project.BinaryResults == null) throw new InvalidDataException("Project contains no results.");
            return ReadBinary(project);
        }
        string directory = Path.Combine(Path.GetTempPath(), "prepomax-frd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "results.frd");
            File.WriteAllBytes(path, project.Frd);
            var results = FrdFileReader.Read(path) ?? throw new InvalidDataException("FRD results could not be read.");
            var mesh = project.Model.Mesh;
            if (results.Mesh.Nodes.Count != mesh.Nodes.Count || results.Mesh.Elements.Count != mesh.Elements.Count)
                throw new InvalidDataException("Result mesh does not match the editable model.");
            static bool Close(double a, double b) => double.IsFinite(a) && double.IsFinite(b) &&
                Math.Abs(a - b) <= 1e-5 * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b)));
            foreach (var node in mesh.Nodes.Values)
                if (!results.Mesh.Nodes.TryGetValue(node.Id, out var result) ||
                    !Close(node.X, result.X) || !Close(node.Y, result.Y) || !Close(node.Z, result.Z))
                    throw new InvalidDataException("Result node coordinates do not match the editable model.");
            foreach (var element in mesh.Elements.Values)
                if (!results.Mesh.Elements.TryGetValue(element.Id, out var result) ||
                    result.GetType() != element.GetType() || !result.NodeIds.SequenceEqual(element.NodeIds))
                    throw new InvalidDataException("Result connectivity does not match the editable model.");
            results.UnitSystem = project.Model.UnitSystem;
            return results;
        }
        finally { Directory.Delete(directory, true); }
    }

    static FeResults ReadBinary(ReopenedStaticProject project)
    {
        var payload = ResultPayload.Read(project.BinaryResults!);
        if (payload.ResultNames.Length != 1)
            throw new NotSupportedException("The portable viewer currently supports one binary result set per project.");
        string directory = Path.Combine(Path.GetTempPath(), "prepomax-results-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "results.bin");
            using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                output.Write(payload.BinaryData.Span);
            var collection = new ResultsCollection();
            foreach (string name in payload.ResultNames)
            {
                var resultMesh = new FeMesh(MeshRepresentation.Results);
                var result = new FeResults(name, project.Model.UnitSystem)
                {
                    HashName = name,
                    Mesh = resultMesh
                };
                collection.Add(name, result);
            }
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                ReadCollection(collection, input, payload.BinaryVersion);
            }
            return ValidateResult(collection, project.Model.UnitSystem);
        }
        finally { Directory.Delete(directory, true); }
    }

    internal static FeResults? Load(string path, CancellationToken cancellationToken, out ResultLoadTimings timings)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (AnalysisArchive.Describe(path).Payloads.Contains("results.bin", StringComparer.Ordinal))
            return ReadBinaryArchive(path, cancellationToken, out timings);
        var watch = Stopwatch.StartNew();
        var project = StaticProjectArchive.Open(path);
        double archiveMilliseconds = watch.Elapsed.TotalMilliseconds;
        cancellationToken.ThrowIfCancellationRequested();
        watch.Restart();
        var results = project.Frd == null && project.BinaryResults == null ? null : Read(project);
        cancellationToken.ThrowIfCancellationRequested();
        timings = new(archiveMilliseconds, watch.Elapsed.TotalMilliseconds);
        return results;
    }

    static FeResults ReadBinaryArchive(string archivePath, CancellationToken cancellationToken, out ResultLoadTimings timings)
    {
        string directory = Path.Combine(Path.GetTempPath(), "prepomax-results-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var watch = Stopwatch.StartNew();
            using var metadata = new MemoryStream();
            string resultPath = Path.Combine(directory, "results.bin");
            using var input = new FileStream(resultPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            var info = AnalysisArchive.CopyPayloads(archivePath, new Dictionary<string, Stream>(StringComparer.Ordinal)
            {
                ["model.json"] = metadata,
                ["results.bin"] = input
            }, cancellationToken);
            if (!info.Payloads.Contains("model.json", StringComparer.Ordinal) ||
                !info.Payloads.Contains("results.bin", StringComparer.Ordinal))
                throw new InvalidDataException("Binary results require editable model metadata.");
            var units = StaticModelMetadata.ReadUnitSystem(metadata.ToArray());
            double archiveMilliseconds = watch.Elapsed.TotalMilliseconds;
            cancellationToken.ThrowIfCancellationRequested();
            watch.Restart();
            input.Position = 0;
            ResultPayloadHeader header = ResultPayload.ReadHeader(input, input.Length);
            if (header.ResultNames.Length != 1)
                throw new NotSupportedException("The portable viewer currently supports one binary result set per project.");
            var collection = CreateCollection(header.ResultNames, units);
            ReadCollection(collection, input, header.BinaryVersion);
            var results = ValidateResult(collection, units);
            cancellationToken.ThrowIfCancellationRequested();
            timings = new(archiveMilliseconds, watch.Elapsed.TotalMilliseconds);
            return results;
        }
        finally { Directory.Delete(directory, true); }
    }

    internal static FeResults ReadBinaryCollection(string path, ResultPayloadHeader header, CaeGlobals.UnitSystem units)
    {
        var collection = CreateCollection(header.ResultNames, units);
        using var input = File.OpenRead(path);
        ReadCollection(collection, input, header.BinaryVersion);
        return ValidateResult(collection, units);
    }

    static ResultsCollection CreateCollection(string[] names, CaeGlobals.UnitSystem units)
    {
        var collection = new ResultsCollection();
        foreach (string name in names)
        {
            var result = new FeResults(name, units) { HashName = name, Mesh = new FeMesh(MeshRepresentation.Results) };
            collection.Add(name, result);
        }
        return collection;
    }

    static void ReadCollection(ResultsCollection collection, FileStream input, int version)
    {
        if (!ResultsCollection.ReadFromFileStream(collection, input, version))
            throw new InvalidDataException("Binary result metadata does not match its payload.");
        if (input.Position != input.Length) throw new InvalidDataException("Trailing binary result data.");
    }

    static FeResults ValidateResult(ResultsCollection collection, CaeGlobals.UnitSystem units)
    {
        FeResults results = collection.CurrentResult ?? throw new InvalidDataException("Binary result payload is empty.");
        if (results.Mesh.Nodes.Count == 0 || results.Mesh.Nodes.Count > 10_000_000 ||
            results.Mesh.Elements.Count == 0 || results.Mesh.Elements.Count > 10_000_000 ||
            results.Mesh.Parts.Count == 0 || results.Mesh.Parts.Count > 100_000)
            throw new InvalidDataException("Invalid binary result mesh size.");
        foreach (var element in results.Mesh.Elements.Values)
            if (element.NodeIds.Length is < 2 or > 32 || element.NodeIds.Any(id => !results.Mesh.Nodes.ContainsKey(id)))
                throw new InvalidDataException("Binary result element has invalid connectivity.");
        results.UnitSystem = units;
        return results;
    }
}

using System.IO.Compression;
using System.Formats.Nrbf;
using System.Text;
using System.Text.Json;

bool convert = args.Length == 3 && args[1] == "--convert";
bool inspectResults = args.Length == 2 && args[1] == "--results";
if (!convert && (args.Length is < 1 or > 2 || (args.Length == 2 && args[1] is not ("--mesh" or "--results"))))
{
    Console.Error.WriteLine("Usage: PrePoMax.PmxInspect PROJECT.pmx [--mesh | --results | --convert OUTPUT.pma]");
    return 2;
}
try
{
    if (convert && Path.GetFullPath(args[0]) == Path.GetFullPath(args[2]))
        throw new InvalidDataException("Source and destination paths must differ.");
    if (convert && File.Exists(args[2]))
        throw new IOException("Conversion destination already exists.");
    using var file = File.OpenRead(args[0]);
    using var reader = new BinaryReader(file, Encoding.ASCII, leaveOpen: true);
    byte[] header = new byte[32];
    file.ReadExactly(header);
    string version = Encoding.ASCII.GetString(header).TrimEnd('\0');
    if (!version.StartsWith("PrePoMax v", StringComparison.Ordinal) ||
        !Version.TryParse(version[10..].Split(' ')[0], out var parsed) || parsed < new Version(2, 0, 6))
        throw new InvalidDataException("Only unprotected compressed PMX version 2.0.6 or newer is supported.");
    int compressedLength = reader.ReadInt32();
    if (compressedLength <= 0 || compressedLength > 256 * 1024 * 1024 || compressedLength > file.Length - file.Position)
        throw new InvalidDataException("Invalid model block length or compressed model exceeds 256 MiB.");
    // Only the declared model block is exposed to the decompressor, never results.
    byte[] compressed = new byte[compressedLength];
    file.ReadExactly(compressed);
    using var block = new MemoryStream(compressed, writable: false);
    using var inflate = new DeflateStream(block, CompressionMode.Decompress);
    using var inflatedReader = new BinaryReader(inflate);
    long recordLength = inflatedReader.ReadInt64();
    if (recordLength <= 0 || recordLength > 128 * 1024 * 1024)
        throw new InvalidDataException("Invalid serialized record length or records exceed 128 MiB.");
    byte[] payload = new byte[(int)recordLength];
    inflate.ReadExactly(payload);
    using var recordsStream = new MemoryStream(payload, writable: false);
    var root = NrbfDecoder.Decode(recordsStream, out var records, leaveOpen: true);
    if (recordsStream.Position != recordsStream.Length)
        throw new InvalidDataException("Trailing bytes in serialized record section.");
    var classes = records.Values.OfType<ClassRecord>()
        .GroupBy(record => record.TypeName.AssemblyQualifiedName)
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .Select(group => new { type = group.Key, count = group.Count(),
            members = group.SelectMany(record => record.MemberNames).Distinct().Order(StringComparer.Ordinal).ToArray() }).ToArray();
    ClassRecord? controller = null;
    ClassRecord? currentModel = null;
    if (root is SZArrayRecord<SerializationRecord> roots && roots.Length <= 16 &&
        roots.GetArray().FirstOrDefault() is ClassRecord rootController &&
        rootController.TypeName.FullName == "PrePoMax.Controller" && rootController.HasMember("_model"))
    {
        controller = rootController;
        currentModel = controller.GetClassRecord("_model");
    }
    if (convert)
    {
        if (currentModel == null || controller == null) throw new InvalidDataException("Current model/controller metadata was not found.");
        long resultsOffset = file.Position;
        var converted = LegacyPmxConverter.Convert(currentModel, inflatedReader, parsed);
        string[] resultNames = LegacyPmxResultsReader.ExpectedResultNames(controller);
        byte[]? binaryResults = null;
        if (file.Length != resultsOffset)
        {
            file.Position = resultsOffset;
            LegacyPmxResultsReader.Read(file, controller, parsed);
            if (resultNames.Length != 0)
            {
                file.Position = resultsOffset;
                binaryResults = PrePoMax.Persistence.ResultPayload.Create(
                    parsed.Major * 1_000_000 + parsed.Minor * 1000 + Math.Max(parsed.Build, 0),
                    resultNames, file, file.Length - resultsOffset);
            }
        }
        else if (resultNames.Length != 0)
            throw new InvalidDataException("Result metadata exists but the results tail is missing.");
        string destination = Path.GetFullPath(args[2]);
        string staging = Path.Combine(Path.GetDirectoryName(destination)!, "." + Path.GetFileName(destination) + "." + Guid.NewGuid() + ".convert");
        PrePoMax.Persistence.ReopenedStaticProject reopened;
        try
        {
            PrePoMax.Persistence.StaticProjectArchive.Save(staging, converted, binaryResults: binaryResults);
            reopened = PrePoMax.Persistence.StaticProjectArchive.Open(staging);
            File.Move(staging, destination, overwrite: false);
        }
        finally { if (File.Exists(staging)) File.Delete(staging); }
        Console.WriteLine(JsonSerializer.Serialize(new {
            status = "converted-analysis-model-slice",
            source = Path.GetFullPath(args[0]), destination,
            nodes = reopened.Model.Mesh.Nodes.Count, elements = reopened.Model.Mesh.Elements.Count,
            parts = reopened.Model.Mesh.Parts.Count, nodeSets = reopened.Model.Mesh.NodeSets.Count,
            elementSets = reopened.Model.Mesh.ElementSets.Count, surfaces = reopened.Model.Mesh.Surfaces.Count,
            materials = reopened.Model.Materials.Count, sections = reopened.Model.Sections.Count,
            referencePoints = reopened.Model.Mesh.ReferencePoints.Count, amplitudes = reopened.Model.Amplitudes.Count,
            cylinders = reopened.Model.Cylinders.Count, surfaceInteractions = reopened.Model.SurfaceInteractions.Count,
            contactPairs = reopened.Model.ContactPairs.Count, constraints = reopened.Model.Constraints.Count,
            steps = reopened.Model.StepCollection.StepsList.Count,
            binaryResultSets = resultNames.Length, hasBinaryResults = reopened.BinaryResults != null,
            scope = "Fail-closed conversion of the supported 3D analysis model and validated versioned result stream. Empty legacy element sets are omitted and duplicate memberships normalized. Geometry, command/UI state, formulas and unsupported definitions remain outside this slice."
        }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    object? modelBinary = null;
    object? resultsInventory = null;
    var meshBlockers = new List<ConversionFinding>();
    if (args.Length == 2 && args[1] == "--mesh")
    {
        var meshInventory = new MeshInventory(inflatedReader, parsed);
        modelBinary = meshInventory.Read(currentModel);
        meshBlockers.AddRange(meshInventory.ConversionBlockers);
    }
    if (inspectResults)
    {
        if (controller == null) throw new InvalidDataException("Current controller metadata was not found.");
        resultsInventory = LegacyPmxResultsReader.Read(file, controller, parsed);
    }
    Console.WriteLine(JsonSerializer.Serialize(new {
        format = version, fileBytes = file.Length, compressedModelBytes = compressedLength,
        serializedBytes = recordLength, remainingResultsAreaBytes = file.Length - 36 - compressedLength,
        rootRecord = root.RecordType.ToString(), recordCount = records.Count,
        activeModel = ActiveModelInventory.Inspect(root),
        userKeywords = currentModel == null ? null : UserKeywordInventory.Inspect(currentModel),
        coordinateSystems = currentModel == null ? null : CoordinateSystemInventory.Inspect(currentModel),
        regionMetadata = currentModel == null ? null : RegionInventory.Inspect(currentModel),
        conversionCapability = ConversionCapability.Inspect(currentModel, meshBlockers,
            args.Length == 2 && args[1] == "--mesh"),
        modelBinary, resultsInventory,
        scope = inspectResults
            ? "Read-only inventory with bounded, version-aware result field/history decoding; result mesh is integrity-checked without materialization; no conversion performed."
            : "Read-only inventory; optional mesh structural checks, results not validated; no conversion performed.", classes
    }, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or NotSupportedException or
    System.Runtime.Serialization.SerializationException or KeyNotFoundException or InvalidOperationException or CaeGlobals.CaeException)
{
    Console.Error.WriteLine("PMX inspection failed: " + ex.Message);
    return 1;
}

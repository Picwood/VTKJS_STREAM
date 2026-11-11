using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrePoMax.Persistence;

/// <summary>A solver snapshot, not editable FeModel or legacy PMX persistence.</summary>
public sealed record AnalysisSnapshot(string Name, byte[] Input, byte[]? Frd = null, byte[]? Dat = null,
    byte[]? ModelMetadata = null, byte[]? ModelMesh = null, byte[]? BinaryResults = null,
    byte[]? GeometrySource = null, string? GeometryFileName = null, byte[]? GeometryTopology = null,
    byte[]? MaterialIdentities = null);
public sealed record AnalysisArchiveInfo(string Name, int Version, string[] Payloads);

public static class AnalysisArchive
{
    public const long MaxPayloadBytes = 1536L * 1024 * 1024;
    private const string Format = "prepomax-analysis-snapshot";
    private static readonly string[] PayloadNames = ["analysis.inp", "results.frd", "history.dat", "model.json", "model.mesh", "results.bin", "geometry.source", "geometry.topology.json", "material-identities.json"];
    private sealed record Payload(string Name, long Length, string Sha256);
    private sealed record Manifest(string Format, int Version, string Name, Payload[] Payloads, string? GeometryFileName = null);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };

    public static void Save(string path, AnalysisSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (string.IsNullOrWhiteSpace(snapshot.Name) || snapshot.Name.Length > 1024)
            throw new ArgumentException("An analysis name of 1–1024 characters is required.", nameof(snapshot));
        ArgumentNullException.ThrowIfNull(snapshot.Input);
        // Own payloads throughout hashing/writing, independently of subsequent caller edits.
        if (snapshot.ModelMesh != null && snapshot.ModelMetadata == null)
            throw new InvalidDataException("External model mesh requires model metadata.");
        if (snapshot.BinaryResults != null)
        {
            if (snapshot.ModelMetadata == null) throw new InvalidDataException("Binary results require model metadata.");
            ResultPayload.Read(snapshot.BinaryResults);
        }
        if ((snapshot.GeometrySource == null) != (snapshot.GeometryFileName == null) ||
            snapshot.GeometrySource != null && snapshot.ModelMetadata == null)
            throw new InvalidDataException("Geometry source requires editable model metadata and a file name.");
        if (snapshot.GeometrySource != null)
        {
            ValidateGeometryFileName(snapshot.GeometryFileName!);
            if (snapshot.GeometrySource.LongLength is 0 or > 128L * 1024 * 1024)
                throw new InvalidDataException("Geometry source must be nonempty and at most 128 MiB.");
        }
        if (snapshot.GeometryTopology != null)
        {
            if (snapshot.GeometrySource == null) throw new InvalidDataException("CAD topology requires retained geometry.");

        }
        if (snapshot.MaterialIdentities != null && (snapshot.ModelMetadata == null || snapshot.MaterialIdentities.Length > ProjectMaterialIdentities.MaximumBytes))
            throw new InvalidDataException("Material identities require bounded editable metadata.");
        var values = new[] { snapshot.Input, snapshot.Frd, snapshot.Dat, snapshot.ModelMetadata, snapshot.ModelMesh,
            snapshot.BinaryResults, snapshot.GeometrySource, snapshot.GeometryTopology, snapshot.MaterialIdentities };
        long total = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] is not { } bytes) continue;
            total = checked(total + bytes.LongLength);
            if (bytes.Length == 0 || total > MaxPayloadBytes) throw new InvalidDataException("Invalid archive payload size.");
            values[i] = bytes.ToArray();
        }
        if (values[7] != null) CadTopologyCodec.Deserialize(values[7]!, values[6]!);
        var payloads = values.Select((bytes, i) => bytes == null ? null :
            new Payload(PayloadNames[i], bytes.LongLength, Convert.ToHexString(SHA256.HashData(bytes))))
            .OfType<Payload>().ToArray();
        var manifest = new Manifest(Format, snapshot.MaterialIdentities != null ? 7 : snapshot.GeometryTopology != null ? 6 : snapshot.GeometrySource != null ? 5 : snapshot.BinaryResults != null ? 4 :
            snapshot.ModelMesh != null ? 3 : snapshot.ModelMetadata != null ? 2 : 1, snapshot.Name, payloads,
            snapshot.GeometryFileName);
        string destination = Path.GetFullPath(path);
        string temporary = Path.Combine(Path.GetDirectoryName(destination)!, "." + Path.GetFileName(destination) + "." + Guid.NewGuid() + ".tmp");
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var zip = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true))
                {
                    using (var stream = zip.CreateEntry("manifest.json").Open())
                        JsonSerializer.Serialize(stream, manifest, JsonOptions);
                    for (int i = 0; i < values.Length; i++)
                        if (values[i] is { } bytes)
                        {
                            using var stream = zip.CreateEntry(PayloadNames[i], PayloadNames[i] == "results.bin"
                                ? CompressionLevel.NoCompression : CompressionLevel.Optimal).Open();
                            stream.Write(bytes);
                        }
                }
                file.Flush(flushToDisk: true);
            }
            File.Move(temporary, destination, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static AnalysisSnapshot Open(string path)
    {
        using var file = File.OpenRead(path);
        using var zip = new ZipArchive(file, ZipArchiveMode.Read);
        Manifest manifest = ReadManifest(zip, out var entries);
        var data = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        long total = 0;
        foreach (var payload in manifest.Payloads)
        {
            ZipArchiveEntry entry = ValidatePayload(payload, entries, data.Keys, ref total);
            byte[] bytes = ReadBounded(entry, payload.Length);
            if (Convert.ToHexString(SHA256.HashData(bytes)) != payload.Sha256)
                throw new InvalidDataException("Payload checksum mismatch.");
            data.Add(payload.Name, bytes);
        }
        ValidateComplete(entries, data.Keys);
        byte[]? binaryResults = data.GetValueOrDefault("results.bin");
        if (binaryResults != null) ResultPayload.Read(binaryResults);
        byte[]? geometryTopology = data.GetValueOrDefault("geometry.topology.json");
        if (geometryTopology != null) CadTopologyCodec.Deserialize(geometryTopology, data["geometry.source"]);
        return new AnalysisSnapshot(manifest.Name, data["analysis.inp"], data.GetValueOrDefault("results.frd"),
            data.GetValueOrDefault("history.dat"), data.GetValueOrDefault("model.json"), data.GetValueOrDefault("model.mesh"),
            binaryResults, data.GetValueOrDefault("geometry.source"), manifest.GeometryFileName, geometryTopology,
            data.GetValueOrDefault("material-identities.json"));
    }

    public static AnalysisArchiveInfo Describe(string path)
    {
        using var file = File.OpenRead(path);
        using var zip = new ZipArchive(file, ZipArchiveMode.Read);
        Manifest manifest = ReadManifest(zip, out var entries);
        long total = 0;
        var declared = new HashSet<string>(StringComparer.Ordinal);
        foreach (var payload in manifest.Payloads)
        {
            ValidatePayload(payload, entries, declared, ref total);
            declared.Add(payload.Name);
        }
        ValidateComplete(entries, declared);
        return new(manifest.Name, manifest.Version, declared.Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>Validates every payload while copying only requested entries to caller-owned streams.</summary>
    public static AnalysisArchiveInfo CopyPayloads(string path, IReadOnlyDictionary<string, Stream> destinations,
                                                  CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(destinations);
        if (destinations.Any(pair => !PayloadNames.Contains(pair.Key) || pair.Value == null || !pair.Value.CanWrite))
            throw new ArgumentException("Payload destinations must be named writable streams.", nameof(destinations));
        using var file = File.OpenRead(path);
        using var zip = new ZipArchive(file, ZipArchiveMode.Read);
        Manifest manifest = ReadManifest(zip, out var entries);
        long total = 0;
        var declared = new HashSet<string>(StringComparer.Ordinal);
        byte[] buffer = new byte[128 * 1024];
        foreach (var payload in manifest.Payloads)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ZipArchiveEntry entry = ValidatePayload(payload, entries, declared, ref total);
            declared.Add(payload.Name);
            using var source = entry.Open();
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            destinations.TryGetValue(payload.Name, out var destination);
            long remaining = payload.Length;
            while (remaining != 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                if (read == 0) throw new EndOfStreamException("Truncated archive payload.");
                hash.AppendData(buffer, 0, read);
                destination?.Write(buffer, 0, read);
                remaining -= read;
            }
            if (source.ReadByte() != -1) throw new InvalidDataException("Archive entry length mismatch.");
            if (Convert.ToHexString(hash.GetHashAndReset()) != payload.Sha256)
                throw new InvalidDataException("Payload checksum mismatch.");
        }
        ValidateComplete(entries, declared);
        cancellationToken.ThrowIfCancellationRequested();
        return new(manifest.Name, manifest.Version, declared.Order(StringComparer.Ordinal).ToArray());
    }

    private static Manifest ReadManifest(ZipArchive zip, out Dictionary<string, ZipArchiveEntry> entries)
    {
        if (zip.Entries.Count < 2 || zip.Entries.Count > PayloadNames.Length + 1) throw new InvalidDataException("Unexpected archive entries.");
        entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName != "manifest.json" && !PayloadNames.Contains(entry.FullName))
                throw new InvalidDataException("Unknown archive entry.");
            if (!entries.TryAdd(entry.FullName, entry)) throw new InvalidDataException("Duplicate archive entry.");
        }
        if (!entries.TryGetValue("manifest.json", out var manifestEntry)) throw new InvalidDataException("Missing manifest.");
        Manifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<Manifest>(ReadBounded(manifestEntry, 16384), JsonOptions)
                ?? throw new InvalidDataException("Empty manifest.");
        }
        catch (JsonException ex) { throw new InvalidDataException("Invalid manifest.", ex); }
        if (manifest.Format != Format || manifest.Version is < 1 or > 7) throw new InvalidDataException("Unsupported archive format/version.");
        if (string.IsNullOrWhiteSpace(manifest.Name) || manifest.Name.Length > 1024 || manifest.Payloads == null)
            throw new InvalidDataException("Missing analysis metadata.");
        bool legacyShape = manifest.Version < 5 && !entries.ContainsKey("geometry.topology.json") &&
            (manifest.Version >= 2) == entries.ContainsKey("model.json") &&
            (manifest.Version >= 3) == entries.ContainsKey("model.mesh") &&
            (manifest.Version == 4) == entries.ContainsKey("results.bin") &&
            !entries.ContainsKey("geometry.source") && manifest.GeometryFileName == null;
        bool geometryShape = manifest.Version is 5 or 6 &&
            (manifest.Version == 6) == entries.ContainsKey("geometry.topology.json") && entries.ContainsKey("model.json") &&
            entries.ContainsKey("geometry.source") && manifest.GeometryFileName != null;
        bool identityShape = manifest.Version == 7 && entries.ContainsKey("material-identities.json") && entries.ContainsKey("model.json") &&
            entries.ContainsKey("geometry.source") == (manifest.GeometryFileName != null) &&
            (!entries.ContainsKey("geometry.topology.json") || entries.ContainsKey("geometry.source"));
        if (manifest.Version < 7 && entries.ContainsKey("material-identities.json") || !legacyShape && !geometryShape && !identityShape)
            throw new InvalidDataException("Model metadata does not match archive version.");
        if (manifest.GeometryFileName != null) ValidateGeometryFileName(manifest.GeometryFileName);
        return manifest;
    }

    private static ZipArchiveEntry ValidatePayload(Payload payload, Dictionary<string, ZipArchiveEntry> entries,
                                                   IEnumerable<string> declared, ref long total)
    {
        if (payload == null || !PayloadNames.Contains(payload.Name) || declared.Contains(payload.Name, StringComparer.Ordinal) ||
            !entries.TryGetValue(payload.Name, out var entry) || payload.Length <= 0 || payload.Length != entry.Length)
            throw new InvalidDataException("Invalid payload declaration.");
        if (payload.Name == "geometry.source" && payload.Length > 128L * 1024 * 1024)
            throw new InvalidDataException("Geometry source exceeds size limit.");
        if (payload.Name == "geometry.topology.json" && payload.Length > CadTopologyCodec.MaximumBytes)
            throw new InvalidDataException("CAD topology exceeds size limit.");
        if (payload.Name == "material-identities.json" && payload.Length > ProjectMaterialIdentities.MaximumBytes)
            throw new InvalidDataException("Material identity payload exceeds size limit.");
        if (payload.Length > MaxPayloadBytes - total) throw new InvalidDataException("Archive exceeds payload limit.");
        total += payload.Length;
        return entry;
    }

    private static void ValidateComplete(Dictionary<string, ZipArchiveEntry> entries, IEnumerable<string> declared)
    {
        var names = declared.ToHashSet(StringComparer.Ordinal);
        if (!names.Contains("analysis.inp") || names.Count != entries.Count - 1)
            throw new InvalidDataException("Missing input or undeclared payload.");
    }

    private static byte[] ReadBounded(ZipArchiveEntry entry, long limit)
    {
        if (entry.Length < 0 || entry.Length > limit) throw new InvalidDataException("Archive entry exceeds limit.");
        byte[] data = new byte[checked((int)entry.Length)];
        using var stream = entry.Open();
        stream.ReadExactly(data);
        if (stream.ReadByte() != -1) throw new InvalidDataException("Archive entry length mismatch.");
        return data;
    }

    private static void ValidateGeometryFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name != Path.GetFileName(name) ||
            name.IndexOfAny(['/', '\\']) >= 0 || name.Any(char.IsControl))
            throw new InvalidDataException("Invalid geometry source file name.");
    }
}

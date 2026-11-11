using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CaeModel;

namespace PrePoMax.Persistence;

public sealed record ProjectMaterialIdentity(Guid Id, string Name);

/// <summary>Versioned, bounded material identities. They identify project entities, not owners.</summary>
public static class ProjectMaterialIdentities
{
    public const int MaximumBytes = 2 * 1024 * 1024;
    private sealed record Document(int Version, ProjectMaterialIdentity[] Materials);
    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    public static ProjectMaterialIdentity[] Reconcile(FeModel model, ProjectMaterialIdentity[] previous)
    {
        Validate(previous);
        var names = previous.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        return model.Materials.Values.Select(material => names.TryGetValue(material.Name, out var old)
            ? old with { Name = material.Name } : new ProjectMaterialIdentity(Guid.NewGuid(), material.Name)).ToArray();
    }

    public static byte[] Serialize(ProjectMaterialIdentity[] identities, FeModel model)
    {
        ValidateModel(identities, model);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new Document(1, identities), Options);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Material identity payload exceeds its size limit.");
        return bytes;
    }

    public static ProjectMaterialIdentity[] Deserialize(byte[] bytes, FeModel model)
    {
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Material identity payload exceeds its size limit.");
        try
        {
            var document = JsonSerializer.Deserialize<Document>(bytes, Options);
            if (document?.Version != 1) throw new InvalidDataException("Unsupported material identity version.");
            ValidateModel(document.Materials, model);
            return document.Materials;
        }
        catch (JsonException error) { throw new InvalidDataException("Invalid material identities.", error); }
    }

    // Deterministic migration for legacy archives: repeated reads, copies and result-only
    // attachments resolve the same IDs. The first edit persists them in the v7 archive.
    internal static ProjectMaterialIdentity[] FromLegacy(byte[] metadata, byte[]? mesh, FeModel model)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(metadata);
        if (mesh != null) hash.AppendData(mesh);
        byte[] seed = hash.GetHashAndReset();
        return model.Materials.Values.Select(material =>
        {
            hash.AppendData(seed);
            hash.AppendData(Encoding.UTF8.GetBytes(material.Name));
            return new ProjectMaterialIdentity(new Guid(hash.GetHashAndReset().AsSpan(0, 16)), material.Name);
        }).ToArray();
    }

    private static void ValidateModel(ProjectMaterialIdentity[] identities, FeModel model)
    {
        Validate(identities);
        var names = model.Materials.Values.Select(item => item.Name).ToHashSet(StringComparer.Ordinal);
        if (identities.Length != names.Count || identities.Any(item => !names.Contains(item.Name)))
            throw new InvalidDataException("Material identities do not match the model.");
    }

    private static void Validate(ProjectMaterialIdentity[] identities)
    {
        if (identities == null || identities.Length > 10000 || identities.Any(item => item == null || item.Id == Guid.Empty ||
            string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 1024) ||
            identities.Select(item => item.Id).Distinct().Count() != identities.Length ||
            identities.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != identities.Length)
            throw new InvalidDataException("Material identities must have unique, nonempty IDs and names.");
    }
}

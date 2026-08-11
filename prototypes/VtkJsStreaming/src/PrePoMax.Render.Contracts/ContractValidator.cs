using System.Globalization;

namespace PrePoMax.Render.Contracts;

public sealed class ContractValidationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public static class ContractValidator
{
    private static readonly HashSet<string> Layers = ["base", "selection", "overlay"];
    private static readonly HashSet<string> Representations = ["surface", "wire", "points", "surface-with-edges"];

    public static long CalculateByteLength(string dataType, int components, long elementCount)
    {
        if (components <= 0 || elementCount < 0)
        {
            throw new ContractValidationException("resource-length-invalid", "Components must be positive and element count cannot be negative.");
        }

        try
        {
            return checked(elementCount * components * RenderDataTypes.SizeOf(dataType));
        }
        catch (OverflowException exception)
        {
            throw new ContractValidationException("resource-length-invalid", $"Resource byte length overflows Int64: {exception.Message}");
        }
    }

    public static void Validate(ResourceDescriptor descriptor)
    {
        ValidateOpaqueId(descriptor.ResourceId, nameof(descriptor.ResourceId));
        if (descriptor.Components <= 0 || descriptor.ElementCount < 0 || descriptor.Version <= 0)
        {
            throw new ContractValidationException("resource-descriptor-invalid", "Resource counts and version are invalid.");
        }

        if (descriptor.ByteOrder != "little-endian" || descriptor.ContentEncoding != "identity")
        {
            throw new ContractValidationException("resource-descriptor-invalid", "V1 resources must be little-endian and identity encoded.");
        }

        if (descriptor.ByteLength != CalculateByteLength(descriptor.DataType, descriptor.Components, descriptor.ElementCount))
        {
            throw new ContractValidationException("resource-length-invalid", "Descriptor byte length does not match its schema.");
        }

        if (descriptor.Sha256.Length != 64 || descriptor.Sha256.Any(character => !Uri.IsHexDigit(character) || char.IsUpper(character)))
        {
            throw new ContractValidationException("resource-hash-invalid", "SHA-256 must be 64 lowercase hexadecimal characters.");
        }
    }

    public static void Validate(SceneManifest manifest)
    {
        if (manifest.Protocol != new ProtocolVersion(ProtocolConstants.Major, ProtocolConstants.Minor))
        {
            throw new ContractValidationException("protocol-version-unsupported", "Only protocol 1.0 is supported.");
        }

        ValidateOpaqueId(manifest.SceneId, nameof(manifest.SceneId));
        ValidateVector(manifest.WorldOrigin, 3, nameof(manifest.WorldOrigin));
        ValidateBounds(manifest.Bounds, nameof(manifest.Bounds));

        var resources = manifest.Resources.ToDictionary(resource => resource.ResourceId, StringComparer.Ordinal);
        foreach (var resource in manifest.Resources)
        {
            Validate(resource);
        }

        foreach (var actor in manifest.Actors)
        {
            ValidateOpaqueId(actor.ActorId, nameof(actor.ActorId));
            if (actor.ParentActorId is not null)
            {
                ValidateOpaqueId(actor.ParentActorId, nameof(actor.ParentActorId));
            }

            if (!Layers.Contains(actor.Layer) || !Representations.Contains(actor.Representation))
            {
                throw new ContractValidationException("actor-invalid", "Actor layer or representation is unsupported.");
            }

            ValidateBounds(actor.Bounds, $"actor {actor.ActorId} bounds");
            foreach (var chunk in actor.Chunks)
            {
                ValidateChunk(chunk, resources);
            }
        }
    }

    public static void ValidateOpaqueId(string value, string fieldName)
    {
        if (!Guid.TryParseExact(value, "D", out var parsed) || parsed.ToString("D", CultureInfo.InvariantCulture) != value)
        {
            throw new ContractValidationException("identifier-invalid", $"{fieldName} must be a lowercase canonical UUID.");
        }
    }

    private static void ValidateChunk(RenderChunk chunk, IReadOnlyDictionary<string, ResourceDescriptor> resources)
    {
        ValidateOpaqueId(chunk.ChunkId, nameof(chunk.ChunkId));
        ValidateBounds(chunk.Bounds, $"chunk {chunk.ChunkId} bounds");
        if (chunk.PointCount < 0 || chunk.TriangleCount < 0)
        {
            throw new ContractValidationException("chunk-invalid", "Chunk counts cannot be negative.");
        }

        RequireResource(chunk.Resources.Positions, "positions", RenderDataTypes.Float32, 3, chunk.PointCount, resources);
        RequireResource(chunk.Resources.TriangleConnectivity, "triangleConnectivity", RenderDataTypes.UInt32, 3, chunk.TriangleCount, resources);
        RequireResource(chunk.Resources.SourcePointIds, "sourcePointIds", RenderDataTypes.Int32, 1, chunk.PointCount, resources);
        RequireResource(chunk.Resources.SourceElementIds, "sourceElementIds", RenderDataTypes.Int32, 1, chunk.TriangleCount, resources);
        RequireResource(chunk.Resources.PointScalars, "pointScalars", RenderDataTypes.Float32, 1, chunk.PointCount, resources);
        if (chunk.Resources.Normals is not null)
        {
            RequireResource(chunk.Resources.Normals, "normals", RenderDataTypes.Float32, 3, chunk.PointCount, resources);
        }
    }

    private static void RequireResource(ResourceReference reference, string semantic, string dataType, int components, long count, IReadOnlyDictionary<string, ResourceDescriptor> resources)
    {
        if (!resources.TryGetValue(reference.ResourceId, out var descriptor))
        {
            throw new ContractValidationException("resource-not-found", $"Chunk references missing resource {reference.ResourceId}.");
        }

        if (descriptor.Semantic != semantic || descriptor.DataType != dataType || descriptor.Components != components || descriptor.ElementCount != count)
        {
            throw new ContractValidationException("resource-descriptor-invalid", $"Resource {reference.ResourceId} does not match its {semantic} use.");
        }
    }

    private static void ValidateBounds(IReadOnlyList<double> values, string name)
    {
        ValidateVector(values, 6, name);
        for (var index = 0; index < 6; index += 2)
        {
            if (values[index] > values[index + 1])
            {
                throw new ContractValidationException("bounds-invalid", $"{name} minimum exceeds maximum.");
            }
        }
    }

    private static void ValidateVector(IReadOnlyList<double> values, int count, string name)
    {
        if (values.Count != count || values.Any(value => !double.IsFinite(value)))
        {
            throw new ContractValidationException("numeric-value-invalid", $"{name} must contain {count} finite values.");
        }
    }
}

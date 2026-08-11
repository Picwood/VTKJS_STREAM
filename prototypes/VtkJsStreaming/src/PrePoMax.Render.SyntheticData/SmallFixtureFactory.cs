using System.Security.Cryptography;
using PrePoMax.Render.Contracts;

namespace PrePoMax.Render.SyntheticData;

public static class SmallFixtureFactory
{
    public const string SceneId = "10000000-0000-0000-0000-000000000001";
    public const string FrontActorId = "20000000-0000-0000-0000-000000000001";
    public const string BackActorId = "20000000-0000-0000-0000-000000000002";

    private static readonly double[] Origin = [1_234_567.0, 2_345_678.0, 3_456_789.0];

    public static SyntheticScene Create() => CreateVersion(alternateField: false);

    public static SyntheticScene CreateAlternateField() => CreateVersion(alternateField: true);

    private static SyntheticScene CreateVersion(bool alternateField)
    {
        var resources = new Dictionary<string, IRenderResource>(StringComparer.Ordinal);
        var front = CreateActor(
            actorId: FrontActorId,
            chunkId: "30000000-0000-0000-0000-000000000001",
            resourcePrefix: 1,
            name: "Front triangle",
            z: 0.25f,
            sourcePointIds: [101, 102, 103],
            sourceElementId: 1001,
            scalarResourceSuffix: alternateField ? 11 : 5,
            fieldVersion: alternateField ? 2 : 1,
            scalars: alternateField ? [1.0f, 0.0f, -1.0f] : [-1.0f, 0.0f, 1.0f],
            resources);
        var back = CreateActor(
            actorId: BackActorId,
            chunkId: "30000000-0000-0000-0000-000000000002",
            resourcePrefix: 6,
            name: "Back triangle",
            z: -0.25f,
            sourcePointIds: [201, 202, 203],
            sourceElementId: 2001,
            scalarResourceSuffix: alternateField ? 12 : 10,
            fieldVersion: alternateField ? 2 : 1,
            scalars: alternateField ? [-1.0f, 0.0f, 1.0f] : [1.0f, 0.0f, -1.0f],
            resources);

        var manifest = new SceneManifest(
            new ProtocolVersion(1, 0),
            SceneId,
            alternateField ? 2 : 1,
            "deterministic-two-triangle-fixture",
            new SceneUnits("m"),
            Origin,
            [Origin[0], Origin[0] + 1.0, Origin[1], Origin[1] + 1.0, Origin[2] - 0.25, Origin[2] + 0.25],
            new SceneBackground("#FFFFFF", "#DDE7F0"),
            [front, back],
            resources.Values.Select(resource => resource.Descriptor).OrderBy(resource => resource.ResourceId, StringComparer.Ordinal).ToArray(),
            new ScalarFieldMetadata(
                alternateField ? "fixture-wave-inverted" : "fixture-wave",
                "value",
                "Pa",
                "point",
                [-1.0, 1.0],
                [-1.0, 1.0],
                "nan-color",
                alternateField ? 103 : 101,
                alternateField ? 101 : 103),
            DateTimeOffset.Parse("2026-08-05T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

        ContractValidator.Validate(manifest);
        return new SyntheticScene(manifest, resources);
    }

    private static RenderActor CreateActor(
        string actorId,
        string chunkId,
        int resourcePrefix,
        string name,
        float z,
        int[] sourcePointIds,
        int sourceElementId,
        int scalarResourceSuffix,
        int fieldVersion,
        float[] scalars,
        IDictionary<string, IRenderResource> output)
    {
        float[] positions = [0.0f, 0.0f, z, 1.0f, 0.0f, z, 0.0f, 1.0f, z];
        uint[] connectivity = [0, 1, 2];
        int[] sourceElementIds = [sourceElementId];

        var positionsResource = AddResource(resourcePrefix, "positions", RenderDataTypes.Float32, 3, 3, LittleEndianArrayWriter.Write(positions), output);
        var connectivityResource = AddResource(resourcePrefix + 1, "triangleConnectivity", RenderDataTypes.UInt32, 3, 1, LittleEndianArrayWriter.Write(connectivity), output);
        var pointsResource = AddResource(resourcePrefix + 2, "sourcePointIds", RenderDataTypes.Int32, 1, 3, LittleEndianArrayWriter.Write(sourcePointIds), output);
        var elementsResource = AddResource(resourcePrefix + 3, "sourceElementIds", RenderDataTypes.Int32, 1, 1, LittleEndianArrayWriter.Write(sourceElementIds), output);
        var scalarsResource = AddResource(scalarResourceSuffix, "pointScalars", RenderDataTypes.Float32, 1, 3, LittleEndianArrayWriter.Write(scalars), output, fieldVersion);

        var bounds = new[] { Origin[0], Origin[0] + 1.0, Origin[1], Origin[1] + 1.0, Origin[2] + z, Origin[2] + z };
        var chunk = new RenderChunk(
            chunkId,
            resourcePrefix == 1 ? 0 : 1,
            resourcePrefix == 1 ? 100 : 90,
            bounds,
            3,
            1,
            new ChunkResources(
                new ResourceReference(positionsResource),
                new ResourceReference(connectivityResource),
                new ResourceReference(pointsResource),
                new ResourceReference(elementsResource),
                new ResourceReference(scalarsResource)));

        return new RenderActor(
            actorId,
            name,
            null,
            "base",
            true,
            true,
            "surface",
            new RenderMaterial([0.70, 0.70, 0.75, 1.0], [0.85, 0.35, 0.35, 1.0], 0.15, 0.85, 0.0, true, false, 1.0, 5.0),
            bounds,
            1,
            fieldVersion,
            [chunk]);
    }

    private static string AddResource(
        int suffix,
        string semantic,
        string dataType,
        int components,
        long elementCount,
        byte[] body,
        IDictionary<string, IRenderResource> output,
        int version = 1)
    {
        var id = $"40000000-0000-0000-0000-{suffix:D12}";
        var hash = Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant();
        var descriptor = new ResourceDescriptor(
            id,
            semantic,
            dataType,
            components,
            elementCount,
            body.LongLength,
            "little-endian",
            "identity",
            hash,
            version,
            true,
            $"/api/v1/scenes/{SceneId}/resources/{id}");
        ContractValidator.Validate(descriptor);
        output.Add(id, new BinaryResource(descriptor, body));
        return id;
    }
}

using System.Buffers.Binary;
using PrePoMax.Render.Contracts;

namespace PrePoMax.Render.SyntheticData;

public sealed record LargeFixtureOptions(int ChunkCellRows = 50, long ResourceCapBytes = 8 * 1024 * 1024)
{
    public const int Width = 2000;
    public const int Height = 1500;
    public const int NodeCount = Width * Height;
    public const int LogicalQuadCount = (Width - 1) * (Height - 1);
    public const int TriangleCount = LogicalQuadCount * 2;

    public void Validate()
    {
        if (ChunkCellRows is < 1 or >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(ChunkCellRows), $"Chunk cell rows must be from 1 through {Height - 1}.");
        }
        if (ResourceCapBytes < 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(ResourceCapBytes), "The resource cap must be at least 1 MiB.");
        }
        var largestRows = Math.Min(ChunkCellRows, Height - 1);
        var largestConnectivity = checked((long)largestRows * (Width - 1) * 2 * 3 * sizeof(uint));
        if (largestConnectivity > ResourceCapBytes)
        {
            throw new ArgumentException(
                $"ChunkCellRows={ChunkCellRows} creates a {largestConnectivity}-byte connectivity resource above the {ResourceCapBytes}-byte cap.");
        }
    }
}

public static class LargeFixtureFactory
{
    public const string SceneId = "10000000-0000-0000-0000-000000000002";
    public const string ActorId = "20000000-0000-0000-0000-000000000003";
    private static readonly double[] Origin = [1_234_567.0, 2_345_678.0, 3_456_789.0];

    public static (SyntheticScene BaseField, SyntheticScene AlternateField) CreateVersions(LargeFixtureOptions? options = null)
    {
        options ??= new LargeFixtureOptions();
        options.Validate();
        var allResources = new Dictionary<string, IRenderResource>(StringComparer.Ordinal);
        var baseChunks = new List<RenderChunk>();
        var alternateChunks = new List<RenderChunk>();
        var chunkIndex = 0;
        for (var firstCellRow = 0; firstCellRow < LargeFixtureOptions.Height - 1; firstCellRow += options.ChunkCellRows)
        {
            var chunkFirstCellRow = firstCellRow;
            var cellRows = Math.Min(options.ChunkCellRows, LargeFixtureOptions.Height - 1 - firstCellRow);
            var pointCount = checked((cellRows + 1) * LargeFixtureOptions.Width);
            var triangleCount = checked(cellRows * (LargeFixtureOptions.Width - 1) * 2);
            var chunkId = Id(6, chunkIndex + 1);
            var position = AddResource(allResources, chunkIndex, 1, "positions", RenderDataTypes.Float32, 3, pointCount, 1,
                (offset, destination) => FillWords(offset, destination, word => PositionWord(chunkFirstCellRow, word)));
            var connectivity = AddResource(allResources, chunkIndex, 2, "triangleConnectivity", RenderDataTypes.UInt32, 3, triangleCount, 1,
                (offset, destination) => FillWords(offset, destination, word => ConnectivityWord(word)));
            var sourcePoints = AddResource(allResources, chunkIndex, 3, "sourcePointIds", RenderDataTypes.Int32, 1, pointCount, 1,
                (offset, destination) => FillWords(offset, destination, word => checked((uint)(chunkFirstCellRow * LargeFixtureOptions.Width + word + 1))));
            var sourceElements = AddResource(allResources, chunkIndex, 4, "sourceElementIds", RenderDataTypes.Int32, 1, triangleCount, 1,
                (offset, destination) => FillWords(offset, destination, word => checked((uint)(chunkFirstCellRow * (LargeFixtureOptions.Width - 1) + word / 2 + 1))));
            var baseScalars = AddResource(allResources, chunkIndex, 5, "pointScalars", RenderDataTypes.Float32, 1, pointCount, 1,
                (offset, destination) => FillWords(offset, destination, word => ScalarWord(chunkFirstCellRow, word, false)));
            var alternateScalars = AddResource(allResources, chunkIndex, 6, "pointScalars", RenderDataTypes.Float32, 1, pointCount, 2,
                (offset, destination) => FillWords(offset, destination, word => ScalarWord(chunkFirstCellRow, word, true)));

            var bounds = new[]
            {
                Origin[0], Origin[0] + (LargeFixtureOptions.Width - 1) * 0.001,
                Origin[1] + firstCellRow * 0.001, Origin[1] + (firstCellRow + cellRows) * 0.001,
                Origin[2] - 0.05, Origin[2] + 0.05,
            };
            baseChunks.Add(CreateChunk(chunkId, chunkIndex, bounds, pointCount, triangleCount, position, connectivity, sourcePoints, sourceElements, baseScalars));
            alternateChunks.Add(CreateChunk(chunkId, chunkIndex, bounds, pointCount, triangleCount, position, connectivity, sourcePoints, sourceElements, alternateScalars));
            chunkIndex++;
        }

        var baseManifest = CreateManifest(baseChunks, allResources, alternate: false);
        var alternateManifest = CreateManifest(alternateChunks, allResources, alternate: true);
        ContractValidator.Validate(baseManifest);
        ContractValidator.Validate(alternateManifest);
        return (
            new SyntheticScene(baseManifest, SelectResources(baseManifest, allResources)),
            new SyntheticScene(alternateManifest, SelectResources(alternateManifest, allResources)));
    }

    private static SceneManifest CreateManifest(
        IReadOnlyList<RenderChunk> chunks,
        IReadOnlyDictionary<string, IRenderResource> allResources,
        bool alternate)
    {
        var actorBounds = new[]
        {
            Origin[0], Origin[0] + (LargeFixtureOptions.Width - 1) * 0.001,
            Origin[1], Origin[1] + (LargeFixtureOptions.Height - 1) * 0.001,
            Origin[2] - 0.05, Origin[2] + 0.05,
        };
        var actor = new RenderActor(
            ActorId,
            "Canonical 3M grid",
            null,
            "base",
            true,
            true,
            "surface",
            new RenderMaterial([0.70, 0.70, 0.75, 1.0], [0.85, 0.35, 0.35, 1.0], 0.15, 0.85, 0.0, true, true, 1.0, 3.0),
            actorBounds,
            1,
            alternate ? 2 : 1,
            chunks);
        var referencedIds = chunks.SelectMany(ResourceIds).ToHashSet(StringComparer.Ordinal);
        return new SceneManifest(
            new ProtocolVersion(1, 0),
            SceneId,
            alternate ? 2 : 1,
            "synthetic-3m-grid",
            new SceneUnits("m"),
            Origin,
            actorBounds,
            new SceneBackground("#FFFFFF", "#DDE7F0"),
            [actor],
            allResources.Values.Where(resource => referencedIds.Contains(resource.Descriptor.ResourceId))
                .Select(resource => resource.Descriptor).OrderBy(resource => resource.ResourceId, StringComparer.Ordinal).ToArray(),
            new ScalarFieldMetadata(
                alternate ? "canonical-wave-inverted" : "canonical-wave",
                "value",
                "Pa",
                "point",
                [-1.01, 1.01],
                [-1.01, 1.01],
                "nan-color",
                null,
                null),
            DateTimeOffset.Parse("2026-08-05T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static RenderChunk CreateChunk(
        string chunkId,
        int ordinal,
        IReadOnlyList<double> bounds,
        int pointCount,
        int triangleCount,
        string positions,
        string connectivity,
        string sourcePoints,
        string sourceElements,
        string scalars) => new(
            chunkId,
            ordinal,
            10_000 - ordinal,
            bounds,
            pointCount,
            triangleCount,
            new ChunkResources(
                new ResourceReference(positions),
                new ResourceReference(connectivity),
                new ResourceReference(sourcePoints),
                new ResourceReference(sourceElements),
                new ResourceReference(scalars)));

    private static string AddResource(
        IDictionary<string, IRenderResource> resources,
        int chunkIndex,
        int kind,
        string semantic,
        string dataType,
        int components,
        long elementCount,
        int version,
        ResourceFill fill)
    {
        var serial = checked((chunkIndex + 1) * 10 + kind);
        var resourceId = Id(5, serial);
        var resource = GeneratedBinaryResource.Create(
            resourceId,
            semantic,
            dataType,
            components,
            elementCount,
            version,
            $"/api/v1/scenes/{SceneId}/resources/{resourceId}",
            fill);
        resources.Add(resourceId, resource);
        return resourceId;
    }

    private static IReadOnlyDictionary<string, IRenderResource> SelectResources(
        SceneManifest manifest,
        IReadOnlyDictionary<string, IRenderResource> allResources) =>
        manifest.Resources.ToDictionary(
            descriptor => descriptor.ResourceId,
            descriptor => allResources[descriptor.ResourceId],
            StringComparer.Ordinal);

    private static IEnumerable<string> ResourceIds(RenderChunk chunk)
    {
        yield return chunk.Resources.Positions.ResourceId;
        yield return chunk.Resources.TriangleConnectivity.ResourceId;
        yield return chunk.Resources.SourcePointIds.ResourceId;
        yield return chunk.Resources.SourceElementIds.ResourceId;
        yield return chunk.Resources.PointScalars.ResourceId;
    }

    private static uint PositionWord(int firstCellRow, long word)
    {
        var point = word / 3;
        var component = (int)(word % 3);
        var column = (int)(point % LargeFixtureOptions.Width);
        var row = checked(firstCellRow + (int)(point / LargeFixtureOptions.Width));
        var wave = Wave(column, row);
        var value = component switch
        {
            0 => column * 0.001f,
            1 => row * 0.001f,
            _ => 0.05f * wave,
        };
        return unchecked((uint)BitConverter.SingleToInt32Bits(value));
    }

    private static uint ConnectivityWord(long word)
    {
        var triangle = word / 3;
        var vertex = (int)(word % 3);
        var quad = triangle / 2;
        var row = checked((uint)(quad / (LargeFixtureOptions.Width - 1)));
        var column = checked((uint)(quad % (LargeFixtureOptions.Width - 1)));
        var lowerLeft = row * LargeFixtureOptions.Width + column;
        return (triangle & 1) == 0
            ? vertex switch { 0 => lowerLeft, 1 => lowerLeft + 1, _ => lowerLeft + LargeFixtureOptions.Width + 1 }
            : vertex switch { 0 => lowerLeft, 1 => lowerLeft + LargeFixtureOptions.Width + 1, _ => lowerLeft + LargeFixtureOptions.Width };
    }

    private static uint ScalarWord(int firstCellRow, long point, bool alternate)
    {
        var column = (int)(point % LargeFixtureOptions.Width);
        var row = checked(firstCellRow + (int)(point / LargeFixtureOptions.Width));
        var wave = Wave(column, row);
        var scalar = wave + 0.005f * wave;
        if (alternate)
        {
            scalar = -scalar;
        }
        return unchecked((uint)BitConverter.SingleToInt32Bits(scalar));
    }

    private static float Wave(int column, int row) =>
        (float)(Math.Sin(column * 0.01) * Math.Cos(row * 0.01));

    private static void FillWords(long byteOffset, Span<byte> destination, Func<long, uint> valueAt)
    {
        var output = 0;
        Span<byte> word = stackalloc byte[sizeof(uint)];
        while (output < destination.Length)
        {
            var absoluteOffset = byteOffset + output;
            var wordIndex = absoluteOffset / sizeof(uint);
            var byteInWord = (int)(absoluteOffset % sizeof(uint));
            BinaryPrimitives.WriteUInt32LittleEndian(word, valueAt(wordIndex));
            var count = Math.Min(sizeof(uint) - byteInWord, destination.Length - output);
            word.Slice(byteInWord, count).CopyTo(destination[output..]);
            output += count;
        }
    }

    private static string Id(int prefix, int serial) => $"{prefix}0000000-0000-0000-0000-{serial:D12}";
}

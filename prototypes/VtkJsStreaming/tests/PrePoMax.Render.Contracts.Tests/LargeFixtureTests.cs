using System.Buffers.Binary;
using System.Text.Json;
using PrePoMax.Render.SyntheticData;

namespace PrePoMax.Render.Contracts.Tests;

public sealed class LargeFixtureTests
{
    private static readonly Lazy<(SyntheticScene BaseField, SyntheticScene AlternateField)> Scenes =
        new(() => LargeFixtureFactory.CreateVersions());

    [Fact]
    public void CanonicalCountsAndResourceCap_AreExact()
    {
        var scene = Scenes.Value.BaseField;
        var chunks = Assert.Single(scene.Manifest.Actors).Chunks;
        Assert.Equal(LargeFixtureOptions.TriangleCount, chunks.Sum(chunk => chunk.TriangleCount));
        Assert.Equal(LargeFixtureOptions.NodeCount, LargeFixtureOptions.Width * LargeFixtureOptions.Height);
        Assert.Equal(LargeFixtureOptions.LogicalQuadCount, LargeFixtureOptions.TriangleCount / 2);
        Assert.Equal(30, chunks.Count);
        Assert.All(scene.Manifest.Resources, descriptor => Assert.InRange(descriptor.ByteLength, 1, 8 * 1024 * 1024));
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(scene.Manifest).Length < 1_048_576);
    }

    [Fact]
    public void SourceIds_AreStableAcrossChunkBoundariesAndFields()
    {
        var (baseField, alternateField) = Scenes.Value;
        var chunks = Assert.Single(baseField.Manifest.Actors).Chunks;
        Assert.Equal(
            chunks.SelectMany(TopologyIds),
            Assert.Single(alternateField.Manifest.Actors).Chunks.SelectMany(TopologyIds));

        var first = chunks[0];
        var second = chunks[1];
        Assert.Equal(1, ReadInt32(baseField.Resources[first.Resources.SourcePointIds.ResourceId], 0));
        Assert.Equal(102_000, ReadInt32(baseField.Resources[first.Resources.SourcePointIds.ResourceId], first.PointCount - 1));
        Assert.Equal(100_001, ReadInt32(baseField.Resources[second.Resources.SourcePointIds.ResourceId], 0));
        var last = chunks[^1];
        Assert.Equal(3_000_000, ReadInt32(baseField.Resources[last.Resources.SourcePointIds.ResourceId], last.PointCount - 1));
        Assert.Equal(1, ReadInt32(baseField.Resources[first.Resources.SourceElementIds.ResourceId], 0));
        Assert.Equal(2_996_501, ReadInt32(baseField.Resources[last.Resources.SourceElementIds.ResourceId], last.TriangleCount - 1));
    }

    [Fact]
    public async Task GeneratedResource_SupportsRangesAndCancellationWithoutMaterializedBody()
    {
        var scene = Scenes.Value.BaseField;
        var descriptor = scene.Manifest.Resources.First(resource => resource.Semantic == "triangleConnectivity");
        var resource = Assert.IsType<GeneratedBinaryResource>(scene.Resources[descriptor.ResourceId]);
        var expected = new byte[37];
        resource.Read(3, expected);
        await using var output = new MemoryStream();
        await resource.WriteAsync(output, 3, expected.Length, CancellationToken.None);
        Assert.Equal(expected, output.ToArray());

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await resource.WriteAsync(Stream.Null, 0, descriptor.ByteLength, cancellation.Token));
    }

    private static IEnumerable<string> TopologyIds(RenderChunk chunk)
    {
        yield return chunk.Resources.Positions.ResourceId;
        yield return chunk.Resources.TriangleConnectivity.ResourceId;
        yield return chunk.Resources.SourcePointIds.ResourceId;
        yield return chunk.Resources.SourceElementIds.ResourceId;
    }

    private static int ReadInt32(IRenderResource resource, int index)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        resource.Read(index * sizeof(int), bytes);
        return BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }
}

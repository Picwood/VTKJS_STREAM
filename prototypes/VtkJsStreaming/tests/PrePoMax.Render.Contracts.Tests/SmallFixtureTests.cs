using System.Buffers.Binary;
using System.Text.Json;
using PrePoMax.Render.SyntheticData;

namespace PrePoMax.Render.Contracts.Tests;

public sealed class SmallFixtureTests
{
    [Fact]
    public void Create_IsDeterministicAndHasTwoMappedTriangles()
    {
        var first = SmallFixtureFactory.Create();
        var second = SmallFixtureFactory.Create();

        Assert.Equal(2, first.Manifest.Actors.Count);
        Assert.Equal(10, first.Manifest.Resources.Count);
        Assert.Equal(
            first.Manifest.Resources.Select(resource => resource.Sha256),
            second.Manifest.Resources.Select(resource => resource.Sha256));
        Assert.All(first.Manifest.Actors, actor => Assert.Equal(1, Assert.Single(actor.Chunks).TriangleCount));
    }

    [Fact]
    public void SourceIds_ArePackedLittleEndianWithoutJsonPayloads()
    {
        var scene = SmallFixtureFactory.Create();
        var frontChunk = scene.Manifest.Actors[0].Chunks[0];
        var pointBytes = Assert.IsType<BinaryResource>(scene.Resources[frontChunk.Resources.SourcePointIds.ResourceId]).Body.Span;
        Assert.Equal(101, BinaryPrimitives.ReadInt32LittleEndian(pointBytes));
        Assert.Equal(102, BinaryPrimitives.ReadInt32LittleEndian(pointBytes[4..]));
        Assert.Equal(103, BinaryPrimitives.ReadInt32LittleEndian(pointBytes[8..]));

        var json = JsonSerializer.Serialize(scene.Manifest);
        Assert.DoesNotContain("\"coordinates\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"connectivity\":[", json, StringComparison.OrdinalIgnoreCase);
        Assert.True(json.Length < 1_048_576);
    }

    [Fact]
    public void DescriptorLengthsAndHashesMatchBodies()
    {
        var scene = SmallFixtureFactory.Create();
        foreach (var resource in scene.Resources.Values)
        {
            var binary = Assert.IsType<BinaryResource>(resource);
            Assert.Equal(resource.Descriptor.ByteLength, binary.Body.Length);
            Assert.Equal(
                resource.Descriptor.Sha256,
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(binary.Body.Span)).ToLowerInvariant());
        }
    }

    [Fact]
    public void AlternateField_ChangesOnlyScalarResourcesAndFieldVersions()
    {
        var initial = SmallFixtureFactory.Create();
        var alternate = SmallFixtureFactory.CreateAlternateField();

        Assert.Equal(2, alternate.Manifest.SceneRevision);
        Assert.All(alternate.Manifest.Actors, actor => Assert.Equal(2, actor.FieldVersion));
        Assert.Equal("fixture-wave-inverted", alternate.Manifest.Field.Name);
        for (var actorIndex = 0; actorIndex < initial.Manifest.Actors.Count; actorIndex++)
        {
            var initialChunk = Assert.Single(initial.Manifest.Actors[actorIndex].Chunks);
            var alternateChunk = Assert.Single(alternate.Manifest.Actors[actorIndex].Chunks);
            Assert.Equal(initialChunk.Resources.Positions, alternateChunk.Resources.Positions);
            Assert.Equal(initialChunk.Resources.TriangleConnectivity, alternateChunk.Resources.TriangleConnectivity);
            Assert.Equal(initialChunk.Resources.SourcePointIds, alternateChunk.Resources.SourcePointIds);
            Assert.Equal(initialChunk.Resources.SourceElementIds, alternateChunk.Resources.SourceElementIds);
            Assert.NotEqual(initialChunk.Resources.PointScalars, alternateChunk.Resources.PointScalars);
        }
        Assert.All(
            alternate.Manifest.Resources.Where(resource => resource.Semantic == "pointScalars"),
            resource => Assert.Equal(2, resource.Version));
    }
}

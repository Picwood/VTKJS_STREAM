using System.Buffers;
using System.Security.Cryptography;
using PrePoMax.Render.Contracts;

namespace PrePoMax.Render.SyntheticData;

public interface IRenderResource
{
    ResourceDescriptor Descriptor { get; }
    ValueTask WriteAsync(Stream destination, long offset, long length, CancellationToken cancellationToken);
    void Read(long offset, Span<byte> destination);
}

public sealed record BinaryResource(ResourceDescriptor Descriptor, ReadOnlyMemory<byte> Body) : IRenderResource
{
    public async ValueTask WriteAsync(Stream destination, long offset, long length, CancellationToken cancellationToken) =>
        await destination.WriteAsync(Body.Slice(checked((int)offset), checked((int)length)), cancellationToken);

    public void Read(long offset, Span<byte> destination) =>
        Body.Span.Slice(checked((int)offset), destination.Length).CopyTo(destination);
}

public delegate void ResourceFill(long byteOffset, Span<byte> destination);

public sealed class GeneratedBinaryResource : IRenderResource
{
    public const int DefaultBufferSize = 256 * 1024;
    private readonly ResourceFill fill;
    private readonly int bufferSize;

    private GeneratedBinaryResource(ResourceDescriptor descriptor, ResourceFill fill, int bufferSize)
    {
        Descriptor = descriptor;
        this.fill = fill;
        this.bufferSize = bufferSize;
    }

    public ResourceDescriptor Descriptor { get; }

    public static GeneratedBinaryResource Create(
        string resourceId,
        string semantic,
        string dataType,
        int components,
        long elementCount,
        int version,
        string uri,
        ResourceFill fill,
        int bufferSize = DefaultBufferSize)
    {
        var byteLength = ContractValidator.CalculateByteLength(dataType, components, elementCount);
        var hash = CalculateHash(byteLength, fill, bufferSize);
        var descriptor = new ResourceDescriptor(
            resourceId,
            semantic,
            dataType,
            components,
            elementCount,
            byteLength,
            "little-endian",
            "identity",
            hash,
            version,
            true,
            uri);
        ContractValidator.Validate(descriptor);
        return new GeneratedBinaryResource(descriptor, fill, bufferSize);
    }

    public async ValueTask WriteAsync(Stream destination, long offset, long length, CancellationToken cancellationToken)
    {
        ValidateRange(offset, length);
        var buffer = ArrayPool<byte>.Shared.Rent(checked((int)Math.Min(bufferSize, length)));
        try
        {
            var written = 0L;
            while (written < length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = checked((int)Math.Min(buffer.Length, length - written));
                fill(offset + written, buffer.AsSpan(0, count));
                await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                written += count;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public void Read(long offset, Span<byte> destination)
    {
        ValidateRange(offset, destination.Length);
        fill(offset, destination);
    }

    private static string CalculateHash(long byteLength, ResourceFill fill, int bufferSize)
    {
        if (bufferSize < sizeof(double))
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize));
        }
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(checked((int)Math.Min(bufferSize, byteLength)));
        try
        {
            var offset = 0L;
            while (offset < byteLength)
            {
                var count = checked((int)Math.Min(buffer.Length, byteLength - offset));
                fill(offset, buffer.AsSpan(0, count));
                hash.AppendData(buffer, 0, count);
                offset += count;
            }
            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void ValidateRange(long offset, long length)
    {
        if (offset < 0 || length < 0 || offset > Descriptor.ByteLength - length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
    }
}

public sealed record SelectionCellTopology(int SourceElementId, int VtkCellType, IReadOnlyList<int> LocalPointIndices);

public sealed record SelectionActorTopology(
    string Name,
    IReadOnlyList<int> SourceNodeIds,
    IReadOnlyList<double> Coordinates,
    IReadOnlyList<SelectionCellTopology> Cells,
    IReadOnlyList<float>? Scalars = null,
    bool SectionViewPossible = true);

public sealed class SyntheticScene(
    SceneManifest manifest,
    IReadOnlyDictionary<string, IRenderResource> resources,
    IReadOnlyList<SelectionActorTopology>? selectionActors = null)
{
    public SceneManifest Manifest { get; } = manifest;
    public IReadOnlyDictionary<string, IRenderResource> Resources { get; } = resources;
    public IReadOnlyList<SelectionActorTopology> SelectionActors { get; } = selectionActors ?? [];
}

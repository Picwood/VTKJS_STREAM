using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace PrePoMax.Render.Server;

internal static class HostControlReader
{
    public const string VisibilityContentType = "application/vnd.prepomax.actor-visibility-v1";
    public const string ActorMutationContentType = "application/vnd.prepomax.actor-mutation-v1";
    public const string ActorRemovalContentType = "application/vnd.prepomax.actor-removal-v1";
    public const string CustomCameraContentType = "application/vnd.prepomax.camera-v1";
    public const string FieldMetadataContentType = "application/vnd.prepomax.field-metadata-v1";
    public const string ScalarUpdateContentType = "application/vnd.prepomax.scalar-update-v1";
    public const string AnimationFrameContentType = "application/vnd.prepomax.animation-frame-v1";
    public const string AnimationFrameV2ContentType = "application/vnd.prepomax.animation-frame-v2";
    public const int MaximumVisibilityBytes = 1024 * 1024;
    private static readonly byte[] VisibilityMagic = "PMXVIZ1\0"u8.ToArray();
    private static readonly byte[] ActorMutationMagic = "PMXACT1\0"u8.ToArray();
    private static readonly byte[] ActorRemovalMagic = "PMXREM1\0"u8.ToArray();
    private static readonly byte[] CustomCameraMagic = "PMXCAM1\0"u8.ToArray();
    private static readonly byte[] CustomCameraV2Magic = "PMXCAM2\0"u8.ToArray();
    private static readonly byte[] FieldMetadataMagic = "PMXFLD1\0"u8.ToArray();
    private static readonly byte[] ScalarUpdateMagic = "PMXSCL1\0"u8.ToArray();
    private static readonly byte[] AnimationFrameMagic = "PMXFRM1\0"u8.ToArray();
    private static readonly byte[] AnimationFrameV2Magic = "PMXFRM2\0"u8.ToArray();

    public static async Task<IReadOnlyDictionary<string, bool>> ReadVisibilityAsync(
        Stream source,
        long contentLength,
        CancellationToken cancellationToken)
    {
        if (contentLength < VisibilityMagic.Length + sizeof(int) || contentLength > MaximumVisibilityBytes)
        {
            throw new HostControlException("host-visibility-size-invalid", "The actor visibility command size is invalid.");
        }

        var body = new byte[checked((int)contentLength)];
        await source.ReadExactlyAsync(body, cancellationToken);
        var reader = new Reader(body, "host-visibility-format-invalid", "host-visibility-entry-invalid");
        if (!reader.ReadBytes(VisibilityMagic.Length).SequenceEqual(VisibilityMagic))
        {
            throw new HostControlException("host-visibility-format-invalid", "The actor visibility command magic or version is invalid.");
        }

        var count = reader.ReadInt32();
        if (count is < 1 or > 512)
        {
            throw new HostControlException("host-visibility-count-invalid", "The actor visibility command count is invalid.");
        }

        var updates = new Dictionary<string, bool>(count, StringComparer.Ordinal);
        for (var index = 0; index < count; index++)
        {
            var name = reader.ReadString(1024);
            var visible = reader.ReadByte();
            if (visible > 1 || !updates.TryAdd(name, visible != 0))
            {
                throw new HostControlException("host-visibility-entry-invalid", "An actor visibility entry is invalid or duplicated.");
            }
        }
        if (!reader.AtEnd)
        {
            throw new HostControlException("host-visibility-format-invalid", "The actor visibility command contains trailing bytes.");
        }
        return updates;
    }

    public static async Task<HostActorMutation> ReadActorMutationAsync(
        Stream source,
        long contentLength,
        CancellationToken cancellationToken)
    {
        if (contentLength is < 22 or > 4096)
        {
            throw new HostControlException("host-actor-size-invalid", "The actor mutation command size is invalid.");
        }
        var body = new byte[checked((int)contentLength)];
        await source.ReadExactlyAsync(body, cancellationToken);
        var reader = new Reader(body, "host-actor-format-invalid", "host-actor-entry-invalid");
        if (!reader.ReadBytes(ActorMutationMagic.Length).SequenceEqual(ActorMutationMagic))
        {
            throw new HostControlException("host-actor-format-invalid", "The actor mutation command magic or version is invalid.");
        }
        var mutation = new HostActorMutation(reader.ReadString(1024), reader.ReadString(1024), reader.ReadInt32());
        if (!reader.AtEnd)
        {
            throw new HostControlException("host-actor-format-invalid", "The actor mutation command contains trailing bytes.");
        }
        return mutation;
    }

    public static async Task<IReadOnlyList<string>> ReadActorRemovalAsync(
        Stream source,
        long contentLength,
        CancellationToken cancellationToken)
    {
        if (contentLength < ActorRemovalMagic.Length + sizeof(int) || contentLength > MaximumVisibilityBytes)
        {
            throw new HostControlException("host-removal-size-invalid", "The actor removal command size is invalid.");
        }
        var body = new byte[checked((int)contentLength)];
        await source.ReadExactlyAsync(body, cancellationToken);
        var reader = new Reader(body, "host-removal-format-invalid", "host-removal-entry-invalid");
        if (!reader.ReadBytes(ActorRemovalMagic.Length).SequenceEqual(ActorRemovalMagic))
        {
            throw new HostControlException("host-removal-format-invalid", "The actor removal command magic or version is invalid.");
        }
        var count = reader.ReadInt32();
        if (count is < 1 or > 512)
        {
            throw new HostControlException("host-removal-count-invalid", "The actor removal command count is invalid.");
        }
        var names = new List<string>(count);
        var distinct = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < count; index++)
        {
            var name = reader.ReadString(1024);
            if (!distinct.Add(name))
                throw new HostControlException("host-removal-entry-invalid", "An actor removal entry is duplicated.");
            names.Add(name);
        }
        if (!reader.AtEnd)
            throw new HostControlException("host-removal-format-invalid", "The actor removal command contains trailing bytes.");
        return names;
    }

    public static async Task<HostCustomCamera> ReadCustomCameraAsync(
        Stream source,
        long contentLength,
        CancellationToken cancellationToken)
    {
        const int v1Length = 8 + 10 * sizeof(double);
        const int v2Length = 8 + sizeof(int) + 10 * sizeof(double);
        if (contentLength != v1Length && contentLength != v2Length)
        {
            throw new HostControlException("host-camera-size-invalid", "The custom camera command size is invalid.");
        }
        var body = new byte[checked((int)contentLength)];
        await source.ReadExactlyAsync(body, cancellationToken);
        var reader = new Reader(body, "host-camera-format-invalid", "host-camera-numeric-invalid");
        var magic = reader.ReadBytes(CustomCameraMagic.Length).ToArray();
        string operation;
        if (magic.SequenceEqual(CustomCameraMagic) && contentLength == v1Length) operation = "set";
        else if (magic.SequenceEqual(CustomCameraV2Magic) && contentLength == v2Length)
        {
            operation = reader.ReadInt32() switch
            {
                0 => "set",
                1 => "fit",
                2 => "adjust",
                _ => throw new HostControlException(
                    "host-camera-operation-invalid", "The custom camera operation is invalid."),
            };
        }
        else
        {
            throw new HostControlException("host-camera-format-invalid", "The custom camera command magic or version is invalid.");
        }
        var position = new[] { reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble() };
        var focalPoint = new[] { reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble() };
        var viewUp = new[] { reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble() };
        var parallelScale = reader.ReadDouble();
        var values = position.Concat(focalPoint).Concat(viewUp).Append(parallelScale);
        var direction = position.Zip(focalPoint, (left, right) => right - left).ToArray();
        var directionLengthSquared = direction.Sum(value => value * value);
        var upLengthSquared = viewUp.Sum(value => value * value);
        var cross = new[]
        {
            direction[1] * viewUp[2] - direction[2] * viewUp[1],
            direction[2] * viewUp[0] - direction[0] * viewUp[2],
            direction[0] * viewUp[1] - direction[1] * viewUp[0],
        };
        var crossLengthSquared = cross.Sum(value => value * value);
        if (values.Any(value => !double.IsFinite(value)) || parallelScale <= 0
            || directionLengthSquared <= 1e-24 || upLengthSquared <= 1e-24
            || crossLengthSquared <= 1e-24 * directionLengthSquared * upLengthSquared)
        {
            throw new HostControlException("host-camera-numeric-invalid", "Custom camera values are invalid or degenerate.");
        }
        if (!reader.AtEnd)
            throw new HostControlException("host-camera-format-invalid", "The custom camera command contains trailing bytes.");
        return new HostCustomCamera(position, focalPoint, viewUp, parallelScale, operation);
    }

    public static async Task<HostFieldMetadata> ReadFieldMetadataAsync(
        Stream source,
        long contentLength,
        CancellationToken cancellationToken)
    {
        if (contentLength is < 18 or > 4096)
        {
            throw new HostControlException("host-field-size-invalid", "The field metadata command size is invalid.");
        }
        var body = new byte[checked((int)contentLength)];
        await source.ReadExactlyAsync(body, cancellationToken);
        var reader = new Reader(body, "host-field-format-invalid", "host-field-entry-invalid");
        if (!reader.ReadBytes(FieldMetadataMagic.Length).SequenceEqual(FieldMetadataMagic))
        {
            throw new HostControlException("host-field-format-invalid", "The field metadata command magic or version is invalid.");
        }
        var metadata = new HostFieldMetadata(
            reader.ReadString(1024),
            reader.ReadString(1024),
            reader.ReadString(1024, allowEmpty: true));
        if (!reader.AtEnd)
        {
            throw new HostControlException("host-field-format-invalid", "The field metadata command contains trailing bytes.");
        }
        return metadata;
    }

    public static async Task<IReadOnlyDictionary<string, float[]>> ReadScalarUpdateAsync(
        Stream source,
        long contentLength,
        CancellationToken cancellationToken)
    {
        const int maximumBytes = 32 * 1024 * 1024;
        if (contentLength < ScalarUpdateMagic.Length + sizeof(int) || contentLength > maximumBytes)
        {
            throw new HostControlException("host-scalars-size-invalid", "The scalar update command size is invalid.");
        }
        var body = new byte[checked((int)contentLength)];
        await source.ReadExactlyAsync(body, cancellationToken);
        var reader = new Reader(body, "host-scalars-format-invalid", "host-scalars-entry-invalid");
        if (!reader.ReadBytes(ScalarUpdateMagic.Length).SequenceEqual(ScalarUpdateMagic))
        {
            throw new HostControlException("host-scalars-format-invalid", "The scalar update command magic or version is invalid.");
        }
        var actorCount = reader.ReadInt32();
        if (actorCount is < 1 or > 512)
        {
            throw new HostControlException("host-scalars-count-invalid", "The scalar update actor count is invalid.");
        }
        var updates = new Dictionary<string, float[]>(actorCount, StringComparer.Ordinal);
        long totalValues = 0;
        for (var actorIndex = 0; actorIndex < actorCount; actorIndex++)
        {
            var name = reader.ReadString(1024);
            var valueCount = reader.ReadInt32();
            totalValues = checked(totalValues + valueCount);
            if (valueCount < 1 || totalValues > 5_000_000)
            {
                throw new HostControlException("host-scalars-count-invalid", "A scalar update count is invalid.");
            }
            if (!updates.TryAdd(name, new float[valueCount]))
            {
                throw new HostControlException("host-scalars-count-invalid", "A scalar update actor is duplicated.");
            }
            var values = updates[name];
            for (var index = 0; index < values.Length; index++)
            {
                values[index] = reader.ReadSingle();
                if (float.IsInfinity(values[index]))
                {
                    throw new HostControlException("host-scalars-numeric-invalid", "Scalar update values cannot be infinite.");
                }
            }
        }
        if (!reader.AtEnd)
        {
            throw new HostControlException("host-scalars-format-invalid", "The scalar update command contains trailing bytes.");
        }
        return updates;
    }

    public static async Task<IReadOnlyDictionary<string, HostAnimationFrame>> ReadAnimationFrameAsync(
        Stream source,
        long contentLength,
        CancellationToken cancellationToken)
    {
        const int maximumBytes = 256 * 1024 * 1024;
        if (contentLength < AnimationFrameMagic.Length + sizeof(int) || contentLength > maximumBytes)
            throw new HostControlException("host-frame-size-invalid", "The animation frame command size is invalid.");
        using var reader = new PooledFrameReader(source, contentLength, cancellationToken);
        try
        {
            var magic = await reader.ReadMemoryAsync(AnimationFrameMagic.Length);
            var version2 = magic.Span.SequenceEqual(AnimationFrameV2Magic);
            if (!version2 && !magic.Span.SequenceEqual(AnimationFrameMagic))
                throw new HostControlException("host-frame-format-invalid", "The animation frame magic or version is invalid.");
            var actorCount = await reader.ReadInt32Async();
            if (actorCount is < 1 or > 512)
                throw new HostControlException("host-frame-count-invalid", "The animation frame actor count is invalid.");
            var origin = version2
                ? new[] { await reader.ReadDoubleAsync(), await reader.ReadDoubleAsync(), await reader.ReadDoubleAsync() }
                : new[] { 0d, 0d, 0d };
            if (origin.Any(value => !double.IsFinite(value)))
                throw new HostControlException("host-frame-numeric-invalid", "The animation frame origin must be finite.");
            var updates = new Dictionary<string, HostAnimationFrame>(actorCount, StringComparer.Ordinal);
            long totalRenderPoints = 0;
            long totalLocatorPoints = 0;
            for (var actorIndex = 0; actorIndex < actorCount; actorIndex++)
            {
                var name = await reader.ReadStringAsync(1024);
                var pointCount = await reader.ReadInt32Async();
                totalRenderPoints = checked(totalRenderPoints + pointCount);
                if (pointCount < 1 || totalRenderPoints > 5_000_000)
                    throw new HostControlException("host-frame-count-invalid", "An animation render-point count is invalid.");
                var coordinates = version2 ? null : new double[checked(pointCount * 3)];
                var relativeCoordinates = version2 ? new float[checked(pointCount * 3)] : null;
                var scalars = new float[pointCount];
                await reader.ReadRenderPointsAsync(version2, origin, coordinates, relativeCoordinates, scalars);

                var locatorPointCount = await reader.ReadInt32Async();
                totalLocatorPoints = checked(totalLocatorPoints + locatorPointCount);
                if (locatorPointCount < 0 || totalLocatorPoints > 5_000_000)
                    throw new HostControlException("host-frame-count-invalid", "An animation locator-point count is invalid.");
                var locatorCoordinates = version2 ? null : new double[checked(locatorPointCount * 3)];
                var relativeLocatorCoordinates = version2 ? new float[checked(locatorPointCount * 3)] : null;
                await reader.ReadLocatorCoordinatesAsync(version2, origin, locatorCoordinates, relativeLocatorCoordinates);

                var frame = version2
                    ? HostAnimationFrame.FromOriginRelative(origin, relativeCoordinates!, scalars, relativeLocatorCoordinates!)
                    : new HostAnimationFrame(coordinates!, scalars, locatorCoordinates!);
                if (!updates.TryAdd(name, frame))
                    throw new HostControlException("host-frame-entry-invalid", "An animation frame actor is duplicated.");
            }
            if (!reader.AtEnd)
                throw new HostControlException("host-frame-format-invalid", "The animation frame command contains trailing bytes.");
            return updates;
        }
        catch (EndOfStreamException exception)
        {
            throw new HostControlException("host-frame-format-invalid", "The animation frame command ended unexpectedly.", exception);
        }
    }

    private sealed class PooledFrameReader : IDisposable
    {
        private const int BufferSize = 64 * 1024;
        private readonly Stream source;
        private readonly long contentLength;
        private readonly CancellationToken cancellationToken;
        private byte[]? buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        private int offset;
        private int available;
        private long consumed;

        public PooledFrameReader(Stream source, long contentLength, CancellationToken cancellationToken)
        {
            this.source = source;
            this.contentLength = contentLength;
            this.cancellationToken = cancellationToken;
        }

        public bool AtEnd => consumed == contentLength;

        public async ValueTask<ReadOnlyMemory<byte>> ReadMemoryAsync(int count)
        {
            if (count < 0 || count > buffer!.Length || consumed > contentLength - count)
                throw new EndOfStreamException();
            if (available < count)
            {
                if (available > 0) buffer.AsSpan(offset, available).CopyTo(buffer);
                offset = 0;
                while (available < count)
                {
                    var read = await source.ReadAsync(buffer.AsMemory(available, buffer.Length - available), cancellationToken);
                    if (read == 0) throw new EndOfStreamException();
                    available += read;
                }
            }
            var result = buffer.AsMemory(offset, count);
            offset += count;
            available -= count;
            consumed += count;
            return result;
        }

        public async ValueTask<int> ReadInt32Async()
        {
            var bytes = await ReadMemoryAsync(sizeof(int));
            return BinaryPrimitives.ReadInt32LittleEndian(bytes.Span);
        }

        public async ValueTask<double> ReadDoubleAsync()
        {
            var bytes = await ReadMemoryAsync(sizeof(double));
            return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(bytes.Span));
        }

        public async ValueTask<string> ReadStringAsync(int maximumBytes)
        {
            var count = await ReadInt32Async();
            if (count < 1 || count > maximumBytes)
                throw new HostControlException("host-frame-entry-invalid", "An actor name length is invalid.");
            try
            {
                var bytes = await ReadMemoryAsync(count);
                return new UTF8Encoding(false, true).GetString(bytes.Span);
            }
            catch (DecoderFallbackException exception)
            {
                throw new HostControlException("host-frame-entry-invalid", "An actor name is not valid UTF-8.", exception);
            }
        }

        public async Task ReadRenderPointsAsync(bool version2, double[] origin, double[]? coordinates,
            float[]? relativeCoordinates, float[] scalars)
        {
            var stride = version2 ? 4 * sizeof(float) : 3 * sizeof(double) + sizeof(float);
            var point = 0;
            while (point < scalars.Length)
            {
                var count = Math.Min(scalars.Length - point, buffer!.Length / stride);
                var bytes = await ReadMemoryAsync(count * stride);
                DecodeRenderPoints(bytes, version2, origin, point, count, coordinates, relativeCoordinates, scalars);
                point += count;
            }
        }

        public async Task ReadLocatorCoordinatesAsync(bool version2, double[] origin, double[]? coordinates,
            float[]? relativeCoordinates)
        {
            var destinationLength = version2 ? relativeCoordinates!.Length : coordinates!.Length;
            var valueSize = version2 ? sizeof(float) : sizeof(double);
            var index = 0;
            while (index < destinationLength)
            {
                var count = Math.Min(destinationLength - index, buffer!.Length / valueSize);
                var bytes = await ReadMemoryAsync(count * valueSize);
                DecodeLocatorCoordinates(bytes, version2, origin, index, count, coordinates, relativeCoordinates);
                index += count;
            }
        }

        public void Dispose()
        {
            var rented = Interlocked.Exchange(ref buffer, null);
            if (rented is not null) ArrayPool<byte>.Shared.Return(rented);
        }

        private static void DecodeRenderPoints(ReadOnlyMemory<byte> bytes, bool version2, double[] origin,
            int firstPoint, int pointCount, double[]? coordinates, float[]? relativeCoordinates, float[] scalars)
        {
            var span = bytes.Span;
            var byteOffset = 0;
            for (var localPoint = 0; localPoint < pointCount; localPoint++)
            {
                var point = firstPoint + localPoint;
                for (var component = 0; component < 3; component++)
                {
                    if (version2)
                    {
                        var relative = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(span.Slice(byteOffset, sizeof(float))));
                        var value = origin[component] + relative;
                        if (!double.IsFinite(value)) ThrowInvalidCoordinates();
                        relativeCoordinates![point * 3 + component] = relative;
                        byteOffset += sizeof(float);
                    }
                    else
                    {
                        var value = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(span.Slice(byteOffset, sizeof(double))));
                        if (!double.IsFinite(value)) ThrowInvalidCoordinates();
                        coordinates![point * 3 + component] = value;
                        byteOffset += sizeof(double);
                    }
                }
                var scalar = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(span.Slice(byteOffset, sizeof(float))));
                if (float.IsInfinity(scalar))
                    throw new HostControlException("host-frame-numeric-invalid", "Animation scalars cannot be infinite.");
                scalars[point] = scalar;
                byteOffset += sizeof(float);
            }
        }

        private static void DecodeLocatorCoordinates(ReadOnlyMemory<byte> bytes, bool version2, double[] origin,
            int firstIndex, int count, double[]? coordinates, float[]? relativeCoordinates)
        {
            var span = bytes.Span;
            var valueSize = version2 ? sizeof(float) : sizeof(double);
            for (var localIndex = 0; localIndex < count; localIndex++)
            {
                var index = firstIndex + localIndex;
                if (version2)
                {
                    var relative = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(span.Slice(localIndex * valueSize, valueSize)));
                    if (!double.IsFinite(origin[index % 3] + relative)) ThrowInvalidCoordinates();
                    relativeCoordinates![index] = relative;
                }
                else
                {
                    var value = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(span.Slice(localIndex * valueSize, valueSize)));
                    if (!double.IsFinite(value)) ThrowInvalidCoordinates();
                    coordinates![index] = value;
                }
            }
        }

        private static void ThrowInvalidCoordinates() =>
            throw new HostControlException("host-frame-numeric-invalid", "Animation coordinates must be finite.");
    }

    private sealed class Reader(byte[] body, string formatCode, string entryCode)
    {
        private int offset;
        public bool AtEnd => offset == body.Length;

        public int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(ReadBytes(sizeof(int)));

        public byte ReadByte() => ReadBytes(1)[0];

        public double ReadDouble() => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(ReadBytes(sizeof(double))));

        public float ReadSingle() => BitConverter.Int32BitsToSingle(ReadInt32());

        public ReadOnlySpan<byte> ReadBytes(int count)
        {
            if (count < 0 || offset > body.Length - count)
            {
                throw new HostControlException(formatCode, "The host control command ended unexpectedly.");
            }
            var result = body.AsSpan(offset, count);
            offset += count;
            return result;
        }

        public string ReadString(int maximumBytes, bool allowEmpty = false)
        {
            var count = ReadInt32();
            if (count < (allowEmpty ? 0 : 1) || count > maximumBytes)
            {
                throw new HostControlException(entryCode, "An actor name length is invalid.");
            }
            try
            {
                return new UTF8Encoding(false, true).GetString(ReadBytes(count));
            }
            catch (DecoderFallbackException exception)
            {
                throw new HostControlException(entryCode, "An actor name is not valid UTF-8.", exception);
            }
        }
    }
}

internal sealed record HostActorMutation(string OldName, string NewName, int Argb);

internal sealed record HostCustomCamera(
    IReadOnlyList<double> Position,
    IReadOnlyList<double> FocalPoint,
    IReadOnlyList<double> ViewUp,
    double ParallelScale,
    string Operation);

internal sealed record HostFieldMetadata(string Name, string Component, string Units);

internal sealed class HostAnimationFrame
{
    private readonly double[]? absoluteCoordinates;
    private readonly double[]? absoluteLocatorCoordinates;
    private readonly float[]? relativeCoordinates;
    private readonly float[]? relativeLocatorCoordinates;
    private readonly double[] origin;

    public HostAnimationFrame(double[] coordinates, float[] scalars, double[] locatorCoordinates)
    {
        absoluteCoordinates = coordinates;
        absoluteLocatorCoordinates = locatorCoordinates;
        Scalars = scalars;
        origin = [0d, 0d, 0d];
    }

    private HostAnimationFrame(double[] origin, float[] coordinates, float[] scalars, float[] locatorCoordinates)
    {
        this.origin = origin;
        relativeCoordinates = coordinates;
        relativeLocatorCoordinates = locatorCoordinates;
        Scalars = scalars;
    }

    public static HostAnimationFrame FromOriginRelative(
        double[] origin, float[] coordinates, float[] scalars, float[] locatorCoordinates) =>
        new(origin, coordinates, scalars, locatorCoordinates);

    public float[] Scalars { get; }
    public int CoordinateCount => absoluteCoordinates?.Length ?? relativeCoordinates!.Length;
    public int LocatorCoordinateCount => absoluteLocatorCoordinates?.Length ?? relativeLocatorCoordinates!.Length;
    public bool UsesCompactCoordinates => relativeCoordinates is not null;
    public long CoordinateStorageBytes => UsesCompactCoordinates
        ? checked((long)(relativeCoordinates!.Length + relativeLocatorCoordinates!.Length) * sizeof(float))
        : checked((long)(absoluteCoordinates!.Length + absoluteLocatorCoordinates!.Length) * sizeof(double));
    public long ExpandedCoordinateBytes =>
        checked((long)(CoordinateCount + LocatorCoordinateCount) * sizeof(double));

    public double GetCoordinate(int index) => absoluteCoordinates is not null
        ? absoluteCoordinates[index]
        : origin[index % 3] + relativeCoordinates![index];

    public void FillSceneRelativePositions(IReadOnlyList<double> sceneOrigin, long byteOffset, Span<byte> destination)
    {
        var byteLength = checked((long)CoordinateCount * sizeof(float));
        ValidateFillRange(byteOffset, destination.Length, byteLength);
        var destinationOffset = 0;
        var coordinateIndex = checked((int)(byteOffset / sizeof(float)));
        var coordinateByteOffset = checked((int)(byteOffset % sizeof(float)));
        if (coordinateByteOffset != 0 && destination.Length != 0)
        {
            var relative = (float)(GetCoordinate(coordinateIndex) - sceneOrigin[coordinateIndex % 3]);
            var bits = BitConverter.SingleToInt32Bits(relative);
            coordinateIndex++;
            var count = Math.Min(sizeof(float) - coordinateByteOffset, destination.Length);
            for (var index = 0; index < count; index++)
                destination[destinationOffset++] = (byte)(bits >> ((coordinateByteOffset + index) * 8));
        }
        while (destinationOffset <= destination.Length - sizeof(float))
        {
            var relative = (float)(GetCoordinate(coordinateIndex) - sceneOrigin[coordinateIndex % 3]);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(destinationOffset, sizeof(float)),
                BitConverter.SingleToInt32Bits(relative));
            coordinateIndex++;
            destinationOffset += sizeof(float);
        }
        if (destinationOffset < destination.Length)
        {
            var bits = BitConverter.SingleToInt32Bits(
                (float)(GetCoordinate(coordinateIndex) - sceneOrigin[coordinateIndex % 3]));
            for (var index = 0; destinationOffset < destination.Length; index++)
                destination[destinationOffset++] = (byte)(bits >> (index * 8));
        }
    }

    public void FillScalars(long byteOffset, Span<byte> destination)
    {
        var bytes = MemoryMarshal.AsBytes(Scalars.AsSpan());
        ValidateFillRange(byteOffset, destination.Length, bytes.Length);
        if (BitConverter.IsLittleEndian)
        {
            bytes.Slice(checked((int)byteOffset), destination.Length).CopyTo(destination);
            return;
        }
        var destinationOffset = 0;
        var scalarIndex = checked((int)(byteOffset / sizeof(float)));
        var scalarByteOffset = checked((int)(byteOffset % sizeof(float)));
        while (destinationOffset < destination.Length)
        {
            var bits = BitConverter.SingleToInt32Bits(Scalars[scalarIndex++]);
            var count = Math.Min(sizeof(float) - scalarByteOffset, destination.Length - destinationOffset);
            for (var index = 0; index < count; index++)
                destination[destinationOffset++] = (byte)(bits >> ((scalarByteOffset + index) * 8));
            scalarByteOffset = 0;
        }
    }

    public double[] CalculateRenderBounds()
    {
        var bounds = new[] { double.MaxValue, double.MinValue, double.MaxValue, double.MinValue, double.MaxValue, double.MinValue };
        for (var index = 0; index < CoordinateCount; index++)
        {
            var component = index % 3;
            var value = GetCoordinate(index);
            bounds[component * 2] = Math.Min(bounds[component * 2], value);
            bounds[component * 2 + 1] = Math.Max(bounds[component * 2 + 1], value);
        }
        return bounds;
    }

    public double[] MaterializeLocatorCoordinates()
    {
        var result = new double[LocatorCoordinateCount];
        if (absoluteLocatorCoordinates is not null)
        {
            Array.Copy(absoluteLocatorCoordinates, result, result.Length);
            return result;
        }
        for (var index = 0; index < result.Length; index++)
            result[index] = origin[index % 3] + relativeLocatorCoordinates![index];
        return result;
    }

    public IReadOnlyList<double> GetLocatorCoordinates() => absoluteLocatorCoordinates is not null
        ? absoluteLocatorCoordinates
        : new OriginRelativeCoordinateList(origin, relativeLocatorCoordinates!);

    private static void ValidateFillRange(long byteOffset, int destinationLength, long byteLength)
    {
        if (byteOffset < 0 || byteOffset > byteLength - destinationLength)
            throw new ArgumentOutOfRangeException(nameof(byteOffset));
    }
}

internal sealed class OriginRelativeCoordinateList(double[] origin, float[] relativeCoordinates)
    : IReadOnlyList<double>
{
    public int Count => relativeCoordinates.Length;
    public double this[int index] => origin[index % 3] + relativeCoordinates[index];
    internal double GetCoordinate(int index) => origin[index % 3] + relativeCoordinates[index];
    internal void ExpandBounds(IReadOnlyList<int> points, double[] bounds)
    {
        // Indexed access avoids boxing the IReadOnlyList enumerator for every BVH cell.
        for (var pointIndex = 0; pointIndex < points.Count; pointIndex++)
        {
            var point = points[pointIndex];
            for (var axis = 0; axis < 3; axis++)
            {
                var value = origin[axis] + relativeCoordinates[point * 3 + axis];
                bounds[axis * 2] = Math.Min(bounds[axis * 2], value);
                bounds[axis * 2 + 1] = Math.Max(bounds[axis * 2 + 1], value);
            }
        }
    }

    public IEnumerator<double> GetEnumerator()
    {
        for (var index = 0; index < Count; index++) yield return this[index];
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

internal sealed class HostControlException(string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using PrePoMax.Render.Contracts;
using PrePoMax.Render.SyntheticData;

namespace PrePoMax.Render.Server;

internal static class HostSceneUploadReader
{
    public const string ContentType = "application/vnd.prepomax.render-scene-v2";
    public const string LegacyContentType = "application/vnd.prepomax.render-scene-v1";
    public const long DefaultMaximumBytes = 256L * 1024 * 1024;
    private static readonly byte[] MagicV1 = "PMXSCN1\0"u8.ToArray();
    private static readonly byte[] MagicV2 = "PMXSCN2\0"u8.ToArray();

    public static async Task<SyntheticScene> ReadAsync(
        Stream source,
        long contentLength,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        if (contentLength < MagicV2.Length + sizeof(int) || contentLength > maximumBytes)
        {
            throw new HostSceneUploadException("scene-upload-size-invalid", "The scene upload size is missing or outside the configured bound.");
        }

        var reader = await BoundedBinaryReader.CreateAsync(source, contentLength, cancellationToken);
        var magic = reader.ReadBytes(MagicV2.Length);
        var version = magic.AsSpan().SequenceEqual(MagicV2) ? 2 : magic.AsSpan().SequenceEqual(MagicV1) ? 1 : 0;
        if (version == 0)
        {
            throw new HostSceneUploadException("scene-upload-format-invalid", "The scene upload magic or version is invalid.");
        }

        var actorCount = reader.ReadInt32();
        if (actorCount is < 0 or > 512)
        {
            throw new HostSceneUploadException("scene-upload-count-invalid", "The uploaded actor count is invalid.");
        }

        var actors = new List<UploadedActor>(actorCount);
        long totalPoints = 0;
        long totalTriangles = 0;
        long totalLocatorPoints = 0;
        long totalLocatorCells = 0;
        long totalLocatorReferences = 0;
        for (var actorIndex = 0; actorIndex < actorCount; actorIndex++)
        {
            var name = reader.ReadString(1024);
            var argb = reader.ReadInt32();
            var flags = reader.ReadBytes(4);
            if (flags[0] > 1 || flags[1] > 1 || flags[2] > 1 || flags[3] > 1)
            {
                throw new HostSceneUploadException("scene-upload-flags-invalid", "Uploaded actor flags are invalid.");
            }
            var ambient = reader.ReadDouble();
            var diffuse = reader.ReadDouble();
            var lineWidth = reader.ReadSingle();
            var pointSize = reader.ReadSingle();
            var pointCount = reader.ReadInt32();
            var triangleCount = reader.ReadInt32();
            var locatorPointCount = version >= 2 ? reader.ReadInt32() : 0;
            var locatorCellCount = version >= 2 ? reader.ReadInt32() : 0;
            var locatorReferenceCount = version >= 2 ? reader.ReadInt32() : 0;
            if (pointCount is < 1 or > 5_000_000 || triangleCount is < 1 or > 10_000_000)
            {
                throw new HostSceneUploadException("scene-upload-count-invalid", "An uploaded actor has invalid point or triangle counts.");
            }
            if (locatorPointCount is < 0 or > 5_000_000 || locatorCellCount is < 0 or > 5_000_000 ||
                locatorReferenceCount is < 0 or > 50_000_000 ||
                ((locatorPointCount == 0) != (locatorCellCount == 0)) ||
                (locatorCellCount == 0 && locatorReferenceCount != 0))
                throw new HostSceneUploadException("scene-upload-locator-count-invalid", "Uploaded locator counts are invalid.");
            totalPoints = checked(totalPoints + pointCount);
            totalTriangles = checked(totalTriangles + triangleCount);
            totalLocatorPoints = checked(totalLocatorPoints + locatorPointCount);
            totalLocatorCells = checked(totalLocatorCells + locatorCellCount);
            totalLocatorReferences = checked(totalLocatorReferences + locatorReferenceCount);
            if (totalPoints > 5_000_000 || totalTriangles > 10_000_000)
            {
                throw new HostSceneUploadException("scene-upload-count-invalid", "The uploaded scene exceeds the MVP point or triangle bound.");
            }
            if (totalLocatorPoints > 5_000_000 || totalLocatorCells > 5_000_000 || totalLocatorReferences > 50_000_000)
                throw new HostSceneUploadException("scene-upload-locator-count-invalid", "The uploaded locator topology exceeds its bounds.");

            var coordinates = new double[checked(pointCount * 3)];
            var sourcePointIds = new int[pointCount];
            var scalars = new float[pointCount];
            for (var point = 0; point < pointCount; point++)
            {
                sourcePointIds[point] = reader.ReadInt32();
                for (var component = 0; component < 3; component++)
                {
                    var coordinate = reader.ReadDouble();
                    if (!double.IsFinite(coordinate))
                    {
                        throw new HostSceneUploadException("scene-upload-numeric-invalid", "Uploaded coordinates must be finite.");
                    }
                    coordinates[point * 3 + component] = coordinate;
                }
                scalars[point] = reader.ReadSingle();
                if (float.IsInfinity(scalars[point]))
                {
                    throw new HostSceneUploadException("scene-upload-numeric-invalid", "Uploaded scalars cannot be infinite.");
                }
            }

            var connectivity = new uint[checked(triangleCount * 3)];
            var sourceElementIds = new int[triangleCount];
            for (var triangle = 0; triangle < triangleCount; triangle++)
            {
                sourceElementIds[triangle] = reader.ReadInt32();
                for (var vertex = 0; vertex < 3; vertex++)
                {
                    var localPoint = reader.ReadUInt32();
                    if (localPoint >= pointCount)
                    {
                        throw new HostSceneUploadException("scene-upload-connectivity-invalid", "Triangle connectivity references a missing point.");
                    }
                    connectivity[triangle * 3 + vertex] = localPoint;
                }
            }

            var locatorCoordinates = new double[checked(locatorPointCount * 3)];
            var locatorNodeIds = new int[locatorPointCount];
            for (var point = 0; point < locatorPointCount; point++)
            {
                locatorNodeIds[point] = reader.ReadInt32();
                for (var component = 0; component < 3; component++)
                {
                    var coordinate = reader.ReadDouble();
                    if (!double.IsFinite(coordinate))
                        throw new HostSceneUploadException("scene-upload-locator-numeric-invalid", "Locator coordinates must be finite.");
                    locatorCoordinates[point * 3 + component] = coordinate;
                }
            }
            var locatorCells = new SelectionCellTopology[locatorCellCount];
            var actualLocatorReferences = 0L;
            for (var cell = 0; cell < locatorCellCount; cell++)
            {
                var elementId = reader.ReadInt32();
                var vtkCellType = reader.ReadInt32();
                var nodeCount = reader.ReadInt32();
                if (nodeCount != ExpectedNodeCount(vtkCellType))
                    throw new HostSceneUploadException("scene-upload-locator-cell-invalid", "A locator cell type or node count is unsupported.");
                actualLocatorReferences = checked(actualLocatorReferences + nodeCount);
                if (actualLocatorReferences > locatorReferenceCount)
                    throw new HostSceneUploadException("scene-upload-locator-count-invalid", "Locator connectivity exceeds its declared count.");
                var localPoints = new int[nodeCount];
                for (var node = 0; node < nodeCount; node++)
                {
                    localPoints[node] = reader.ReadInt32();
                    if (localPoints[node] < 0 || localPoints[node] >= locatorPointCount)
                        throw new HostSceneUploadException("scene-upload-locator-connectivity-invalid", "Locator connectivity references a missing point.");
                }
                locatorCells[cell] = new SelectionCellTopology(elementId, vtkCellType, localPoints);
            }
            if (actualLocatorReferences != locatorReferenceCount)
                throw new HostSceneUploadException("scene-upload-locator-count-invalid", "Locator connectivity does not match its declared count.");

            if (!double.IsFinite(ambient) || !double.IsFinite(diffuse)
                || !float.IsFinite(lineWidth) || !float.IsFinite(pointSize)
                || ambient is < 0 or > 1 || diffuse is < 0 or > 1
                || lineWidth <= 0 || pointSize <= 0)
            {
                throw new HostSceneUploadException("scene-upload-material-invalid", "Uploaded material values are invalid.");
            }

            actors.Add(new UploadedActor(
                name,
                argb,
                flags[0] != 0,
                flags[1] != 0,
                flags[2] != 0,
                flags[3] == 0,
                ambient,
                diffuse,
                lineWidth,
                pointSize,
                coordinates,
                sourcePointIds,
                scalars,
                connectivity,
                sourceElementIds,
                locatorCoordinates,
                locatorNodeIds,
                locatorCells));
        }

        if (reader.BytesRead != contentLength)
        {
            throw new HostSceneUploadException("scene-upload-format-invalid", "The scene upload contains trailing or missing bytes.");
        }
        return BuildScene(actors);
    }

    private static int ExpectedNodeCount(int vtkCellType) => vtkCellType switch
    {
        1 => 1, 3 => 2, 5 => 3, 9 => 4, 10 => 4, 11 => 8, 12 => 8, 13 => 6, 14 => 5,
        21 => 3, 22 => 6, 23 => 8, 24 => 10, 25 => 20, 26 => 15, 27 => 13, 29 => 27,
        _ => -1,
    };

    private static SyntheticScene BuildScene(IReadOnlyList<UploadedActor> uploadedActors)
    {
        var sceneId = Guid.NewGuid().ToString("D");
        var allCoordinates = uploadedActors.SelectMany(actor => actor.Coordinates).ToArray();
        var sceneBounds = CalculateBounds(allCoordinates);
        var origin = allCoordinates.Length == 0
            ? new[] { 0.0, 0.0, 0.0 }
            : new[]
            {
                sceneBounds[0] / 2 + sceneBounds[1] / 2,
                sceneBounds[2] / 2 + sceneBounds[3] / 2,
                sceneBounds[4] / 2 + sceneBounds[5] / 2,
            };
        var resources = new Dictionary<string, IRenderResource>(StringComparer.Ordinal);
        var renderActors = new List<RenderActor>(uploadedActors.Count);
        var finiteScalars = new List<float>();

        for (var ordinal = 0; ordinal < uploadedActors.Count; ordinal++)
        {
            var uploaded = uploadedActors[ordinal];
            var actorId = Guid.NewGuid().ToString("D");
            var chunkId = Guid.NewGuid().ToString("D");
            var pointCount = uploaded.SourcePointIds.Length;
            var triangleCount = uploaded.SourceElementIds.Length;
            var positions = new float[checked(pointCount * 3)];
            for (var index = 0; index < positions.Length; index++)
            {
                positions[index] = (float)(uploaded.Coordinates[index] - origin[index % 3]);
                if (!float.IsFinite(positions[index]))
                {
                    throw new HostSceneUploadException(
                        "scene-upload-numeric-invalid",
                        "Uploaded coordinates exceed the Float32 origin-relative render range.");
                }
            }
            finiteScalars.AddRange(uploaded.Scalars.Where(float.IsFinite));

            var positionsId = AddResource(sceneId, "positions", RenderDataTypes.Float32, 3, pointCount, LittleEndianArrayWriter.Write(positions), resources);
            var connectivityId = AddResource(sceneId, "triangleConnectivity", RenderDataTypes.UInt32, 3, triangleCount, LittleEndianArrayWriter.Write(uploaded.Connectivity), resources);
            var pointIdsId = AddResource(sceneId, "sourcePointIds", RenderDataTypes.Int32, 1, pointCount, LittleEndianArrayWriter.Write(uploaded.SourcePointIds), resources);
            var elementIdsId = AddResource(sceneId, "sourceElementIds", RenderDataTypes.Int32, 1, triangleCount, LittleEndianArrayWriter.Write(uploaded.SourceElementIds), resources);
            var scalarsId = AddResource(sceneId, "pointScalars", RenderDataTypes.Float32, 1, pointCount, LittleEndianArrayWriter.Write(uploaded.Scalars), resources);
            var bounds = CalculateBounds(uploaded.Coordinates);
            var alpha = ((uploaded.Argb >> 24) & 0xff) / 255.0;
            var red = ((uploaded.Argb >> 16) & 0xff) / 255.0;
            var green = ((uploaded.Argb >> 8) & 0xff) / 255.0;
            var blue = (uploaded.Argb & 0xff) / 255.0;
            var material = new RenderMaterial(
                [red, green, blue, alpha],
                [red, green, blue, alpha],
                uploaded.Ambient,
                uploaded.Diffuse,
                0,
                uploaded.BackfaceCulling,
                uploaded.SmoothShading,
                uploaded.LineWidth,
                uploaded.PointSize);
            var chunk = new RenderChunk(
                chunkId,
                ordinal,
                100 - Math.Min(ordinal, 99),
                bounds,
                pointCount,
                triangleCount,
                new ChunkResources(
                    new ResourceReference(positionsId),
                    new ResourceReference(connectivityId),
                    new ResourceReference(pointIdsId),
                    new ResourceReference(elementIdsId),
                    new ResourceReference(scalarsId)));
            renderActors.Add(new RenderActor(
                actorId,
                uploaded.Name,
                null,
                "base",
                true,
                uploaded.Pickable,
                "surface",
                material,
                bounds,
                1,
                1,
                [chunk]));
        }

        var scalarBounds = finiteScalars.Count == 0
            ? new[] { 0.0, 1.0 }
            : new[] { (double)finiteScalars.Min(), (double)finiteScalars.Max() };
        if (scalarBounds[0] == scalarBounds[1]) scalarBounds[1] = scalarBounds[0] + 1;
        var manifest = new SceneManifest(
            new ProtocolVersion(1, 0),
            sceneId,
            1,
            "PrePoMax current surface snapshot",
            new SceneUnits("model-unit"),
            origin,
            sceneBounds,
            new SceneBackground("#FFFFFF", "#DDE7F0"),
            renderActors,
            resources.Values.Select(resource => resource.Descriptor).ToArray(),
            new ScalarFieldMetadata("PrePoMax values", "value", "model-unit", "point", scalarBounds, scalarBounds, "nan-color", null, null),
            DateTimeOffset.UtcNow);
        ContractValidator.Validate(manifest);
        var selectionActors = uploadedActors
            .Where(actor => actor.LocatorNodeIds.Length > 0)
            .Select(actor =>
            {
                var scalarsByNode = actor.SourcePointIds
                    .Select((nodeId, index) => (nodeId, scalar: actor.Scalars[index]))
                    .GroupBy(item => item.nodeId)
                    .ToDictionary(group => group.Key, group => group.First().scalar);
                var locatorScalars = actor.LocatorNodeIds
                    .Select(nodeId => scalarsByNode.TryGetValue(nodeId, out var scalar) ? scalar : float.NaN)
                    .ToArray();
                return new SelectionActorTopology(actor.Name, actor.LocatorNodeIds,
                    actor.LocatorCoordinates, actor.LocatorCells, locatorScalars, actor.SectionViewPossible);
            })
            .ToArray();
        return new SyntheticScene(manifest, resources, selectionActors);
    }

    private static string AddResource(
        string sceneId,
        string semantic,
        string dataType,
        int components,
        long elementCount,
        byte[] body,
        IDictionary<string, IRenderResource> resources)
    {
        var resourceId = Guid.NewGuid().ToString("D");
        var descriptor = new ResourceDescriptor(
            resourceId,
            semantic,
            dataType,
            components,
            elementCount,
            body.LongLength,
            "little-endian",
            "identity",
            Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant(),
            1,
            true,
            $"/api/v1/scenes/{sceneId}/resources/{resourceId}");
        resources.Add(resourceId, new BinaryResource(descriptor, body));
        return resourceId;
    }

    private static double[] CalculateBounds(IReadOnlyList<double> coordinates)
    {
        if (coordinates.Count == 0) return [0, 0, 0, 0, 0, 0];
        var bounds = new[] { double.MaxValue, double.MinValue, double.MaxValue, double.MinValue, double.MaxValue, double.MinValue };
        for (var index = 0; index < coordinates.Count; index += 3)
        {
            for (var component = 0; component < 3; component++)
            {
                var value = coordinates[index + component];
                bounds[component * 2] = Math.Min(bounds[component * 2], value);
                bounds[component * 2 + 1] = Math.Max(bounds[component * 2 + 1], value);
            }
        }
        return bounds;
    }

    private sealed record UploadedActor(
        string Name,
        int Argb,
        bool Pickable,
        bool BackfaceCulling,
        bool SmoothShading,
        bool SectionViewPossible,
        double Ambient,
        double Diffuse,
        float LineWidth,
        float PointSize,
        double[] Coordinates,
        int[] SourcePointIds,
        float[] Scalars,
        uint[] Connectivity,
        int[] SourceElementIds,
        double[] LocatorCoordinates,
        int[] LocatorNodeIds,
        SelectionCellTopology[] LocatorCells);

    private sealed class BoundedBinaryReader
    {
        private readonly byte[] _body;
        private int _offset;

        private BoundedBinaryReader(byte[] body) => _body = body;

        public long BytesRead => _offset;

        public static async Task<BoundedBinaryReader> CreateAsync(
            Stream source,
            long length,
            CancellationToken cancellationToken)
        {
            var body = new byte[checked((int)length)];
            await source.ReadExactlyAsync(body, cancellationToken);
            return new BoundedBinaryReader(body);
        }

        public int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(ReadSpan(sizeof(int)));

        public uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(ReadSpan(sizeof(uint)));

        public float ReadSingle() => BitConverter.Int32BitsToSingle(ReadInt32());

        public double ReadDouble() => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(ReadSpan(sizeof(long))));

        public string ReadString(int maximumBytes)
        {
            var count = ReadInt32();
            if (count is < 1 || count > maximumBytes)
            {
                throw new HostSceneUploadException("scene-upload-string-invalid", "An uploaded string length is invalid.");
            }
            try
            {
                var value = new UTF8Encoding(false, true).GetString(ReadSpan(count));
                return value;
            }
            catch (DecoderFallbackException exception)
            {
                throw new HostSceneUploadException("scene-upload-string-invalid", "An uploaded string is not valid UTF-8.", exception);
            }
        }

        public byte[] ReadBytes(int count) => ReadSpan(count).ToArray();

        private ReadOnlySpan<byte> ReadSpan(int count)
        {
            if (count < 0 || _offset > _body.Length - count)
            {
                throw new HostSceneUploadException("scene-upload-format-invalid", "The scene upload ended unexpectedly.");
            }
            var span = _body.AsSpan(_offset, count);
            _offset += count;
            return span;
        }

    }
}

internal sealed class HostSceneUploadException(string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}

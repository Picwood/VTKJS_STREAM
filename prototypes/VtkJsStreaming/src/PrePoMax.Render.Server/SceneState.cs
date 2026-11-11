using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using PrePoMax.Render.Contracts;
using PrePoMax.Render.SyntheticData;

namespace PrePoMax.Render.Server;

internal sealed class SceneState
{
    private readonly object gate = new();
    private readonly SyntheticScene baseField;
    private readonly SyntheticScene alternateField;
    private IReadOnlyDictionary<string, IRenderResource> resourceCatalog;
    private SyntheticScene current;
    private SelectionSpatialIndex selectionIndex;
    private readonly Dictionary<string, bool> actorVisibility = new(StringComparer.Ordinal);
    private HostCameraState camera = HostCameraState.Standard("reset");
    private HostFieldMetadata fieldMetadata = new("PrePoMax values", "value", "model-unit");

    public SceneState()
    {
        var initialization = Stopwatch.StartNew();
        if (string.Equals(Environment.GetEnvironmentVariable("PREPOMAX_RENDER_SCENE"), "large", StringComparison.OrdinalIgnoreCase))
        {
            var options = new LargeFixtureOptions(
                ReadIntegerEnvironment("PREPOMAX_RENDER_CHUNK_CELL_ROWS", 50),
                ReadLongEnvironment("PREPOMAX_RENDER_RESOURCE_CAP_BYTES", 8 * 1024 * 1024));
            (baseField, alternateField) = LargeFixtureFactory.CreateVersions(options);
        }
        else
        {
            baseField = SmallFixtureFactory.Create();
            alternateField = SmallFixtureFactory.CreateAlternateField();
        }
        current = baseField;
        selectionIndex = new SelectionSpatialIndex(current.Manifest, current.SelectionActors);
        resourceCatalog = baseField.Resources.Values
            .Concat(alternateField.Resources.Values)
            .GroupBy(resource => resource.Descriptor.ResourceId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        InitializationMilliseconds = initialization.Elapsed.TotalMilliseconds;
    }

    public double InitializationMilliseconds { get; }

    public HostCameraState Camera
    {
        get { lock (gate) return camera; }
    }

    public SceneManifest Manifest
    {
        get
        {
            lock (gate)
            {
                return current.Manifest;
            }
        }
    }

    public bool TryGetResource(string resourceId, out IRenderResource? resource) =>
        resourceCatalog.TryGetValue(resourceId, out resource);

    public SectionGeometryResult ResolveSection(SectionResolveRequest request)
    {
        lock (gate) return SectionGeometryBuilder.Build(current.Manifest, current.SelectionActors, request);
    }

    public SceneReplacementResult Replace(SyntheticScene replacement)
    {
        lock (gate)
        {
            var previous = current.Manifest;
            var actors = replacement.Manifest.Actors
                .Select(actor => actorVisibility.TryGetValue(actor.Name, out var visible)
                    ? actor with { Visible = visible }
                    : actor)
                .ToArray();
            var manifest = replacement.Manifest with
            {
                Actors = actors,
                Field = ApplyFieldMetadata(replacement.Manifest.Field),
            };
            current = new SyntheticScene(manifest, replacement.Resources, replacement.SelectionActors);
            selectionIndex = new SelectionSpatialIndex(manifest, replacement.SelectionActors);
            resourceCatalog = replacement.Resources;
            return new SceneReplacementResult(previous, manifest, camera);
        }
    }

    public VisibilityUpdateResult SetActorVisibility(IReadOnlyDictionary<string, bool> requested)
    {
        lock (gate)
        {
            foreach (var pair in requested)
            {
                if (!actorVisibility.ContainsKey(pair.Key) && actorVisibility.Count >= 4096)
                {
                    return VisibilityUpdateResult.Capacity(current.Manifest.SceneRevision);
                }
                actorVisibility[pair.Key] = pair.Value;
            }

            var changed = new List<ActorVisibilityUpdate>();
            var actors = current.Manifest.Actors.Select(actor =>
            {
                if (!requested.TryGetValue(actor.Name, out var visible) || actor.Visible == visible) return actor;
                changed.Add(new ActorVisibilityUpdate(actor.ActorId, visible));
                return actor with { Visible = visible };
            }).ToArray();
            if (changed.Count == 0)
            {
                return VisibilityUpdateResult.Success(current.Manifest.SceneRevision, changed);
            }

            var revision = checked(current.Manifest.SceneRevision + 1);
            var manifest = current.Manifest with { SceneRevision = revision, Actors = actors };
            current = new SyntheticScene(manifest, current.Resources, current.SelectionActors);
            return VisibilityUpdateResult.Success(revision, changed);
        }
    }

    public void SetCameraView(string view)
    {
        lock (gate) camera = HostCameraState.Standard(view);
    }

    public void SetCustomCamera(HostCustomCamera custom)
    {
        lock (gate)
        {
            camera = HostCameraState.Custom(custom.Position, custom.FocalPoint, custom.ViewUp,
                                            custom.ParallelScale, custom.Operation);
        }
    }

    public void SetFieldMetadata(HostFieldMetadata metadata)
    {
        lock (gate) fieldMetadata = metadata;
    }

    public ScalarUpdateResult UpdateScalars(IReadOnlyDictionary<string, float[]> requested)
    {
        lock (gate)
        {
            var actorsByName = current.Manifest.Actors.ToDictionary(actor => actor.Name, StringComparer.Ordinal);
            foreach (var pair in requested)
            {
                if (!actorsByName.TryGetValue(pair.Key, out var actor))
                {
                    return ScalarUpdateResult.Invalid(current.Manifest.SceneRevision, $"Actor '{pair.Key}' is not present in the current scene.");
                }

                var expectedCount = actor.Chunks.Sum(chunk => chunk.PointCount);
                if (pair.Value.Length != expectedCount)
                {
                    return ScalarUpdateResult.Invalid(
                        current.Manifest.SceneRevision,
                        $"Actor '{pair.Key}' has {pair.Value.Length} scalar values; {expectedCount} are required.");
                }
            }

            var resources = current.Resources.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            var chunks = new List<FieldChunkUpdate>();
            var actors = current.Manifest.Actors.Select(actor =>
            {
                if (!requested.TryGetValue(actor.Name, out var values)) return actor;

                var fieldVersion = checked(actor.FieldVersion + 1);
                var offset = 0;
                var updatedChunks = actor.Chunks.Select(chunk =>
                {
                    resources.Remove(chunk.Resources.PointScalars.ResourceId);
                    var body = LittleEndianArrayWriter.Write(values.AsSpan(offset, chunk.PointCount));
                    offset += chunk.PointCount;
                    var resourceId = Guid.NewGuid().ToString("D");
                    var descriptor = new ResourceDescriptor(
                        resourceId,
                        "pointScalars",
                        RenderDataTypes.Float32,
                        1,
                        chunk.PointCount,
                        body.LongLength,
                        "little-endian",
                        "identity",
                        Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant(),
                        fieldVersion,
                        true,
                        $"/api/v1/scenes/{current.Manifest.SceneId}/resources/{resourceId}");
                    ContractValidator.Validate(descriptor);
                    var resource = new BinaryResource(descriptor, body);
                    resources.Add(resourceId, resource);
                    resourceCatalog = resourceCatalog
                        .Concat(new[] { new KeyValuePair<string, IRenderResource>(resourceId, resource) })
                        .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                    chunks.Add(new FieldChunkUpdate(actor.ActorId, chunk.ChunkId, fieldVersion, descriptor));
                    return chunk with
                    {
                        Resources = chunk.Resources with { PointScalars = new ResourceReference(resourceId) },
                    };
                }).ToArray();
                return actor with { FieldVersion = fieldVersion, Chunks = updatedChunks };
            }).ToArray();

            var range = CalculateScalarRange(actors, resources);
            var field = new ScalarFieldMetadata(
                fieldMetadata.Name,
                fieldMetadata.Component,
                fieldMetadata.Units,
                "point",
                range,
                range,
                "nan-color",
                null,
                null);
            var revision = checked(current.Manifest.SceneRevision + 1);
            var manifest = current.Manifest with
            {
                SceneRevision = revision,
                Actors = actors,
                Resources = resources.Values.Select(resource => resource.Descriptor).OrderBy(resource => resource.ResourceId, StringComparer.Ordinal).ToArray(),
                Field = field,
            };
            ContractValidator.Validate(manifest);
            var selectionActors = UpdateLocatorScalars(current.SelectionActors, requested);
            current = new SyntheticScene(manifest, resources, selectionActors);
            return ScalarUpdateResult.Success(revision, new FieldUpdatedPayload(field, chunks));
        }
    }

    public ActorMutationResult UpdateActor(HostActorMutation mutation)
    {
        lock (gate)
        {
            if (mutation.OldName != mutation.NewName
                && current.Manifest.Actors.Any(actor => actor.Name == mutation.NewName))
            {
                return ActorMutationResult.Conflict(current.Manifest.SceneRevision);
            }

            if (actorVisibility.Remove(mutation.OldName, out var visible))
            {
                actorVisibility[mutation.NewName] = visible;
            }

            var actorIndex = current.Manifest.Actors
                .Select((actor, index) => (actor, index))
                .FirstOrDefault(pair => pair.actor.Name == mutation.OldName);
            if (actorIndex.actor is null)
            {
                return ActorMutationResult.Success(current.Manifest.SceneRevision, null);
            }

            var rgba = new[]
            {
                ((mutation.Argb >> 16) & 0xff) / 255.0,
                ((mutation.Argb >> 8) & 0xff) / 255.0,
                (mutation.Argb & 0xff) / 255.0,
                ((mutation.Argb >> 24) & 0xff) / 255.0,
            };
            var material = actorIndex.actor.Material with { FrontRgba = rgba };
            var updatedActor = actorIndex.actor with { Name = mutation.NewName, Material = material };
            var actors = current.Manifest.Actors.ToArray();
            actors[actorIndex.index] = updatedActor;
            var revision = checked(current.Manifest.SceneRevision + 1);
            var manifest = current.Manifest with { SceneRevision = revision, Actors = actors };
            var selectionActors = current.SelectionActors.Select(actor => actor.Name == mutation.OldName
                ? actor with { Name = mutation.NewName }
                : actor).ToArray();
            current = new SyntheticScene(manifest, current.Resources, selectionActors);
            selectionIndex = selectionIndex.Refit(manifest, selectionActors);
            return ActorMutationResult.Success(
                revision,
                new ActorPresentationUpdate(updatedActor.ActorId, updatedActor.Name, rgba));
        }
    }

    public ActorRemovalResult RemoveActors(IReadOnlyCollection<string> names)
    {
        lock (gate)
        {
            var requested = new HashSet<string>(names, StringComparer.Ordinal);
            var removed = current.Manifest.Actors.Where(actor => requested.Contains(actor.Name)).ToArray();
            foreach (var name in requested) actorVisibility.Remove(name);
            if (removed.Length == 0)
                return new ActorRemovalResult(current.Manifest.SceneRevision, Array.Empty<string>());

            var removedActorIds = removed.Select(actor => actor.ActorId).ToHashSet(StringComparer.Ordinal);
            var removedResourceIds = removed.SelectMany(actor => actor.Chunks).SelectMany(chunk =>
            {
                var references = new List<string>
                {
                    chunk.Resources.Positions.ResourceId,
                    chunk.Resources.TriangleConnectivity.ResourceId,
                    chunk.Resources.SourcePointIds.ResourceId,
                    chunk.Resources.SourceElementIds.ResourceId,
                    chunk.Resources.PointScalars.ResourceId,
                };
                if (chunk.Resources.Normals is not null) references.Add(chunk.Resources.Normals.ResourceId);
                return references;
            }).ToHashSet(StringComparer.Ordinal);
            var resources = current.Resources
                .Where(pair => !removedResourceIds.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            resourceCatalog = resourceCatalog
                .Where(pair => !removedResourceIds.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            var actors = current.Manifest.Actors.Where(actor => !removedActorIds.Contains(actor.ActorId)).ToArray();
            var selectionActors = current.SelectionActors.Where(actor => !requested.Contains(actor.Name)).ToArray();
            var revision = checked(current.Manifest.SceneRevision + 1);
            var manifest = current.Manifest with
            {
                SceneRevision = revision,
                Actors = actors,
                Resources = resources.Values.Select(resource => resource.Descriptor)
                    .OrderBy(resource => resource.ResourceId, StringComparer.Ordinal).ToArray(),
            };
            ContractValidator.Validate(manifest);
            current = new SyntheticScene(manifest, resources, selectionActors);
            selectionIndex = new SelectionSpatialIndex(manifest, selectionActors);
            return new ActorRemovalResult(revision, removed.Select(actor => actor.ActorId).ToArray());
        }
    }

    public FrameUpdateResult UpdateAnimationFrame(IReadOnlyDictionary<string, HostAnimationFrame> requested)
    {
        lock (gate)
        {
            var phaseTimer = Stopwatch.StartNew();
            var phaseAllocated = GC.GetTotalAllocatedBytes(false);
            var actorsByName = current.Manifest.Actors.ToDictionary(actor => actor.Name, StringComparer.Ordinal);
            var locatorByName = current.SelectionActors.ToDictionary(actor => actor.Name, StringComparer.Ordinal);
            foreach (var pair in requested)
            {
                if (!actorsByName.TryGetValue(pair.Key, out var actor) || actor.Chunks.Count != 1)
                    return FrameUpdateResult.Invalid(current.Manifest.SceneRevision,
                        $"Animation actor '{pair.Key}' is unavailable or is not a single host chunk.");
                if (pair.Value.CoordinateCount != actor.Chunks[0].PointCount * 3 ||
                    pair.Value.Scalars.Length != actor.Chunks[0].PointCount)
                    return FrameUpdateResult.Invalid(current.Manifest.SceneRevision,
                        $"Animation actor '{pair.Key}' render-point counts do not match the current scene.");
                for (var index = 0; index < pair.Value.CoordinateCount; index++)
                {
                    var relative = pair.Value.GetCoordinate(index) - current.Manifest.WorldOrigin[index % 3];
                    if (!double.IsFinite(relative) || relative < -float.MaxValue || relative > float.MaxValue)
                        return FrameUpdateResult.Invalid(current.Manifest.SceneRevision,
                            $"Animation actor '{pair.Key}' exceeds the Float32 origin-relative coordinate range.");
                }
                var expectedLocatorCoordinates = locatorByName.TryGetValue(pair.Key, out var locator)
                    ? locator.SourceNodeIds.Count * 3 : 0;
                if (pair.Value.LocatorCoordinateCount != expectedLocatorCoordinates)
                    return FrameUpdateResult.Invalid(current.Manifest.SceneRevision,
                        $"Animation actor '{pair.Key}' locator-point count does not match the current scene.");
            }

            phaseTimer.Stop();
            var validation = new FrameUpdatePhaseMetrics(
                phaseTimer.Elapsed.TotalMilliseconds,
                Math.Max(0, GC.GetTotalAllocatedBytes(false) - phaseAllocated));
            phaseTimer.Restart();
            phaseAllocated = GC.GetTotalAllocatedBytes(false);

            var resources = current.Resources.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            var chunkUpdates = new List<FrameChunkUpdate>();
            var actorUpdates = new List<FrameActorUpdate>();
            var origin = current.Manifest.WorldOrigin;
            var actors = current.Manifest.Actors.Select(actor =>
            {
                if (!requested.TryGetValue(actor.Name, out var frame)) return actor;
                var chunk = actor.Chunks[0];
                resources.Remove(chunk.Resources.Positions.ResourceId);
                resources.Remove(chunk.Resources.PointScalars.ResourceId);
                var version = checked(actor.FieldVersion + 1);
                ResourceFill positionFill = (byteOffset, destination) =>
                    frame.FillSceneRelativePositions(origin, byteOffset, destination);
                var positions = CreateFrameResource(current.Manifest.SceneId, "positions", 3,
                    chunk.PointCount, positionFill, version);
                var scalars = CreateFrameResource(current.Manifest.SceneId, "pointScalars", 1,
                    chunk.PointCount, frame.FillScalars, version);
                resources.Add(positions.Descriptor.ResourceId, positions);
                resources.Add(scalars.Descriptor.ResourceId, scalars);
                var bounds = frame.CalculateRenderBounds();
                var updatedChunk = chunk with
                {
                    Bounds = bounds,
                    Resources = chunk.Resources with
                    {
                        Positions = new ResourceReference(positions.Descriptor.ResourceId),
                        PointScalars = new ResourceReference(scalars.Descriptor.ResourceId),
                    },
                };
                chunkUpdates.Add(new FrameChunkUpdate(actor.ActorId, chunk.ChunkId, bounds,
                    positions.Descriptor, scalars.Descriptor));
                actorUpdates.Add(new FrameActorUpdate(actor.ActorId, bounds));
                return actor with { Bounds = bounds, FieldVersion = version, Chunks = new[] { updatedChunk } };
            }).ToArray();
            var selectionActors = current.SelectionActors.Select(actor =>
            {
                if (!requested.TryGetValue(actor.Name, out var frame)) return actor;
                return actor with
                {
                    Coordinates = frame.GetLocatorCoordinates(),
                    Scalars = MapLocatorScalars(actor, frame.Scalars),
                };
            }).ToArray();
            phaseTimer.Stop();
            var resourcesPhase = new FrameUpdatePhaseMetrics(
                phaseTimer.Elapsed.TotalMilliseconds,
                Math.Max(0, GC.GetTotalAllocatedBytes(false) - phaseAllocated));
            phaseTimer.Restart();
            phaseAllocated = GC.GetTotalAllocatedBytes(false);

            var range = CalculateScalarRange(actors, resources);
            var field = ApplyFieldMetadata(current.Manifest.Field) with
            {
                ValidRange = range,
                DisplayedRange = range,
            };
            var sceneBounds = MergeBounds(actors.Select(actor => actor.Bounds));
            var revision = checked(current.Manifest.SceneRevision + 1);
            var manifest = current.Manifest with
            {
                SceneRevision = revision,
                Bounds = sceneBounds,
                Actors = actors,
                Resources = resources.Values.Select(resource => resource.Descriptor)
                    .OrderBy(resource => resource.ResourceId, StringComparer.Ordinal).ToArray(),
                Field = field,
            };
            ContractValidator.Validate(manifest);
            phaseTimer.Stop();
            var manifestPhase = new FrameUpdatePhaseMetrics(
                phaseTimer.Elapsed.TotalMilliseconds,
                Math.Max(0, GC.GetTotalAllocatedBytes(false) - phaseAllocated));
            phaseTimer.Restart();
            phaseAllocated = GC.GetTotalAllocatedBytes(false);

            selectionIndex.RefitCoordinates(selectionActors);
            resourceCatalog = resources;
            current = new SyntheticScene(manifest, resources, selectionActors);
            phaseTimer.Stop();
            var refitPhase = new FrameUpdatePhaseMetrics(
                phaseTimer.Elapsed.TotalMilliseconds,
                Math.Max(0, GC.GetTotalAllocatedBytes(false) - phaseAllocated));
            return FrameUpdateResult.Success(revision,
                new FrameUpdatedPayload(field, sceneBounds, actorUpdates, chunkUpdates),
                new FrameUpdateDiagnostics(validation, resourcesPhase, manifestPhase, refitPhase));
        }
    }

    public FieldUpdateResult ToggleField(long expectedRevision)
    {
        lock (gate)
        {
            if (!ReferenceEquals(current, baseField) && !ReferenceEquals(current, alternateField))
            {
                return FieldUpdateResult.Unsupported(current.Manifest.SceneRevision);
            }
            if (current.Manifest.SceneRevision != expectedRevision)
            {
                return FieldUpdateResult.Conflict(current.Manifest.SceneRevision);
            }

            var target = current.Manifest.Actors[0].FieldVersion == 1 ? alternateField : baseField;
            var revision = checked(current.Manifest.SceneRevision + 1);
            var manifest = target.Manifest with { SceneRevision = revision };
            current = new SyntheticScene(manifest, target.Resources);

            var descriptors = manifest.Resources.ToDictionary(resource => resource.ResourceId, StringComparer.Ordinal);
            var chunks = manifest.Actors.SelectMany(actor => actor.Chunks.Select(chunk => new FieldChunkUpdate(
                actor.ActorId,
                chunk.ChunkId,
                actor.FieldVersion,
                descriptors[chunk.Resources.PointScalars.ResourceId]))).ToArray();
            return FieldUpdateResult.Success(revision, new FieldUpdatedPayload(manifest.Field, chunks));
        }
    }

    public PickResolutionResult ResolvePick(PickResolveRequest request)
    {
        lock (gate)
        {
            if (request.RenderPrimitive != "triangle" || request.Mode != "element")
            {
                return PickResolutionResult.Invalid("Only triangle/element picks are supported by this prototype.");
            }

            var actor = current.Manifest.Actors.SingleOrDefault(candidate => candidate.ActorId == request.ActorId);
            var chunk = actor?.Chunks.SingleOrDefault(candidate => candidate.ChunkId == request.ChunkId);
            if (actor is null || chunk is null || !actor.Visible || !actor.Pickable)
            {
                return PickResolutionResult.Invalid("The picked actor or chunk is unavailable for selection.");
            }
            if (request.PrimitiveIndex < 0 || request.PrimitiveIndex >= chunk.TriangleCount)
            {
                return PickResolutionResult.Invalid("The rendered triangle index is outside the chunk.");
            }
            if (!HasFiniteValues(request.WorldPosition, 3)
                || !HasFiniteValues(request.DisplayPosition, 2)
                || !HasFiniteValues(request.CameraDirection, 3)
                || !double.IsFinite(request.SelectionPrecision) || request.SelectionPrecision <= 0)
            {
                return PickResolutionResult.Invalid("Pick coordinates, camera direction, and selection precision must be finite and valid.");
            }
            var sceneBounds = current.Manifest.Bounds;
            var maximumSpan = Math.Max(sceneBounds[1] - sceneBounds[0],
                Math.Max(sceneBounds[3] - sceneBounds[2], sceneBounds[5] - sceneBounds[4]));
            if (request.SelectionPrecision > Math.Max(1e-9, maximumSpan * 16))
                return PickResolutionResult.Invalid("The pick selection precision is outside the current scene scale.");

            var expectedElementId = ReadInt32(resourceCatalog[chunk.Resources.SourceElementIds.ResourceId], request.PrimitiveIndex);
            if (request.SourceElementId != expectedElementId)
            {
                return PickResolutionResult.Invalid("The source element does not match the rendered triangle.");
            }

            var connectivity = resourceCatalog[chunk.Resources.TriangleConnectivity.ResourceId];
            var pointIds = resourceCatalog[chunk.Resources.SourcePointIds.ResourceId];
            var triangleOffset = checked(request.PrimitiveIndex * 3);
            var expectedNodeIds = new int[3];
            for (var vertex = 0; vertex < 3; vertex++)
            {
                var localPointIndex = checked((int)ReadUInt32(connectivity, triangleOffset + vertex));
                if (localPointIndex < 0 || localPointIndex >= chunk.PointCount)
                {
                    return PickResolutionResult.Invalid("The stored triangle connectivity is invalid.");
                }
                expectedNodeIds[vertex] = ReadInt32(pointIds, localPointIndex);
            }
            if (!expectedNodeIds.Contains(request.CandidateSourceNodeId))
            {
                return PickResolutionResult.Invalid("The candidate source node is not a vertex of the rendered triangle.");
            }

            // Validate against the actual Float32 geometry sent to the renderer.
            // Original double mesh bounds can differ after origin-relative encoding.
            var positions = resourceCatalog[chunk.Resources.Positions.ResourceId];
            for (var axis = 0; axis < 3; axis++)
            {
                double minimum = double.PositiveInfinity, maximum = double.NegativeInfinity;
                for (var vertex = 0; vertex < 3; vertex++)
                {
                    int localIndex = checked((int)ReadUInt32(connectivity, triangleOffset + vertex));
                    double value = ReadSingle(positions, localIndex * 3 + axis);
                    minimum = Math.Min(minimum, value); maximum = Math.Max(maximum, value);
                }
                double origin = current.Manifest.WorldOrigin[axis];
                double relativeHit = request.WorldPosition[axis] - origin;
                double tolerance = Math.Max(1e-4, Math.Abs(origin) * 1.7763568394002505e-15 +
                    Math.Max(Math.Abs(minimum), Math.Abs(maximum)) * 1.7763568394002505e-15);
                if (relativeHit < minimum - tolerance || relativeHit > maximum + tolerance)
                    return PickResolutionResult.Invalid("The world hit lies outside the selected triangle bounds.");
            }

            return PickResolutionResult.Success(new ResolvedPick(
                request.ActorId,
                request.ChunkId,
                request.RenderPrimitive,
                request.PrimitiveIndex,
                expectedElementId,
                request.CandidateSourceNodeId,
                expectedNodeIds,
                request.WorldPosition,
                request.DisplayPosition,
                request.CameraDirection,
                request.SelectionPrecision,
                request.Mode,
                true));
        }
    }

    public AreaResolutionResult ResolveArea(AreaResolveRequest request)
    {
        const int maximumChunks = 4096;
        const int maximumPrimitives = 100_000;
        lock (gate)
        {
            if (request.Chunks is null || request.Chunks.Count > maximumChunks ||
                !HasFiniteValues(request.CameraDirection, 3))
                return AreaResolutionResult.Invalid("The area candidate set is malformed or exceeds its bounds.");
            if (!SelectionSectionPlaneFilter.TryCreate(request.SectionPlane, out var sectionHalfSpace))
                return AreaResolutionResult.Invalid("The section selection plane is malformed.");

            var primitiveCount = 0;
            var triangles = new List<ResolvedAreaTriangle>();
            var seen = new HashSet<(string ActorId, string ChunkId, int PrimitiveIndex)>();
            foreach (var candidate in request.Chunks)
            {
                if (candidate is null || string.IsNullOrWhiteSpace(candidate.ActorId) ||
                    string.IsNullOrWhiteSpace(candidate.ChunkId) || candidate.PrimitiveIndices is null)
                    return AreaResolutionResult.Invalid("An area chunk candidate is malformed.");
                primitiveCount = checked(primitiveCount + candidate.PrimitiveIndices.Count);
                if (primitiveCount > maximumPrimitives)
                    return AreaResolutionResult.Invalid("The area candidate set exceeds 100000 rendered triangles.");

                var actor = current.Manifest.Actors.SingleOrDefault(value => value.ActorId == candidate.ActorId);
                var chunk = actor?.Chunks.SingleOrDefault(value => value.ChunkId == candidate.ChunkId);
                if (actor is null || chunk is null || !actor.Visible || !actor.Pickable)
                    return AreaResolutionResult.Invalid("An area actor or chunk is unavailable for selection.");

                var connectivity = resourceCatalog[chunk.Resources.TriangleConnectivity.ResourceId];
                var pointIds = resourceCatalog[chunk.Resources.SourcePointIds.ResourceId];
                var elementIds = resourceCatalog[chunk.Resources.SourceElementIds.ResourceId];
                var positions = resourceCatalog[chunk.Resources.Positions.ResourceId];
                foreach (var primitiveIndex in candidate.PrimitiveIndices)
                {
                    if (primitiveIndex < 0 || primitiveIndex >= chunk.TriangleCount)
                        return AreaResolutionResult.Invalid("An area triangle index is outside its chunk.");
                    if (!seen.Add((candidate.ActorId, candidate.ChunkId, primitiveIndex))) continue;

                    var triangleNodeIds = new int[3];
                    var sectionVisible = request.SectionPlane is null;
                    var triangleOffset = checked(primitiveIndex * 3);
                    for (var vertex = 0; vertex < 3; vertex++)
                    {
                        var localPointIndex = checked((int)ReadUInt32(connectivity, triangleOffset + vertex));
                        if (localPointIndex < 0 || localPointIndex >= chunk.PointCount)
                            return AreaResolutionResult.Invalid("Stored area triangle connectivity is invalid.");
                        triangleNodeIds[vertex] = ReadInt32(pointIds, localPointIndex);
                        if (!sectionVisible)
                        {
                            var positionOffset = localPointIndex * 3;
                            sectionVisible = sectionHalfSpace.Contains(
                                current.Manifest.WorldOrigin[0] + ReadSingle(positions, positionOffset),
                                current.Manifest.WorldOrigin[1] + ReadSingle(positions, positionOffset + 1),
                                current.Manifest.WorldOrigin[2] + ReadSingle(positions, positionOffset + 2));
                        }
                    }
                    if (!sectionVisible) continue;
                    triangles.Add(new ResolvedAreaTriangle(candidate.ActorId, actor.Name,
                        ReadInt32(elementIds, primitiveIndex), triangleNodeIds));
                }
            }

            return AreaResolutionResult.Success(new ResolvedArea(
                triangles, request.CameraDirection, request.CompletelyInside, true));
        }
    }

    public FrustumResolutionResult ResolveFrustum(FrustumResolveRequest request)
    {
        lock (gate)
        {
            if (!selectionIndex.IsAvailable)
                return FrustumResolutionResult.Invalid("The current scene has no full-cell locator topology.");
            return selectionIndex.Query(request, current.Manifest);
        }
    }

    private static bool HasFiniteValues(IReadOnlyList<double>? values, int expectedCount) =>
        values is { Count: var count } && count == expectedCount && values.All(double.IsFinite);

    private ScalarFieldMetadata ApplyFieldMetadata(ScalarFieldMetadata field) => field with
    {
        Name = fieldMetadata.Name,
        Component = fieldMetadata.Component,
        Units = fieldMetadata.Units,
    };

    private static GeneratedBinaryResource CreateFrameResource(string sceneId, string semantic,
        int components, long elementCount, ResourceFill fill, int version)
    {
        var resourceId = Guid.NewGuid().ToString("D");
        return GeneratedBinaryResource.Create(resourceId, semantic, RenderDataTypes.Float32,
            components, elementCount, version, $"/api/v1/scenes/{sceneId}/resources/{resourceId}", fill);
    }

    private static double[] CalculateBounds(IReadOnlyList<double> coordinates)
    {
        var bounds = new[]
        {
            double.PositiveInfinity, double.NegativeInfinity,
            double.PositiveInfinity, double.NegativeInfinity,
            double.PositiveInfinity, double.NegativeInfinity,
        };
        for (var offset = 0; offset < coordinates.Count; offset += 3)
        {
            bounds[0] = Math.Min(bounds[0], coordinates[offset]);
            bounds[1] = Math.Max(bounds[1], coordinates[offset]);
            bounds[2] = Math.Min(bounds[2], coordinates[offset + 1]);
            bounds[3] = Math.Max(bounds[3], coordinates[offset + 1]);
            bounds[4] = Math.Min(bounds[4], coordinates[offset + 2]);
            bounds[5] = Math.Max(bounds[5], coordinates[offset + 2]);
        }
        return bounds;
    }

    private static double[] MergeBounds(IEnumerable<IReadOnlyList<double>> source)
    {
        var result = new[]
        {
            double.PositiveInfinity, double.NegativeInfinity,
            double.PositiveInfinity, double.NegativeInfinity,
            double.PositiveInfinity, double.NegativeInfinity,
        };
        foreach (var bounds in source)
        {
            result[0] = Math.Min(result[0], bounds[0]);
            result[1] = Math.Max(result[1], bounds[1]);
            result[2] = Math.Min(result[2], bounds[2]);
            result[3] = Math.Max(result[3], bounds[3]);
            result[4] = Math.Min(result[4], bounds[4]);
            result[5] = Math.Max(result[5], bounds[5]);
        }
        return result;
    }

    private static double[] CalculateScalarRange(
        IReadOnlyList<RenderActor> actors,
        IReadOnlyDictionary<string, IRenderResource> resources)
    {
        var minimum = double.PositiveInfinity;
        var maximum = double.NegativeInfinity;
        var buffer = new byte[64 * 1024];
        foreach (var chunk in actors.SelectMany(actor => actor.Chunks))
        {
            var resource = resources[chunk.Resources.PointScalars.ResourceId];
            for (var byteOffset = 0L; byteOffset < resource.Descriptor.ByteLength; byteOffset += buffer.Length)
            {
                var byteCount = checked((int)Math.Min(buffer.Length, resource.Descriptor.ByteLength - byteOffset));
                resource.Read(byteOffset, buffer.AsSpan(0, byteCount));
                for (var offset = 0; offset < byteCount; offset += sizeof(float))
                {
                    var value = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(offset, sizeof(float))));
                    if (!float.IsFinite(value)) continue;
                    minimum = Math.Min(minimum, value);
                    maximum = Math.Max(maximum, value);
                }
            }
        }

        if (!double.IsFinite(minimum)) return [0, 1];
        if (minimum == maximum) return [minimum, minimum + 1];
        return [minimum, maximum];
    }

    private static int ReadIntegerEnvironment(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var value) ? value : fallback;

    private static long ReadLongEnvironment(string name, long fallback) =>
        long.TryParse(Environment.GetEnvironmentVariable(name), out var value) ? value : fallback;

    private static int ReadInt32(IRenderResource resource, int index)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        resource.Read(checked(index * sizeof(int)), bytes);
        return BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }

    private static uint ReadUInt32(IRenderResource resource, int index)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        resource.Read(checked(index * sizeof(uint)), bytes);
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }

    private static float ReadSingle(IRenderResource resource, int index)
    {
        Span<byte> bytes = stackalloc byte[sizeof(float)];
        resource.Read(checked(index * sizeof(float)), bytes);
        return BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes));
    }

    private SelectionActorTopology[] UpdateLocatorScalars(
        IReadOnlyList<SelectionActorTopology> selectionActors,
        IReadOnlyDictionary<string, float[]> updates) => selectionActors.Select(actor =>
    {
        return updates.TryGetValue(actor.Name, out var values)
            ? actor with { Scalars = MapLocatorScalars(actor, values) }
            : actor;
    }).ToArray();

    private float[] MapLocatorScalars(SelectionActorTopology topology, IReadOnlyList<float> renderScalars)
    {
        var actor = current.Manifest.Actors.Single(candidate => candidate.Name == topology.Name);
        var valuesByNode = new Dictionary<int, float>();
        var scalarOffset = 0;
        foreach (var chunk in actor.Chunks)
        {
            var pointIds = current.Resources[chunk.Resources.SourcePointIds.ResourceId];
            for (var point = 0; point < chunk.PointCount; point++)
                valuesByNode.TryAdd(ReadInt32(pointIds, point), renderScalars[scalarOffset + point]);
            scalarOffset += chunk.PointCount;
        }
        return topology.SourceNodeIds.Select(nodeId =>
            valuesByNode.TryGetValue(nodeId, out var scalar) ? scalar : float.NaN).ToArray();
    }
}

internal sealed record SceneReplacementResult(SceneManifest Previous, SceneManifest Current, HostCameraState Camera);

internal sealed record HostCameraState(
    string Kind,
    string? View,
    IReadOnlyList<double>? Position,
    IReadOnlyList<double>? FocalPoint,
    IReadOnlyList<double>? ViewUp,
    double? ParallelScale,
    string? Operation)
{
    public static HostCameraState Standard(string view) => new("standard", view, null, null, null, null, null);

    public static HostCameraState Custom(
        IReadOnlyList<double> position,
        IReadOnlyList<double> focalPoint,
        IReadOnlyList<double> viewUp,
        double parallelScale,
        string operation) => new("custom", null, position, focalPoint, viewUp, parallelScale, operation);
}

internal sealed record ActorPresentationUpdate(
    string ActorId,
    string Name,
    IReadOnlyList<double> FrontRgba);

internal sealed record ActorMutationResult(bool Succeeded, long SceneRevision, ActorPresentationUpdate? Actor)
{
    public static ActorMutationResult Success(long revision, ActorPresentationUpdate? actor) => new(true, revision, actor);
    public static ActorMutationResult Conflict(long revision) => new(false, revision, null);
}

internal sealed record ActorRemovalResult(long SceneRevision, IReadOnlyList<string> ActorIds);

internal sealed record FrameChunkUpdate(
    string ActorId,
    string ChunkId,
    IReadOnlyList<double> Bounds,
    ResourceDescriptor Positions,
    ResourceDescriptor PointScalars);

internal sealed record FrameActorUpdate(string ActorId, IReadOnlyList<double> Bounds);

internal sealed record FrameUpdatedPayload(
    ScalarFieldMetadata Field,
    IReadOnlyList<double> Bounds,
    IReadOnlyList<FrameActorUpdate> Actors,
    IReadOnlyList<FrameChunkUpdate> Chunks);

internal sealed record FrameUpdatePhaseMetrics(double Milliseconds, long AllocatedBytes);

internal sealed record FrameUpdateDiagnostics(
    FrameUpdatePhaseMetrics Validation,
    FrameUpdatePhaseMetrics Resources,
    FrameUpdatePhaseMetrics Manifest,
    FrameUpdatePhaseMetrics Refit);

internal sealed record FrameUpdateResult(
    bool Succeeded,
    long SceneRevision,
    FrameUpdatedPayload? Payload,
    string? Error,
    FrameUpdateDiagnostics? Diagnostics)
{
    public static FrameUpdateResult Success(long revision, FrameUpdatedPayload payload, FrameUpdateDiagnostics diagnostics) =>
        new(true, revision, payload, null, diagnostics);
    public static FrameUpdateResult Invalid(long revision, string error) =>
        new(false, revision, null, error, null);
}

internal sealed record ActorVisibilityUpdate(string ActorId, bool Visible);

internal sealed record VisibilityUpdateResult(bool Succeeded, long SceneRevision, IReadOnlyList<ActorVisibilityUpdate> Actors)
{
    public static VisibilityUpdateResult Success(long revision, IReadOnlyList<ActorVisibilityUpdate> actors) =>
        new(true, revision, actors);

    public static VisibilityUpdateResult Capacity(long revision) =>
        new(false, revision, Array.Empty<ActorVisibilityUpdate>());
}

internal sealed record FieldUpdateResult(bool Succeeded, bool IsSupported, long SceneRevision, FieldUpdatedPayload? Payload)
{
    public static FieldUpdateResult Success(long revision, FieldUpdatedPayload payload) => new(true, true, revision, payload);
    public static FieldUpdateResult Conflict(long revision) => new(false, true, revision, null);
    public static FieldUpdateResult Unsupported(long revision) => new(false, false, revision, null);
}

internal sealed record ScalarUpdateResult(bool Succeeded, long SceneRevision, FieldUpdatedPayload? Payload, string? Error)
{
    public static ScalarUpdateResult Success(long revision, FieldUpdatedPayload payload) => new(true, revision, payload, null);
    public static ScalarUpdateResult Invalid(long revision, string error) => new(false, revision, null, error);
}

internal sealed record PickResolutionResult(bool Succeeded, ResolvedPick? Pick, string? Error)
{
    public static PickResolutionResult Success(ResolvedPick pick) => new(true, pick, null);
    public static PickResolutionResult Invalid(string error) => new(false, null, error);
}

internal sealed record AreaResolutionResult(bool Succeeded, ResolvedArea? Area, string? Error)
{
    public static AreaResolutionResult Success(ResolvedArea area) => new(true, area, null);
    public static AreaResolutionResult Invalid(string error) => new(false, null, error);
}

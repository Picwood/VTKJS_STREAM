using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using PrePoMax.Render.Contracts;
using PrePoMax.Render.SyntheticData;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;

namespace PrePoMax.Render.Server.Tests;

public sealed class LifecycleControlTests
{
    [Fact]
    public void SessionStore_EnforcesCapacityAndExpiresCredentials()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero));
        var store = new SessionStore(new SessionStoreOptions(TimeSpan.FromSeconds(1), 1), clock);

        Assert.True(store.TryCreate(out var first));
        Assert.NotNull(first);
        Assert.False(store.TryCreate(out _));

        var context = new DefaultHttpContext();
        context.Request.Headers["X-PrePoMax-Session"] = first.Token;
        Assert.True(store.TryAuthorize(context, out var authorized));
        Assert.Equal(first.SessionId, authorized?.SessionId);

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.False(store.TryAuthorize(context, out _));
        Assert.True(store.TryCreate(out _));
    }

    [Fact]
    public void CommandRateLimiter_UsesAnIndependentFixedWindowPerSession()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero));
        var limiter = new CommandRateLimiter(
            new CommandRateLimitOptions(2),
            new SessionStoreOptions(TimeSpan.FromMinutes(1), 2),
            clock);

        Assert.True(limiter.TryAcquire("session-a", out _));
        Assert.True(limiter.TryAcquire("session-a", out _));
        Assert.False(limiter.TryAcquire("session-a", out var retryAfter));
        Assert.Equal(TimeSpan.FromSeconds(1), retryAfter);
        Assert.True(limiter.TryAcquire("session-b", out _));

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(limiter.TryAcquire("session-a", out _));
    }

    [Fact]
    public async Task EventHub_ShutdownPublishesTerminalMessageAndCompletesStream()
    {
        var manifest = SmallFixtureFactory.Create().Manifest;
        var hub = new SceneEventHub();
        using var subscription = hub.Subscribe(manifest);

        await hub.ShutdownAsync(new CancellationToken(canceled: true));
        var message = await subscription.Reader.ReadAsync();

        Assert.Equal("server.shutting-down", message.Type);
        Assert.Equal(manifest.SceneId, message.SceneId);
        Assert.Equal(manifest.SceneRevision, message.SceneRevision);
        Assert.False(await subscription.Reader.WaitToReadAsync());
    }

    [Fact]
    public async Task WebSocket_ShutdownDeliversTerminalMessageBeforeClose()
    {
        using var factory = new RenderServerFactory();
        using var client = factory.CreateClient();
        var sessionResponse = await client.PostAsync("/api/v1/sessions", null);
        sessionResponse.EnsureSuccessStatusCode();
        var session = Assert.IsType<SessionCreated>(await sessionResponse.Content.ReadFromJsonAsync<SessionCreated>());
        var webSocketClient = factory.Server.CreateWebSocketClient();
        webSocketClient.ConfigureRequest = request => request.Headers["X-PrePoMax-Session"] = session.AccessToken;
        using var socket = await webSocketClient.ConnectAsync(
            new Uri($"ws://localhost/api/v1/scenes/{session.SceneId}/events"),
            CancellationToken.None);
        await ReceiveAsync(socket); // loading.progress

        await factory.Services.GetRequiredService<SceneEventHub>().ShutdownAsync(CancellationToken.None);
        var terminal = await ReceiveAsync(socket);
        using var document = JsonDocument.Parse(terminal.Payload);

        Assert.Equal("server.shutting-down", document.RootElement.GetProperty("type").GetString());
        var close = await socket.ReceiveAsync(new byte[128], CancellationToken.None);
        Assert.Equal(WebSocketMessageType.Close, close.MessageType);
        Assert.Equal(WebSocketCloseStatus.EndpointUnavailable, close.CloseStatus);
    }

    [Fact]
    public async Task HostSceneUpload_BuildsValidatedOriginRelativeResources()
    {
        var body = BuildHostSceneUpload(localPointOverride: null);
        var scene = await HostSceneUploadReader.ReadAsync(
            new MemoryStream(body),
            body.LongLength,
            HostSceneUploadReader.DefaultMaximumBytes,
            CancellationToken.None);

        ContractValidator.Validate(scene.Manifest);
        var actor = Assert.Single(scene.Manifest.Actors);
        var chunk = Assert.Single(actor.Chunks);
        Assert.Equal("Imported bracket", actor.Name);
        Assert.Equal(3, chunk.PointCount);
        Assert.Equal(1, chunk.TriangleCount);
        Assert.Equal([1_000_000.5, 2_000_000.5, 3_000_000.0], scene.Manifest.WorldOrigin);

        var positions = scene.Resources[chunk.Resources.Positions.ResourceId];
        var positionBytes = new byte[sizeof(float) * 3];
        positions.Read(0, positionBytes);
        Assert.Equal(-0.5f, BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(positionBytes)));
        Assert.Equal(-0.5f, BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(positionBytes[4..])));
        Assert.Equal(0f, BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(positionBytes[8..])));

        var sourceElements = scene.Resources[chunk.Resources.SourceElementIds.ResourceId];
        var elementBytes = new byte[sizeof(int)];
        sourceElements.Read(0, elementBytes);
        Assert.Equal(501, BinaryPrimitives.ReadInt32LittleEndian(elementBytes));
    }

    [Fact]
    public async Task HostSceneUpload_RejectsOutOfRangeConnectivity()
    {
        var body = BuildHostSceneUpload(localPointOverride: 3u);
        var exception = await Assert.ThrowsAsync<HostSceneUploadException>(() => HostSceneUploadReader.ReadAsync(
            new MemoryStream(body),
            body.LongLength,
            HostSceneUploadReader.DefaultMaximumBytes,
            CancellationToken.None));

        Assert.Equal("scene-upload-connectivity-invalid", exception.Code);
    }

    [Fact]
    public async Task HostSceneUpload_RejectsNonCanonicalFlags()
    {
        var body = BuildHostSceneUpload(localPointOverride: null, flags: [1, 1, 2, 0]);
        var exception = await Assert.ThrowsAsync<HostSceneUploadException>(() => HostSceneUploadReader.ReadAsync(
            new MemoryStream(body),
            body.LongLength,
            HostSceneUploadReader.DefaultMaximumBytes,
            CancellationToken.None));

        Assert.Equal("scene-upload-flags-invalid", exception.Code);
    }

    [Fact]
    public async Task HostSceneUploadV2_RetainsValidatedFullCellLocatorTopology()
    {
        var body = BuildHostSceneUploadV2(locatorPointOverride: null);
        var scene = await HostSceneUploadReader.ReadAsync(new MemoryStream(body), body.LongLength,
            HostSceneUploadReader.DefaultMaximumBytes, CancellationToken.None);

        var locator = Assert.Single(scene.SelectionActors);
        Assert.Equal("Imported bracket", locator.Name);
        Assert.Equal([101, 102, 103, 104], locator.SourceNodeIds);
        Assert.Equal(12, locator.Coordinates.Count);
        var cell = Assert.Single(locator.Cells);
        Assert.Equal(501, cell.SourceElementId);
        Assert.Equal(10, cell.VtkCellType);
        Assert.Equal([0, 1, 2, 3], cell.LocalPointIndices);
    }

    [Fact]
    public async Task HostSceneUploadV2_RejectsInvalidLocatorConnectivity()
    {
        var body = BuildHostSceneUploadV2(locatorPointOverride: 4);
        var exception = await Assert.ThrowsAsync<HostSceneUploadException>(() => HostSceneUploadReader.ReadAsync(
            new MemoryStream(body), body.LongLength, HostSceneUploadReader.DefaultMaximumBytes,
            CancellationToken.None));

        Assert.Equal("scene-upload-locator-connectivity-invalid", exception.Code);
    }

    [Fact]
    public async Task FrustumIndex_ReturnsServerDerivedNodesAndBorderCells()
    {
        var body = BuildHostSceneUploadV2(locatorPointOverride: null);
        var scene = await HostSceneUploadReader.ReadAsync(new MemoryStream(body), body.LongLength,
            HostSceneUploadReader.DefaultMaximumBytes, CancellationToken.None);
        var state = new SceneState();
        state.Replace(scene);
        IReadOnlyList<IReadOnlyList<double>> corners =
        [
            [999_999d, 1_999_999d, 2_999_999d], [1_000_002d, 1_999_999d, 2_999_999d],
            [1_000_002d, 2_000_002d, 2_999_999d], [999_999d, 2_000_002d, 2_999_999d],
            [999_999d, 1_999_999d, 3_000_002d], [1_000_002d, 1_999_999d, 3_000_002d],
            [1_000_002d, 2_000_002d, 3_000_002d], [999_999d, 2_000_002d, 3_000_002d],
        ];

        var resolution = state.ResolveFrustum(new FrustumResolveRequest(corners, [0d, 0d, -1d], true));
        Assert.True(resolution.Succeeded, resolution.Error);
        Assert.Equal([101, 102, 103, 104], resolution.Frustum!.SourceNodeIds.Order());
        Assert.Equal([501], resolution.Frustum.SourceElementIds);
        Assert.Equal(["Imported bracket"], resolution.Frustum.ActorNames);
    }

    [Fact]
    public async Task FrustumIndex_FiltersNodesAndCellsBySectionRetainedHalfSpace()
    {
        var body = BuildHostSceneUploadV2(locatorPointOverride: null);
        var scene = await HostSceneUploadReader.ReadAsync(new MemoryStream(body), body.LongLength,
            HostSceneUploadReader.DefaultMaximumBytes, CancellationToken.None);
        var state = new SceneState();
        state.Replace(scene);
        IReadOnlyList<IReadOnlyList<double>> corners =
        [
            [999_999d, 1_999_999d, 2_999_999d], [1_000_002d, 1_999_999d, 2_999_999d],
            [1_000_002d, 2_000_002d, 2_999_999d], [999_999d, 2_000_002d, 2_999_999d],
            [999_999d, 1_999_999d, 3_000_002d], [1_000_002d, 1_999_999d, 3_000_002d],
            [1_000_002d, 2_000_002d, 3_000_002d], [999_999d, 2_000_002d, 3_000_002d],
        ];

        var crossing = state.ResolveFrustum(new FrustumResolveRequest(corners, [0d, 0d, -1d], true,
            new SelectionSectionPlane([1_000_000.5d, 0d, 0d], [2d, 0d, 0d])));
        Assert.True(crossing.Succeeded, crossing.Error);
        Assert.Equal([102], crossing.Frustum!.SourceNodeIds);
        Assert.Equal([501], crossing.Frustum.SourceElementIds);

        var clipped = state.ResolveFrustum(new FrustumResolveRequest(corners, [0d, 0d, -1d], true,
            new SelectionSectionPlane([1_000_002d, 0d, 0d], [1d, 0d, 0d])));
        Assert.True(clipped.Succeeded, clipped.Error);
        Assert.Empty(clipped.Frustum!.SourceNodeIds);
        Assert.Empty(clipped.Frustum.SourceElementIds);

        var malformed = state.ResolveFrustum(new FrustumResolveRequest(corners, [0d, 0d, -1d], true,
            new SelectionSectionPlane([0d, 0d, 0d], [0d, 0d, 0d])));
        Assert.False(malformed.Succeeded);
        Assert.Contains("section selection plane", malformed.Error);
    }

    [Fact]
    public async Task AnimationFrame_RefitsLocatorBvhToDeformedCoordinates()
    {
        var body = BuildHostSceneUploadV2(locatorPointOverride: null);
        var scene = await HostSceneUploadReader.ReadAsync(new MemoryStream(body), body.LongLength,
            HostSceneUploadReader.DefaultMaximumBytes, CancellationToken.None);
        var state = new SceneState();
        state.Replace(scene);
        var renderCoordinates = new[]
        {
            1_000_100d, 2_000_000d, 3_000_000d,
            1_000_101d, 2_000_000d, 3_000_000d,
            1_000_100d, 2_000_001d, 3_000_000d,
        };
        var locatorCoordinates = new[]
        {
            1_000_100d, 2_000_000d, 3_000_000d,
            1_000_101d, 2_000_000d, 3_000_000d,
            1_000_100d, 2_000_001d, 3_000_000d,
            1_000_100d, 2_000_000d, 3_000_001d,
        };
        var update = state.UpdateAnimationFrame(new Dictionary<string, HostAnimationFrame>
        {
            ["Imported bracket"] = new(renderCoordinates, [1f, 2f, 3f], locatorCoordinates),
        });
        Assert.True(update.Succeeded, update.Error);

        IReadOnlyList<IReadOnlyList<double>> shiftedCorners =
        [
            [1_000_099d, 1_999_999d, 2_999_999d], [1_000_102d, 1_999_999d, 2_999_999d],
            [1_000_102d, 2_000_002d, 2_999_999d], [1_000_099d, 2_000_002d, 2_999_999d],
            [1_000_099d, 1_999_999d, 3_000_002d], [1_000_102d, 1_999_999d, 3_000_002d],
            [1_000_102d, 2_000_002d, 3_000_002d], [1_000_099d, 2_000_002d, 3_000_002d],
        ];
        var resolution = state.ResolveFrustum(new FrustumResolveRequest(shiftedCorners, [0d, 0d, -1d], true));
        Assert.True(resolution.Succeeded, resolution.Error);
        Assert.Equal([101, 102, 103, 104], resolution.Frustum!.SourceNodeIds.Order());
        Assert.Equal([501], resolution.Frustum.SourceElementIds);
    }

    [Fact]
    public async Task AnimationFrameV2_RefitsLocatorBvhFromCompactCoordinates()
    {
        var sceneBody = BuildHostSceneUploadV2(locatorPointOverride: null);
        var scene = await HostSceneUploadReader.ReadAsync(new MemoryStream(sceneBody), sceneBody.LongLength,
            HostSceneUploadReader.DefaultMaximumBytes, CancellationToken.None);
        var state = new SceneState();
        state.Replace(scene);
        var frameBody = BuildAnimationFrameV2(
            [1_000_000d, 2_000_000d, 3_000_000d],
            ("Imported bracket", [100f, 0f, 0f, 101f, 0f, 0f, 100f, 1f, 0f], [1f, 2f, 3f],
                [100f, 0f, 0f, 101f, 0f, 0f, 100f, 1f, 0f, 100f, 0f, 1f]));
        var frame = await HostControlReader.ReadAnimationFrameAsync(
            new MemoryStream(frameBody), frameBody.LongLength, CancellationToken.None);

        Assert.IsType<OriginRelativeCoordinateList>(frame["Imported bracket"].GetLocatorCoordinates());
        var update = state.UpdateAnimationFrame(frame);
        Assert.True(update.Succeeded, update.Error);

        IReadOnlyList<IReadOnlyList<double>> shiftedCorners =
        [
            [1_000_099d, 1_999_999d, 2_999_999d], [1_000_102d, 1_999_999d, 2_999_999d],
            [1_000_102d, 2_000_002d, 2_999_999d], [1_000_099d, 2_000_002d, 2_999_999d],
            [1_000_099d, 1_999_999d, 3_000_002d], [1_000_102d, 1_999_999d, 3_000_002d],
            [1_000_102d, 2_000_002d, 3_000_002d], [1_000_099d, 2_000_002d, 3_000_002d],
        ];
        var resolution = state.ResolveFrustum(new FrustumResolveRequest(shiftedCorners, [0d, 0d, -1d], true));
        Assert.True(resolution.Succeeded, resolution.Error);
        Assert.Equal([101, 102, 103, 104], resolution.Frustum!.SourceNodeIds.Order());
        Assert.Equal([501], resolution.Frustum.SourceElementIds);
    }

    [Fact]
    public void FrustumIndex_ReportsUnavailableForFixtureWithoutLocatorTopology()
    {
        var resolution = new SceneState().ResolveFrustum(new FrustumResolveRequest([], [0d, 0d, -1d], true));
        Assert.False(resolution.Succeeded);
        Assert.Contains("no full-cell locator", resolution.Error);
    }

    [Fact]
    public async Task HostVisibilityCommand_ValidatesAndDecodesNames()
    {
        var body = BuildVisibilityCommand(("Front triangle", false), ("Back triangle", true));
        var updates = await HostControlReader.ReadVisibilityAsync(
            new MemoryStream(body), body.LongLength, CancellationToken.None);

        Assert.Equal(2, updates.Count);
        Assert.False(updates["Front triangle"]);
        Assert.True(updates["Back triangle"]);
    }

    [Fact]
    public async Task HostVisibilityCommand_RejectsDuplicateNames()
    {
        var body = BuildVisibilityCommand(("Front triangle", false), ("Front triangle", true));
        var exception = await Assert.ThrowsAsync<HostControlException>(() => HostControlReader.ReadVisibilityAsync(
            new MemoryStream(body), body.LongLength, CancellationToken.None));

        Assert.Equal("host-visibility-entry-invalid", exception.Code);
    }

    [Fact]
    public void HostViewState_PersistsAcrossSceneReplacement()
    {
        var state = new SceneState();
        var update = state.SetActorVisibility(new Dictionary<string, bool> { ["Front triangle"] = false });
        state.SetCameraView("top");

        Assert.True(update.Succeeded);
        Assert.Single(update.Actors);
        Assert.False(state.Manifest.Actors.Single(actor => actor.Name == "Front triangle").Visible);

        var replacement = state.Replace(SmallFixtureFactory.CreateAlternateField());
        Assert.Equal("standard", replacement.Camera.Kind);
        Assert.Equal("top", replacement.Camera.View);
        Assert.False(replacement.Current.Actors.Single(actor => actor.Name == "Front triangle").Visible);

        state.SetCustomCamera(new HostCustomCamera(
            [10, 20, 30], [1, 2, 3], [0, 1, 0], 42, "fit"));
        var customReplacement = state.Replace(SmallFixtureFactory.Create());
        Assert.Equal("custom", customReplacement.Camera.Kind);
        Assert.Equal("fit", customReplacement.Camera.Operation);
        Assert.Equal(42, customReplacement.Camera.ParallelScale);
    }

    [Fact]
    public async Task HostActorMutation_DecodesAndUpdatesPresentation()
    {
        var body = BuildActorMutation("Front triangle", "Renamed triangle", unchecked((int)0xff336699));
        var mutation = await HostControlReader.ReadActorMutationAsync(
            new MemoryStream(body), body.LongLength, CancellationToken.None);
        var state = new SceneState();
        var update = state.UpdateActor(mutation);

        Assert.True(update.Succeeded);
        Assert.NotNull(update.Actor);
        Assert.Equal("Renamed triangle", update.Actor.Name);
        var actor = state.Manifest.Actors.Single(candidate => candidate.Name == "Renamed triangle");
        Assert.Equal([0.2, 0.4, 0.6, 1.0], actor.Material.FrontRgba);
    }

    [Fact]
    public async Task HostActorRemoval_DecodesAndRemovesActorResourcesAndSelectionTopology()
    {
        var body = BuildActorRemoval("Front triangle", "missing actor");
        var names = await HostControlReader.ReadActorRemovalAsync(
            new MemoryStream(body), body.LongLength, CancellationToken.None);
        var state = new SceneState();
        var before = state.Manifest;
        var removedActor = before.Actors.Single(actor => actor.Name == "Front triangle");
        var removedResourceId = removedActor.Chunks[0].Resources.Positions.ResourceId;

        var update = state.RemoveActors(names);

        Assert.Equal(before.SceneRevision + 1, update.SceneRevision);
        Assert.Equal([removedActor.ActorId], update.ActorIds);
        Assert.DoesNotContain(state.Manifest.Actors, actor => actor.Name == "Front triangle");
        Assert.False(state.TryGetResource(removedResourceId, out _));
        Assert.False(state.ResolveFrustum(new FrustumResolveRequest([], [0d, 0d, -1d], true)).Succeeded);
    }

    [Fact]
    public async Task HostActorRemoval_RejectsDuplicateNames()
    {
        var body = BuildActorRemoval("Front triangle", "Front triangle");
        var exception = await Assert.ThrowsAsync<HostControlException>(() => HostControlReader.ReadActorRemovalAsync(
            new MemoryStream(body), body.LongLength, CancellationToken.None));
        Assert.Equal("host-removal-entry-invalid", exception.Code);
    }

    [Fact]
    public async Task HostAnimationFrame_ReplacesOnlyPositionsAndScalars()
    {
        double[] coordinates =
        [
            1_234_567, 2_345_678, 3_456_789.5,
            1_234_568, 2_345_678, 3_456_789.5,
            1_234_567, 2_345_679, 3_456_789.5,
        ];
        var body = BuildAnimationFrame(("Front triangle", coordinates, new[] { 4f, 5f, 6f }, Array.Empty<double>()));
        var frame = await HostControlReader.ReadAnimationFrameAsync(
            new MemoryStream(body), body.LongLength, CancellationToken.None);
        var state = new SceneState();
        var before = state.Manifest.Actors.Single(actor => actor.Name == "Front triangle").Chunks[0];

        var update = state.UpdateAnimationFrame(frame);

        Assert.True(update.Succeeded, update.Error);
        var diagnostics = Assert.IsType<FrameUpdateDiagnostics>(update.Diagnostics);
        Assert.All(new[]
        {
            diagnostics.Validation,
            diagnostics.Resources,
            diagnostics.Manifest,
            diagnostics.Refit,
        }, phase =>
        {
            Assert.True(phase.Milliseconds >= 0);
            Assert.True(phase.AllocatedBytes >= 0);
        });
        var after = state.Manifest.Actors.Single(actor => actor.Name == "Front triangle").Chunks[0];
        Assert.NotEqual(before.Resources.Positions.ResourceId, after.Resources.Positions.ResourceId);
        Assert.NotEqual(before.Resources.PointScalars.ResourceId, after.Resources.PointScalars.ResourceId);
        Assert.Equal(before.Resources.TriangleConnectivity.ResourceId, after.Resources.TriangleConnectivity.ResourceId);
        Assert.Equal(before.Resources.SourcePointIds.ResourceId, after.Resources.SourcePointIds.ResourceId);
        Assert.Equal(before.Resources.SourceElementIds.ResourceId, after.Resources.SourceElementIds.ResourceId);
        Assert.Equal(3_456_789.5, after.Bounds[4]);
        Assert.Equal([-1d, 6d], state.Manifest.Field.ValidRange);

        Assert.True(state.TryGetResource(after.Resources.Positions.ResourceId, out var positionResource));
        Assert.True(state.TryGetResource(after.Resources.PointScalars.ResourceId, out var scalarResource));
        Assert.IsType<GeneratedBinaryResource>(positionResource);
        Assert.IsType<GeneratedBinaryResource>(scalarResource);
        var origin = state.Manifest.WorldOrigin;
        var expectedPositions = LittleEndianArrayWriter.Write(coordinates
            .Select((value, index) => (float)(value - origin[index % 3])).ToArray());
        var expectedScalars = LittleEndianArrayWriter.Write(new[] { 4f, 5f, 6f });
        var positionRange = new byte[expectedPositions.Length - 2];
        positionResource!.Read(1, positionRange);
        Assert.Equal(expectedPositions[1..^1], positionRange);
        var scalarRange = new byte[expectedScalars.Length - 2];
        scalarResource!.Read(1, scalarRange);
        Assert.Equal(expectedScalars[1..^1], scalarRange);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(expectedPositions)).ToLowerInvariant(),
            positionResource.Descriptor.Sha256);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(expectedScalars)).ToLowerInvariant(),
            scalarResource.Descriptor.Sha256);
    }

    [Fact]
    public async Task HostAnimationFrame_RejectsNonFiniteCoordinates()
    {
        var body = BuildAnimationFrame(("Front triangle",
            new[] { double.NaN, 0d, 0d }, new[] { 1f }, Array.Empty<double>()));
        var exception = await Assert.ThrowsAsync<HostControlException>(() => HostControlReader.ReadAnimationFrameAsync(
            new MemoryStream(body), body.LongLength, CancellationToken.None));
        Assert.Equal("host-frame-numeric-invalid", exception.Code);
    }

    [Fact]
    public async Task HostAnimationFrameV2_ReconstructsOriginRelativeFloatCoordinates()
    {
        var body = BuildAnimationFrameV2(
            [1_234_567d, 2_345_678d, 3_456_789d],
            ("Front triangle", new[] { 0f, 0f, 0.5f, 1f, 0f, 0.5f, 0f, 1f, 0.5f },
                new[] { 4f, 5f, 6f }, new[] { -1f, 2f, 3f }));

        var frame = await HostControlReader.ReadAnimationFrameAsync(
            new MemoryStream(body), body.LongLength, CancellationToken.None);

        Assert.True(frame["Front triangle"].UsesCompactCoordinates);
        Assert.Equal(48L, frame["Front triangle"].CoordinateStorageBytes);
        Assert.Equal(96L, frame["Front triangle"].ExpandedCoordinateBytes);
        Assert.Equal(1_234_567d, frame["Front triangle"].GetCoordinate(0));
        Assert.Equal(3_456_789.5d, frame["Front triangle"].GetCoordinate(2));
        Assert.Equal([1_234_566d, 2_345_680d, 3_456_792d],
            frame["Front triangle"].MaterializeLocatorCoordinates());
    }

    [Fact]
    public async Task HostAnimationFrameV2_ReadsFragmentedRequestBody()
    {
        var body = BuildAnimationFrameV2(
            [10d, 20d, 30d],
            ("Front triangle", new[] { 1f, 2f, 3f, 4f, 5f, 6f },
                new[] { 7f, 8f }, new[] { 9f, 10f, 11f }));

        var frame = await HostControlReader.ReadAnimationFrameAsync(
            new FragmentedReadStream(body, 3), body.LongLength, CancellationToken.None);

        Assert.Equal(2, frame["Front triangle"].Scalars.Length);
        Assert.Equal(11d, frame["Front triangle"].GetCoordinate(0));
        Assert.Equal([19d, 30d, 41d], frame["Front triangle"].MaterializeLocatorCoordinates());
    }

    [Fact]
    public async Task HostAnimationFrameV2_ReportsTruncatedRequestAsProtocolError()
    {
        var body = BuildAnimationFrameV2(
            [10d, 20d, 30d],
            ("Front triangle", new[] { 1f, 2f, 3f }, new[] { 7f }, new[] { 9f, 10f, 11f }));

        var exception = await Assert.ThrowsAsync<HostControlException>(() => HostControlReader.ReadAnimationFrameAsync(
            new MemoryStream(body[..^1]), body.LongLength, CancellationToken.None));

        Assert.Equal("host-frame-format-invalid", exception.Code);
    }

    [Fact]
    public async Task HostCustomCamera_ValidatesNonDegenerateVectors()
    {
        var body = BuildCustomCamera(
            [10, 20, 30], [1, 2, 3], [0, 1, 0], 42);
        var camera = await HostControlReader.ReadCustomCameraAsync(
            new MemoryStream(body), body.LongLength, CancellationToken.None);

        Assert.Equal([10, 20, 30], camera.Position);
        Assert.Equal(42, camera.ParallelScale);
        Assert.Equal("set", camera.Operation);

        var fitBody = BuildCustomCameraV2(
            [10, 20, 30], [1, 2, 3], [0, 1, 0], 42, 1);
        var fit = await HostControlReader.ReadCustomCameraAsync(
            new MemoryStream(fitBody), fitBody.LongLength, CancellationToken.None);
        Assert.Equal("fit", fit.Operation);

        var invalidOperation = BuildCustomCameraV2(
            [10, 20, 30], [1, 2, 3], [0, 1, 0], 42, 3);
        var operationException = await Assert.ThrowsAsync<HostControlException>(() =>
            HostControlReader.ReadCustomCameraAsync(
                new MemoryStream(invalidOperation), invalidOperation.LongLength, CancellationToken.None));
        Assert.Equal("host-camera-operation-invalid", operationException.Code);

        var invalid = BuildCustomCamera([1, 1, 1], [1, 1, 1], [0, 1, 0], 42);
        var exception = await Assert.ThrowsAsync<HostControlException>(() => HostControlReader.ReadCustomCameraAsync(
            new MemoryStream(invalid), invalid.LongLength, CancellationToken.None));
        Assert.Equal("host-camera-numeric-invalid", exception.Code);
    }

    [Fact]
    public async Task HostFieldCommands_DecodeMetadataAndScalarValues()
    {
        var metadataBody = BuildFieldMetadata("Displacements", "Magnitude", "mm");
        var metadata = await HostControlReader.ReadFieldMetadataAsync(
            new MemoryStream(metadataBody), metadataBody.LongLength, CancellationToken.None);
        var scalarBody = BuildScalarUpdate(("Front triangle", new[] { 3f, float.NaN, -2f }));
        var scalars = await HostControlReader.ReadScalarUpdateAsync(
            new MemoryStream(scalarBody), scalarBody.LongLength, CancellationToken.None);

        Assert.Equal(new HostFieldMetadata("Displacements", "Magnitude", "mm"), metadata);
        Assert.Equal(3f, scalars["Front triangle"][0]);
        Assert.True(float.IsNaN(scalars["Front triangle"][1]));
    }

    [Fact]
    public void HostScalarUpdate_ReplacesOnlyScalarResourcesAndAppliesMetadata()
    {
        var state = new SceneState();
        state.SetFieldMetadata(new HostFieldMetadata("Displacements", "Magnitude", "mm"));
        var before = state.Manifest;
        var frontBefore = before.Actors.Single(actor => actor.Name == "Front triangle");
        var backBefore = before.Actors.Single(actor => actor.Name == "Back triangle");

        var update = state.UpdateScalars(new Dictionary<string, float[]>
        {
            ["Front triangle"] = [3f, float.NaN, -2f],
        });

        Assert.True(update.Succeeded);
        Assert.Equal(before.SceneRevision + 1, update.SceneRevision);
        Assert.Single(update.Payload!.Chunks);
        Assert.Equal("Displacements", update.Payload.Field.Name);
        Assert.Equal("Magnitude", update.Payload.Field.Component);
        Assert.Equal("mm", update.Payload.Field.Units);
        Assert.Equal([-2d, 3d], update.Payload.Field.ValidRange);
        var frontAfter = state.Manifest.Actors.Single(actor => actor.Name == "Front triangle");
        var backAfter = state.Manifest.Actors.Single(actor => actor.Name == "Back triangle");
        Assert.Equal(frontBefore.FieldVersion + 1, frontAfter.FieldVersion);
        Assert.NotEqual(frontBefore.Chunks[0].Resources.PointScalars, frontAfter.Chunks[0].Resources.PointScalars);
        Assert.Equal(frontBefore.Chunks[0].Resources.Positions, frontAfter.Chunks[0].Resources.Positions);
        Assert.Equal(frontBefore.Chunks[0].Resources.TriangleConnectivity, frontAfter.Chunks[0].Resources.TriangleConnectivity);
        Assert.Equal(backBefore, backAfter);
    }

    private static byte[] BuildHostSceneUpload(uint? localPointOverride, byte[]? flags = null)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("PMXSCN1\0"));
            writer.Write(1);
            var name = Encoding.UTF8.GetBytes("Imported bracket");
            writer.Write(name.Length);
            writer.Write(name);
            writer.Write(unchecked((int)0xff6699cc));
            writer.Write(flags ?? [1, 1, 1, 0]);
            writer.Write(0.2d);
            writer.Write(0.8d);
            writer.Write(1.5f);
            writer.Write(4f);
            writer.Write(3);
            writer.Write(1);
            WritePoint(writer, 101, 1_000_000, 2_000_000, 3_000_000, 1f);
            WritePoint(writer, 102, 1_000_001, 2_000_000, 3_000_000, 2f);
            WritePoint(writer, 103, 1_000_000, 2_000_001, 3_000_000, 3f);
            writer.Write(501);
            writer.Write(0u);
            writer.Write(1u);
            writer.Write(localPointOverride ?? 2u);
        }
        return stream.ToArray();
    }

    private static byte[] BuildHostSceneUploadV2(int? locatorPointOverride)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("PMXSCN2\0"));
            writer.Write(1);
            var name = Encoding.UTF8.GetBytes("Imported bracket");
            writer.Write(name.Length);
            writer.Write(name);
            writer.Write(unchecked((int)0xff6699cc));
            writer.Write(new byte[] { 1, 1, 1, 0 });
            writer.Write(0.2d);
            writer.Write(0.8d);
            writer.Write(1.5f);
            writer.Write(4f);
            writer.Write(3); // render points
            writer.Write(1); // render triangles
            writer.Write(4); // locator points
            writer.Write(1); // locator cells
            writer.Write(4); // locator connectivity references
            WritePoint(writer, 101, 1_000_000, 2_000_000, 3_000_000, 1f);
            WritePoint(writer, 102, 1_000_001, 2_000_000, 3_000_000, 2f);
            WritePoint(writer, 103, 1_000_000, 2_000_001, 3_000_000, 3f);
            writer.Write(501);
            writer.Write(0u);
            writer.Write(1u);
            writer.Write(2u);
            WriteLocatorPoint(writer, 101, 1_000_000, 2_000_000, 3_000_000);
            WriteLocatorPoint(writer, 102, 1_000_001, 2_000_000, 3_000_000);
            WriteLocatorPoint(writer, 103, 1_000_000, 2_000_001, 3_000_000);
            WriteLocatorPoint(writer, 104, 1_000_000, 2_000_000, 3_000_001);
            writer.Write(501);
            writer.Write(10); // VTK_TETRA
            writer.Write(4);
            writer.Write(0);
            writer.Write(1);
            writer.Write(2);
            writer.Write(locatorPointOverride ?? 3);
        }
        return stream.ToArray();
    }

    private static void WriteLocatorPoint(BinaryWriter writer, int id, double x, double y, double z)
    {
        writer.Write(id);
        writer.Write(x);
        writer.Write(y);
        writer.Write(z);
    }

    private static byte[] BuildVisibilityCommand(params (string Name, bool Visible)[] entries)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("PMXVIZ1\0"));
            writer.Write(entries.Length);
            foreach (var entry in entries)
            {
                var name = Encoding.UTF8.GetBytes(entry.Name);
                writer.Write(name.Length);
                writer.Write(name);
                writer.Write(entry.Visible ? (byte)1 : (byte)0);
            }
        }
        return stream.ToArray();
    }

    private static byte[] BuildActorMutation(string oldName, string newName, int argb)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("PMXACT1\0"));
            foreach (var nameValue in new[] { oldName, newName })
            {
                var name = Encoding.UTF8.GetBytes(nameValue);
                writer.Write(name.Length);
                writer.Write(name);
            }
            writer.Write(argb);
        }
        return stream.ToArray();
    }

    private static byte[] BuildActorRemoval(params string[] names)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("PMXREM1\0"));
            writer.Write(names.Length);
            foreach (var value in names)
            {
                var name = Encoding.UTF8.GetBytes(value);
                writer.Write(name.Length);
                writer.Write(name);
            }
        }
        return stream.ToArray();
    }

    private static byte[] BuildAnimationFrame(
        params (string Name, double[] Coordinates, float[] Scalars, double[] LocatorCoordinates)[] actors)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("PMXFRM1\0"));
            writer.Write(actors.Length);
            foreach (var actor in actors)
            {
                var name = Encoding.UTF8.GetBytes(actor.Name);
                writer.Write(name.Length);
                writer.Write(name);
                writer.Write(actor.Scalars.Length);
                for (var point = 0; point < actor.Scalars.Length; point++)
                {
                    writer.Write(actor.Coordinates[point * 3]);
                    writer.Write(actor.Coordinates[point * 3 + 1]);
                    writer.Write(actor.Coordinates[point * 3 + 2]);
                    writer.Write(actor.Scalars[point]);
                }
                writer.Write(actor.LocatorCoordinates.Length / 3);
                foreach (var coordinate in actor.LocatorCoordinates) writer.Write(coordinate);
            }
        }
        return stream.ToArray();
    }

    private static byte[] BuildAnimationFrameV2(
        double[] origin,
        params (string Name, float[] Coordinates, float[] Scalars, float[] LocatorCoordinates)[] actors)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("PMXFRM2\0"));
            writer.Write(actors.Length);
            foreach (var value in origin) writer.Write(value);
            foreach (var actor in actors)
            {
                var name = Encoding.UTF8.GetBytes(actor.Name);
                writer.Write(name.Length);
                writer.Write(name);
                writer.Write(actor.Scalars.Length);
                for (var point = 0; point < actor.Scalars.Length; point++)
                {
                    writer.Write(actor.Coordinates[point * 3]);
                    writer.Write(actor.Coordinates[point * 3 + 1]);
                    writer.Write(actor.Coordinates[point * 3 + 2]);
                    writer.Write(actor.Scalars[point]);
                }
                writer.Write(actor.LocatorCoordinates.Length / 3);
                foreach (var coordinate in actor.LocatorCoordinates) writer.Write(coordinate);
            }
        }
        return stream.ToArray();
    }

    private static byte[] BuildCustomCamera(
        double[] position,
        double[] focalPoint,
        double[] viewUp,
        double parallelScale)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("PMXCAM1\0"));
            foreach (var value in position.Concat(focalPoint).Concat(viewUp)) writer.Write(value);
            writer.Write(parallelScale);
        }
        return stream.ToArray();
    }

    private static byte[] BuildCustomCameraV2(
        double[] position,
        double[] focalPoint,
        double[] viewUp,
        double parallelScale,
        int operation)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("PMXCAM2\0"));
            writer.Write(operation);
            foreach (var value in position.Concat(focalPoint).Concat(viewUp)) writer.Write(value);
            writer.Write(parallelScale);
        }
        return stream.ToArray();
    }

    private static byte[] BuildFieldMetadata(string name, string component, string units)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("PMXFLD1\0"));
            foreach (var value in new[] { name, component, units })
            {
                var bytes = Encoding.UTF8.GetBytes(value);
                writer.Write(bytes.Length);
                writer.Write(bytes);
            }
        }
        return stream.ToArray();
    }

    private static byte[] BuildScalarUpdate(params (string Name, float[] Values)[] entries)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("PMXSCL1\0"));
            writer.Write(entries.Length);
            foreach (var entry in entries)
            {
                var name = Encoding.UTF8.GetBytes(entry.Name);
                writer.Write(name.Length);
                writer.Write(name);
                writer.Write(entry.Values.Length);
                foreach (var value in entry.Values) writer.Write(value);
            }
        }
        return stream.ToArray();
    }

    private static void WritePoint(BinaryWriter writer, int id, double x, double y, double z, float scalar)
    {
        writer.Write(id);
        writer.Write(x);
        writer.Write(y);
        writer.Write(z);
        writer.Write(scalar);
    }

    private static async Task<(byte[] Payload, WebSocketReceiveResult Result)> ReceiveAsync(WebSocket socket)
    {
        var buffer = new byte[4096];
        var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
        Assert.Equal(WebSocketMessageType.Text, result.MessageType);
        return (buffer[..result.Count], result);
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset current = utcNow;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan duration) => current += duration;
    }

    private sealed class FragmentedReadStream(byte[] body, int maximumRead) : MemoryStream(body)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, maximumRead)], cancellationToken);
    }
}

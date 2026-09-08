using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PrePoMax.Render.Contracts;

namespace PrePoMax.Render.Server.Tests;

public sealed class ProtocolEndpointTests(RenderServerFactory factory) : IClassFixture<RenderServerFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ProductionSessionBootstrap_RequiresHostAuthorityReusesSessionAndAdoptsCookie()
    {
        const string hostToken = "production-test-host-token";
        using var production = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("PREPOMAX_RENDER_HOST_TOKEN", hostToken);
        });
        using var client = production.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsync("/api/v1/sessions", null)).StatusCode);
        using (var invalid = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sessions"))
        {
            invalid.Headers.Add("X-PrePoMax-Host-Token", "invalid");
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(invalid)).StatusCode);
        }

        SessionCreated first;
        using (var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sessions"))
        {
            create.Headers.Add("X-PrePoMax-Host-Token", hostToken);
            var response = await client.SendAsync(create);
            response.EnsureSuccessStatusCode();
            first = Assert.IsType<SessionCreated>(
                await response.Content.ReadFromJsonAsync<SessionCreated>(JsonOptions));
        }

        using (var reuse = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sessions"))
        {
            reuse.Headers.Add("X-PrePoMax-Host-Token", hostToken);
            reuse.Headers.Add("X-PrePoMax-Session", first.AccessToken);
            var response = await client.SendAsync(reuse);
            response.EnsureSuccessStatusCode();
            var second = Assert.IsType<SessionCreated>(
                await response.Content.ReadFromJsonAsync<SessionCreated>(JsonOptions));
            Assert.Equal(first, second);
        }

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsync("/api/v1/sessions/adopt", null)).StatusCode);
        using (var adopt = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sessions/adopt"))
        {
            adopt.Headers.Add("X-PrePoMax-Session", first.AccessToken);
            adopt.Headers.Add("Cookie", "prepomax-render-session=stale-cookie-from-another-loopback-port");
            var response = await client.SendAsync(adopt);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
            Assert.Contains("prepomax-render-session=", cookie);
            Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Manifest_RequiresSessionAndContainsOnlyResourceDescriptors()
    {
        using var anonymous = factory.CreateClient();
        var unauthorized = await anonymous.GetAsync($"/api/v1/scenes/{SyntheticSceneId}/manifest");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal("session-invalid", (await unauthorized.Content.ReadFromJsonAsync<ApiError>(JsonOptions))?.Code);

        using var authorized = await CreateAuthorizedClientAsync();
        var response = await authorized.GetAsync($"/api/v1/scenes/{SyntheticSceneId}/manifest");
        response.EnsureSuccessStatusCode();
        Assert.Equal(ProtocolConstants.Version, GetSingleHeader(response, ProtocolConstants.HeaderName));

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        Assert.Equal(2, document.RootElement.GetProperty("actors").GetArrayLength());
        Assert.Equal(10, document.RootElement.GetProperty("resources").GetArrayLength());
        Assert.DoesNotContain("\"connectivity\":[", json, StringComparison.OrdinalIgnoreCase);
        Assert.True(response.Content.Headers.ContentLength is null or < 1_048_576);
    }

    [Fact]
    public async Task Resource_SupportsRangesStableEtagsAndConditionalRequests()
    {
        using var client = await CreateAuthorizedClientAsync();
        var uri = $"/api/v1/scenes/{SyntheticSceneId}/resources/{PositionsResourceId}";
        var complete = await client.GetAsync(uri);
        complete.EnsureSuccessStatusCode();
        Assert.Equal(36, complete.Content.Headers.ContentLength);
        Assert.Equal(ProtocolConstants.ResourceContentType, complete.Content.Headers.ContentType?.MediaType);
        Assert.Equal("bytes", GetSingleHeader(complete, "Accept-Ranges"));
        Assert.Contains("immutable", GetSingleHeader(complete, "Cache-Control"), StringComparison.Ordinal);
        var etag = Assert.IsType<EntityTagHeaderValue>(complete.Headers.ETag);

        using var rangeRequest = new HttpRequestMessage(HttpMethod.Get, uri);
        rangeRequest.Headers.Range = new RangeHeaderValue(0, 3);
        var range = await client.SendAsync(rangeRequest);
        Assert.Equal(HttpStatusCode.PartialContent, range.StatusCode);
        Assert.Equal(4, (await range.Content.ReadAsByteArrayAsync()).Length);
        Assert.Equal("bytes 0-3/36", range.Content.Headers.ContentRange?.ToString());

        using var conditionalRequest = new HttpRequestMessage(HttpMethod.Get, uri);
        conditionalRequest.Headers.IfNoneMatch.Add(etag);
        var conditional = await client.SendAsync(conditionalRequest);
        Assert.Equal(HttpStatusCode.NotModified, conditional.StatusCode);
    }

    [Fact]
    public async Task SectionGeometry_IsAuthenticatedBoundedBinaryAndDoesNotAdvanceRevision()
    {
        using var anonymous = factory.CreateClient();
        var request = new SectionResolveRequest([0, 0, 0], [1, 0, 0], true, [0, 0, 0, 0]);
        var unauthorized = await anonymous.PostAsJsonAsync($"/api/v1/scenes/{SyntheticSceneId}/section", request, JsonOptions);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var client = await CreateAuthorizedClientAsync();
        var before = Assert.IsType<SceneManifest>(await client.GetFromJsonAsync<SceneManifest>(
            $"/api/v1/scenes/{SyntheticSceneId}/manifest", JsonOptions));
        var response = await client.PostAsJsonAsync($"/api/v1/scenes/{SyntheticSceneId}/section", request, JsonOptions);
        response.EnsureSuccessStatusCode();
        Assert.Equal(SectionGeometryWriter.ContentType, response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("PMXSEC1\0", System.Text.Encoding.ASCII.GetString(bytes, 0, 8));
        Assert.Equal(0, BitConverter.ToInt32(bytes, 8));
        Assert.Equal(0, BitConverter.ToInt32(bytes, 12));
        var after = Assert.IsType<SceneManifest>(await client.GetFromJsonAsync<SceneManifest>(
            $"/api/v1/scenes/{SyntheticSceneId}/manifest", JsonOptions));
        Assert.Equal(before.SceneRevision, after.SceneRevision);
    }

    [Fact]
    public async Task UnsupportedProtocolMajor_ReturnsStableUpgradeError()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/scenes");
        request.Headers.Add(ProtocolConstants.HeaderName, "2.0");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.UpgradeRequired, response.StatusCode);
        Assert.Equal("protocol-version-unsupported", (await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions))?.Code);
    }

    [Fact]
    public async Task WebSocket_UsesSessionAuthenticationAndEmitsControlJson()
    {
        var session = await CreateSessionAsync();
        var webSocketClient = factory.Server.CreateWebSocketClient();
        webSocketClient.ConfigureRequest = request => request.Headers["X-PrePoMax-Session"] = session.AccessToken;
        using var socket = await webSocketClient.ConnectAsync(
            new Uri($"ws://localhost/api/v1/scenes/{session.SceneId}/events"),
            CancellationToken.None);

        var bytes = new byte[4096];
        var result = await socket.ReceiveAsync(bytes, CancellationToken.None);
        Assert.Equal(WebSocketMessageType.Text, result.MessageType);
        using var message = JsonDocument.Parse(bytes.AsMemory(0, result.Count));
        Assert.Equal(ProtocolConstants.Version, message.RootElement.GetProperty("protocol").GetString());
        Assert.Equal("loading.progress", message.RootElement.GetProperty("type").GetString());
        Assert.Equal(session.SceneId, message.RootElement.GetProperty("sceneId").GetString());

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "test complete", CancellationToken.None);
    }

    [Fact]
    public async Task FieldToggle_PublishesOnlyNewScalarDescriptorsAndRejectsStaleRevision()
    {
        var session = await CreateSessionAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-PrePoMax-Session", session.AccessToken);
        var before = Assert.IsType<SceneManifest>(await client.GetFromJsonAsync<SceneManifest>(
            $"/api/v1/scenes/{session.SceneId}/manifest",
            JsonOptions));

        var webSocketClient = factory.Server.CreateWebSocketClient();
        webSocketClient.ConfigureRequest = request => request.Headers["X-PrePoMax-Session"] = session.AccessToken;
        using var socket = await webSocketClient.ConnectAsync(
            new Uri($"ws://localhost/api/v1/scenes/{session.SceneId}/events"),
            CancellationToken.None);
        await ReceiveMessageAsync(socket); // loading.progress

        var command = new ProtocolMessage(
            ProtocolConstants.Version,
            Guid.NewGuid().ToString("D"),
            null,
            session.SceneId,
            before.SceneRevision,
            "field.toggle",
            DateTimeOffset.UtcNow,
            new { });
        var commandResponse = await client.PostAsJsonAsync(
            $"/api/v1/scenes/{session.SceneId}/commands",
            command,
            JsonOptions);
        commandResponse.EnsureSuccessStatusCode();

        using var eventDocument = JsonDocument.Parse(await ReceiveMessageAsync(socket));
        Assert.Equal("field.updated", eventDocument.RootElement.GetProperty("type").GetString());
        Assert.Equal(before.SceneRevision + 1, eventDocument.RootElement.GetProperty("sceneRevision").GetInt64());
        var payload = eventDocument.RootElement.GetProperty("payload");
        Assert.Equal(2, payload.GetProperty("chunks").GetArrayLength());
        foreach (var chunk in payload.GetProperty("chunks").EnumerateArray())
        {
            var resource = chunk.GetProperty("resource");
            Assert.Equal("pointScalars", resource.GetProperty("semantic").GetString());
            Assert.Equal(12, resource.GetProperty("byteLength").GetInt64());
        }

        var after = Assert.IsType<SceneManifest>(await client.GetFromJsonAsync<SceneManifest>(
            $"/api/v1/scenes/{session.SceneId}/manifest",
            JsonOptions));
        Assert.Equal(before.SceneRevision + 1, after.SceneRevision);
        Assert.Equal(TopologyResourceIds(before), TopologyResourceIds(after));
        Assert.False(ScalarResourceIds(before).SequenceEqual(ScalarResourceIds(after), StringComparer.Ordinal));

        var stale = await client.PostAsJsonAsync(
            $"/api/v1/scenes/{session.SceneId}/commands",
            command,
            JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("revision-conflict", (await stale.Content.ReadFromJsonAsync<ApiError>(JsonOptions))?.Code);
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "test complete", CancellationToken.None);
    }

    [Fact]
    public async Task PickResolve_ValidatesTopologyMappingAndLargeWorldHit()
    {
        var session = await CreateSessionAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-PrePoMax-Session", session.AccessToken);
        var manifest = Assert.IsType<SceneManifest>(await client.GetFromJsonAsync<SceneManifest>(
            $"/api/v1/scenes/{session.SceneId}/manifest",
            JsonOptions));
        var actor = manifest.Actors.Single(candidate => candidate.ActorId == "20000000-0000-0000-0000-000000000001");
        var chunk = Assert.Single(actor.Chunks);
        var pick = new PickResolveRequest(
            actor.ActorId,
            chunk.ChunkId,
            "triangle",
            0,
            1001,
            101,
            [manifest.WorldOrigin[0] + 0.2, manifest.WorldOrigin[1] + 0.2, manifest.WorldOrigin[2] + 0.25],
            [320.0, 240.0],
            [0.0, 0.0, -1.0],
            0.01,
            "element");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/scenes/{session.SceneId}/commands",
            PickCommand(session.SceneId, manifest.SceneRevision, pick),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var resolved = document.RootElement.GetProperty("payload").GetProperty("result");
        Assert.True(resolved.GetProperty("validated").GetBoolean());
        Assert.Equal(1001, resolved.GetProperty("sourceElementId").GetInt32());
        Assert.Equal(101, resolved.GetProperty("candidateSourceNodeId").GetInt32());
        Assert.Equal(new[] { 101, 102, 103 },
                     resolved.GetProperty("triangleSourceNodeIds").EnumerateArray().Select(value => value.GetInt32()));
        Assert.Equal(manifest.WorldOrigin[0] + 0.2, resolved.GetProperty("worldPosition")[0].GetDouble(), 8);
        Assert.Equal(0.01, resolved.GetProperty("selectionPrecision").GetDouble(), 8);

        var forged = pick with { SourceElementId = 9999 };
        var rejected = await client.PostAsJsonAsync(
            $"/api/v1/scenes/{session.SceneId}/commands",
            PickCommand(session.SceneId, manifest.SceneRevision, forged),
            JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal("pick-invalid", (await rejected.Content.ReadFromJsonAsync<ApiError>(JsonOptions))?.Code);

        var excessivePrecision = pick with { SelectionPrecision = 1e30 };
        rejected = await client.PostAsJsonAsync(
            $"/api/v1/scenes/{session.SceneId}/commands",
            PickCommand(session.SceneId, manifest.SceneRevision, excessivePrecision),
            JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal("pick-invalid", (await rejected.Content.ReadFromJsonAsync<ApiError>(JsonOptions))?.Code);
    }

    [Fact]
    public async Task AreaResolve_DerivesTopologyAndRejectsOutOfRangePrimitive()
    {
        var session = await CreateSessionAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-PrePoMax-Session", session.AccessToken);
        var manifest = Assert.IsType<SceneManifest>(await client.GetFromJsonAsync<SceneManifest>(
            $"/api/v1/scenes/{session.SceneId}/manifest", JsonOptions));
        var actor = manifest.Actors.Single(candidate => candidate.ActorId == "20000000-0000-0000-0000-000000000001");
        var chunk = Assert.Single(actor.Chunks);
        var area = new AreaResolveRequest(
            [new AreaChunkCandidate(actor.ActorId, chunk.ChunkId, [0])],
            [0.0, 0.0, -1.0], true);
        var command = new ProtocolMessage(ProtocolConstants.Version, Guid.NewGuid().ToString("D"), null,
            session.SceneId, manifest.SceneRevision, "area.resolve", DateTimeOffset.UtcNow, area);

        var response = await client.PostAsJsonAsync($"/api/v1/scenes/{session.SceneId}/commands", command, JsonOptions);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var result = document.RootElement.GetProperty("payload").GetProperty("result");
        Assert.True(result.GetProperty("validated").GetBoolean());
        Assert.True(result.GetProperty("completelyInside").GetBoolean());
        var triangle = Assert.Single(result.GetProperty("triangles").EnumerateArray());
        Assert.Equal(1001, triangle.GetProperty("sourceElementId").GetInt32());
        Assert.Equal(new[] { 101, 102, 103 },
            triangle.GetProperty("triangleSourceNodeIds").EnumerateArray().Select(value => value.GetInt32()));

        var sectionCrossing = area with
        {
            SectionPlane = new SelectionSectionPlane(
                [manifest.WorldOrigin[0] + 0.5, manifest.WorldOrigin[1], manifest.WorldOrigin[2]], [1, 0, 0]),
        };
        var sectionCommand = command with
        {
            MessageId = Guid.NewGuid().ToString("D"),
            Payload = sectionCrossing,
        };
        response = await client.PostAsJsonAsync($"/api/v1/scenes/{session.SceneId}/commands", sectionCommand, JsonOptions);
        response.EnsureSuccessStatusCode();
        using var sectionDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Single(sectionDocument.RootElement.GetProperty("payload").GetProperty("result")
            .GetProperty("triangles").EnumerateArray());

        var fullyClipped = sectionCrossing with
        {
            SectionPlane = new SelectionSectionPlane(
                [manifest.WorldOrigin[0] + 2, manifest.WorldOrigin[1], manifest.WorldOrigin[2]], [1, 0, 0]),
        };
        sectionCommand = sectionCommand with { MessageId = Guid.NewGuid().ToString("D"), Payload = fullyClipped };
        response = await client.PostAsJsonAsync($"/api/v1/scenes/{session.SceneId}/commands", sectionCommand, JsonOptions);
        response.EnsureSuccessStatusCode();
        using var clippedDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(clippedDocument.RootElement.GetProperty("payload").GetProperty("result")
            .GetProperty("triangles").EnumerateArray());

        var malformedSection = sectionCrossing with
        {
            SectionPlane = new SelectionSectionPlane([0, 0, 0], [0, 0, 0]),
        };
        sectionCommand = sectionCommand with { MessageId = Guid.NewGuid().ToString("D"), Payload = malformedSection };
        var malformedResponse = await client.PostAsJsonAsync(
            $"/api/v1/scenes/{session.SceneId}/commands", sectionCommand, JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, malformedResponse.StatusCode);
        Assert.Equal("area-invalid", (await malformedResponse.Content.ReadFromJsonAsync<ApiError>(JsonOptions))?.Code);

        var forged = area with { Chunks = [new AreaChunkCandidate(actor.ActorId, chunk.ChunkId, [chunk.TriangleCount])] };
        var rejectedCommand = command with { MessageId = Guid.NewGuid().ToString("D"), Payload = forged };
        var rejected = await client.PostAsJsonAsync($"/api/v1/scenes/{session.SceneId}/commands", rejectedCommand, JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal("area-invalid", (await rejected.Content.ReadFromJsonAsync<ApiError>(JsonOptions))?.Code);
    }

    [Fact]
    public async Task UnsupportedCommand_ReturnsStableClientError()
    {
        var session = await CreateSessionAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-PrePoMax-Session", session.AccessToken);
        var manifest = Assert.IsType<SceneManifest>(await client.GetFromJsonAsync<SceneManifest>(
            $"/api/v1/scenes/{session.SceneId}/manifest",
            JsonOptions));
        var command = new ProtocolMessage(
            ProtocolConstants.Version,
            Guid.NewGuid().ToString("D"),
            null,
            session.SceneId,
            manifest.SceneRevision,
            "unsupported.command",
            DateTimeOffset.UtcNow,
            new { });

        var response = await client.PostAsJsonAsync(
            $"/api/v1/scenes/{session.SceneId}/commands",
            command,
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("command-unsupported", (await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions))?.Code);
    }

    [Fact]
    public async Task CommandRateLimit_ReturnsRetryableStableError()
    {
        using var limitedFactory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<CommandRateLimitOptions>();
            services.RemoveAll<CommandRateLimiter>();
            services.AddSingleton(new CommandRateLimitOptions(1));
            services.AddSingleton<CommandRateLimiter>();
        }));
        using var client = limitedFactory.CreateClient();
        var sessionResponse = await client.PostAsync("/api/v1/sessions", null);
        sessionResponse.EnsureSuccessStatusCode();
        var session = Assert.IsType<SessionCreated>(await sessionResponse.Content.ReadFromJsonAsync<SessionCreated>(JsonOptions));
        client.DefaultRequestHeaders.Add("X-PrePoMax-Session", session.AccessToken);
        var manifest = Assert.IsType<SceneManifest>(await client.GetFromJsonAsync<SceneManifest>(
            $"/api/v1/scenes/{session.SceneId}/manifest",
            JsonOptions));
        var command = new ProtocolMessage(
            ProtocolConstants.Version,
            Guid.NewGuid().ToString("D"),
            null,
            session.SceneId,
            manifest.SceneRevision,
            "unsupported.command",
            DateTimeOffset.UtcNow,
            new { });

        var first = await client.PostAsJsonAsync($"/api/v1/scenes/{session.SceneId}/commands", command, JsonOptions);
        var second = await client.PostAsJsonAsync($"/api/v1/scenes/{session.SceneId}/commands", command, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal("1", Assert.Single(second.Headers.GetValues("Retry-After")));
        var error = await second.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
        Assert.Equal("command-rate-limited", error?.Code);
        Assert.True(error?.Retryable);
    }

    private async Task<HttpClient> CreateAuthorizedClientAsync()
    {
        var session = await CreateSessionAsync();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-PrePoMax-Session", session.AccessToken);
        return client;
    }

    private async Task<SessionCreated> CreateSessionAsync()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsync("/api/v1/sessions", null);
        response.EnsureSuccessStatusCode();
        return Assert.IsType<SessionCreated>(await response.Content.ReadFromJsonAsync<SessionCreated>(JsonOptions));
    }

    private static string GetSingleHeader(HttpResponseMessage response, string name) =>
        Assert.Single(response.Headers.GetValues(name));

    private static async Task<byte[]> ReceiveMessageAsync(WebSocket socket)
    {
        var buffer = new byte[8192];
        var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
        Assert.Equal(WebSocketMessageType.Text, result.MessageType);
        Assert.True(result.EndOfMessage);
        return buffer[..result.Count];
    }

    private static string[] TopologyResourceIds(SceneManifest manifest) => manifest.Actors
        .SelectMany(actor => actor.Chunks)
        .SelectMany(chunk => new[]
        {
            chunk.Resources.Positions.ResourceId,
            chunk.Resources.TriangleConnectivity.ResourceId,
            chunk.Resources.SourcePointIds.ResourceId,
            chunk.Resources.SourceElementIds.ResourceId,
        })
        .ToArray();

    private static string[] ScalarResourceIds(SceneManifest manifest) => manifest.Actors
        .SelectMany(actor => actor.Chunks)
        .Select(chunk => chunk.Resources.PointScalars.ResourceId)
        .ToArray();

    private static ProtocolMessage PickCommand(string sceneId, long revision, PickResolveRequest pick) => new(
        ProtocolConstants.Version,
        Guid.NewGuid().ToString("D"),
        null,
        sceneId,
        revision,
        "pick.resolve",
        DateTimeOffset.UtcNow,
        pick);

    private const string SyntheticSceneId = "10000000-0000-0000-0000-000000000001";
    private const string PositionsResourceId = "40000000-0000-0000-0000-000000000001";
}

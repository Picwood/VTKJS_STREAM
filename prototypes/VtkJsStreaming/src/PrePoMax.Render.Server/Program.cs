using System.Net;
using System.Net.WebSockets;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.FileProviders;
using PrePoMax.Render.Contracts;
using PrePoMax.Render.Server;
using PrePoMax.Render.SyntheticData;

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
};
var builder = WebApplication.CreateBuilder(args);
var configuredPort = GetConfiguredPort();
var allowedOrigins = GetAllowedOrigins(builder.Environment.IsDevelopment());
var clientRoot = GetClientRoot();
var hostShutdownToken = Environment.GetEnvironmentVariable("PREPOMAX_RENDER_HOST_TOKEN");
var hostUploadMaximumBytes = GetHostUploadMaximumBytes();
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = hostUploadMaximumBytes;
    if (configuredPort == 0)
    {
        options.Listen(IPAddress.Loopback, 0);
    }
    else
    {
        options.ListenLocalhost(configuredPort);
    }
});
builder.Services.AddCors(options => options.AddPolicy("LocalClient", policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(SessionStoreOptions.FromEnvironment());
builder.Services.AddSingleton<SessionStore>();
builder.Services.AddSingleton(CommandRateLimitOptions.FromEnvironment());
builder.Services.AddSingleton<CommandRateLimiter>();
builder.Services.AddSingleton<SceneState>();
builder.Services.AddSingleton<SceneEventHub>();
builder.Services.AddHostedService<RenderServerLifecycle>();
builder.Services.AddSingleton(ResourceDelayOptions.FromEnvironment(builder.Environment.IsDevelopment()));

var app = builder.Build();
app.UseCors("LocalClient");
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; connect-src 'self' http://127.0.0.1:* ws://127.0.0.1:*; style-src 'self'; img-src 'self' blob: data:; worker-src 'self' blob:";
    await next(context);
});
if (clientRoot is not null)
{
    var clientFiles = new PhysicalFileProvider(clientRoot);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = clientFiles });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = clientFiles });
}
var webSocketOptions = new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) };
foreach (var origin in allowedOrigins)
{
    webSocketOptions.AllowedOrigins.Add(origin);
}
app.UseWebSockets(webSocketOptions);

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.Headers[ProtocolConstants.HeaderName] = ProtocolConstants.Version;
        if (context.Request.Headers.TryGetValue(ProtocolConstants.HeaderName, out var requestedVersion)
            && !requestedVersion.ToString().StartsWith("1.", StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
            await context.Response.WriteAsJsonAsync(new ApiError("protocol-version-unsupported", "Only protocol major version 1 is supported."));
            return;
        }
    }

    await next(context);
});

app.Use(async (context, next) =>
{
    var isPublic = context.Request.Path == "/healthz"
        || context.Request.Path == "/api/v1/sessions"
        || context.Request.Path.StartsWithSegments("/api/v1/host");
    if (!isPublic && context.Request.Path.StartsWithSegments("/api"))
    {
        var sessions = context.RequestServices.GetRequiredService<SessionStore>();
        if (!sessions.TryAuthorize(context, out var session) || session is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new ApiError("session-invalid", "A valid, unexpired local render session is required."));
            return;
        }

        context.Items[typeof(RenderSession)] = session;
    }

    await next(context);
});

app.MapGet("/healthz", () => Results.Ok(new { status = "ok", protocol = ProtocolConstants.Version }));

app.MapPost("/api/v1/host/shutdown", (HttpContext context, IHostApplicationLifetime lifetime) =>
{
    var suppliedToken = context.Request.Headers["X-PrePoMax-Host-Token"].ToString();
    if (!TokensEqual(hostShutdownToken, suppliedToken))
    {
        return Results.Unauthorized();
    }

    context.Response.OnCompleted(() =>
    {
        lifetime.StopApplication();
        return Task.CompletedTask;
    });
    return Results.Accepted(value: new { status = "stopping" });
});

app.MapPost("/api/v1/host/scene", async (
    HttpContext context,
    SceneState sceneState,
    SceneEventHub eventHub,
    CancellationToken cancellationToken) =>
{
    var suppliedToken = context.Request.Headers["X-PrePoMax-Host-Token"].ToString();
    if (!TokensEqual(hostShutdownToken, suppliedToken))
    {
        return Results.Unauthorized();
    }
    if (!string.Equals(context.Request.ContentType, HostSceneUploadReader.ContentType, StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(context.Request.ContentType, HostSceneUploadReader.LegacyContentType, StringComparison.OrdinalIgnoreCase))
    {
        return Results.Json(
            new ApiError("scene-upload-content-type-invalid", "The host scene upload content type is invalid."),
            statusCode: StatusCodes.Status415UnsupportedMediaType);
    }

    try
    {
        var replacement = await HostSceneUploadReader.ReadAsync(
            context.Request.Body,
            context.Request.ContentLength ?? -1,
            hostUploadMaximumBytes,
            cancellationToken);
        var replaced = sceneState.Replace(replacement);
        eventHub.Publish(new ProtocolMessage(
            ProtocolConstants.Version,
            Guid.NewGuid().ToString("D"),
            null,
            replaced.Previous.SceneId,
            checked(replaced.Previous.SceneRevision + 1),
            "scene.replaced",
            DateTimeOffset.UtcNow,
            new
            {
                reason = "host-snapshot-uploaded",
                sceneId = replaced.Current.SceneId,
                camera = replaced.Camera,
            }));
        return Results.Ok(new
        {
            sceneId = replaced.Current.SceneId,
            actors = replaced.Current.Actors.Count,
            points = replaced.Current.Actors.Sum(actor => actor.Chunks.Sum(chunk => (long)chunk.PointCount)),
            triangles = replaced.Current.Actors.Sum(actor => actor.Chunks.Sum(chunk => (long)chunk.TriangleCount)),
            locatorPoints = replacement.SelectionActors.Sum(actor => (long)actor.SourceNodeIds.Count),
            locatorCells = replacement.SelectionActors.Sum(actor => (long)actor.Cells.Count),
        });
    }
    catch (HostSceneUploadException exception)
    {
        return Results.BadRequest(new ApiError(exception.Code, exception.Message));
    }
});

app.MapPost("/api/v1/host/actors/visibility", async (
    HttpContext context,
    SceneState sceneState,
    SceneEventHub eventHub,
    CancellationToken cancellationToken) =>
{
    var suppliedToken = context.Request.Headers["X-PrePoMax-Host-Token"].ToString();
    if (!TokensEqual(hostShutdownToken, suppliedToken))
    {
        return Results.Unauthorized();
    }
    if (!string.Equals(context.Request.ContentType, HostControlReader.VisibilityContentType, StringComparison.OrdinalIgnoreCase))
    {
        return Results.Json(
            new ApiError("host-visibility-content-type-invalid", "The actor visibility command content type is invalid."),
            statusCode: StatusCodes.Status415UnsupportedMediaType);
    }

    try
    {
        var requested = await HostControlReader.ReadVisibilityAsync(
            context.Request.Body,
            context.Request.ContentLength ?? -1,
            cancellationToken);
        var update = sceneState.SetActorVisibility(requested);
        if (!update.Succeeded)
        {
            return Results.Json(
                new ApiError("host-visibility-capacity-reached", "The retained host actor-visibility state is full."),
                statusCode: StatusCodes.Status409Conflict);
        }
        if (update.Actors.Count > 0)
        {
            var manifest = sceneState.Manifest;
            eventHub.Publish(new ProtocolMessage(
                ProtocolConstants.Version,
                Guid.NewGuid().ToString("D"),
                null,
                manifest.SceneId,
                update.SceneRevision,
                "actor.visibility.updated",
                DateTimeOffset.UtcNow,
                new { actors = update.Actors }));
        }
        return Results.Ok(new { sceneRevision = update.SceneRevision, matchedActors = update.Actors.Count });
    }
    catch (HostControlException exception)
    {
        return Results.BadRequest(new ApiError(exception.Code, exception.Message));
    }
});

app.MapPost("/api/v1/host/camera/{view}", (
    string view,
    HttpContext context,
    SceneState sceneState,
    SceneEventHub eventHub) =>
{
    var suppliedToken = context.Request.Headers["X-PrePoMax-Host-Token"].ToString();
    if (!TokensEqual(hostShutdownToken, suppliedToken))
    {
        return Results.Unauthorized();
    }
    if (view is not ("reset" or "isometric" or "front" or "back" or "left" or "right" or "top" or "bottom"))
    {
        return Results.BadRequest(new ApiError("host-camera-view-invalid", "The requested camera view is not supported."));
    }

    sceneState.SetCameraView(view);
    var manifest = sceneState.Manifest;
    eventHub.Publish(new ProtocolMessage(
        ProtocolConstants.Version,
        Guid.NewGuid().ToString("D"),
        null,
        manifest.SceneId,
        manifest.SceneRevision,
        "camera.updated",
        DateTimeOffset.UtcNow,
        sceneState.Camera));
    return Results.Ok(new { view });
});

app.MapPost("/api/v1/host/camera/custom", async (
    HttpContext context,
    SceneState sceneState,
    SceneEventHub eventHub,
    CancellationToken cancellationToken) =>
{
    var suppliedToken = context.Request.Headers["X-PrePoMax-Host-Token"].ToString();
    if (!TokensEqual(hostShutdownToken, suppliedToken)) return Results.Unauthorized();
    if (!string.Equals(context.Request.ContentType, HostControlReader.CustomCameraContentType, StringComparison.OrdinalIgnoreCase))
    {
        return Results.Json(
            new ApiError("host-camera-content-type-invalid", "The custom camera command content type is invalid."),
            statusCode: StatusCodes.Status415UnsupportedMediaType);
    }
    try
    {
        var custom = await HostControlReader.ReadCustomCameraAsync(
            context.Request.Body, context.Request.ContentLength ?? -1, cancellationToken);
        sceneState.SetCustomCamera(custom);
        var manifest = sceneState.Manifest;
        eventHub.Publish(new ProtocolMessage(
            ProtocolConstants.Version,
            Guid.NewGuid().ToString("D"),
            null,
            manifest.SceneId,
            manifest.SceneRevision,
            "camera.updated",
            DateTimeOffset.UtcNow,
            sceneState.Camera));
        return Results.Ok(sceneState.Camera);
    }
    catch (HostControlException exception)
    {
        return Results.BadRequest(new ApiError(exception.Code, exception.Message));
    }
});

app.MapPost("/api/v1/host/actor", async (
    HttpContext context,
    SceneState sceneState,
    SceneEventHub eventHub,
    CancellationToken cancellationToken) =>
{
    var suppliedToken = context.Request.Headers["X-PrePoMax-Host-Token"].ToString();
    if (!TokensEqual(hostShutdownToken, suppliedToken)) return Results.Unauthorized();
    if (!string.Equals(context.Request.ContentType, HostControlReader.ActorMutationContentType, StringComparison.OrdinalIgnoreCase))
    {
        return Results.Json(
            new ApiError("host-actor-content-type-invalid", "The actor mutation command content type is invalid."),
            statusCode: StatusCodes.Status415UnsupportedMediaType);
    }
    try
    {
        var mutation = await HostControlReader.ReadActorMutationAsync(
            context.Request.Body, context.Request.ContentLength ?? -1, cancellationToken);
        var update = sceneState.UpdateActor(mutation);
        if (!update.Succeeded)
        {
            return Results.Conflict(new ApiError("host-actor-name-conflict", "The requested actor name already exists."));
        }
        if (update.Actor is not null)
        {
            var manifest = sceneState.Manifest;
            eventHub.Publish(new ProtocolMessage(
                ProtocolConstants.Version,
                Guid.NewGuid().ToString("D"),
                null,
                manifest.SceneId,
                update.SceneRevision,
                "actor.presentation.updated",
                DateTimeOffset.UtcNow,
                new { actors = new[] { update.Actor } }));
        }
        return Results.Ok(new { sceneRevision = update.SceneRevision, matchedActors = update.Actor is null ? 0 : 1 });
    }
    catch (HostControlException exception)
    {
        return Results.BadRequest(new ApiError(exception.Code, exception.Message));
    }
});

app.MapPost("/api/v1/host/actors/remove", async (
    HttpContext context,
    SceneState sceneState,
    SceneEventHub eventHub,
    CancellationToken cancellationToken) =>
{
    var suppliedToken = context.Request.Headers["X-PrePoMax-Host-Token"].ToString();
    if (!TokensEqual(hostShutdownToken, suppliedToken)) return Results.Unauthorized();
    if (!string.Equals(context.Request.ContentType, HostControlReader.ActorRemovalContentType, StringComparison.OrdinalIgnoreCase))
    {
        return Results.Json(
            new ApiError("host-removal-content-type-invalid", "The actor removal command content type is invalid."),
            statusCode: StatusCodes.Status415UnsupportedMediaType);
    }
    try
    {
        var names = await HostControlReader.ReadActorRemovalAsync(
            context.Request.Body, context.Request.ContentLength ?? -1, cancellationToken);
        var update = sceneState.RemoveActors(names);
        if (update.ActorIds.Count > 0)
        {
            var manifest = sceneState.Manifest;
            eventHub.Publish(new ProtocolMessage(
                ProtocolConstants.Version,
                Guid.NewGuid().ToString("D"),
                null,
                manifest.SceneId,
                update.SceneRevision,
                "actor.removed",
                DateTimeOffset.UtcNow,
                new { actorIds = update.ActorIds }));
        }
        return Results.Ok(new { sceneRevision = update.SceneRevision, matchedActors = update.ActorIds.Count });
    }
    catch (HostControlException exception)
    {
        return Results.BadRequest(new ApiError(exception.Code, exception.Message));
    }
});

app.MapPost("/api/v1/host/field/metadata", async (
    HttpContext context,
    SceneState sceneState,
    CancellationToken cancellationToken) =>
{
    var suppliedToken = context.Request.Headers["X-PrePoMax-Host-Token"].ToString();
    if (!TokensEqual(hostShutdownToken, suppliedToken)) return Results.Unauthorized();
    if (!string.Equals(context.Request.ContentType, HostControlReader.FieldMetadataContentType, StringComparison.OrdinalIgnoreCase))
    {
        return Results.Json(
            new ApiError("host-field-content-type-invalid", "The field metadata command content type is invalid."),
            statusCode: StatusCodes.Status415UnsupportedMediaType);
    }
    try
    {
        var metadata = await HostControlReader.ReadFieldMetadataAsync(
            context.Request.Body, context.Request.ContentLength ?? -1, cancellationToken);
        sceneState.SetFieldMetadata(metadata);
        return Results.Ok(metadata);
    }
    catch (HostControlException exception)
    {
        return Results.BadRequest(new ApiError(exception.Code, exception.Message));
    }
});

app.MapPost("/api/v1/host/scalars", async (
    HttpContext context,
    SceneState sceneState,
    SceneEventHub eventHub,
    CancellationToken cancellationToken) =>
{
    var suppliedToken = context.Request.Headers["X-PrePoMax-Host-Token"].ToString();
    if (!TokensEqual(hostShutdownToken, suppliedToken)) return Results.Unauthorized();
    if (!string.Equals(context.Request.ContentType, HostControlReader.ScalarUpdateContentType, StringComparison.OrdinalIgnoreCase))
    {
        return Results.Json(
            new ApiError("host-scalars-content-type-invalid", "The scalar update command content type is invalid."),
            statusCode: StatusCodes.Status415UnsupportedMediaType);
    }
    try
    {
        var requested = await HostControlReader.ReadScalarUpdateAsync(
            context.Request.Body, context.Request.ContentLength ?? -1, cancellationToken);
        var update = sceneState.UpdateScalars(requested);
        if (!update.Succeeded)
        {
            return Results.BadRequest(new ApiError("host-scalars-scene-mismatch", update.Error!));
        }

        var manifest = sceneState.Manifest;
        eventHub.Publish(new ProtocolMessage(
            ProtocolConstants.Version,
            Guid.NewGuid().ToString("D"),
            null,
            manifest.SceneId,
            update.SceneRevision,
            "field.updated",
            DateTimeOffset.UtcNow,
            update.Payload!));
        return Results.Ok(new
        {
            sceneRevision = update.SceneRevision,
            actors = requested.Count,
            chunks = update.Payload!.Chunks.Count,
        });
    }
    catch (HostControlException exception)
    {
        return Results.BadRequest(new ApiError(exception.Code, exception.Message));
    }
});

app.MapPost("/api/v1/host/frame", async (
    HttpContext context,
    SceneState sceneState,
    SceneEventHub eventHub,
    CancellationToken cancellationToken) =>
{
    var totalTimer = Stopwatch.StartNew();
    var suppliedToken = context.Request.Headers["X-PrePoMax-Host-Token"].ToString();
    if (!TokensEqual(hostShutdownToken, suppliedToken)) return Results.Unauthorized();
    if (!string.Equals(context.Request.ContentType, HostControlReader.AnimationFrameContentType, StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(context.Request.ContentType, HostControlReader.AnimationFrameV2ContentType, StringComparison.OrdinalIgnoreCase))
    {
        return Results.Json(new ApiError("host-frame-content-type-invalid", "The animation frame content type is invalid."),
            statusCode: StatusCodes.Status415UnsupportedMediaType);
    }
    try
    {
        var allocatedBeforeRead = GC.GetTotalAllocatedBytes(false);
        var readTimer = Stopwatch.StartNew();
        var frame = await HostControlReader.ReadAnimationFrameAsync(
            context.Request.Body, context.Request.ContentLength ?? -1, cancellationToken);
        readTimer.Stop();
        var allocatedAfterRead = GC.GetTotalAllocatedBytes(false);
        var updateTimer = Stopwatch.StartNew();
        var update = sceneState.UpdateAnimationFrame(frame);
        updateTimer.Stop();
        var allocatedAfterUpdate = GC.GetTotalAllocatedBytes(false);
        if (!update.Succeeded)
            return Results.BadRequest(new ApiError("host-frame-scene-mismatch", update.Error!));
        var manifest = sceneState.Manifest;
        eventHub.Publish(new ProtocolMessage(
            ProtocolConstants.Version,
            Guid.NewGuid().ToString("D"),
            null,
            manifest.SceneId,
            update.SceneRevision,
            "frame.updated",
            DateTimeOffset.UtcNow,
            update.Payload!));
        totalTimer.Stop();
        context.Response.Headers["X-PrePoMax-Frame-Read-Ms"] = readTimer.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        context.Response.Headers["X-PrePoMax-Frame-Update-Ms"] = updateTimer.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        context.Response.Headers["X-PrePoMax-Frame-Total-Ms"] = totalTimer.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        context.Response.Headers["X-PrePoMax-Frame-Read-Allocated-Bytes"] = Math.Max(0, allocatedAfterRead - allocatedBeforeRead).ToString(System.Globalization.CultureInfo.InvariantCulture);
        context.Response.Headers["X-PrePoMax-Frame-Update-Allocated-Bytes"] = Math.Max(0, allocatedAfterUpdate - allocatedAfterRead).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (update.Diagnostics is { } diagnostics)
        {
            WriteFramePhaseHeaders(context.Response.Headers, "Validate", diagnostics.Validation);
            WriteFramePhaseHeaders(context.Response.Headers, "Resources", diagnostics.Resources);
            WriteFramePhaseHeaders(context.Response.Headers, "Manifest", diagnostics.Manifest);
            WriteFramePhaseHeaders(context.Response.Headers, "Refit", diagnostics.Refit);
        }
        return Results.Ok(new { sceneRevision = update.SceneRevision, actors = frame.Count });
    }
    catch (HostControlException exception)
    {
        return Results.BadRequest(new ApiError(exception.Code, exception.Message));
    }
});

app.MapPost("/api/v1/sessions", (HttpContext context, SessionStore sessions, SceneState sceneState) =>
{
    if (!sessions.TryCreate(out var session) || session is null)
    {
        return Results.Json(
            new ApiError("session-capacity-reached", "The local render session limit has been reached. Retry after an existing session expires.", true),
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    var manifest = sceneState.Manifest;
    context.Response.Cookies.Append("prepomax-render-session", session.Token, new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        IsEssential = true,
        MaxAge = sessions.Lifetime,
    });
    return Results.Ok(new SessionCreated(session.SessionId, session.Token, manifest.SceneId));
});

app.MapGet("/api/v1/scenes", (SceneState sceneState) =>
{
    var manifest = sceneState.Manifest;
    return Results.Ok(new[] { new SceneSummary(manifest.SceneId, manifest.Name, manifest.SceneRevision) });
});

app.MapGet("/api/v1/diagnostics", (SceneState sceneState) =>
{
    using var process = Process.GetCurrentProcess();
    process.Refresh();
    var gc = GC.GetGCMemoryInfo();
    var manifest = sceneState.Manifest;
    return Results.Ok(new
    {
        processId = process.Id,
        workingSetBytes = process.WorkingSet64,
        peakWorkingSetBytes = process.PeakWorkingSet64,
        managedHeapBytes = GC.GetTotalMemory(false),
        committedManagedBytes = gc.TotalCommittedBytes,
        totalAllocatedBytes = GC.GetTotalAllocatedBytes(false),
        gen0Collections = GC.CollectionCount(0),
        gen1Collections = GC.CollectionCount(1),
        gen2Collections = GC.CollectionCount(2),
        sceneInitializationMs = sceneState.InitializationMilliseconds,
        sceneId = manifest.SceneId,
        sceneRevision = manifest.SceneRevision,
        actors = manifest.Actors.Count,
        chunks = manifest.Actors.Sum(actor => actor.Chunks.Count),
        uniqueSourceNodes = manifest.SceneId == LargeFixtureFactory.SceneId
            ? LargeFixtureOptions.NodeCount
            : manifest.Actors.Sum(actor => actor.Chunks.Sum(chunk => (long)chunk.PointCount)),
        logicalQuads = manifest.SceneId == LargeFixtureFactory.SceneId
            ? LargeFixtureOptions.LogicalQuadCount
            : manifest.Actors.Sum(actor => actor.Chunks.Sum(chunk => (long)chunk.TriangleCount / 2)),
        chunkPointTuples = manifest.Actors.Sum(actor => actor.Chunks.Sum(chunk => (long)chunk.PointCount)),
        triangles = manifest.Actors.Sum(actor => actor.Chunks.Sum(chunk => (long)chunk.TriangleCount)),
        descriptorBytes = manifest.Resources.Sum(resource => resource.ByteLength),
    });
});

app.MapGet("/api/v1/scenes/{sceneId}/manifest", (string sceneId, SceneState sceneState) =>
{
    var manifest = sceneState.Manifest;
    return sceneId == manifest.SceneId
        ? Results.Ok(manifest)
        : Results.NotFound(new ApiError("scene-not-found", "The requested scene does not exist."));
});

app.MapGet("/api/v1/scenes/{sceneId}/resources/{resourceId}", async (
    string sceneId,
    string resourceId,
    HttpContext context,
    SceneState sceneState,
    ResourceDelayOptions delayOptions,
    CancellationToken cancellationToken) =>
{
    var manifest = sceneState.Manifest;
    if (sceneId != manifest.SceneId)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(new ApiError("scene-not-found", "The requested scene does not exist."), cancellationToken);
        return;
    }

    if (!sceneState.TryGetResource(resourceId, out var resource) || resource is null)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(new ApiError("resource-not-found", "The requested resource does not exist."), cancellationToken);
        return;
    }

    if (delayOptions.ShouldDelay(manifest, resourceId))
    {
        context.Response.Headers["X-PrePoMax-Test-Delay-Ms"] = delayOptions.Delay.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await Task.Delay(delayOptions.Delay, cancellationToken);
    }
    await ResourceHttpWriter.WriteAsync(context, resource, cancellationToken);
});

app.MapPost("/api/v1/scenes/{sceneId}/section", async (
    string sceneId,
    SectionResolveRequest request,
    HttpContext context,
    SceneState sceneState,
    CancellationToken cancellationToken) =>
{
    var manifest = sceneState.Manifest;
    if (sceneId != manifest.SceneId)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(new ApiError("scene-not-found", "The requested scene does not exist."), cancellationToken);
        return;
    }
    try
    {
        var result = sceneState.ResolveSection(request);
        await SectionGeometryWriter.WriteAsync(context.Response, result, cancellationToken);
    }
    catch (SectionGeometryException exception)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new ApiError(exception.Code, exception.Message), cancellationToken);
    }
});

app.MapGet("/api/v1/scenes/{sceneId}/events", async (
    string sceneId,
    HttpContext context,
    SceneState sceneState,
    SceneEventHub eventHub) =>
{
    var manifest = sceneState.Manifest;
    if (sceneId != manifest.SceneId)
    {
        return Results.NotFound(new ApiError("scene-not-found", "The requested scene does not exist."));
    }

    if (!context.WebSockets.IsWebSocketRequest)
    {
        return Results.BadRequest(new ApiError("websocket-required", "This endpoint requires a WebSocket upgrade."));
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    await RunEventSocketAsync(socket, manifest, eventHub, jsonOptions, context.RequestAborted);

    return Results.Empty;
});

app.MapPost("/api/v1/scenes/{sceneId}/commands", (
    string sceneId,
    ProtocolMessage command,
    HttpContext context,
    SceneState sceneState,
    SceneEventHub eventHub,
    CommandRateLimiter rateLimiter) =>
{
    var manifest = sceneState.Manifest;
    if (sceneId != manifest.SceneId)
    {
        return Results.NotFound(new ApiError("scene-not-found", "The requested scene does not exist."));
    }

    var session = (RenderSession)context.Items[typeof(RenderSession)]!;
    if (!rateLimiter.TryAcquire(session.SessionId, out var retryAfter))
    {
        context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Results.Json(
            new ApiError("command-rate-limited", "The session command rate limit was exceeded.", true),
            statusCode: StatusCodes.Status429TooManyRequests);
    }

    if (command.Protocol != ProtocolConstants.Version)
    {
        return Results.Json(new ApiError("protocol-version-unsupported", "Only protocol 1.0 commands are supported."), statusCode: StatusCodes.Status426UpgradeRequired);
    }

    if (command.SceneRevision != manifest.SceneRevision)
    {
        return Results.Conflict(new ApiError("revision-conflict", "The command targets a stale scene revision.", true));
    }

    if (command.Type is not ("field.toggle" or "pick.resolve" or "area.resolve" or "frustum.resolve"))
    {
        return Results.BadRequest(new ApiError("command-unsupported", "The requested command type is not supported."));
    }

    object resultPayload = new { commandType = command.Type };
    var resultRevision = manifest.SceneRevision;
    if (command.Type == "field.toggle")
    {
        var update = sceneState.ToggleField(command.SceneRevision);
        if (!update.Succeeded || update.Payload is null)
        {
            if (!update.IsSupported)
            {
                return Results.BadRequest(new ApiError("field-update-unsupported", "The current host-provided scene has no alternate scalar fixture."));
            }
            return Results.Conflict(new ApiError("revision-conflict", "The command targets a stale scene revision.", true));
        }

        resultRevision = update.SceneRevision;
        var fieldEvent = new ProtocolMessage(
            ProtocolConstants.Version,
            Guid.NewGuid().ToString("D"),
            command.MessageId,
            manifest.SceneId,
            update.SceneRevision,
            "field.updated",
            DateTimeOffset.UtcNow,
            update.Payload);
        eventHub.Publish(fieldEvent);
        resultPayload = new { commandType = command.Type, field = update.Payload };
    }
    else if (command.Type == "pick.resolve")
    {
        PickResolveRequest? request;
        try
        {
            request = command.Payload is JsonElement element
                ? element.Deserialize<PickResolveRequest>(jsonOptions)
                : null;
        }
        catch (JsonException)
        {
            request = null;
        }
        if (request is null)
        {
            return Results.BadRequest(new ApiError("pick-invalid", "The pick payload is missing or malformed."));
        }

        var resolution = sceneState.ResolvePick(request);
        if (!resolution.Succeeded || resolution.Pick is null)
        {
            return Results.BadRequest(new ApiError("pick-invalid", resolution.Error ?? "The pick could not be validated."));
        }
        resultPayload = new { commandType = command.Type, result = resolution.Pick };
    }
    else if (command.Type == "area.resolve")
    {
        AreaResolveRequest? request;
        try
        {
            request = command.Payload is JsonElement element
                ? element.Deserialize<AreaResolveRequest>(jsonOptions)
                : null;
        }
        catch (JsonException)
        {
            request = null;
        }
        if (request is null)
            return Results.BadRequest(new ApiError("area-invalid", "The area payload is missing or malformed."));

        var resolution = sceneState.ResolveArea(request);
        if (!resolution.Succeeded || resolution.Area is null)
            return Results.BadRequest(new ApiError("area-invalid", resolution.Error ?? "The area could not be validated."));
        resultPayload = new { commandType = command.Type, result = resolution.Area };
    }
    else if (command.Type == "frustum.resolve")
    {
        FrustumResolveRequest? request;
        try
        {
            request = command.Payload is JsonElement element
                ? element.Deserialize<FrustumResolveRequest>(jsonOptions)
                : null;
        }
        catch (JsonException)
        {
            request = null;
        }
        if (request is null)
            return Results.BadRequest(new ApiError("frustum-invalid", "The frustum payload is missing or malformed."));
        var resolution = sceneState.ResolveFrustum(request);
        if (!resolution.Succeeded || resolution.Frustum is null)
            return Results.BadRequest(new ApiError(selectionIndexUnavailable(resolution.Error) ? "frustum-unavailable" : "frustum-invalid",
                resolution.Error ?? "The frustum could not be resolved."));
        resultPayload = new { commandType = command.Type, result = resolution.Frustum };
    }

    return Results.Ok(new ProtocolMessage(
        ProtocolConstants.Version,
        Guid.NewGuid().ToString("D"),
        command.MessageId,
        manifest.SceneId,
        resultRevision,
        "command.completed",
        DateTimeOffset.UtcNow,
        resultPayload));
});

static bool selectionIndexUnavailable(string? error) => error?.Contains("no full-cell locator", StringComparison.Ordinal) == true;

await app.RunAsync();

static int GetConfiguredPort()
{
    var value = Environment.GetEnvironmentVariable("PREPOMAX_RENDER_PORT");
    if (string.IsNullOrWhiteSpace(value))
    {
        return 0;
    }

    return int.TryParse(value, out var port) && port is > 0 and <= 65_535
        ? port
        : throw new InvalidOperationException("PREPOMAX_RENDER_PORT must be an integer from 1 through 65535.");
}

static string[] GetAllowedOrigins(bool isDevelopment)
{
    var configured = Environment.GetEnvironmentVariable("PREPOMAX_RENDER_ALLOWED_ORIGINS");
    if (!string.IsNullOrWhiteSpace(configured))
    {
        return configured.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    return isDevelopment ? ["http://127.0.0.1:5173"] : [];
}

static string? GetClientRoot()
{
    var configured = Environment.GetEnvironmentVariable("PREPOMAX_RENDER_CLIENT_ROOT");
    if (string.IsNullOrWhiteSpace(configured))
    {
        return null;
    }

    var fullPath = Path.GetFullPath(configured);
    return Directory.Exists(fullPath) && File.Exists(Path.Combine(fullPath, "index.html"))
        ? fullPath
        : throw new InvalidOperationException("PREPOMAX_RENDER_CLIENT_ROOT must contain a built index.html file.");
}

static bool TokensEqual(string? expected, string supplied)
{
    if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(supplied))
    {
        return false;
    }

    var expectedBytes = Encoding.UTF8.GetBytes(expected);
    var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
    return expectedBytes.Length == suppliedBytes.Length
        && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
}

static void WriteFramePhaseHeaders(
    IHeaderDictionary headers,
    string phase,
    FrameUpdatePhaseMetrics metrics)
{
    headers[$"X-PrePoMax-Frame-{phase}-Ms"] = metrics.Milliseconds.ToString(
        "F3", System.Globalization.CultureInfo.InvariantCulture);
    headers[$"X-PrePoMax-Frame-{phase}-Allocated-Bytes"] = metrics.AllocatedBytes.ToString(
        System.Globalization.CultureInfo.InvariantCulture);
}

static long GetHostUploadMaximumBytes()
{
    const string name = "PREPOMAX_RENDER_HOST_UPLOAD_MAX_BYTES";
    var configured = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrWhiteSpace(configured)) return HostSceneUploadReader.DefaultMaximumBytes;
    return long.TryParse(configured, out var value) && value is >= 1_048_576 and <= 1_073_741_824
        ? value
        : throw new InvalidOperationException($"{name} must be an integer from 1048576 through 1073741824.");
}

static async Task RunEventSocketAsync(
    WebSocket socket,
    SceneManifest manifest,
    SceneEventHub eventHub,
    JsonSerializerOptions jsonOptions,
    CancellationToken cancellationToken)
{
    using var subscription = eventHub.Subscribe(manifest);
    using var connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    var initial = new ProtocolMessage(
        ProtocolConstants.Version,
        Guid.NewGuid().ToString("D"),
        null,
        manifest.SceneId,
        manifest.SceneRevision,
        "loading.progress",
        DateTimeOffset.UtcNow,
        new { stage = "fixture-ready", completedUnits = 2, totalUnits = 2 });
    await SendMessageAsync(socket, initial, jsonOptions, connectionCancellation.Token);

    var receiveTask = ReceiveUntilClosedAsync(socket, connectionCancellation.Token);
    try
    {
        while (socket.State == WebSocketState.Open && !receiveTask.IsCompleted)
        {
            var eventTask = subscription.Reader.ReadAsync(connectionCancellation.Token).AsTask();
            var completed = await Task.WhenAny(receiveTask, eventTask);
            if (completed == receiveTask)
            {
                break;
            }

            var message = await eventTask;
            await SendMessageAsync(socket, message, jsonOptions, connectionCancellation.Token);
            if (message.Type == "server.shutting-down")
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.EndpointUnavailable, "server shutting down", CancellationToken.None);
                break;
            }
        }
    }
    catch (ChannelClosedException)
    {
        // The event hub completed during orderly shutdown.
    }
    catch (OperationCanceledException) when (connectionCancellation.IsCancellationRequested)
    {
        // Normal request shutdown or client disconnect.
    }
    finally
    {
        await connectionCancellation.CancelAsync();
        try
        {
            await receiveTask;
        }
        catch (OperationCanceledException)
        {
            // Normal connection teardown.
        }
    }
}

static async Task ReceiveUntilClosedAsync(WebSocket socket, CancellationToken cancellationToken)
{
    var receiveBuffer = new byte[4096];
    while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
    {
        var received = await socket.ReceiveAsync(receiveBuffer, cancellationToken);
        if (received.MessageType == WebSocketMessageType.Close)
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "client closed", CancellationToken.None);
            return;
        }
    }
}

static Task SendMessageAsync(
    WebSocket socket,
    ProtocolMessage message,
    JsonSerializerOptions jsonOptions,
    CancellationToken cancellationToken) =>
    socket.SendAsync(
        JsonSerializer.SerializeToUtf8Bytes(message, jsonOptions),
        WebSocketMessageType.Text,
        true,
        cancellationToken);

public partial class Program;

internal sealed record ResourceDelayOptions(int ChunkOrdinal, TimeSpan Delay)
{
    public static ResourceDelayOptions FromEnvironment(bool isDevelopment)
    {
        if (!isDevelopment)
        {
            return new ResourceDelayOptions(-1, TimeSpan.Zero);
        }

        var ordinalValue = Environment.GetEnvironmentVariable("PREPOMAX_RENDER_DELAY_CHUNK_ORDINAL");
        var delayValue = Environment.GetEnvironmentVariable("PREPOMAX_RENDER_DELAY_MS");
        return int.TryParse(ordinalValue, out var ordinal)
            && int.TryParse(delayValue, out var milliseconds)
            && ordinal >= 0
            && milliseconds is > 0 and <= 10_000
                ? new ResourceDelayOptions(ordinal, TimeSpan.FromMilliseconds(milliseconds))
                : new ResourceDelayOptions(-1, TimeSpan.Zero);
    }

    public bool ShouldDelay(SceneManifest manifest, string resourceId)
    {
        if (Delay == TimeSpan.Zero)
        {
            return false;
        }

        return manifest.Actors
            .SelectMany(actor => actor.Chunks)
            .Where(chunk => chunk.Ordinal == ChunkOrdinal)
            .SelectMany(chunk => new[]
            {
                chunk.Resources.Positions.ResourceId,
                chunk.Resources.TriangleConnectivity.ResourceId,
                chunk.Resources.SourcePointIds.ResourceId,
                chunk.Resources.SourceElementIds.ResourceId,
                chunk.Resources.PointScalars.ResourceId,
            })
            .Contains(resourceId, StringComparer.Ordinal);
    }
}

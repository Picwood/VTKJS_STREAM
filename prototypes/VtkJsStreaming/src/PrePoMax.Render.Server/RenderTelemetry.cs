using System.Diagnostics;

namespace PrePoMax.Render.Server;

internal static partial class RenderTelemetry
{
    public const string ServerVersion = "0.1.0";
    public static readonly object CommandMessageIdItemKey = new();
    public static readonly object CommandCorrelationIdItemKey = new();

    [LoggerMessage(6000, LogLevel.Information,
        "Render server started protocol={ProtocolVersion} serverVersion={ServerVersion}")]
    public static partial void ServerStarted(ILogger logger, string protocolVersion, string serverVersion);

    [LoggerMessage(6001, LogLevel.Information,
        "Render server stopping protocol={ProtocolVersion} serverVersion={ServerVersion}")]
    public static partial void ServerStopping(ILogger logger, string protocolVersion, string serverVersion);

    [LoggerMessage(6010, LogLevel.Warning,
        "Production session creation rejected reason={Reason}")]
    public static partial void SessionRejected(ILogger logger, string reason);

    [LoggerMessage(6011, LogLevel.Information,
        "Render session created sessionId={SessionId} sceneId={SceneId}")]
    public static partial void SessionCreated(ILogger logger, string sessionId, string sceneId);

    [LoggerMessage(6012, LogLevel.Information,
        "Render session reused sessionId={SessionId} sceneId={SceneId}")]
    public static partial void SessionReused(ILogger logger, string sessionId, string sceneId);

    [LoggerMessage(6013, LogLevel.Information,
        "Browser session adopted sessionId={SessionId} sceneId={SceneId}")]
    public static partial void SessionAdopted(ILogger logger, string sessionId, string sceneId);

    [LoggerMessage(6020, LogLevel.Information,
        "Render request completed operation={Operation} requestId={RequestId} sessionId={SessionId} sceneId={SceneId} commandMessageId={CommandMessageId} correlationId={CorrelationId} statusCode={StatusCode} elapsedMs={ElapsedMs} responseBytes={ResponseBytes} canceled={Canceled}")]
    public static partial void RequestCompleted(
        ILogger logger, string operation, string requestId, string sessionId, string sceneId,
        string commandMessageId, string correlationId, int statusCode, double elapsedMs,
        long responseBytes, bool canceled);

    [LoggerMessage(6021, LogLevel.Information,
        "Render resource completed requestId={RequestId} sessionId={SessionId} sceneId={SceneId} resourceId={ResourceId} statusCode={StatusCode} elapsedMs={ElapsedMs} responseBytes={ResponseBytes} canceled={Canceled}")]
    public static partial void ResourceCompleted(
        ILogger logger, string requestId, string sessionId, string sceneId, string resourceId,
        int statusCode, double elapsedMs, long responseBytes, bool canceled);

    public static string? ClassifyOperation(PathString path)
    {
        var value = path.Value;
        if (value is null) return null;
        if (value == "/api/v1/sessions") return "session.create";
        if (value == "/api/v1/sessions/adopt") return "session.adopt";
        if (value == "/api/v1/diagnostics") return "diagnostics.read";
        if (value == "/api/v1/host/scene") return "host.scene.replace";
        if (value == "/api/v1/host/shutdown") return "host.shutdown";
        if (value.EndsWith("/manifest", StringComparison.Ordinal)) return "manifest.read";
        if (value.Contains("/resources/", StringComparison.Ordinal)) return "resource.read";
        if (value.EndsWith("/section", StringComparison.Ordinal)) return "section.resolve";
        if (value.EndsWith("/events", StringComparison.Ordinal)) return "events.connect";
        if (value.EndsWith("/commands", StringComparison.Ordinal)) return "command.execute";
        return null;
    }

    public static string SafeIdentifier(object? value)
    {
        var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        return Guid.TryParseExact(text, "D", out var parsed) ? parsed.ToString("D") : "none";
    }

    public static string SafeCorrelation(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 64) return "none";
        return value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or ':')
            ? value
            : "none";
    }

    public static double ElapsedMilliseconds(long startedTimestamp) =>
        Math.Round(Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds, 3);
}

internal sealed class RenderDiagnosticsState
{
    private long requestCount;
    private long resourceRequestCount;
    private long resourceResponseBytes;
    private long canceledRequestCount;
    private string? lastSafeErrorCode;

    public void Record(string operation, int statusCode, long responseBytes, bool canceled)
    {
        Interlocked.Increment(ref requestCount);
        if (operation == "resource.read")
        {
            Interlocked.Increment(ref resourceRequestCount);
            if (responseBytes > 0) Interlocked.Add(ref resourceResponseBytes, responseBytes);
        }
        if (canceled) Interlocked.Increment(ref canceledRequestCount);
        if (statusCode >= 400)
            Volatile.Write(ref lastSafeErrorCode, "http-" + statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public RenderDiagnosticsSnapshot Snapshot() => new(
        RenderTelemetry.ServerVersion,
        Interlocked.Read(ref requestCount),
        Interlocked.Read(ref resourceRequestCount),
        Interlocked.Read(ref resourceResponseBytes),
        Interlocked.Read(ref canceledRequestCount),
        Volatile.Read(ref lastSafeErrorCode));
}

internal sealed record RenderDiagnosticsSnapshot(
    string ServerVersion,
    long RequestCount,
    long ResourceRequestCount,
    long ResourceResponseBytes,
    long CanceledRequestCount,
    string? LastSafeErrorCode);

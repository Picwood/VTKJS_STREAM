using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using PrePoMax.Render.Contracts;

namespace PrePoMax.Render.Server.Tests;

public sealed class RenderTelemetryTests(RenderServerFactory factory) : IClassFixture<RenderServerFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ProductionTelemetry_IsStructuredCorrelatedAndContainsNoSecretsOrPhysicalPaths()
    {
        const string hostToken = "telemetry-production-host-token";
        var provider = new CaptureLoggerProvider();
        using var production = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("PREPOMAX_RENDER_HOST_TOKEN", hostToken);
            builder.ConfigureLogging(logging => logging.AddProvider(provider));
        });
        using var client = production.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        using (var rejected = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sessions"))
        {
            rejected.Headers.Add("X-PrePoMax-Host-Token", "incorrect-host-token");
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(rejected)).StatusCode);
        }

        SessionCreated session;
        using (var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sessions"))
        {
            create.Headers.Add("X-PrePoMax-Host-Token", hostToken);
            var response = await client.SendAsync(create);
            response.EnsureSuccessStatusCode();
            session = Assert.IsType<SessionCreated>(
                await response.Content.ReadFromJsonAsync<SessionCreated>(JsonOptions));
            Assert.Equal(RenderTelemetry.ServerVersion,
                Assert.Single(response.Headers.GetValues("X-PrePoMax-Server-Version")));
        }

        client.DefaultRequestHeaders.Add("X-PrePoMax-Session", session.AccessToken);
        var manifest = Assert.IsType<SceneManifest>(await client.GetFromJsonAsync<SceneManifest>(
            $"/api/v1/scenes/{session.SceneId}/manifest", JsonOptions));
        var resource = manifest.Resources.First(item => item.Semantic == "positions");
        (await client.GetAsync(resource.Uri)).EnsureSuccessStatusCode();

        var messageId = Guid.NewGuid().ToString("D");
        var correlationId = Guid.NewGuid().ToString("D");
        var command = new ProtocolMessage(
            ProtocolConstants.Version, messageId, correlationId, session.SceneId,
            manifest.SceneRevision, "unsupported.command", DateTimeOffset.UtcNow, new { });
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/api/v1/scenes/{session.SceneId}/commands", command, JsonOptions)).StatusCode);

        var diagnostics = await client.GetFromJsonAsync<JsonElement>("/api/v1/diagnostics", JsonOptions);
        Assert.Equal(RenderTelemetry.ServerVersion, diagnostics.GetProperty("serverVersion").GetString());
        Assert.True(diagnostics.GetProperty("requestCount").GetInt64() >= 4);
        Assert.True(diagnostics.GetProperty("resourceRequestCount").GetInt64() >= 1);
        Assert.True(diagnostics.GetProperty("resourceResponseBytes").GetInt64() >= resource.ByteLength);
        Assert.Equal("http-400", diagnostics.GetProperty("lastSafeErrorCode").GetString());

        var logs = provider.Entries.ToArray();
        Assert.Contains(logs, entry => entry.EventId == 6000);
        Assert.Contains(logs, entry => entry.EventId == 6010);
        Assert.Contains(logs, entry => entry.EventId == 6011);
        Assert.Contains(logs, entry => entry.EventId == 6021
            && entry.State.GetValueOrDefault("ResourceId") == resource.ResourceId
            && entry.State.GetValueOrDefault("SessionId") == session.SessionId
            && entry.State.GetValueOrDefault("RequestId") != "none");
        Assert.Contains(logs, entry => entry.EventId == 6020
            && entry.State.GetValueOrDefault("CommandMessageId") == messageId
            && entry.State.GetValueOrDefault("CorrelationId") == correlationId
            && entry.State.GetValueOrDefault("StatusCode") == "400");

        var captured = string.Join('\n', logs.Select(entry => entry.Message));
        Assert.DoesNotContain(hostToken, captured, StringComparison.Ordinal);
        Assert.DoesNotContain(session.AccessToken, captured, StringComparison.Ordinal);
        Assert.DoesNotContain(Directory.GetCurrentDirectory(), captured, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/api/v1/scenes/", captured, StringComparison.Ordinal);
    }

    private sealed class CaptureLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<CapturedLog> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CaptureLogger(categoryName, Entries);

        public void Dispose() { }
    }

    private sealed class CaptureLogger(string category, ConcurrentQueue<CapturedLog> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var values = state is IEnumerable<KeyValuePair<string, object?>> properties
                ? properties.ToDictionary(pair => pair.Key, pair => Convert.ToString(
                    pair.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                    StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal);
            entries.Enqueue(new CapturedLog(category, eventId.Id, formatter(state, exception), values));
        }
    }

    private sealed record CapturedLog(
        string Category,
        int EventId,
        string Message,
        IReadOnlyDictionary<string, string> State);
}

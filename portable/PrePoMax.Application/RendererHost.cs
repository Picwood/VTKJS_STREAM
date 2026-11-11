using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Runtime;

namespace PrePoMax.Application;

public sealed record RendererSceneSummary(string SceneId, int Actors, long Points, long Triangles);
public sealed record RendererPublicationTimings(double ExportMilliseconds, double UploadMilliseconds,
                                                double SessionMilliseconds, long SceneBytes, double TotalMilliseconds);

/// <summary>Owns a private renderer process and its host/session credentials. Browser transport is supplied by a trusted UI adapter.</summary>
public sealed class RendererHost : IAsyncDisposable
{
    private readonly Process _process;
    private readonly HttpClient _client;
    private readonly Task _stdout, _stderr;
    private int _disposed;
    private string? _bootstrap;
    private readonly ProjectResultSession _results = new();
    public ResultSessionAccess? ResultAccess => _results.LastAccess;
    public RendererPublicationTimings? LastPublication { get; private set; }
    public Uri Address => _client.BaseAddress!;
    private RendererHost(Process process, HttpClient client, Task stdout, Task stderr)
        => (_process, _client, _stdout, _stderr) = (process, client, stdout, stderr);

    public static async Task<RendererHost> StartAsync(string dotnetPath, string serverAssembly,
                                                     CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string server = Path.GetFullPath(serverAssembly);
        if (!File.Exists(server)) throw new FileNotFoundException("Renderer assembly not found.", server);
        var info = new ProcessStartInfo(Path.GetFullPath(dotnetPath))
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(server)!, CreateNoWindow = true
        };
        info.ArgumentList.Add(server);
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        info.Environment["PREPOMAX_RENDER_HOST_TOKEN"] = token;
        info.Environment.Remove("PREPOMAX_RENDER_PORT");
        info.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        info.Environment["DOTNET_ENVIRONMENT"] = "Production";
        var ready = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        var process = new Process { StartInfo = info };
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false, UseCookies = false })
            { Timeout = TimeSpan.FromSeconds(30), MaxResponseContentBufferSize = 16 * 1024 * 1024 };
        try { process.Start(); }
        catch { client.Dispose(); process.Dispose(); throw; }
        async Task DrainAsync(StreamReader reader, bool acceptReady)
        {
            // Fixed buffers bound retained log memory, even for a child emitting no newlines.
            char[] buffer = new char[1024];
            var line = new System.Text.StringBuilder();
            bool overflow = false;
            int count;
            while ((count = await reader.ReadAsync(buffer)) != 0)
                for (int i = 0; i < count; i++)
                {
                    char c = buffer[i];
                    if (c == '\n')
                    {
                        const string prefix = "PREPOMAX_RENDER_READY=";
                        string value = line.ToString().TrimEnd('\r');
                        if (acceptReady && !overflow && value.StartsWith(prefix, StringComparison.Ordinal) &&
                            Uri.TryCreate(value[prefix.Length..], UriKind.Absolute, out var uri) &&
                            uri.Scheme == "http" && uri.Host == "127.0.0.1" && uri.Port > 0 &&
                            uri.AbsolutePath == "/" && uri.Query == "" && uri.Fragment == "" && uri.UserInfo == "")
                            ready.TrySetResult(uri);
                        line.Clear(); overflow = false;
                    }
                    else if (line.Length < 2048) line.Append(c);
                    else overflow = true;
                }
            if (acceptReady) ready.TrySetException(new IOException("Renderer exited before readiness."));
        }
        var host = new RendererHost(process, client, DrainAsync(process.StandardOutput, true), DrainAsync(process.StandardError, false));
        try
        {
            client.BaseAddress = await ready.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            client.DefaultRequestHeaders.Add("X-PrePoMax-Host-Token", token);
            return host;
        }
        catch { await host.DisposeAsync(); throw; }
    }

    public ResultField[] ListResultFields(string projectPath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        return _results.List(projectPath, cancellationToken);
    }

    public void ClearResultSession()
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        _results.Clear();
    }

    /// <summary>Call serially, including field listing. Returns only non-secret scene metadata; retains the session internally.</summary>
    public async Task<RendererSceneSummary> PublishAsync(string projectPath, CancellationToken cancellationToken = default,
        ResultField? field = null, double deformationScale = 0, int[]? selectedNodes = null,
        ProjectElementFace[]? selectedFaces = null)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        LastPublication = null;
        var totalWatch = Stopwatch.StartNew();
        string directory = Path.Combine(Path.GetTempPath(), "prepomax-scene-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (field != null) CompactLargeSceneHeap();
            string scene = Path.Combine(directory, "scene.bin");
            var watch = Stopwatch.StartNew();
            string? units = ProjectScene.ExportCore(projectPath, scene, field, deformationScale, selectedNodes,
                selectedFaces, _results, cancellationToken);
            double exportMilliseconds = watch.Elapsed.TotalMilliseconds;
            long sceneBytes = new FileInfo(scene).Length;
            if (sceneBytes >= 32L * 1024 * 1024) CompactLargeSceneHeap();
            cancellationToken.ThrowIfCancellationRequested();
            watch.Restart();
            _bootstrap = null;
            {
                using var metadata = new MemoryStream();
                using (var writer = new BinaryWriter(metadata, System.Text.Encoding.UTF8, true))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("PMXFLD1\0"));
                    foreach (string text in new[] { field == null ? "Mesh geometry" : $"{field.Name} (step {field.Step}, increment {field.Increment})", field?.Component ?? "none", units ?? "" })
                    {
                        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
                        if (bytes.Length > 1024) throw new InvalidDataException("Field label exceeds renderer limit.");
                        writer.Write(bytes.Length); writer.Write(bytes);
                    }
                }
                using var body = new ByteArrayContent(metadata.ToArray());
                body.Headers.ContentType = new("application/vnd.prepomax.field-metadata-v1");
                using var applied = await _client.PostAsync("api/v1/host/field/metadata", body, cancellationToken);
                applied.EnsureSuccessStatusCode();
            }
            await using var stream = File.OpenRead(scene);
            using var content = new StreamContent(stream);
            content.Headers.ContentType = new("application/vnd.prepomax.render-scene-v1");
            using var published = await _client.PostAsync("api/v1/host/scene", content, cancellationToken);
            published.EnsureSuccessStatusCode();
            var summary = await published.Content.ReadFromJsonAsync<RendererSceneSummary>(cancellationToken)
                ?? throw new InvalidDataException("Renderer returned no scene.");
            double uploadMilliseconds = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            using var session = await _client.PostAsync("api/v1/sessions", null, cancellationToken);
            session.EnsureSuccessStatusCode();
            using var credentials = JsonDocument.Parse(await session.Content.ReadAsStringAsync(cancellationToken));
            _client.DefaultRequestHeaders.Remove("X-PrePoMax-Session");
            _client.DefaultRequestHeaders.Add("X-PrePoMax-Session", credentials.RootElement.GetProperty("accessToken").GetString());
            using var manifest = await _client.GetAsync("api/v1/scenes/" + Uri.EscapeDataString(summary.SceneId) + "/manifest", cancellationToken);
            manifest.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await manifest.Content.ReadAsStringAsync(cancellationToken));
            if (document.RootElement.GetProperty("actors").GetArrayLength() != summary.Actors ||
                document.RootElement.GetProperty("sceneId").GetString() != summary.SceneId)
                throw new InvalidDataException("Published scene does not match authenticated manifest.");
            _bootstrap = JsonSerializer.Serialize(new
            {
                type = "prepomax.session.bootstrap-v1",
                sessionId = credentials.RootElement.GetProperty("sessionId").GetString(),
                accessToken = credentials.RootElement.GetProperty("accessToken").GetString(),
                sceneId = summary.SceneId
            });
            if (field == null) _results.Clear();
            LastPublication = new(exportMilliseconds, uploadMilliseconds, watch.Elapsed.TotalMilliseconds, sceneBytes, totalWatch.Elapsed.TotalMilliseconds);
            return summary;
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void CompactLargeSceneHeap()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
    }

    /// <summary>Handles a top-level ready message from the trusted WebView adapter.
    /// Source must come from the native navigation event, never from message JSON.
    /// The adapter must recheck navigation and target the same frame when posting.
    /// The callback receives credentials: do not log, persist or use a URL transport.
    /// Serialize calls with publication and navigation on the UI adapter.</summary>
    public bool TryBootstrapBrowser(Uri source, bool isTopLevel, string message, Action<string> postJson)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        ArgumentNullException.ThrowIfNull(postJson);
        if (_bootstrap == null || !isTopLevel || !source.IsAbsoluteUri ||
            source.Scheme != Address.Scheme || source.Host != Address.Host || source.Port != Address.Port ||
            source.UserInfo.Length != 0 || source.Fragment.Length != 0 ||
            (source.AbsolutePath != "/" && source.AbsolutePath != "/index.html") ||
            source.Query != "?embedded=1&surfaceSelection=1" || message.Length > 1024) return false;
        try
        {
            using var document = JsonDocument.Parse(message);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 1 ||
                !root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String ||
                type.GetString() != "prepomax.client.ready-v1") return false;
        }
        catch (JsonException) { return false; }
        postJson(_bootstrap);
        return true;
    }

    public Uri BrowserAddress => new(Address, "?embedded=1&surfaceSelection=1");

    /// <summary>Read-only server-to-server access for a web workspace gateway. Serialize with publication.
    /// The caller owns the response and must dispose it after streaming. Credentials stay inside this host.</summary>
    public Task<HttpResponseMessage> ReadSceneAsync(string sceneId, string? resourceId = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        string path = "api/v1/scenes/" + Uri.EscapeDataString(sceneId) +
            (resourceId == null ? "/manifest" : "/resources/" + Uri.EscapeDataString(resourceId));
        return _client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _results.Dispose();
        _bootstrap = null;
        try
        {
            if (!_process.HasExited && _client.BaseAddress != null)
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try { using var response = await _client.PostAsync("api/v1/host/shutdown", null, deadline.Token); }
                catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException) { }
                try { await _process.WaitForExitAsync(deadline.Token); } catch (OperationCanceledException) { }
            }
            if (!_process.HasExited) _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
            await Task.WhenAll(_stdout, _stderr);
        }
        finally { _client.Dispose(); _process.Dispose(); }
    }
}

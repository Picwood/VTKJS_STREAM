using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PrePoMax.Rendering
{
    internal sealed class VtkJsRendererHost : IDisposable
    {
        private static readonly Regex ListeningUrlPattern = new Regex(
            @"Now listening on:\s+(http://127\.0\.0\.1:\d+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private Process _process;
        private string _hostToken;
        private Uri _baseUri;
        private readonly SemaphoreSlim _mutationGate = new SemaphoreSlim(1, 1);

        public async Task<Uri> StartAsync()
        {
            if (_process != null) return _baseUri;

            string serverPath = ResolveServerPath();
            string clientRoot = ResolveClientRoot();
            _hostToken = CreateToken();
            var ready = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
            var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = Quote(serverPath),
                WorkingDirectory = Path.GetDirectoryName(serverPath),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            process.StartInfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Production";
            process.StartInfo.EnvironmentVariables["PREPOMAX_RENDER_PORT"] = "";
            process.StartInfo.EnvironmentVariables["PREPOMAX_RENDER_CLIENT_ROOT"] = clientRoot;
            process.StartInfo.EnvironmentVariables["PREPOMAX_RENDER_HOST_TOKEN"] = _hostToken;
            process.OutputDataReceived += (sender, args) => TrySetReady(args.Data, ready);
            process.ErrorDataReceived += (sender, args) => TrySetReady(args.Data, ready);
            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) =>
                ready.TrySetException(new InvalidOperationException("The VTK.js renderer host exited before becoming ready."));

            if (!process.Start())
                throw new InvalidOperationException("The VTK.js renderer host could not be started.");

            _process = process;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            Task completed = await Task.WhenAny(ready.Task, Task.Delay(TimeSpan.FromSeconds(20))).ConfigureAwait(false);
            if (completed != ready.Task)
            {
                await StopAsync().ConfigureAwait(false);
                throw new TimeoutException("The VTK.js renderer host did not become ready within 20 seconds.");
            }

            _baseUri = await ready.Task.ConfigureAwait(false);
            return _baseUri;
        }

        public async Task StopAsync()
        {
            await _mutationGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await StopCoreAsync().ConfigureAwait(false);
            }
            finally
            {
                _mutationGate.Release();
            }
        }

        private async Task StopCoreAsync()
        {
            Process process = _process;
            _process = null;
            if (process == null) return;

            try
            {
                if (!process.HasExited && _baseUri != null && _hostToken != null)
                {
                    var request = (HttpWebRequest)WebRequest.Create(new Uri(_baseUri, "/api/v1/host/shutdown"));
                    request.Method = "POST";
                    request.Headers["X-PrePoMax-Host-Token"] = _hostToken;
                    request.Timeout = 2000;
                    using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                    {
                        if (response.StatusCode != HttpStatusCode.Accepted)
                            throw new InvalidOperationException("The renderer host refused its shutdown request.");
                    }
                }
            }
            catch (Exception)
            {
                // The fallback below terminates only the child process owned by this control.
            }

            try
            {
                bool exited = await Task.Run(() => process.WaitForExit(4000)).ConfigureAwait(false);
                if (!exited)
                {
                    process.Kill();
                    await Task.Run(() => process.WaitForExit(2000)).ConfigureAwait(false);
                }
            }
            finally
            {
                process.Dispose();
                _baseUri = null;
                _hostToken = null;
            }
        }

        public async Task UploadSceneAsync(VtkJsSceneSnapshot snapshot)
        {
            await _mutationGate.WaitAsync().ConfigureAwait(false);
            try
            {
                EnsureReady("scene upload");
                var request = CreateHostRequest("/api/v1/host/scene", "application/vnd.prepomax.render-scene-v2", snapshot.ContentLength);
                using (Stream requestStream = await request.GetRequestStreamAsync().ConfigureAwait(false))
                {
                    await Task.Run(() => snapshot.WriteTo(requestStream)).ConfigureAwait(false);
                }
                await RequireOkAsync(request, "The renderer host rejected the PrePoMax scene snapshot.").ConfigureAwait(false);
            }
            finally
            {
                _mutationGate.Release();
            }
        }

        public async Task UploadScalarsAsync(VtkJsScalarSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException("snapshot");
            if (snapshot.ActorCount == 0) return;
            await _mutationGate.WaitAsync().ConfigureAwait(false);
            try
            {
                EnsureReady("scalar update");
                var request = CreateHostRequest(
                    "/api/v1/host/scalars",
                    "application/vnd.prepomax.scalar-update-v1",
                    snapshot.ContentLength);
                using (Stream requestStream = await request.GetRequestStreamAsync().ConfigureAwait(false))
                {
                    await Task.Run(() => snapshot.WriteTo(requestStream)).ConfigureAwait(false);
                }
                await RequireOkAsync(request, "The renderer host rejected a PrePoMax scalar update.").ConfigureAwait(false);
            }
            finally
            {
                _mutationGate.Release();
            }
        }

        public async Task<VtkJsFrameUploadMetrics> UploadFrameAsync(VtkJsFrameSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException("snapshot");
            await _mutationGate.WaitAsync().ConfigureAwait(false);
            try
            {
                EnsureReady("animation frame update");
                var totalTimer = Stopwatch.StartNew();
                var writeTimer = Stopwatch.StartNew();
                var request = CreateHostRequest(
                    "/api/v1/host/frame",
                    "application/vnd.prepomax.animation-frame-v2",
                    snapshot.ContentLength);
                using (Stream requestStream = await request.GetRequestStreamAsync().ConfigureAwait(false))
                {
                    await Task.Run(() => snapshot.WriteTo(requestStream)).ConfigureAwait(false);
                }
                writeTimer.Stop();
                var responseTimer = Stopwatch.StartNew();
                using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                {
                    responseTimer.Stop();
                    totalTimer.Stop();
                    if (response.StatusCode != HttpStatusCode.OK)
                        throw new InvalidOperationException("The renderer host rejected a PrePoMax animation frame.");
                    return new VtkJsFrameUploadMetrics(
                        writeTimer.Elapsed.TotalMilliseconds,
                        responseTimer.Elapsed.TotalMilliseconds,
                        totalTimer.Elapsed.TotalMilliseconds,
                        ReadTimingHeader(response, "X-PrePoMax-Frame-Read-Ms"),
                        ReadTimingHeader(response, "X-PrePoMax-Frame-Update-Ms"),
                        ReadTimingHeader(response, "X-PrePoMax-Frame-Total-Ms"),
                        ReadLongHeader(response, "X-PrePoMax-Frame-Read-Allocated-Bytes"),
                        ReadLongHeader(response, "X-PrePoMax-Frame-Update-Allocated-Bytes"),
                        ReadTimingHeader(response, "X-PrePoMax-Frame-Validate-Ms"),
                        ReadLongHeader(response, "X-PrePoMax-Frame-Validate-Allocated-Bytes"),
                        ReadTimingHeader(response, "X-PrePoMax-Frame-Resources-Ms"),
                        ReadLongHeader(response, "X-PrePoMax-Frame-Resources-Allocated-Bytes"),
                        ReadTimingHeader(response, "X-PrePoMax-Frame-Manifest-Ms"),
                        ReadLongHeader(response, "X-PrePoMax-Frame-Manifest-Allocated-Bytes"),
                        ReadTimingHeader(response, "X-PrePoMax-Frame-Refit-Ms"),
                        ReadLongHeader(response, "X-PrePoMax-Frame-Refit-Allocated-Bytes"));
                }
            }
            finally
            {
                _mutationGate.Release();
            }
        }

        private static double ReadTimingHeader(HttpWebResponse response, string name)
        {
            double value;
            return double.TryParse(response.Headers[name], System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out value)
                ? value : double.NaN;
        }

        private static long ReadLongHeader(HttpWebResponse response, string name)
        {
            long value;
            return long.TryParse(response.Headers[name], System.Globalization.NumberStyles.Integer,
                                 System.Globalization.CultureInfo.InvariantCulture, out value)
                ? value : -1;
        }

        public async Task SetFieldMetadataAsync(string name, string component, string units)
        {
            byte[] body;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(Encoding.ASCII.GetBytes("PMXFLD1\0"));
                WriteBoundedString(writer, name, false, "Field name");
                WriteBoundedString(writer, component, false, "Field component");
                WriteBoundedString(writer, units, true, "Field units");
                body = stream.ToArray();
            }
            await SendHostBodyAsync(
                "/api/v1/host/field/metadata",
                "application/vnd.prepomax.field-metadata-v1",
                body,
                "The renderer host rejected PrePoMax field metadata.").ConfigureAwait(false);
        }

        public async Task SetActorVisibilityAsync(IEnumerable<string> actorNames, bool visible)
        {
            var names = new List<string>(actorNames ?? throw new ArgumentNullException("actorNames"));
            if (names.Count == 0) return;
            if (names.Count > 512) throw new InvalidOperationException("An actor visibility command exceeds the 512-actor bound.");

            byte[] body;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(Encoding.ASCII.GetBytes("PMXVIZ1\0"));
                writer.Write(names.Count);
                var unique = new HashSet<string>(StringComparer.Ordinal);
                foreach (string actorName in names)
                {
                    if (string.IsNullOrWhiteSpace(actorName) || !unique.Add(actorName))
                        throw new InvalidOperationException("Actor visibility names must be non-empty and unique.");
                    byte[] name = Encoding.UTF8.GetBytes(actorName);
                    if (name.Length > 1024) throw new InvalidOperationException("An actor name exceeds the 1024-byte bound.");
                    writer.Write(name.Length);
                    writer.Write(name);
                    writer.Write(visible ? (byte)1 : (byte)0);
                }
                body = stream.ToArray();
            }

            await _mutationGate.WaitAsync().ConfigureAwait(false);
            try
            {
                EnsureReady("actor visibility update");
                var request = CreateHostRequest(
                    "/api/v1/host/actors/visibility",
                    "application/vnd.prepomax.actor-visibility-v1",
                    body.LongLength);
                using (Stream requestStream = await request.GetRequestStreamAsync().ConfigureAwait(false))
                {
                    await requestStream.WriteAsync(body, 0, body.Length).ConfigureAwait(false);
                }
                await RequireOkAsync(request, "The renderer host rejected an actor visibility update.").ConfigureAwait(false);
            }
            finally
            {
                _mutationGate.Release();
            }
        }

        public async Task SetCameraViewAsync(string view)
        {
            await _mutationGate.WaitAsync().ConfigureAwait(false);
            try
            {
                EnsureReady("camera update");
                var request = CreateHostRequest("/api/v1/host/camera/" + Uri.EscapeDataString(view), null, 0);
                await RequireOkAsync(request, "The renderer host rejected a camera update.").ConfigureAwait(false);
            }
            finally
            {
                _mutationGate.Release();
            }
        }

        public async Task SetCustomCameraAsync(
            double[] position,
            double[] focalPoint,
            double[] viewUp,
            double parallelScale,
            string operation = "set")
        {
            if (position == null || position.Length != 3 || focalPoint == null || focalPoint.Length != 3 ||
                viewUp == null || viewUp.Length != 3)
                throw new InvalidOperationException("Custom camera vectors must contain three values.");
            if (operation != "set" && operation != "fit" && operation != "adjust")
                throw new InvalidOperationException("The custom camera operation is invalid.");

            byte[] body;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                bool version2 = operation != "set";
                writer.Write(Encoding.ASCII.GetBytes(version2 ? "PMXCAM2\0" : "PMXCAM1\0"));
                if (version2) writer.Write(operation == "fit" ? 1 : 2);
                foreach (double value in position) writer.Write(value);
                foreach (double value in focalPoint) writer.Write(value);
                foreach (double value in viewUp) writer.Write(value);
                writer.Write(parallelScale);
                body = stream.ToArray();
            }
            await SendHostBodyAsync(
                "/api/v1/host/camera/custom",
                "application/vnd.prepomax.camera-v1",
                body,
                "The renderer host rejected a custom camera update.").ConfigureAwait(false);
        }

        public async Task UpdateActorAsync(string oldName, string newName, int argb)
        {
            byte[] oldNameBytes = Encoding.UTF8.GetBytes(oldName ?? string.Empty);
            byte[] newNameBytes = Encoding.UTF8.GetBytes(newName ?? string.Empty);
            if (oldNameBytes.Length < 1 || oldNameBytes.Length > 1024 ||
                newNameBytes.Length < 1 || newNameBytes.Length > 1024)
                throw new InvalidOperationException("Actor mutation names must contain 1 through 1024 UTF-8 bytes.");

            byte[] body;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(Encoding.ASCII.GetBytes("PMXACT1\0"));
                writer.Write(oldNameBytes.Length);
                writer.Write(oldNameBytes);
                writer.Write(newNameBytes.Length);
                writer.Write(newNameBytes);
                writer.Write(argb);
                body = stream.ToArray();
            }
            await SendHostBodyAsync(
                "/api/v1/host/actor",
                "application/vnd.prepomax.actor-mutation-v1",
                body,
                "The renderer host rejected an actor presentation update.").ConfigureAwait(false);
        }

        public async Task RemoveActorsAsync(IEnumerable<string> actorNames)
        {
            if (actorNames == null) throw new ArgumentNullException("actorNames");
            string[] names = actorNames.Distinct(StringComparer.Ordinal).ToArray();
            if (names.Length < 1 || names.Length > 512)
                throw new InvalidOperationException("Actor removal must contain 1 through 512 distinct names.");

            byte[] body;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(Encoding.ASCII.GetBytes("PMXREM1\0"));
                writer.Write(names.Length);
                foreach (string name in names)
                {
                    byte[] encoded = Encoding.UTF8.GetBytes(name ?? string.Empty);
                    if (encoded.Length < 1 || encoded.Length > 1024)
                        throw new InvalidOperationException("Actor removal names must contain 1 through 1024 UTF-8 bytes.");
                    writer.Write(encoded.Length);
                    writer.Write(encoded);
                }
                body = stream.ToArray();
            }
            await SendHostBodyAsync(
                "/api/v1/host/actors/remove",
                "application/vnd.prepomax.actor-removal-v1",
                body,
                "The renderer host rejected an actor removal.").ConfigureAwait(false);
        }

        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
        }

        private static void TrySetReady(string line, TaskCompletionSource<Uri> ready)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            Match match = ListeningUrlPattern.Match(line);
            Uri uri;
            if (match.Success && Uri.TryCreate(match.Groups[1].Value, UriKind.Absolute, out uri))
                ready.TrySetResult(uri);
        }

        private void EnsureReady(string operation)
        {
            if (_process == null || _baseUri == null || string.IsNullOrEmpty(_hostToken))
                throw new InvalidOperationException("The VTK.js renderer host is not ready for a " + operation + ".");
        }

        private HttpWebRequest CreateHostRequest(string path, string contentType, long contentLength)
        {
            var request = (HttpWebRequest)WebRequest.Create(new Uri(_baseUri, path));
            request.Method = "POST";
            request.ContentLength = contentLength;
            if (!string.IsNullOrEmpty(contentType)) request.ContentType = contentType;
            request.AllowWriteStreamBuffering = false;
            request.Headers["X-PrePoMax-Host-Token"] = _hostToken;
            return request;
        }

        private async Task SendHostBodyAsync(string path, string contentType, byte[] body, string error)
        {
            await _mutationGate.WaitAsync().ConfigureAwait(false);
            try
            {
                EnsureReady("host control update");
                var request = CreateHostRequest(path, contentType, body.LongLength);
                using (Stream requestStream = await request.GetRequestStreamAsync().ConfigureAwait(false))
                {
                    await requestStream.WriteAsync(body, 0, body.Length).ConfigureAwait(false);
                }
                await RequireOkAsync(request, error).ConfigureAwait(false);
            }
            finally
            {
                _mutationGate.Release();
            }
        }

        private static async Task RequireOkAsync(HttpWebRequest request, string error)
        {
            using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
            {
                if (response.StatusCode != HttpStatusCode.OK) throw new InvalidOperationException(error);
            }
        }

        private static void WriteBoundedString(BinaryWriter writer, string value, bool allowEmpty, string label)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            if ((!allowEmpty && bytes.Length == 0) || bytes.Length > 1024)
                throw new InvalidOperationException(label + " must contain " + (allowEmpty ? "0" : "1") + " through 1024 UTF-8 bytes.");
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private static string ResolveServerPath()
        {
            string configured = Environment.GetEnvironmentVariable("PREPOMAX_VTKJS_SERVER_PATH");
            string installed = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VtkJsRenderer", "server",
                                            "PrePoMax.Render.Server.dll");
            string development = Path.Combine(GetRepositoryRoot(), "prototypes", "VtkJsStreaming", "src",
                                              "PrePoMax.Render.Server", "bin", "Release", "net8.0",
                                              "PrePoMax.Render.Server.dll");
            return RequireFile(configured, installed, development, "renderer server");
        }

        private static string ResolveClientRoot()
        {
            string configured = Environment.GetEnvironmentVariable("PREPOMAX_VTKJS_CLIENT_ROOT");
            string installed = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VtkJsRenderer", "client");
            string development = Path.Combine(GetRepositoryRoot(), "prototypes", "VtkJsStreaming", "src", "client", "dist");
            string root = RequireDirectory(configured, installed, development, "renderer client");
            if (!File.Exists(Path.Combine(root, "index.html")))
                throw new FileNotFoundException("The VTK.js renderer client has not been built.", Path.Combine(root, "index.html"));
            return root;
        }

        private static string GetRepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
        }

        private static string RequireFile(string configured, string installed, string development, string description)
        {
            string candidate = !string.IsNullOrWhiteSpace(configured) ? configured : File.Exists(installed) ? installed : development;
            candidate = Path.GetFullPath(candidate);
            if (!File.Exists(candidate)) throw new FileNotFoundException("The " + description + " was not found.", candidate);
            return candidate;
        }

        private static string RequireDirectory(string configured, string installed, string development, string description)
        {
            string candidate = !string.IsNullOrWhiteSpace(configured) ? configured : Directory.Exists(installed) ? installed : development;
            candidate = Path.GetFullPath(candidate);
            if (!Directory.Exists(candidate)) throw new DirectoryNotFoundException("The " + description + " was not found at " + candidate + ".");
            return candidate;
        }

        private static string CreateToken()
        {
            var bytes = new byte[32];
            using (RandomNumberGenerator generator = RandomNumberGenerator.Create()) generator.GetBytes(bytes);
            return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }

    internal sealed class VtkJsFrameUploadMetrics
    {
        public VtkJsFrameUploadMetrics(double writeMs, double responseMs, double totalMs,
                                       double serverReadMs, double serverUpdateMs, double serverTotalMs,
                                       long serverReadAllocatedBytes, long serverUpdateAllocatedBytes,
                                       double validationMs, long validationAllocatedBytes,
                                       double resourcesMs, long resourcesAllocatedBytes,
                                       double manifestMs, long manifestAllocatedBytes,
                                       double refitMs, long refitAllocatedBytes)
        {
            WriteMs = writeMs;
            ResponseMs = responseMs;
            TotalMs = totalMs;
            ServerReadMs = serverReadMs;
            ServerUpdateMs = serverUpdateMs;
            ServerTotalMs = serverTotalMs;
            ServerReadAllocatedBytes = serverReadAllocatedBytes;
            ServerUpdateAllocatedBytes = serverUpdateAllocatedBytes;
            ValidationMs = validationMs;
            ValidationAllocatedBytes = validationAllocatedBytes;
            ResourcesMs = resourcesMs;
            ResourcesAllocatedBytes = resourcesAllocatedBytes;
            ManifestMs = manifestMs;
            ManifestAllocatedBytes = manifestAllocatedBytes;
            RefitMs = refitMs;
            RefitAllocatedBytes = refitAllocatedBytes;
        }

        public double WriteMs { get; private set; }
        public double ResponseMs { get; private set; }
        public double TotalMs { get; private set; }
        public double ServerReadMs { get; private set; }
        public double ServerUpdateMs { get; private set; }
        public double ServerTotalMs { get; private set; }
        public long ServerReadAllocatedBytes { get; private set; }
        public long ServerUpdateAllocatedBytes { get; private set; }
        public double ValidationMs { get; private set; }
        public long ValidationAllocatedBytes { get; private set; }
        public double ResourcesMs { get; private set; }
        public long ResourcesAllocatedBytes { get; private set; }
        public double ManifestMs { get; private set; }
        public long ManifestAllocatedBytes { get; private set; }
        public double RefitMs { get; private set; }
        public long RefitAllocatedBytes { get; private set; }
    }
}

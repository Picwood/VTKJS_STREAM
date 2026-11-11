using System.Net;
using System.Text.Json;
using PrePoMax.Application;

void Check(bool value, string message) { if (!value) throw new Exception(message); }
using var http = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(3) };
Uri address;
await using (var host = await RendererHost.StartAsync(args[1], args[2]))
{
    address = host.Address;
    using var denied = await http.PostAsync(new Uri(address, "api/v1/sessions"), null);
    Check(denied.StatusCode == HttpStatusCode.Unauthorized, "Production session requires host credential");
    using var deniedUpload = await http.PostAsync(new Uri(address, "api/v1/host/scene"), null);
    Check(deniedUpload.StatusCode == HttpStatusCode.Unauthorized, "Scene upload requires host credential");
    const string ready = "{\"type\":\"prepomax.client.ready-v1\"}";
    string? bootstrap = null;
    Check(!host.TryBootstrapBrowser(host.BrowserAddress, true, ready, json => bootstrap = json), "No credentials before publication");
    var first = await host.PublishAsync(args[0]);
    foreach (var source in new[] { new Uri("http://example.com/?embedded=1"), new Uri(address, "other?embedded=1"),
        new Uri(address, "?embedded=1&token=unexpected"), new Uri(address, "?embedded=1#frame") })
        Check(!host.TryBootstrapBrowser(source, true, ready, json => bootstrap = json), "Reject untrusted navigation");
    Check(!host.TryBootstrapBrowser(host.BrowserAddress, false, ready, json => bootstrap = json), "Reject subframe");
    foreach (string invalid in new[] { "{", "null", "{}", "{\"type\":3}", ready.Replace("ready", "other"), new string('x', 1025) })
        Check(!host.TryBootstrapBrowser(host.BrowserAddress, true, invalid, json => bootstrap = json), "Reject malformed message");
    Check(bootstrap == null, "Rejected messages never release credentials");
    Check(host.TryBootstrapBrowser(host.BrowserAddress, true, ready, json => bootstrap = json), "Trusted ready receives bootstrap");
    string firstBootstrap = bootstrap!;
    Check(host.TryBootstrapBrowser(host.BrowserAddress, true, ready, json => bootstrap = json) && bootstrap == firstBootstrap,
        "Repeated ready reuses session");
    using var credentials = JsonDocument.Parse(bootstrap!);
    Check(credentials.RootElement.GetProperty("type").GetString() == "prepomax.session.bootstrap-v1", "Browser message contract");
    using var browser = new HttpClient(new HttpClientHandler { UseProxy = false });
    using var adopt = new HttpRequestMessage(HttpMethod.Post, new Uri(address, "api/v1/sessions/adopt"));
    adopt.Headers.Add("X-PrePoMax-Session", credentials.RootElement.GetProperty("accessToken").GetString());
    using var adopted = await browser.SendAsync(adopt);
    adopted.EnsureSuccessStatusCode();
    using var cookieManifest = await browser.GetAsync(new Uri(address, "api/v1/scenes/" + first.SceneId + "/manifest"));
    cookieManifest.EnsureSuccessStatusCode();
    var second = await host.PublishAsync(args[0]);
    Check(first.Actors == 2 && first.Points == 16 && first.Triangles == 24, "Published multipart topology");
    Check(host.TryBootstrapBrowser(host.BrowserAddress, true, ready, json => bootstrap = json), "Replacement ready accepted");
    using var replacement = JsonDocument.Parse(bootstrap!);
    Check(replacement.RootElement.GetProperty("sceneId").GetString() == second.SceneId, "Bootstrap targets current scene");
    Check(first.SceneId != second.SceneId && second.Actors == 2, "Repeated publication replaces scene");
    using var deniedManifest = await http.GetAsync(new Uri(address, "api/v1/scenes/" + second.SceneId + "/manifest"));
    Check(deniedManifest.StatusCode == HttpStatusCode.Unauthorized, "Manifest requires session");
    using var stop = new CancellationTokenSource(); stop.Cancel();
    bool cancelled = false;
    try { await host.PublishAsync(args[0], stop.Token); } catch (OperationCanceledException) { cancelled = true; }
    Check(cancelled, "Publication honors cancellation");
}
bool closed = false;
try { using var response = await http.GetAsync(new Uri(address, "healthz")); }
catch (HttpRequestException) { closed = true; }
Check(closed, "Disposal closes server listener");
Console.WriteLine("PASS: renderer authentication, replacement, cancellation and process shutdown");

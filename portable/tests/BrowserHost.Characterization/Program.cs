using System.Text.Json;
using PrePoMax.Application;

if (!Console.IsInputRedirected || !Console.IsOutputRedirected)
    throw new InvalidOperationException("Browser acceptance requires private redirected pipes.");

// Test-only private pipe protocol. stdout contains credentials and must never be logged.
await using var host = await RendererHost.StartAsync(args[1], args[2]);
var scene = await host.PublishAsync(args[0]);
Console.WriteLine(JsonSerializer.Serialize(new { address = host.BrowserAddress.AbsoluteUri, scene }));
while (await Console.In.ReadLineAsync() is { } line)
{
    using var request = JsonDocument.Parse(line);
    string? response = null;
    host.TryBootstrapBrowser(new Uri(request.RootElement.GetProperty("source").GetString()!),
        request.RootElement.GetProperty("topLevel").GetBoolean(),
        request.RootElement.GetProperty("message").GetString()!, json => response = json);
    Console.WriteLine(JsonSerializer.Serialize(new { bootstrap = response }));
}

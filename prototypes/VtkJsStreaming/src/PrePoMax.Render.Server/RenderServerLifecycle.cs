namespace PrePoMax.Render.Server;

internal sealed class RenderServerLifecycle(SceneEventHub eventHub) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => eventHub.ShutdownAsync(cancellationToken);
}

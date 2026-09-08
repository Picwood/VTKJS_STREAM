using PrePoMax.Render.Contracts;

namespace PrePoMax.Render.Server;

internal sealed class RenderServerLifecycle(SceneEventHub eventHub, ILogger<RenderServerLifecycle> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        RenderTelemetry.ServerStarted(logger, ProtocolConstants.Version, RenderTelemetry.ServerVersion);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        RenderTelemetry.ServerStopping(logger, ProtocolConstants.Version, RenderTelemetry.ServerVersion);
        return eventHub.ShutdownAsync(cancellationToken);
    }
}

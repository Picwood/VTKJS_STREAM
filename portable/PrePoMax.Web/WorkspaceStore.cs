using System.Security.Cryptography;

namespace PrePoMax.Web;

/// <summary>Bounded, cookie-owned pilot workspaces. Hosted identity/durable tenancy is a separate release gate.</summary>
public sealed class WorkspaceStore : BackgroundService
{
    public const string Cookie = "PrePoMax.Workspace";
    private readonly Dictionary<string, Workspace> _workspaces = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    public Workspace? Find(string? token)
    {
        lock (_sync) return token != null && _workspaces.TryGetValue(token, out var workspace) &&
            workspace.Expires > DateTimeOffset.UtcNow ? workspace : null;
    }

    public (string Token, Workspace Workspace) Create()
    {
        lock (_sync)
        {
            if (_workspaces.Count >= 8) throw new WorkspaceException(503, "All eight pilot workspace slots are in use. Retry after a workspace expires.");
            string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var workspace = new Workspace();
            _workspaces.Add(token, workspace);
            return (token, workspace);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken)) await ReapAsync(all: false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await ReapAsync(all: true);
    }

    private async Task ReapAsync(bool all)
    {
        Workspace[] expired;
        lock (_sync)
        {
            var entries = _workspaces.Where(pair => all || pair.Value.Expires <= DateTimeOffset.UtcNow).ToArray();
            expired = entries.Select(pair => pair.Value).ToArray();
            foreach (var pair in entries) _workspaces.Remove(pair.Key);
        }
        foreach (var workspace in expired)
        {
            await workspace.Gate.WaitAsync();
            try { await workspace.DisposeAsync(); }
            finally { workspace.Gate.Release(); }
        }
    }
}

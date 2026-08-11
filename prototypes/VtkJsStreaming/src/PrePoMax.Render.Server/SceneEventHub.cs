using System.Collections.Concurrent;
using System.Threading.Channels;
using PrePoMax.Render.Contracts;

namespace PrePoMax.Render.Server;

internal sealed class SceneEventHub
{
    private readonly ConcurrentDictionary<Guid, Subscriber> subscribers = new();
    private int shuttingDown;

    public SceneEventSubscription Subscribe(SceneManifest manifest)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<ProtocolMessage>(new BoundedChannelOptions(32)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
        var subscriber = new Subscriber(channel, manifest.SceneId, manifest.SceneRevision);

        if (Volatile.Read(ref shuttingDown) != 0)
        {
            WriteShutdownAndComplete(subscriber);
        }
        else
        {
            subscribers[id] = subscriber;
            if (Volatile.Read(ref shuttingDown) != 0 && subscribers.TryRemove(id, out var removed))
            {
                WriteShutdownAndComplete(removed);
            }
        }

        return new SceneEventSubscription(channel.Reader, () => subscribers.TryRemove(id, out _));
    }

    public void Publish(ProtocolMessage message)
    {
        if (Volatile.Read(ref shuttingDown) != 0)
        {
            return;
        }

        foreach (var subscriber in subscribers.Values)
        {
            subscriber.Channel.Writer.TryWrite(message);
        }
    }

    public async Task ShutdownAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref shuttingDown, 1) != 0)
        {
            return;
        }

        foreach (var pair in subscribers)
        {
            WriteShutdownAndComplete(pair.Value);
        }

        var deadline = TimeProvider.System.GetUtcNow() + TimeSpan.FromSeconds(1);
        while (!subscribers.IsEmpty && TimeProvider.System.GetUtcNow() < deadline && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(25), CancellationToken.None);
        }
    }

    private static void WriteShutdownAndComplete(Subscriber subscriber)
    {
        subscriber.Channel.Writer.TryWrite(new ProtocolMessage(
            ProtocolConstants.Version,
            Guid.NewGuid().ToString("D"),
            null,
            subscriber.SceneId,
            subscriber.SceneRevision,
            "server.shutting-down",
            DateTimeOffset.UtcNow,
            new { reason = "host-stopping" }));
        subscriber.Channel.Writer.TryComplete();
    }

    private sealed record Subscriber(Channel<ProtocolMessage> Channel, string SceneId, long SceneRevision);
}

internal sealed class SceneEventSubscription(ChannelReader<ProtocolMessage> reader, Action dispose) : IDisposable
{
    public ChannelReader<ProtocolMessage> Reader { get; } = reader;

    public void Dispose() => dispose();
}

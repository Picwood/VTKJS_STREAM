using System.Collections.Concurrent;

namespace PrePoMax.Render.Server;

internal sealed record CommandRateLimitOptions(int CommandsPerSecond)
{
    public static CommandRateLimitOptions FromEnvironment()
    {
        const string name = "PREPOMAX_RENDER_COMMANDS_PER_SECOND";
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return new CommandRateLimitOptions(30);
        }

        return int.TryParse(value, out var parsed) && parsed is >= 1 and <= 1_000
            ? new CommandRateLimitOptions(parsed)
            : throw new InvalidOperationException($"{name} must be an integer from 1 through 1000.");
    }
}

internal sealed class CommandRateLimiter(
    CommandRateLimitOptions options,
    SessionStoreOptions sessionOptions,
    TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, RateWindow> windows = new(StringComparer.Ordinal);
    private readonly object windowGate = new();

    public bool TryAcquire(string sessionId, out TimeSpan retryAfter)
    {
        var window = GetOrCreateWindow(sessionId);
        lock (window)
        {
            var now = timeProvider.GetUtcNow();
            var elapsed = now - window.StartedUtc;
            if (elapsed >= TimeSpan.FromSeconds(1) || elapsed < TimeSpan.Zero)
            {
                window.StartedUtc = now;
                window.Count = 0;
                elapsed = TimeSpan.Zero;
            }

            if (window.Count >= options.CommandsPerSecond)
            {
                retryAfter = TimeSpan.FromSeconds(1) - elapsed;
                return false;
            }

            window.Count++;
            retryAfter = TimeSpan.Zero;
            return true;
        }
    }

    private RateWindow GetOrCreateWindow(string sessionId)
    {
        if (windows.TryGetValue(sessionId, out var existing))
        {
            return existing;
        }

        lock (windowGate)
        {
            if (windows.TryGetValue(sessionId, out existing))
            {
                return existing;
            }

            if (windows.Count >= sessionOptions.MaximumSessions)
            {
                var oldest = windows.MinBy(pair => pair.Value.StartedUtc);
                windows.TryRemove(oldest.Key, out _);
            }

            var created = new RateWindow(timeProvider.GetUtcNow());
            windows[sessionId] = created;
            return created;
        }
    }

    private sealed class RateWindow(DateTimeOffset startedUtc)
    {
        public DateTimeOffset StartedUtc { get; set; } = startedUtc;
        public int Count { get; set; }
    }
}

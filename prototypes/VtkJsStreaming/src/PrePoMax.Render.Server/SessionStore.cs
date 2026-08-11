using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace PrePoMax.Render.Server;

internal sealed record RenderSession(
    string SessionId,
    string Token,
    DateTimeOffset CreatedUtc,
    DateTimeOffset ExpiresUtc);

internal sealed record SessionStoreOptions(TimeSpan Lifetime, int MaximumSessions)
{
    public static SessionStoreOptions FromEnvironment() => new(
        TimeSpan.FromMinutes(ReadBoundedInteger("PREPOMAX_RENDER_SESSION_MINUTES", 480, 1, 1_440)),
        ReadBoundedInteger("PREPOMAX_RENDER_MAX_SESSIONS", 64, 1, 1_024));

    private static int ReadBoundedInteger(string name, int fallback, int minimum, int maximum)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return int.TryParse(value, out var parsed) && parsed >= minimum && parsed <= maximum
            ? parsed
            : throw new InvalidOperationException($"{name} must be an integer from {minimum} through {maximum}.");
    }
}

internal sealed class SessionStore(SessionStoreOptions options, TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, RenderSession> sessions = new(StringComparer.Ordinal);
    private readonly object createGate = new();

    public TimeSpan Lifetime => options.Lifetime;

    public bool TryCreate(out RenderSession? session)
    {
        lock (createGate)
        {
            var now = timeProvider.GetUtcNow();
            RemoveExpired(now);
            if (sessions.Count >= options.MaximumSessions)
            {
                session = null;
                return false;
            }

            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
            session = new RenderSession(Guid.NewGuid().ToString("D"), token, now, now + options.Lifetime);
            sessions[token] = session;
            return true;
        }
    }

    public bool TryAuthorize(HttpContext context, out RenderSession? session)
    {
        var token = GetToken(context);
        if (token is null || !sessions.TryGetValue(token, out session))
        {
            session = null;
            return false;
        }

        if (session.ExpiresUtc > timeProvider.GetUtcNow())
        {
            return true;
        }

        sessions.TryRemove(new KeyValuePair<string, RenderSession>(token, session));
        session = null;
        return false;
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var pair in sessions)
        {
            if (pair.Value.ExpiresUtc <= now)
            {
                sessions.TryRemove(pair);
            }
        }
    }

    private static string? GetToken(HttpContext context)
    {
        if (context.Request.Cookies.TryGetValue("prepomax-render-session", out var cookieToken))
        {
            return cookieToken;
        }

        if (context.Request.Headers.TryGetValue("X-PrePoMax-Session", out var headerToken))
        {
            return headerToken.ToString();
        }

        const string bearerPrefix = "Bearer ";
        var authorization = context.Request.Headers.Authorization.ToString();
        return authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[bearerPrefix.Length..]
            : null;
    }
}

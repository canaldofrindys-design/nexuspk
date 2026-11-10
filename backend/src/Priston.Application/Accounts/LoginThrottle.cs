using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Priston.Application.Accounts;

/// <summary>
/// Conta falhas de login por conta e por IP e bloqueia novas tentativas por alguns minutos.
/// Fica em memória: o portal roda em uma única instância da API.
/// </summary>
public sealed class LoginThrottle(IMemoryCache cache, IOptions<AccountOptions> options, TimeProvider clock)
{
    private sealed class Counter
    {
        public int Failures;
        public DateTimeOffset WindowEnds;
    }

    private readonly Lock _gate = new();

    public bool IsLocked(string login, string? ip)
    {
        var o = options.Value;
        return Exceeded($"login:{login.ToUpperInvariant()}", o.MaxFailuresPerLogin)
            || (ip is not null && Exceeded($"ip:{ip}", o.MaxFailuresPerIp));
    }

    public void RecordFailure(string login, string? ip)
    {
        Increment($"login:{login.ToUpperInvariant()}");
        if (ip is not null)
        {
            Increment($"ip:{ip}");
        }
    }

    public void Reset(string login) => cache.Remove($"throttle:login:{login.ToUpperInvariant()}");

    private bool Exceeded(string key, int limit) =>
        cache.TryGetValue($"throttle:{key}", out Counter? c) && c is not null
        && c.WindowEnds > clock.GetUtcNow() && c.Failures >= limit;

    private void Increment(string key)
    {
        var window = TimeSpan.FromMinutes(options.Value.LockoutMinutes);
        var now = clock.GetUtcNow();
        lock (_gate)
        {
            if (!cache.TryGetValue($"throttle:{key}", out Counter? c) || c is null || c.WindowEnds <= now)
            {
                c = new Counter { WindowEnds = now + window };
            }

            c.Failures++;
            cache.Set($"throttle:{key}", c, c.WindowEnds - now);
        }
    }
}

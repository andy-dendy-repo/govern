using System.Threading.RateLimiting;
using Govern.Infrastructure.RateLimiting.Models;
using StackExchange.Redis;

namespace Govern.Infrastructure.RateLimiting.Helpers;

/// <summary>
/// A fixed window counted in Redis: the first request creates the counter with the window as its expiry, every request
/// increments it, and requests past the limit are rejected until the key expires. Unlike the BCL limiter, a window starts at
/// its first request rather than on a timer.
/// </summary>
internal sealed class RedisFixedWindowRateLimiter : RedisRateLimiter
{
    // INCRBY, and set the expiry when this request created the counter; one round trip, atomic.
    private const string Script = """
        local count = redis.call('INCRBY', KEYS[1], ARGV[1])
        if count == tonumber(ARGV[1]) then
          redis.call('PEXPIRE', KEYS[1], ARGV[2])
        end
        return count
        """;

    private readonly RedisKey _key;
    private readonly int _permitLimit;
    private readonly long _windowMilliseconds;

    /// <summary>Creates the limiter.</summary>
    /// <param name="context">The Redis database, clock and logger.</param>
    /// <param name="key">The counter's key.</param>
    /// <param name="permitLimit">Requests allowed per window.</param>
    /// <param name="window">The window length.</param>
    public RedisFixedWindowRateLimiter(RedisLimiterContext context, RedisKey key, int permitLimit, TimeSpan window)
        : base(context, new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = window }))
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(permitLimit, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);
        _key = key;
        _permitLimit = permitLimit;
        _windowMilliseconds = (long)Math.Ceiling(window.TotalMilliseconds);
    }

    /// <summary>
    /// The time since the last request: the counter lives in Redis, so the partition can be dropped. While the in-process
    /// fallback has used permits (Redis was unreachable) it is <see langword="null"/>, so dropping the partition can't reset
    /// the fallback's window.
    /// </summary>
    public override TimeSpan? IdleDuration => FallbackIdleDuration() is null ? null : SinceLastActivity;

    /// <inheritdoc />
    protected override async Task<RateLimitLease> AcquireInRedisAsync(int permitCount)
    {
        var count = (long)await Context.Database.ScriptEvaluateAsync(Script, [_key], [permitCount, _windowMilliseconds]);
        return count <= _permitLimit ? RedisLease.Acquired : RedisLease.Rejected;
    }
}

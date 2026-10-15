using System.Threading.RateLimiting;
using Govern.Infrastructure.RateLimiting.Models;
using StackExchange.Redis;

namespace Govern.Infrastructure.RateLimiting.Helpers;

/// <summary>
/// A concurrency limit counted in Redis: the held leases of a key are a sorted set of lease ids scored by their expiry time.
/// Acquiring drops expired ids and adds one when fewer than the limit remain; disposing the lease removes its id. A lease that
/// is never released (its instance died) frees itself at its expiry.
/// </summary>
internal sealed class RedisConcurrencyLimiter : RedisRateLimiter
{
    private const string Script = """
        redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', ARGV[1])
        if redis.call('ZCARD', KEYS[1]) + tonumber(ARGV[2]) <= tonumber(ARGV[3]) then
          for i = 1, tonumber(ARGV[2]) do
            redis.call('ZADD', KEYS[1], ARGV[4], ARGV[5] .. ':' .. i)
          end
          redis.call('PEXPIRE', KEYS[1], ARGV[6])
          return 1
        end
        return 0
        """;

    private readonly RedisKey _key;
    private readonly int _permitLimit;
    private readonly TimeSpan _leaseLifetime;
    private int _held;

    /// <summary>Creates the limiter.</summary>
    /// <param name="context">The Redis database, clock and logger.</param>
    /// <param name="key">The sorted set's key.</param>
    /// <param name="permitLimit">Leases allowed at once.</param>
    /// <param name="leaseLifetime">How long an unreleased lease counts at most.</param>
    public RedisConcurrencyLimiter(RedisLimiterContext context, RedisKey key, int permitLimit, TimeSpan leaseLifetime)
        : base(context, new ConcurrencyLimiter(new ConcurrencyLimiterOptions { PermitLimit = permitLimit, QueueLimit = 0 }))
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(permitLimit, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseLifetime, TimeSpan.Zero);
        _key = key;
        _permitLimit = permitLimit;
        _leaseLifetime = leaseLifetime;
    }

    /// <summary>
    /// <see langword="null"/> while this instance holds a lease (from Redis or the fallback), so a partitioned policy keeps the
    /// limiter; otherwise the time since the last acquire or release.
    /// </summary>
    public override TimeSpan? IdleDuration =>
        Volatile.Read(ref _held) > 0 || FallbackIdleDuration() is null ? null : SinceLastActivity;

    /// <inheritdoc />
    protected override async Task<RateLimitLease> AcquireInRedisAsync(int permitCount)
    {
        var now = Context.TimeProvider.GetUtcNow();
        var leaseId = Guid.NewGuid().ToString("N");
        var lifetimeMilliseconds = (long)Math.Ceiling(_leaseLifetime.TotalMilliseconds);
        var acquired = (long)await Context.Database.ScriptEvaluateAsync(
            Script,
            [_key],
            [now.ToUnixTimeMilliseconds(), permitCount, _permitLimit, (now + _leaseLifetime).ToUnixTimeMilliseconds(), leaseId, lifetimeMilliseconds]) == 1;
        if (!acquired)
        {
            return RedisLease.Rejected;
        }

        Interlocked.Increment(ref _held);
        return new RedisLease(true, () => Release(leaseId, permitCount));
    }

    // Fire and forget: a release that never reaches Redis (it went down) frees itself at the lease's expiry.
    private void Release(string leaseId, int permitCount)
    {
        RedisValue[] members = [.. Enumerable.Range(1, permitCount).Select(i => (RedisValue)$"{leaseId}:{i}")];
        try
        {
            Context.Database.SortedSetRemove(_key, members, CommandFlags.FireAndForget);
        }
        catch (Exception exception) when (exception is RedisException or RedisTimeoutException)
        {
            // Nothing to do: the lease frees itself at its expiry.
        }

        Interlocked.Decrement(ref _held);
        Touch();
    }
}

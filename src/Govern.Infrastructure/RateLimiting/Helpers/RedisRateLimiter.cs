using System.Threading.RateLimiting;
using Govern.Infrastructure.RateLimiting.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Govern.Infrastructure.RateLimiting.Helpers;

/// <summary>
/// Base of the limiters that count in Redis, so every Api instance shares one count per key. Redis is only reached
/// asynchronously: <see cref="RateLimiter.AttemptAcquire(int)"/> always fails, and callers that honour that contract (the
/// ASP.NET Core rate-limiting middleware and chained limiters) then call <see cref="RateLimiter.AcquireAsync"/>.
/// While Redis is unreachable, requests are counted by an in-process <c>fallback</c> limiter instead, so limits loosen to per
/// instance for the outage rather than blocking every request or none.
/// </summary>
/// <param name="context">The Redis database, clock and logger.</param>
/// <param name="fallback">The in-process limiter used while Redis is unreachable; owned and disposed by this limiter.</param>
internal abstract partial class RedisRateLimiter(RedisLimiterContext context, RateLimiter fallback) : RateLimiter
{
    private long _lastActivity = context.TimeProvider.GetTimestamp();

    /// <summary>The Redis database, clock and logger.</summary>
    protected RedisLimiterContext Context { get; } = context;

    /// <summary>The time since the last acquire or release; subclasses report <see langword="null"/> while leases are held.</summary>
    protected TimeSpan SinceLastActivity => Context.TimeProvider.GetElapsedTime(Interlocked.Read(ref _lastActivity));

    /// <inheritdoc />
    public override RateLimiterStatistics? GetStatistics() => null;

    /// <summary>Marks activity now, for <see cref="RateLimiter.IdleDuration"/>.</summary>
    protected void Touch() => Interlocked.Exchange(ref _lastActivity, Context.TimeProvider.GetTimestamp());

    /// <summary>Asks Redis for <paramref name="permitCount"/> permits.</summary>
    /// <param name="permitCount">Permits requested; at least 1.</param>
    /// <returns>The lease, acquired or rejected.</returns>
    /// <exception cref="RedisException">Redis is unreachable.</exception>
    protected abstract Task<RateLimitLease> AcquireInRedisAsync(int permitCount);

    /// <inheritdoc />
    protected override RateLimitLease AttemptAcquireCore(int permitCount) => RedisLease.Rejected;

    /// <inheritdoc />
    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        Touch();
        if (permitCount == 0)
        {
            return RedisLease.Acquired;
        }

        try
        {
            return await AcquireInRedisAsync(permitCount);
        }
        catch (Exception exception) when (exception is RedisException or RedisTimeoutException)
        {
            Log.FellBack(Context.Logger, exception);
            return await fallback.AcquireAsync(permitCount, cancellationToken);
        }
    }

    /// <summary>The fallback's idle time, for subclasses that combine it with their own.</summary>
    /// <returns><see langword="null"/> while the fallback holds leases.</returns>
    protected TimeSpan? FallbackIdleDuration() => fallback.IdleDuration;

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            fallback.Dispose();
        }

        base.Dispose(disposing);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 2310, Level = LogLevel.Debug, Message = "Redis is unreachable; this request is rate limited per instance")]
        public static partial void FellBack(ILogger logger, Exception exception);
    }
}

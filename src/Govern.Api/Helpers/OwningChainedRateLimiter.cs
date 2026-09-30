using System.Threading.RateLimiting;

namespace Govern.Api.Helpers;

/// <summary>
/// A chain of rate limiters (see <see cref="RateLimiter.CreateChained"/>) that also disposes the limiters it chains and is
/// idle only when all of them are. <see cref="RateLimiter.CreateChained"/> leaves them undisposed (a partitioned policy that
/// drops an idle partition would leak their timers) and can report idle while a concurrency permit is still held. Every limiter must grant a lease for a request to pass; they are asked in the given order.
/// </summary>
internal sealed class OwningChainedRateLimiter : RateLimiter
{
    private readonly RateLimiter[] _limiters;
    private readonly RateLimiter _chain;

    /// <summary>Chains <paramref name="limiters"/>, taking ownership of them.</summary>
    /// <param name="limiters">The limiters, in the order they are asked; at least one.</param>
    /// <exception cref="ArgumentNullException"><paramref name="limiters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="limiters"/> is empty.</exception>
    public OwningChainedRateLimiter(params RateLimiter[] limiters)
    {
        _chain = CreateChained(limiters);
        _limiters = [.. limiters];
    }

    /// <summary>
    /// <see langword="null"/> while any chained limiter is in use (e.g. a concurrency permit is held by an open stream),
    /// otherwise the shortest idle time. The chain returned by <see cref="RateLimiter.CreateChained"/> reports idle as soon as
    /// a time window refills, which lets a partitioned policy drop and recreate the partition while concurrency permits are
    /// still held, resetting the concurrency limit.
    /// </summary>
    public override TimeSpan? IdleDuration
    {
        get
        {
            TimeSpan? shortest = null;
            foreach (var limiter in _limiters)
            {
                if (limiter.IdleDuration is not { } idle)
                {
                    return null;
                }

                shortest = shortest is { } current && current < idle ? current : idle;
            }

            return shortest;
        }
    }

    /// <inheritdoc />
    public override RateLimiterStatistics? GetStatistics() => _chain.GetStatistics();

    /// <inheritdoc />
    protected override RateLimitLease AttemptAcquireCore(int permitCount) => _chain.AttemptAcquire(permitCount);

    /// <inheritdoc />
    protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken) =>
        _chain.AcquireAsync(permitCount, cancellationToken);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _chain.Dispose();
            foreach (var limiter in _limiters)
            {
                limiter.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        await _chain.DisposeAsync().ConfigureAwait(false);
        foreach (var limiter in _limiters)
        {
            await limiter.DisposeAsync().ConfigureAwait(false);
        }

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }
}

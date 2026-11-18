using System.Threading.RateLimiting;

namespace Govern.Application.Common.Abstractions;

/// <summary>
/// Creates the rate limiters behind the Api's rate-limit policies. With one Api instance they count in process; with Redis
/// configured they count in Redis, so the configured limits hold across all instances instead of once per instance.
/// </summary>
public interface IRateLimiterService
{
    /// <summary>Creates a fixed-window limiter: at most <paramref name="permitLimit"/> requests per window.</summary>
    /// <param name="key">
    /// Identifies the counter, unique per policy and partition (e.g. <c>game-commands:user:&lt;id&gt;</c>); limiters created
    /// with the same key share one count across instances.
    /// </param>
    /// <param name="permitLimit">Requests allowed per window; at least 1.</param>
    /// <param name="window">The window length; positive.</param>
    /// <returns>A limiter that never queues: a request over the limit is rejected at once.</returns>
    RateLimiter CreateFixedWindow(string key, int permitLimit, TimeSpan window);

    /// <summary>
    /// Creates a concurrency limiter: at most <paramref name="permitLimit"/> leases held at once (e.g. open streams). A lease
    /// is held until it is disposed.
    /// </summary>
    /// <param name="key">Identifies the counter, as for <see cref="CreateFixedWindow"/>.</param>
    /// <param name="permitLimit">Leases allowed at once; at least 1.</param>
    /// <param name="leaseLifetime">
    /// How long a lease may live at most. A shared store frees a lease after this long even if the instance holding it died
    /// without releasing it; in process it is unused.
    /// </param>
    /// <returns>A limiter that never queues.</returns>
    RateLimiter CreateConcurrency(string key, int permitLimit, TimeSpan leaseLifetime);
}

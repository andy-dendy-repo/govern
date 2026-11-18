using System.Threading.RateLimiting;
using Govern.Application.Common.Abstractions;

namespace Govern.Infrastructure.RateLimiting.Services;

/// <summary>
/// In-process <see cref="IRateLimiterService"/>: the BCL limiters, counting per Api instance. Used when Redis is not configured.
/// </summary>
internal sealed class MemoryRateLimiterService : IRateLimiterService
{
    /// <inheritdoc />
    public RateLimiter CreateFixedWindow(string key, int permitLimit, TimeSpan window) =>
        new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = window });

    /// <inheritdoc />
    public RateLimiter CreateConcurrency(string key, int permitLimit, TimeSpan leaseLifetime) =>
        new ConcurrencyLimiter(new ConcurrencyLimiterOptions { PermitLimit = permitLimit, QueueLimit = 0 });
}

using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Govern.Application.Common.Abstractions;
using Govern.Infrastructure.RateLimiting.Helpers;
using Govern.Infrastructure.RateLimiting.Models;
using Govern.Infrastructure.Redis.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Govern.Infrastructure.RateLimiting.Services;

/// <summary>
/// <see cref="IRateLimiterService"/> that counts in Redis, so every Api instance enforces the same limits once. Keys are
/// <c>&lt;prefix&gt;:rl:&lt;hash of key&gt;</c>: partition keys hold client IP addresses, which are not stored in Redis. While Redis is unreachable each limiter counts in process (see
/// <see cref="RedisRateLimiter"/>).
/// </summary>
internal sealed class RedisRateLimiterService(
    IConnectionMultiplexer redis,
    IOptions<RedisOptions> options,
    TimeProvider timeProvider,
    ILogger<RedisRateLimiterService> logger) : IRateLimiterService
{
    /// <inheritdoc />
    public RateLimiter CreateFixedWindow(string key, int permitLimit, TimeSpan window) =>
        new RedisFixedWindowRateLimiter(CreateContext(), RedisKeyFor(key), permitLimit, window);

    /// <inheritdoc />
    public RateLimiter CreateConcurrency(string key, int permitLimit, TimeSpan leaseLifetime) =>
        new RedisConcurrencyLimiter(CreateContext(), RedisKeyFor(key), permitLimit, leaseLifetime);

    private RedisLimiterContext CreateContext() => new(redis.GetDatabase(), timeProvider, logger);

    private RedisKey RedisKeyFor(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return options.Value.Key($"rl:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16))}");
    }
}

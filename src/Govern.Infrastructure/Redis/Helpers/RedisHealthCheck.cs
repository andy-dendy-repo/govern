using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Govern.Infrastructure.Redis.Helpers;

/// <summary>
/// Readiness of Redis (registered only when Redis is configured): a <c>PING</c>. While Redis is down the Api still serves,
/// with per-instance limits and same-instance live updates, so this check reports <see cref="HealthStatus.Degraded"/>, which
/// keeps <c>/health/ready</c> answering 200 and lets a load balancer keep the instance.
/// </summary>
/// <param name="redis">The shared connection.</param>
internal sealed class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"PING {latency.TotalMilliseconds:F0} ms");
        }
        catch (Exception exception) when (exception is RedisException or RedisTimeoutException)
        {
            return HealthCheckResult.Degraded("Redis is unreachable", exception);
        }
    }
}

using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Govern.Infrastructure.RateLimiting.Models;

/// <summary>What every Redis-backed limiter needs besides its own key and limits.</summary>
/// <param name="Database">The Redis database that holds the counters.</param>
/// <param name="TimeProvider">The clock (lease expiry and idle time).</param>
/// <param name="Logger">Logs falling back to the in-process limiter.</param>
internal sealed record RedisLimiterContext(IDatabase Database, TimeProvider TimeProvider, ILogger Logger);

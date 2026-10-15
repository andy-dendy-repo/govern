using System.Security.Cryptography;
using System.Text;
using Govern.Application.Common.Abstractions;
using Govern.Application.Common.Options;
using Govern.Domain.ValueObjects;
using Govern.Infrastructure.Redis.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Govern.Infrastructure.Caching.Services;

/// <summary>
/// <see cref="ISignInThrottlingService"/> shared by every Api instance: one Redis counter per account, keyed by the SHA-256 of
/// the normalised email (no email is stored in Redis), created with the window as its expiry. The check and the increment run
/// in one script, so parallel attempts on any instance can't all pass. While Redis is unreachable, attempts are counted by
/// <see cref="MemorySignInThrottlingService"/> per instance.
/// </summary>
internal sealed partial class RedisSignInThrottlingService(
    IConnectionMultiplexer redis,
    MemorySignInThrottlingService fallback,
    IOptions<SignInThrottlingOptions> throttlingOptions,
    IOptions<RedisOptions> redisOptions,
    ILogger<RedisSignInThrottlingService> logger) : ISignInThrottlingService
{
    // Refuse at the limit without counting; otherwise count, and start the window on the first attempt.
    private const string Script = """
        local count = tonumber(redis.call('GET', KEYS[1]) or '0')
        if count >= tonumber(ARGV[1]) then
          return 0
        end
        if redis.call('INCR', KEYS[1]) == 1 then
          redis.call('PEXPIRE', KEYS[1], ARGV[2])
        end
        return 1
        """;

    /// <inheritdoc />
    public async ValueTask<bool> TryBeginAttemptAsync(Email email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);
        var options = throttlingOptions.Value;
        try
        {
            var allowed = await redis.GetDatabase().ScriptEvaluateAsync(
                Script,
                [KeyFor(email)],
                [options.MaxFailedAttempts, (long)Math.Ceiling(options.Window.TotalMilliseconds)]);
            return (long)allowed == 1;
        }
        catch (Exception exception) when (exception is RedisException or RedisTimeoutException)
        {
            Log.FellBack(logger, exception);
            return fallback.TryBeginAttempt(email);
        }
    }

    /// <inheritdoc />
    public async ValueTask ResetAsync(Email email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);
        fallback.Reset(email);
        try
        {
            await redis.GetDatabase().KeyDeleteAsync(KeyFor(email));
        }
        catch (Exception exception) when (exception is RedisException or RedisTimeoutException)
        {
            Log.FellBack(logger, exception);
        }
    }

    private RedisKey KeyFor(Email email) =>
        redisOptions.Value.Key($"sign-in:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(email.Value)))}");

    private static partial class Log
    {
        [LoggerMessage(EventId = 2320, Level = LogLevel.Warning, Message = "Redis is unreachable; sign-in attempts are throttled per instance")]
        public static partial void FellBack(ILogger logger, Exception exception);
    }
}

using Govern.Application.Common.Abstractions;
using Govern.Infrastructure.Redis.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Govern.Infrastructure.Realtime.Services;

/// <summary>
/// <see cref="IGameChangeService"/> for several Api instances: a change wakes the local watchers at once (through
/// <see cref="InMemoryGameChangeService"/>) and is published on one Redis pub/sub channel, so every other instance wakes its
/// own watchers of that game. A message carries only the publishing instance and the game id, never game data.
/// </summary>
/// <remarks>
/// Redis pub/sub is at-most-once: messages published while an instance is disconnected are lost. After the subscription
/// connection is restored, every local watcher is signalled once; a stream re-reads its game and pushes only if the player's
/// view changed, so a lost message costs one read, never a missed move.
/// </remarks>
internal sealed partial class RedisGameChangeService(
    InMemoryGameChangeService local,
    IConnectionMultiplexer redis,
    IOptions<RedisOptions> options,
    ILogger<RedisGameChangeService> logger) : IGameChangeService, IHostedService
{
    private readonly string _instanceId = Guid.NewGuid().ToString("N");
    private readonly RedisChannel _channel = RedisChannel.Literal(options.Value.Key("game-changed"));
    private int _subscribed;

    /// <inheritdoc />
    public void Publish(Guid gameId)
    {
        local.Publish(gameId);
        _ = PublishToOtherInstancesAsync(gameId);
    }

    /// <inheritdoc />
    public IGameChangeSubscription Subscribe(Guid gameId) => local.Subscribe(gameId);

    /// <summary>Subscribes to the channel and starts watching the connection for reconnects.</summary>
    /// <param name="cancellationToken">Unused; the subscription never blocks startup on Redis.</param>
    /// <returns>A task that completes once the first subscription attempt finished.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        redis.ConnectionRestored += OnConnectionRestored;
        await EnsureSubscribedAsync();
    }

    /// <summary>Stops listening; the multiplexer itself is disposed by the container.</summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        redis.ConnectionRestored -= OnConnectionRestored;
        return Task.CompletedTask;
    }

    private async Task PublishToOtherInstancesAsync(Guid gameId)
    {
        try
        {
            await redis.GetSubscriber().PublishAsync(_channel, $"{_instanceId}:{gameId:N}");
        }
        catch (Exception exception) when (exception is RedisException or RedisTimeoutException)
        {
            Log.PublishFailed(logger, gameId, exception);
        }
    }

    // Subscribes once. A subscription made while Redis is unreachable fails; the next restored connection retries it.
    private async Task EnsureSubscribedAsync()
    {
        if (Interlocked.CompareExchange(ref _subscribed, 1, 0) != 0)
        {
            return;
        }

        try
        {
            await redis.GetSubscriber().SubscribeAsync(_channel, OnMessage);
        }
        catch (Exception exception) when (exception is RedisException or RedisTimeoutException)
        {
            Volatile.Write(ref _subscribed, 0);
            Log.SubscribeFailed(logger, exception);
        }
    }

    private void OnMessage(RedisChannel channel, RedisValue message)
    {
        var text = message.ToString();
        var separator = text.IndexOf(':', StringComparison.Ordinal);
        if (separator > 0
            && !text.AsSpan(0, separator).SequenceEqual(_instanceId)
            && Guid.TryParseExact(text.AsSpan(separator + 1), "N", out var gameId))
        {
            local.Publish(gameId);
        }
    }

    private void OnConnectionRestored(object? sender, ConnectionFailedEventArgs args)
    {
        if (args.ConnectionType != ConnectionType.Subscription)
        {
            return;
        }

        Log.Resynced(logger);
        _ = EnsureSubscribedAsync();
        local.SignalAll();
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 2300, Level = LogLevel.Warning, Message = "Publishing the change of game {GameId} to other Api instances failed")]
        public static partial void PublishFailed(ILogger logger, Guid gameId, Exception exception);

        [LoggerMessage(EventId = 2301, Level = LogLevel.Warning, Message = "Subscribing to game changes from other Api instances failed; retrying when Redis reconnects")]
        public static partial void SubscribeFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 2302, Level = LogLevel.Information, Message = "Redis subscription connection restored; signalling every open game stream once")]
        public static partial void Resynced(ILogger logger);
    }
}

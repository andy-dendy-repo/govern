using System.Collections.Concurrent;
using System.Threading.Channels;
using Govern.Application.Common.Abstractions;

namespace Govern.Infrastructure.Realtime.Services;

/// <summary>
/// In-process <see cref="IGameChangeService"/>: each subscription is a one-slot channel, so a burst of changes wakes a watcher
/// once and a slow watcher never holds up the publisher. Only watchers on the same Api instance are signalled, which is enough
/// for a single Web App instance; with Redis configured, <see cref="RedisGameChangeService"/> uses this hub as its local fan-out
/// and carries changes between instances.
/// </summary>
internal sealed class InMemoryGameChangeService : IGameChangeService
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Subscription, byte>> _games = new();
    private readonly Lock _gate = new();

    /// <inheritdoc />
    public void Publish(Guid gameId)
    {
        if (_games.TryGetValue(gameId, out var subscriptions))
        {
            foreach (var subscription in subscriptions.Keys)
            {
                subscription.Signal();
            }
        }
    }

    /// <summary>
    /// Signals every current subscription of every game, so each watcher re-reads its game once. Used after the backplane
    /// reconnects, when changes published by other instances may have been missed.
    /// </summary>
    public void SignalAll()
    {
        foreach (var subscriptions in _games.Values)
        {
            foreach (var subscription in subscriptions.Keys)
            {
                subscription.Signal();
            }
        }
    }

    /// <inheritdoc />
    public IGameChangeSubscription Subscribe(Guid gameId)
    {
        var subscription = new Subscription(this, gameId);
        lock (_gate)
        {
            _games.GetOrAdd(gameId, static _ => new ConcurrentDictionary<Subscription, byte>()).TryAdd(subscription, 0);
        }

        return subscription;
    }

    // Removes a disposed subscription, and the game's entry with its last one (under the lock, so a concurrent Subscribe
    // never adds to an entry that is being dropped).
    private void Remove(Subscription subscription)
    {
        lock (_gate)
        {
            if (_games.TryGetValue(subscription.GameId, out var subscriptions)
                && subscriptions.TryRemove(subscription, out _)
                && subscriptions.IsEmpty)
            {
                _games.TryRemove(subscription.GameId, out _);
            }
        }
    }

    private sealed class Subscription(InMemoryGameChangeService owner, Guid gameId) : IGameChangeSubscription
    {
        private readonly Channel<bool> _changes = Channel.CreateBounded<bool>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

        private int _disposed;

        public Guid GameId { get; } = gameId;

        public void Signal() => _changes.Writer.TryWrite(true);

        public async ValueTask WaitAsync(CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
            await _changes.Reader.ReadAsync(cancellationToken);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.Remove(this);
                _changes.Writer.TryComplete();
            }
        }
    }
}

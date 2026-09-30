using System.Runtime.CompilerServices;
using Govern.Client.Features.Games.Clients;
using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Helpers;
using Govern.Client.Features.Games.Models;

namespace Govern.Client.Features.Games.Services;

/// <summary>
/// Keeps a game's live stream open: reconnects with backoff after every end (<see cref="ReconnectBackoff"/>), drops a
/// connection that stays silent for <see cref="SilenceTimeout"/>, and stops for good when the game is gone, finished, or the
/// session ended. Each new connection gets a fresh access token from the auth handler, which refreshes ahead of expiry.
/// </summary>
public sealed class GameWatchService(GamesClient games, TimeProvider timeProvider)
{
    /// <summary>
    /// How long a connection may go without any event (the Api sends a heartbeat after 20 s of silence) before it is
    /// considered dead and reopened.
    /// </summary>
    public static readonly TimeSpan SilenceTimeout = TimeSpan.FromSeconds(50);

    /// <summary>
    /// Watches a game until <paramref name="cancellationToken"/> is cancelled or a final status (<see cref="GameWatchStatus.Gone"/>,
    /// <see cref="GameWatchStatus.SignedOut"/>, <see cref="GameWatchStatus.Finished"/>) is reached.
    /// </summary>
    /// <param name="gameId">The game.</param>
    /// <param name="cancellationToken">Stops watching; the enumeration then throws <see cref="OperationCanceledException"/>.</param>
    /// <returns>
    /// <see cref="GameWatchStatus.Connecting"/> first, then <see cref="GameWatchStatus.Live"/> with each snapshot,
    /// <see cref="GameWatchStatus.Reconnecting"/> with the delay before each new attempt, and at most one final status.
    /// </returns>
    public async IAsyncEnumerable<GameWatchUpdate> WatchAsync(Guid gameId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var state = new WatchState();
        yield return new GameWatchUpdate(GameWatchStatus.Connecting);

        while (true)
        {
            await foreach (var update in ConnectAsync(gameId, state, cancellationToken))
            {
                yield return update;
            }

            if (FinalStatus(state) is { } final)
            {
                yield return new GameWatchUpdate(final, state.Game);
                yield break;
            }

            var delay = NextDelay(state);
            yield return new GameWatchUpdate(GameWatchStatus.Reconnecting, RetryIn: delay);
            await Task.Delay(delay, timeProvider, cancellationToken);
        }
    }

    // One connection: yields its snapshots and records how and when it ended in the state.
    private async IAsyncEnumerable<GameWatchUpdate> ConnectAsync(
        Guid gameId, WatchState state, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        state.ConnectedAt = null;
        state.End = GameStreamEnd.Failed;

        using var connection = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var silence = timeProvider.CreateTimer(CancelConnection, connection, SilenceTimeout, Timeout.InfiniteTimeSpan);
        var events = games.WatchAsync(gameId, connection.Token).GetAsyncEnumerator(connection.Token);
        await using (events)
        {
            while (await NextAsync(events, cancellationToken) is { } next)
            {
                silence.Change(SilenceTimeout, Timeout.InfiniteTimeSpan);
                if (next is GameStreamClosedEvent closed)
                {
                    state.End = closed.Reason;
                    yield break;
                }

                if (next is GameSnapshotEvent snapshot)
                {
                    state.ConnectedAt ??= timeProvider.GetUtcNow();
                    state.Game = snapshot.Game;
                    yield return new GameWatchUpdate(GameWatchStatus.Live, snapshot.Game);
                }
            }
        }
    }

    // The next event, or null when the silence timer dropped the connection. The caller's own cancellation still throws.
    private static async Task<GameStreamEvent?> NextAsync(IAsyncEnumerator<GameStreamEvent> events, CancellationToken cancellationToken)
    {
        try
        {
            return await events.MoveNextAsync() ? events.Current : null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static GameWatchStatus? FinalStatus(WatchState state) => state.End switch
    {
        GameStreamEnd.NotFound => GameWatchStatus.Gone,
        GameStreamEnd.Unauthorized => GameWatchStatus.SignedOut,
        GameStreamEnd.Closed when state.Game?.Phase == GamePhase.Finished => GameWatchStatus.Finished,
        _ => null,
    };

    private TimeSpan NextDelay(WatchState state)
    {
        // Healthy means connected (a snapshot arrived) for a while, not merely a slow attempt, so failures still back off.
        var healthy = state.ConnectedAt is { } connectedAt
            && timeProvider.GetUtcNow() - connectedAt >= ReconnectBackoff.HealthyConnection;
        if (healthy)
        {
            state.Failures = 0;
            if (state.End == GameStreamEnd.Closed)
            {
                return ReconnectBackoff.AfterClose(Random.Shared.NextDouble());
            }
        }

        state.Failures++;
        return state.End == GameStreamEnd.RateLimited
            ? ReconnectBackoff.AfterRateLimit(state.Failures, Random.Shared.NextDouble())
            : ReconnectBackoff.AfterFailure(state.Failures, Random.Shared.NextDouble());
    }

    private static void CancelConnection(object? connection)
    {
        try
        {
            ((CancellationTokenSource)connection!).Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The connection already ended.
        }
    }

    private sealed class WatchState
    {
        public GetGameResponse? Game { get; set; }

        public GameStreamEnd End { get; set; }

        public DateTimeOffset? ConnectedAt { get; set; }

        public int Failures { get; set; }
    }
}

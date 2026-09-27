using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;

namespace Govern.Api.Extensions;

/// <summary>
/// Turns an application stream into server-sent events for <c>TypedResults.ServerSentEvents</c>.
/// </summary>
public static class AsyncEnumerableExtensions
{
    /// <summary>The event type of a keep-alive event; its data is <c>null</c>.</summary>
    public const string HeartbeatEventType = "heartbeat";

    /// <param name="source">The values to send, e.g. a game's snapshots.</param>
    /// <typeparam name="T">The value type.</typeparam>
    extension<T>(IAsyncEnumerable<T> source)
    {
        /// <summary>
        /// Sends each value as an event of <paramref name="eventType"/>, a <see cref="HeartbeatEventType"/> event whenever
        /// nothing was sent for <paramref name="heartbeatInterval"/>, and ends at <paramref name="closeAt"/> (e.g. when the
        /// access token expires; the client reconnects with a fresh one) or when <paramref name="source"/> ends.
        /// </summary>
        /// <param name="eventType">The event type of a value.</param>
        /// <param name="heartbeatInterval">The longest silence before a heartbeat.</param>
        /// <param name="closeAt">When the stream ends at the latest; a past time ends it at once.</param>
        /// <param name="timeProvider">The clock for heartbeats and <paramref name="closeAt"/>.</param>
        /// <param name="cancellationToken">The request's cancellation (the client went away).</param>
        /// <returns>The events.</returns>
        public IAsyncEnumerable<SseItem<T?>> ToServerSentEvents(
            string eventType,
            TimeSpan heartbeatInterval,
            DateTimeOffset closeAt,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
            StreamAsync(source, new StreamSettings(eventType, heartbeatInterval, closeAt, timeProvider), cancellationToken);
    }

    private static async IAsyncEnumerable<SseItem<T?>> StreamAsync<T>(
        IAsyncEnumerable<T> source,
        StreamSettings settings,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var remaining = settings.CloseAt - settings.TimeProvider.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            yield break;
        }

        using var deadline = new CancellationTokenSource(remaining, settings.TimeProvider);
        using var closing = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var values = source.GetAsyncEnumerator(closing.Token);
        var next = values.MoveNextAsync().AsTask();
        try
        {
            while (await WaitAsync(next, settings, closing.Token, cancellationToken) is var step && step != Step.End)
            {
                if (step == Step.Heartbeat)
                {
                    yield return new SseItem<T?>(default, HeartbeatEventType);
                    continue;
                }

                yield return new SseItem<T?>(values.Current, settings.EventType);
                next = values.MoveNextAsync().AsTask();
            }
        }
        finally
        {
            // An async iterator cannot be disposed while a MoveNextAsync is pending; closing cancels it, so wait for it.
            await closing.CancelAsync();
            try
            {
                await DrainAsync(next);
            }
            finally
            {
                await values.DisposeAsync();
            }
        }
    }

    // What to do next: send the value that arrived, send a heartbeat after a silent interval, or end the stream.
    private static async Task<Step> WaitAsync(Task<bool> next, StreamSettings settings, CancellationToken closing, CancellationToken aborted)
    {
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(closing);
        var heartbeat = Task.Delay(settings.HeartbeatInterval, settings.TimeProvider, idle.Token);
        var first = await Task.WhenAny(next, heartbeat);
        await idle.CancelAsync();

        if (first != next)
        {
            return closing.IsCancellationRequested ? Step.End : Step.Heartbeat;
        }

        try
        {
            return await next ? Step.Value : Step.End;
        }
        catch (OperationCanceledException) when (!aborted.IsCancellationRequested)
        {
            return Step.End; // closeAt was reached; only an abort by the client is left to propagate
        }
    }

    private static async Task DrainAsync(Task<bool> next)
    {
        try
        {
            await next;
        }
        catch (OperationCanceledException)
        {
            // Expected: the stream was closed or the client went away.
        }
    }

    private enum Step
    {
        Value,
        Heartbeat,
        End,
    }

    private sealed record StreamSettings(string EventType, TimeSpan HeartbeatInterval, DateTimeOffset CloseAt, TimeProvider TimeProvider);
}

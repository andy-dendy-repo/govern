using System.Collections.Concurrent;
using System.Net;
using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.ClientTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.ClientTests.Features.Games.Services;

public class GameWatchServiceTests : ClientTestContext
{
    private readonly ConcurrentQueue<GameWatchUpdate> _updates = new();

    public GameWatchServiceTests() => SignIn();

    private GameWatchStatus[] Statuses => [.. _updates.Select(static update => update.Status)];

    private int StreamRequests => Api.RequestsTo(TestGames.StreamPath()).Count;

    [Fact]
    public async Task WatchAsync_Snapshot_YieldsConnectingThenLive()
    {
        var stream = Stream();
        await stream.WriteGameAsync(TestGames.CreatorLobby());

        _ = Watch(Ct);

        await Eventually.HoldsAsync(() => Statuses.Length == 2, "the snapshot arrives");
        Statuses.ShouldBe([GameWatchStatus.Connecting, GameWatchStatus.Live]);
        _updates.Last().Game.ShouldNotBeNull().Id.ShouldBe(TestGames.Id);
    }

    [Fact]
    public async Task WatchAsync_HealthyConnectionClosed_ReconnectsAfterTwoToFiveSeconds()
    {
        var first = Stream();
        Stream();
        await first.WriteGameAsync(TestGames.CreatorLobby());
        _ = Watch(Ct);
        await WaitForAsync(GameWatchStatus.Live);

        Time.Advance(TimeSpan.FromSeconds(10));
        first.Complete();
        var retryIn = await WaitForRetryAsync();

        retryIn.ShouldBeInRange(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5));
        Time.Advance(TimeSpan.FromSeconds(1.9));
        await Eventually.NeverAsync(() => StreamRequests > 1, "no reconnect within 2 s");
        Time.Advance(TimeSpan.FromSeconds(3.1));
        await Eventually.HoldsAsync(() => StreamRequests == 2, "the stream reopens within 5 s");
    }

    [Fact]
    public async Task WatchAsync_RepeatedFailures_GrowTheDelay()
    {
        Api.On(HttpMethod.Get, TestGames.StreamPath(), () => FakeApiHandler.Empty(HttpStatusCode.InternalServerError));
        _ = Watch(Ct);

        var delays = new List<TimeSpan>();
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            delays.Add(await WaitForRetryAsync(attempt));
            Time.Advance(delays[^1]);
            await Eventually.HoldsAsync(() => StreamRequests == attempt + 1, $"attempt {attempt + 1} is sent");
        }

        delays[0].ShouldBeInRange(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3));
        delays[1].ShouldBeInRange(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5));
        delays[2].ShouldBeInRange(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(9));
    }

    [Fact]
    public async Task WatchAsync_ConnectionLivedTenSeconds_ResetsTheFailureCount()
    {
        Api.On(HttpMethod.Get, TestGames.StreamPath(), () => FakeApiHandler.Empty(HttpStatusCode.InternalServerError));
        Api.On(HttpMethod.Get, TestGames.StreamPath(), () => FakeApiHandler.Empty(HttpStatusCode.InternalServerError));
        var healthy = Stream();
        await healthy.WriteGameAsync(TestGames.CreatorLobby());
        _ = Watch(Ct);
        Time.Advance(await WaitForRetryAsync(1));
        Time.Advance(await WaitForRetryAsync(2));
        await WaitForAsync(GameWatchStatus.Live);

        Time.Advance(TimeSpan.FromSeconds(10));
        healthy.Fail();

        (await WaitForRetryAsync(3)).ShouldBeInRange(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task WatchAsync_SlowAttemptWithoutSnapshot_KeepsBackingOff()
    {
        Api.On(HttpMethod.Get, TestGames.StreamPath(), () => FakeApiHandler.Empty(HttpStatusCode.InternalServerError));
        Api.On(HttpMethod.Get, TestGames.StreamPath(), () => FakeApiHandler.Empty(HttpStatusCode.InternalServerError));
        var silent = Stream();
        _ = Watch(Ct);
        Time.Advance(await WaitForRetryAsync(1));
        Time.Advance(await WaitForRetryAsync(2));
        await Eventually.HoldsAsync(() => StreamRequests == 3, "the third attempt opens");

        Time.Advance(TimeSpan.FromSeconds(11));
        silent.Fail();

        (await WaitForRetryAsync(3)).ShouldBeInRange(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(9));
    }

    [Fact]
    public async Task WatchAsync_RateLimited_WaitsAtLeastFifteenSeconds()
    {
        Api.On(HttpMethod.Get, TestGames.StreamPath(), () => FakeApiHandler.Empty(HttpStatusCode.TooManyRequests));

        _ = Watch(Ct);

        (await WaitForRetryAsync()).ShouldBeGreaterThanOrEqualTo(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task WatchAsync_SilentForFiftySeconds_Reconnects()
    {
        var stream = Stream();
        Stream();
        await stream.WriteGameAsync(TestGames.CreatorLobby());
        _ = Watch(Ct);
        await WaitForAsync(GameWatchStatus.Live);

        Time.Advance(TimeSpan.FromSeconds(49));
        await Eventually.NeverAsync(() => Statuses.Contains(GameWatchStatus.Reconnecting), "the connection is still within its silence limit");
        Time.Advance(TimeSpan.FromSeconds(1));

        (await WaitForRetryAsync()).ShouldBeInRange(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task WatchAsync_Heartbeats_KeepTheConnection()
    {
        var stream = Stream();
        await stream.WriteGameAsync(TestGames.CreatorLobby());
        _ = Watch(Ct);
        await WaitForAsync(GameWatchStatus.Live);

        Time.Advance(TimeSpan.FromSeconds(40));
        await stream.WriteHeartbeatAsync();
        await stream.WriteGameAsync(TestGames.CreatorLobby(players: 2));
        await Eventually.HoldsAsync(() => Statuses.Count(static status => status == GameWatchStatus.Live) == 2, "the second snapshot arrives");
        Time.Advance(TimeSpan.FromSeconds(40));

        await Eventually.NeverAsync(() => Statuses.Contains(GameWatchStatus.Reconnecting), "events reset the silence limit");
    }

    [Fact]
    public async Task WatchAsync_NotFound_EndsGoneWithoutRetrying()
    {
        Api.On(HttpMethod.Get, TestGames.StreamPath(), () => FakeApiHandler.Empty(HttpStatusCode.NotFound));

        await Watch(Ct);

        Statuses.ShouldBe([GameWatchStatus.Connecting, GameWatchStatus.Gone]);
        StreamRequests.ShouldBe(1);
    }

    [Fact]
    public async Task WatchAsync_FinishedGameClosed_EndsFinishedWithoutReconnecting()
    {
        var stream = Stream();
        await stream.WriteGameAsync(TestGames.Started(GamePhase.Finished));
        stream.Complete();

        await Watch(Ct);

        Statuses.ShouldBe([GameWatchStatus.Connecting, GameWatchStatus.Live, GameWatchStatus.Finished]);
        _updates.Last().Game.ShouldNotBeNull().Phase.ShouldBe(GamePhase.Finished);
        StreamRequests.ShouldBe(1);
    }

    [Fact]
    public async Task WatchAsync_SessionRejected_EndsSignedOut()
    {
        Api.On(HttpMethod.Get, TestGames.StreamPath(), () => FakeApiHandler.Empty(HttpStatusCode.Unauthorized));
        Api.On(HttpMethod.Post, "/api/users/refresh", () => FakeApiHandler.Empty(HttpStatusCode.Unauthorized));

        await Watch(Ct);

        Statuses.ShouldBe([GameWatchStatus.Connecting, GameWatchStatus.SignedOut]);
    }

    [Fact]
    public async Task WatchAsync_Cancelled_StopsReconnecting()
    {
        Api.On(HttpMethod.Get, TestGames.StreamPath(), () => FakeApiHandler.Empty(HttpStatusCode.InternalServerError));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var watching = Watch(cancellation.Token);
        await WaitForRetryAsync();

        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => watching);
        Time.Advance(TimeSpan.FromMinutes(1));
        await Eventually.NeverAsync(() => StreamRequests > 1, "a cancelled watch sends nothing");
    }

    private SseResponse Stream()
    {
        var stream = new SseResponse();
        Api.On(HttpMethod.Get, TestGames.StreamPath(), () => stream.Response);
        return stream;
    }

    // Records each update only once the loop asked for the next one, so any timer it waits on already exists when the test
    // sees the update and advances the clock.
    private Task Watch(CancellationToken cancellationToken) => Task.Run(async () =>
    {
        var watcher = Services.GetRequiredService<GameWatchService>();
        await using var updates = watcher.WatchAsync(TestGames.Id, cancellationToken).GetAsyncEnumerator(cancellationToken);
        var next = updates.MoveNextAsync();
        while (await next)
        {
            var update = updates.Current;
            next = updates.MoveNextAsync();
            _updates.Enqueue(update);
        }
    }, cancellationToken);

    private Task WaitForAsync(GameWatchStatus status) =>
        Eventually.HoldsAsync(() => Statuses.Contains(status), $"the watch reaches {status}");

    private async Task<TimeSpan> WaitForRetryAsync(int count = 1)
    {
        await Eventually.HoldsAsync(
            () => Statuses.Count(static status => status == GameWatchStatus.Reconnecting) >= count, $"reconnect #{count} is scheduled");
        return _updates.Where(static update => update.Status == GameWatchStatus.Reconnecting).ElementAt(count - 1).RetryIn!.Value;
    }
}

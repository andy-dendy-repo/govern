using System.Net;
using Govern.Client.Features.Games.Clients;
using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Models;
using Govern.ClientTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.ClientTests.Features.Games.Clients;

public class GamesClientWatchTests : ClientTestContext
{
    private GamesClient Games => Services.GetRequiredService<GamesClient>();

    public GamesClientWatchTests() => SignIn();

    [Fact]
    public async Task WatchAsync_Stream_YieldsEventsInOrderThenClosed()
    {
        var stream = new SseResponse();
        Api.On(HttpMethod.Get, TestGames.StreamPath(), () => stream.Response);
        await stream.WriteGameAsync(TestGames.CreatorLobby());
        await stream.WriteHeartbeatAsync();
        await stream.WriteRawAsync("something-new", "{}");
        await stream.WriteGameAsync(TestGames.CreatorLobby(players: 2));
        stream.Complete();

        var events = await ReadAllAsync();

        events.Count.ShouldBe(4);
        events[0].ShouldBeOfType<GameSnapshotEvent>().Game.Players.Count.ShouldBe(1);
        events[1].ShouldBeOfType<GameHeartbeatEvent>();
        events[2].ShouldBeOfType<GameSnapshotEvent>().Game.Players.Count.ShouldBe(2);
        events[3].ShouldBe(new GameStreamClosedEvent(GameStreamEnd.Closed));
    }

    [Fact]
    public async Task WatchAsync_Open_AsksForAnEventStreamWithTheBearer()
    {
        var stream = new SseResponse();
        Api.On(HttpMethod.Get, TestGames.StreamPath(), () => stream.Response);
        stream.Complete();

        await ReadAllAsync();

        var request = Api.Requests.Single();
        request.Accept.ShouldBe("text/event-stream");
        request.Authorization.ShouldStartWith("Bearer ");
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, GameStreamEnd.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests, GameStreamEnd.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, GameStreamEnd.Failed)]
    [InlineData(HttpStatusCode.OK, GameStreamEnd.Failed)]
    public async Task WatchAsync_RefusedOrNotAStream_ClosesWithTheReason(HttpStatusCode status, GameStreamEnd reason)
    {
        Api.On(HttpMethod.Get, TestGames.StreamPath(), () => FakeApiHandler.Empty(status));

        var events = await ReadAllAsync();

        events.ShouldBe([new GameStreamClosedEvent(reason)]);
    }

    [Fact]
    public async Task WatchAsync_UnauthorizedAndRefreshRejected_ClosesAsUnauthorized()
    {
        Api.On(HttpMethod.Get, TestGames.StreamPath(), () => FakeApiHandler.Empty(HttpStatusCode.Unauthorized));
        Api.On(HttpMethod.Post, "/api/users/refresh", () => FakeApiHandler.Empty(HttpStatusCode.Unauthorized));

        var events = await ReadAllAsync();

        events.ShouldBe([new GameStreamClosedEvent(GameStreamEnd.Unauthorized)]);
        StoredTokens().ShouldBeNull();
    }

    [Fact]
    public async Task WatchAsync_RefreshUnavailable_ClosesAsFailedAndKeepsTheSession()
    {
        Api.On(HttpMethod.Get, TestGames.StreamPath(), () => FakeApiHandler.Empty(HttpStatusCode.Unauthorized));
        Api.On(HttpMethod.Post, "/api/users/refresh", () => FakeApiHandler.Empty(HttpStatusCode.TooManyRequests));

        var events = await ReadAllAsync();

        events.ShouldBe([new GameStreamClosedEvent(GameStreamEnd.Failed)]);
        StoredTokens().ShouldNotBeNull();
    }

    [Fact]
    public async Task WatchAsync_BadJson_ClosesAsFailed()
    {
        var stream = new SseResponse();
        Api.On(HttpMethod.Get, TestGames.StreamPath(), () => stream.Response);
        await stream.WriteRawAsync("game", "{not json");

        var events = await ReadAllAsync();

        events.ShouldBe([new GameStreamClosedEvent(GameStreamEnd.Failed)]);
    }

    [Fact]
    public async Task WatchAsync_ConnectionReset_ClosesAsFailed()
    {
        var stream = new SseResponse();
        Api.On(HttpMethod.Get, TestGames.StreamPath(), () => stream.Response);
        await stream.WriteGameAsync(TestGames.CreatorLobby());
        stream.Fail();

        var events = await ReadAllAsync();

        events.Last().ShouldBe(new GameStreamClosedEvent(GameStreamEnd.Failed));
    }

    [Fact]
    public async Task WatchAsync_NetworkFailure_ClosesAsFailed()
    {
        Api.On(HttpMethod.Get, TestGames.StreamPath(), () => throw new HttpRequestException("offline"));

        var events = await ReadAllAsync();

        events.ShouldBe([new GameStreamClosedEvent(GameStreamEnd.Failed)]);
    }

    [Fact]
    public async Task WatchAsync_AccessTokenDue_RefreshesBeforeOpening()
    {
        Time.Advance(TimeSpan.FromMinutes(14));
        Api.On(HttpMethod.Post, "/api/users/refresh", () => FakeApiHandler.Json(TestTokens.Pair("player@example.com", Time.GetUtcNow())));
        var stream = new SseResponse();
        Api.On(HttpMethod.Get, TestGames.StreamPath(), () => stream.Response);
        stream.Complete();

        await ReadAllAsync();

        Api.Requests.Select(static request => request.Path).ShouldBe(["/api/users/refresh", TestGames.StreamPath()]);
    }

    [Fact]
    public async Task WatchAsync_Cancelled_Throws()
    {
        var stream = new SseResponse();
        Api.On(HttpMethod.Get, TestGames.StreamPath(), () => stream.Response);
        await stream.WriteGameAsync(TestGames.CreatorLobby());
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await using var events = Games.WatchAsync(TestGames.Id, cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        (await events.MoveNextAsync()).ShouldBeTrue();

        var next = events.MoveNextAsync().AsTask();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => next);
    }

    private async Task<List<GameStreamEvent>> ReadAllAsync()
    {
        List<GameStreamEvent> events = [];
        await foreach (var item in Games.WatchAsync(TestGames.Id, Ct))
        {
            events.Add(item);
        }

        return events;
    }
}

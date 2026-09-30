using System.Net;
using System.Net.Http.Headers;
using System.Net.ServerSentEvents;
using System.Text.Json;
using Govern.Api.Extensions;
using Govern.Api.Features.Games;
using Govern.Application.Games.Contracts;

namespace Govern.IntegrationTests.Support;

/// <summary>
/// An open <c>GET /api/games/{id}/stream</c> response, read event by event. Every read times out, so a missing event fails the
/// test instead of hanging it.
/// </summary>
public sealed class GameEventsReader : IAsyncDisposable
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpResponseMessage _response;
    private readonly IAsyncEnumerator<SseItem<string>> _events;

    private GameEventsReader(HttpResponseMessage response, Stream body, CancellationToken ct)
    {
        _response = response;
        _events = SseParser.Create(body).EnumerateAsync(ct).GetAsyncEnumerator(ct);
    }

    /// <summary>Sends the stream request and returns the response as soon as its headers arrive (the body stays open).</summary>
    public static async Task<HttpResponseMessage> RequestAsync(HttpClient client, string? accessToken, Guid gameId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/games/{gameId}/stream");
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    /// <summary>Opens a stream that must succeed.</summary>
    public static async Task<GameEventsReader> OpenAsync(HttpClient client, string accessToken, Guid gameId, CancellationToken ct)
    {
        var response = await RequestAsync(client, accessToken, gameId, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/event-stream");
        return new GameEventsReader(response, await response.Content.ReadAsStreamAsync(ct), ct);
    }

    /// <summary>Reads the next event of any type.</summary>
    public Task<SseItem<string>> NextEventAsync() => NextEventAsync(DateTimeOffset.UtcNow + ReadTimeout);

    /// <summary>Reads the next <c>game</c> event, skipping heartbeats (all within one read timeout).</summary>
    public async Task<GetGameResponse> NextGameAsync()
    {
        var deadline = DateTimeOffset.UtcNow + ReadTimeout;
        while (true)
        {
            var item = await NextEventAsync(deadline);
            if (item.EventType != AsyncEnumerableExtensions.HeartbeatEventType)
            {
                item.EventType.ShouldBe(WatchGameEndpoint.GameEventType);
                return JsonSerializer.Deserialize<GetGameResponse>(item.Data, GamesApi.Json)!;
            }
        }
    }

    /// <summary>Waits for the server to end the stream, skipping heartbeats (all within one read timeout).</summary>
    public async Task ShouldEndAsync()
    {
        var deadline = DateTimeOffset.UtcNow + ReadTimeout;
        while (await MoveNextAsync(deadline))
        {
            _events.Current.EventType.ShouldBe(AsyncEnumerableExtensions.HeartbeatEventType);
        }
    }

    private async Task<SseItem<string>> NextEventAsync(DateTimeOffset deadline)
    {
        (await MoveNextAsync(deadline)).ShouldBeTrue("the stream ended");
        return _events.Current;
    }

    private async Task<bool> MoveNextAsync(DateTimeOffset deadline)
    {
        var remaining = deadline - DateTimeOffset.UtcNow;
        return await _events.MoveNextAsync().AsTask().WaitAsync(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
    }

    public async ValueTask DisposeAsync()
    {
        _response.Dispose();
        try
        {
            await _events.DisposeAsync();
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException or ObjectDisposedException)
        {
            // The response was closed under a pending read.
        }
    }
}

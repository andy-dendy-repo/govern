using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Govern.Client.Extensions;
using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Models;
using Govern.Client.Models;
using Govern.Core.Engine;
using Govern.Core.State;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Govern.Client.Features.Games.Clients;

/// <summary>
/// Calls the Api's <c>/api/games</c> endpoints and reads a game's live stream. Every call carries the bearer token through
/// <c>AuthorizationMessageHandler</c>.
/// </summary>
public sealed class GamesClient(HttpClient http)
{
    /// <summary>The largest page <see cref="ListAsync"/> accepts, the same as the Api's.</summary>
    public const int MaxPageSize = 50;

    private const string GameEventType = "game";
    private const string HeartbeatEventType = "heartbeat";
    private const string EventStreamMediaType = "text/event-stream";

    /// <summary>Creates a lobby with the caller in seat 0, for the chosen number of players and played on the chosen map.</summary>
    /// <param name="map">The map choice (<see cref="MapOption.Value"/>).</param>
    /// <param name="seats">How many players the game is for.</param>
    /// <param name="turnMode">How the players take their turns.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>
    /// The game and its invite code, or the error (400 for seats or a map the Api doesn't offer; 409 for a cap, e.g.
    /// <c>game.too_many_open_lobbies</c>; 429).
    /// </returns>
    public Task<ApiResult<CreateGameResponse>> CreateAsync(string map, int seats, TurnMode turnMode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(map);
        var request = new HttpRequestMessage(HttpMethod.Post, "api/games")
        {
            Content = JsonContent.Create(new CreateGameRequest(map, seats, turnMode), options: ApiJson.Options),
        };
        return http.SendForResultAsync<CreateGameResponse>(request, cancellationToken);
    }

    /// <summary>Joins the lobby that uses <paramref name="inviteCode"/>; the code is sent in the body only, never in the URL.</summary>
    /// <param name="inviteCode">The invite code.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The game, or the error (404 <c>game.invite_not_found</c>, 409 <c>game.full</c>, a cap or quota; 429).</returns>
    public Task<ApiResult<JoinGameResponse>> JoinAsync(string inviteCode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inviteCode);
        var request = new HttpRequestMessage(HttpMethod.Post, "api/games/join")
        {
            Content = JsonContent.Create(new JoinGameRequest { InviteCode = inviteCode }, options: ApiJson.Options),
        };
        return http.SendForResultAsync<JoinGameResponse>(request, cancellationToken);
    }

    /// <summary>Leaves a lobby the caller joined (not one they created).</summary>
    /// <param name="gameId">The game.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or the error (409 <c>game.creator_cannot_leave</c> or <c>game.already_started</c>).</returns>
    public Task<ApiResult> LeaveAsync(Guid gameId, CancellationToken cancellationToken) =>
        http.SendForResultAsync(new HttpRequestMessage(HttpMethod.Post, $"api/games/{gameId}/leave"), cancellationToken);

    /// <summary>Cancels (deletes) a lobby the caller created.</summary>
    /// <param name="gameId">The game.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or the error (403 <c>game.not_creator</c>, 409 <c>game.already_started</c>).</returns>
    public Task<ApiResult> CancelAsync(Guid gameId, CancellationToken cancellationToken) =>
        http.SendForResultAsync(new HttpRequestMessage(HttpMethod.Delete, $"api/games/{gameId}"), cancellationToken);

    /// <summary>Starts a full lobby the caller created.</summary>
    /// <param name="gameId">The game.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The started game, or the error (403 <c>game.not_creator</c>, 409 <c>game.not_enough_players</c>).</returns>
    public Task<ApiResult<GetGameResponse>> StartAsync(Guid gameId, CancellationToken cancellationToken) =>
        http.SendForResultAsync<GetGameResponse>(new HttpRequestMessage(HttpMethod.Post, $"api/games/{gameId}/start"), cancellationToken);

    /// <summary>Reads a game the caller plays in.</summary>
    /// <param name="gameId">The game.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The game, or the error (404 <c>game.not_found</c> for any game the caller doesn't play in).</returns>
    public Task<ApiResult<GetGameResponse>> GetAsync(Guid gameId, CancellationToken cancellationToken) =>
        http.SendForResultAsync<GetGameResponse>(new HttpRequestMessage(HttpMethod.Get, $"api/games/{gameId}"), cancellationToken);

    /// <summary>Reads a page of the caller's games, most recent activity first.</summary>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Games per page, 1–<see cref="MaxPageSize"/>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The page, or the error.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> or <paramref name="pageSize"/> is out of range.</exception>
    public Task<ApiResult<ListMyGamesResponse>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, MaxPageSize);
        return http.SendForResultAsync<ListMyGamesResponse>(
            new HttpRequestMessage(HttpMethod.Get, $"api/games?page={page}&pageSize={pageSize}"), cancellationToken);
    }

    /// <summary>Sends one of the caller's commands to a running game.</summary>
    /// <param name="gameId">The game.</param>
    /// <param name="expectedVersion">The <c>view.version</c> the command is based on.</param>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>
    /// The game after the command, or the error: 409 <c>game.stale_version</c> (the view changed first; nothing was applied),
    /// 409 with a rule code (<c>game.not_your_turn</c>, <c>move.unreachable</c>, …), 404 <c>game.not_found</c>, 429.
    /// </returns>
    public Task<ApiResult<GetGameResponse>> SendCommandAsync(
        Guid gameId, long expectedVersion, GameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var request = new HttpRequestMessage(HttpMethod.Post, $"api/games/{gameId}/commands")
        {
            Content = JsonContent.Create(new SendGameCommandRequest(expectedVersion, command), options: ApiJson.Options),
        };
        return http.SendForResultAsync<GetGameResponse>(request, cancellationToken);
    }

    /// <summary>
    /// Opens the game's live stream and reads it: a <see cref="GameSnapshotEvent"/> first (the current state), then one per
    /// visible change, with <see cref="GameHeartbeatEvent"/>s in quiet periods. Unless cancelled, the stream always ends with
    /// one <see cref="GameStreamClosedEvent"/> saying why; it never throws for a failed connection.
    /// </summary>
    /// <param name="gameId">The game.</param>
    /// <param name="cancellationToken">Closes the connection; the enumeration then throws <see cref="OperationCanceledException"/>.</param>
    /// <returns>The events.</returns>
    public async IAsyncEnumerable<GameStreamEvent> WatchAsync(Guid gameId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var (response, body, end) = await OpenAsync(gameId, cancellationToken);
        if (response is null || body is null)
        {
            yield return new GameStreamClosedEvent(end);
            yield break;
        }

        using (response)
        await using (body)
        {
            var events = SseParser.Create(body, ParseEvent).EnumerateAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
            await using (events)
            {
                while (true)
                {
                    var (next, reason) = await NextAsync(events, cancellationToken);
                    if (reason is { } stop)
                    {
                        yield return new GameStreamClosedEvent(stop);
                        yield break;
                    }

                    if (next is not null)
                    {
                        yield return next;
                    }
                }
            }
        }
    }

    private async Task<(HttpResponseMessage? Response, Stream? Body, GameStreamEnd End)> OpenAsync(Guid gameId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/games/{gameId}/stream");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(EventStreamMediaType));
        request.SetBrowserResponseStreamingEnabled(true);

        HttpResponseMessage? response = null;
        try
        {
            response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode == HttpStatusCode.OK && response.Content.Headers.ContentType?.MediaType == EventStreamMediaType)
            {
                return (response, await response.Content.ReadAsStreamAsync(cancellationToken), GameStreamEnd.Closed);
            }

            var end = EndFor(response.StatusCode);
            response.Dispose();
            return (null, null, end);
        }
        catch (Exception exception) when (HttpClientExtensions.IsNetworkFailure(exception, cancellationToken))
        {
            response?.Dispose();
            return (null, null, GameStreamEnd.Failed);
        }
        catch (Exception exception) when (IsCancelledFailure(exception, cancellationToken))
        {
            response?.Dispose();
            throw Cancelled(exception, cancellationToken);
        }
        catch
        {
            response?.Dispose();
            throw;
        }
    }

    // One event, or the reason the stream ended. An event of an unknown type comes back as (null, null) and is skipped.
    private static async Task<(GameStreamEvent? Event, GameStreamEnd? End)> NextAsync(
        IAsyncEnumerator<SseItem<GameStreamEvent?>> events, CancellationToken cancellationToken)
    {
        try
        {
            return await events.MoveNextAsync() ? (events.Current.Data, null) : (null, GameStreamEnd.Closed);
        }
        catch (Exception exception) when (HttpClientExtensions.IsNetworkFailure(exception, cancellationToken))
        {
            return (null, GameStreamEnd.Failed);
        }
        catch (Exception exception) when (IsCancelledFailure(exception, cancellationToken))
        {
            throw Cancelled(exception, cancellationToken);
        }
    }

    // A read that fails while the caller cancels (e.g. the connection torn down under it) counts as the cancellation, so a
    // cancelled watch only ever throws OperationCanceledException.
    private static bool IsCancelledFailure(Exception exception, CancellationToken cancellationToken) =>
        cancellationToken.IsCancellationRequested && exception is not OperationCanceledException;

    private static OperationCanceledException Cancelled(Exception exception, CancellationToken cancellationToken) =>
        new("The game stream was cancelled.", exception, cancellationToken);

    private static GameStreamEvent? ParseEvent(string eventType, ReadOnlySpan<byte> data) => eventType switch
    {
        GameEventType => new GameSnapshotEvent(
            JsonSerializer.Deserialize<GetGameResponse>(data, ApiJson.Options) ?? throw new JsonException("A game event without a game.")),
        HeartbeatEventType => new GameHeartbeatEvent(),
        _ => null,
    };

    private static GameStreamEnd EndFor(HttpStatusCode status) => status switch
    {
        HttpStatusCode.NotFound => GameStreamEnd.NotFound,
        HttpStatusCode.Unauthorized => GameStreamEnd.Unauthorized,
        HttpStatusCode.TooManyRequests => GameStreamEnd.RateLimited,
        _ => GameStreamEnd.Failed,
    };
}

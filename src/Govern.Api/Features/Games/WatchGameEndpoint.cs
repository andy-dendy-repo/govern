using System.Security.Claims;
using Govern.Api.Extensions;
using Govern.Api.Models;
using Govern.Api.Options;
using Govern.Application.Games.Contracts;
using Govern.Utils.Mediator.Abstractions;
using Microsoft.Extensions.Options;

namespace Govern.Api.Features.Games;

/// <summary>
/// <c>GET /api/games/{id}/stream</c>: server-sent events with the caller's snapshot of a game, pushed whenever what they see
/// changes.
/// </summary>
public static class WatchGameEndpoint
{
    /// <summary>The event type of a game snapshot; its data is the same JSON as <c>GET /api/games/{id}</c>.</summary>
    public const string GameEventType = "game";

    /// <summary>Maps the endpoint.</summary>
    /// <param name="app">The route builder.</param>
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/games/{id:guid}/stream", HandleAsync)
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitingPolicies.GameStreams)
            .WithName(nameof(WatchGameEndpoint))
            .WithTags("Games")
            .Produces<GetGameResponse>(StatusCodes.Status200OK, "text/event-stream")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    /// <summary>
    /// Streams the game as the caller sees it: a <c>game</c> event with the current snapshot, then one per visible change, and a
    /// <c>heartbeat</c> event (data <c>null</c>) after each silent interval. The stream ends after the finished snapshot, and
    /// when the access token expires (reconnect with a fresh token; the first event is always the current snapshot).
    /// </summary>
    /// <param name="id">Game id.</param>
    /// <param name="user">The signed-in user (for the token's expiry).</param>
    /// <param name="mediator">The mediator.</param>
    /// <param name="options">Stream settings (heartbeat interval).</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="cancellationToken">Request cancellation; ends the stream when the client disconnects.</param>
    /// <returns>The event stream, or a problem.</returns>
    /// <response code="200">An open <c>text/event-stream</c> of <c>game</c> and <c>heartbeat</c> events.</response>
    /// <response code="400">The game id is empty.</response>
    /// <response code="401">Missing or invalid access token.</response>
    /// <response code="404">No such game, or the caller does not play in it.</response>
    /// <response code="429">Too many streams open or opened recently by the caller.</response>
    private static async Task<IResult> HandleAsync(
        Guid id,
        ClaimsPrincipal user,
        IMediatorService mediator,
        IOptions<GameStreamOptions> options,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var result = await mediator.SendAsync(new WatchGameRequest(id), cancellationToken);
        return result.ToHttpResult(response => TypedResults.ServerSentEvents(response.Snapshots.ToServerSentEvents(
            GameEventType,
            options.Value.HeartbeatInterval,
            user.AccessTokenExpiresAt ?? timeProvider.GetUtcNow(),
            timeProvider,
            cancellationToken)));
    }
}

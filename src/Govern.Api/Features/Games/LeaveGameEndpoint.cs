using Govern.Api.Extensions;
using Govern.Api.Models;
using Govern.Application.Games.Contracts;
using Govern.Utils.Mediator.Abstractions;

namespace Govern.Api.Features.Games;

/// <summary>
/// <c>POST /api/games/{id}/leave</c>: a player who joined a lobby gives up their seat.
/// </summary>
public static class LeaveGameEndpoint
{
    /// <summary>Maps the endpoint.</summary>
    /// <param name="app">The route builder.</param>
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/games/{id:guid}/leave", HandleAsync)
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitingPolicies.Games)
            .WithName(nameof(LeaveGameEndpoint))
            .WithTags("Games")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    /// <summary>Leaves a lobby the caller joined; they may join it again with the invite code.</summary>
    /// <param name="id">Game id.</param>
    /// <param name="mediator">The mediator.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>No content.</returns>
    /// <response code="204">Left; the seat is free again.</response>
    /// <response code="400">The game id is empty.</response>
    /// <response code="401">Missing or invalid access token.</response>
    /// <response code="404">No such game, or the caller does not play in it.</response>
    /// <response code="409">
    /// The caller created the lobby (cancel it instead), the game has already started (resign instead), or it changed while
    /// leaving (<c>game.stale_version</c>: reload and try again).
    /// </response>
    /// <response code="429">Too many game requests from this user.</response>
    private static async Task<IResult> HandleAsync(Guid id, IMediatorService mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.SendAsync(new LeaveGameRequest(id), cancellationToken);
        return result.ToHttpResult(static _ => TypedResults.NoContent());
    }
}

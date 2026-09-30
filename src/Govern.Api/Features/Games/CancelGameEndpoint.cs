using Govern.Api.Extensions;
using Govern.Api.Models;
using Govern.Application.Games.Contracts;
using Govern.Utils.Mediator.Abstractions;

namespace Govern.Api.Features.Games;

/// <summary>
/// <c>DELETE /api/games/{id}</c>: the creator cancels a lobby, which deletes it.
/// </summary>
public static class CancelGameEndpoint
{
    /// <summary>Maps the endpoint.</summary>
    /// <param name="app">The route builder.</param>
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/games/{id:guid}", HandleAsync)
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitingPolicies.Games)
            .WithName(nameof(CancelGameEndpoint))
            .WithTags("Games")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    /// <summary>Cancels a lobby: the game and its invite code are deleted, and both seats are freed.</summary>
    /// <param name="id">Game id.</param>
    /// <param name="mediator">The mediator.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>No content.</returns>
    /// <response code="204">Cancelled and deleted.</response>
    /// <response code="400">The game id is empty.</response>
    /// <response code="401">Missing or invalid access token.</response>
    /// <response code="403">The caller plays in the game but did not create it.</response>
    /// <response code="404">No such game, or the caller does not play in it.</response>
    /// <response code="409">
    /// The game has already started (resign instead), or it changed while cancelling (<c>game.stale_version</c>: reload and
    /// try again).
    /// </response>
    /// <response code="429">Too many game requests from this user.</response>
    private static async Task<IResult> HandleAsync(Guid id, IMediatorService mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.SendAsync(new CancelGameRequest(id), cancellationToken);
        return result.ToHttpResult(static _ => TypedResults.NoContent());
    }
}

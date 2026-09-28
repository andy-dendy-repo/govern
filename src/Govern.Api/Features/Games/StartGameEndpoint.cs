using Govern.Api.Extensions;
using Govern.Api.Models;
using Govern.Application.Games.Contracts;
using Govern.Utils.Mediator.Abstractions;

namespace Govern.Api.Features.Games;

/// <summary>
/// <c>POST /api/games/{id}/start</c>: the creator starts a full lobby.
/// </summary>
public static class StartGameEndpoint
{
    /// <summary>Maps the endpoint.</summary>
    /// <param name="app">The route builder.</param>
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/games/{id:guid}/start", HandleAsync)
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitingPolicies.Games)
            .WithName(nameof(StartGameEndpoint))
            .WithTags("Games")
            .Produces<GetGameResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    /// <summary>Starts the game and returns the caller's view of the new board.</summary>
    /// <param name="id">Game id.</param>
    /// <param name="mediator">The mediator.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The started game as the caller sees it.</returns>
    /// <response code="200">Started.</response>
    /// <response code="400">The game id is empty.</response>
    /// <response code="401">Missing or invalid access token.</response>
    /// <response code="403">The caller plays in the game but did not create it.</response>
    /// <response code="404">No such game, or the caller does not play in it.</response>
    /// <response code="409">The game has already started, is waiting for a second player, or changed while starting.</response>
    /// <response code="429">Too many game requests from this user.</response>
    private static async Task<IResult> HandleAsync(Guid id, IMediatorService mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.SendAsync(new StartGameRequest(id), cancellationToken);
        return result.ToHttpResult();
    }
}

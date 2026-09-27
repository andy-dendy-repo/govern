using Govern.Api.Extensions;
using Govern.Application.Games.Contracts;
using Govern.Utils.Mediator.Abstractions;

namespace Govern.Api.Features.Games;

/// <summary>
/// <c>GET /api/games/{id}</c>: a game the caller plays in, with their fog-of-war view once started.
/// </summary>
public static class GetGameEndpoint
{
    /// <summary>Maps the endpoint.</summary>
    /// <param name="app">The route builder.</param>
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/games/{id:guid}", HandleAsync)
            .RequireAuthorization()
            .WithName(nameof(GetGameEndpoint))
            .WithTags("Games")
            .Produces<GetGameResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

    /// <summary>Returns the game as the caller sees it.</summary>
    /// <param name="id">Game id.</param>
    /// <param name="mediator">The mediator.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The game.</returns>
    /// <response code="200">The game.</response>
    /// <response code="400">The game id is empty.</response>
    /// <response code="401">Missing or invalid access token.</response>
    /// <response code="404">No such game, or the caller does not play in it.</response>
    private static async Task<IResult> HandleAsync(Guid id, IMediatorService mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.SendAsync(new GetGameRequest(id), cancellationToken);
        return result.ToHttpResult();
    }
}

using Govern.Api.Extensions;
using Govern.Application.Games.Contracts;
using Govern.Utils.Mediator.Abstractions;

namespace Govern.Api.Features.Games;

/// <summary>
/// <c>POST /api/games/join</c>: takes the free seat of the lobby with the given invite code. The code travels in the body so it
/// never appears in URLs or access logs.
/// </summary>
public static class JoinGameEndpoint
{
    /// <summary>Maps the endpoint.</summary>
    /// <param name="app">The route builder.</param>
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/games/join", HandleAsync)
            .RequireAuthorization()
            .WithName(nameof(JoinGameEndpoint))
            .WithTags("Games")
            .Produces<JoinGameResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

    /// <summary>Joins a lobby by invite code; joining a game you already play in succeeds.</summary>
    /// <param name="request">The invite code.</param>
    /// <param name="mediator">The mediator.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The joined game's id.</returns>
    /// <response code="200">Joined (or already a player).</response>
    /// <response code="400">The invite code is malformed.</response>
    /// <response code="401">Missing or invalid access token.</response>
    /// <response code="404">No game uses this invite code.</response>
    /// <response code="409">The game has started or is full.</response>
    private static async Task<IResult> HandleAsync(JoinGameRequest request, IMediatorService mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.SendAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}

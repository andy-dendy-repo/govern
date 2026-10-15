using Govern.Api.Extensions;
using Govern.Api.Models;
using Govern.Application.Games.Contracts;
using Govern.Utils.Mediator.Abstractions;

namespace Govern.Api.Features.Games;

/// <summary>
/// <c>POST /api/games/{id}/commands</c>: a player sends one command (move, attack, found city, set production, end turn,
/// resign) based on the <c>view.version</c> they last saw.
/// </summary>
public static class SendGameCommandEndpoint
{
    /// <summary>Maps the endpoint.</summary>
    /// <param name="app">The route builder.</param>
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/games/{id:guid}/commands", HandleAsync)
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitingPolicies.GameCommands)
            .WithName(nameof(SendGameCommandEndpoint))
            .WithTags("Games")
            .Accepts<GameCommandBody>("application/json")
            .Produces<GetGameResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    /// <summary>Applies the command and returns the caller's view of the new board.</summary>
    /// <param name="id">Game id.</param>
    /// <param name="body">The expected version and the command, read with the game JSON format.</param>
    /// <param name="mediator">The mediator.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The game as the caller sees it after the command.</returns>
    /// <response code="200">Applied.</response>
    /// <response code="400">Malformed body, unknown command type, empty game id or negative expected version.</response>
    /// <response code="401">Missing or invalid access token.</response>
    /// <response code="404">No such game, or the caller does not play in it.</response>
    /// <response code="409">
    /// <c>game.not_started</c>; <c>game.stale_version</c> (another command came first: reload the game; in a simultaneous game
    /// only when other commands keep winning the race); or an engine rule violation with its own code (<c>game.not_your_turn</c>,
    /// <c>game.turn_ended</c>, <c>move.unreachable</c>, <c>game.finished</c>, …).
    /// </response>
    /// <response code="413">The body is larger than 4 KiB.</response>
    /// <response code="415">The body is not JSON.</response>
    /// <response code="429">Too many commands from this user.</response>
    private static async Task<IResult> HandleAsync(Guid id, GameCommandBody body, IMediatorService mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.SendAsync(new SendGameCommandRequest(id, body.ExpectedVersion, body.Command), cancellationToken);
        return result.ToHttpResult();
    }
}

using Govern.Api.Extensions;
using Govern.Api.Models;
using Govern.Application.Games.Contracts;
using Govern.Utils.Mediator.Abstractions;

namespace Govern.Api.Features.Games;

/// <summary>
/// <c>POST /api/games</c>: creates a lobby with an invite code; the caller takes seat 0.
/// </summary>
public static class CreateGameEndpoint
{
    /// <summary>Maps the endpoint.</summary>
    /// <param name="app">The route builder.</param>
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/games", HandleAsync)
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitingPolicies.Games)
            .WithName(nameof(CreateGameEndpoint))
            .WithTags("Games")
            .Produces<CreateGameResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    /// <summary>Creates a lobby for the caller.</summary>
    /// <param name="mediator">The mediator.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The game id and its invite code.</returns>
    /// <response code="201">Lobby created; <c>Location</c> points to the game.</response>
    /// <response code="401">Missing or invalid access token.</response>
    /// <response code="409">The caller already has the maximum number of open lobbies or unfinished games.</response>
    /// <response code="429">Too many game creates, joins and starts from this user.</response>
    private static async Task<IResult> HandleAsync(IMediatorService mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.SendAsync(new CreateGameRequest(), cancellationToken);
        return result.ToHttpResult(response => TypedResults.Created($"/api/games/{response.Id}", response));
    }
}

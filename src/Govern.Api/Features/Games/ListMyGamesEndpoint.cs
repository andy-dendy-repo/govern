using Govern.Api.Extensions;
using Govern.Application.Games.Contracts;
using Govern.Utils.Mediator.Abstractions;

namespace Govern.Api.Features.Games;

/// <summary>
/// <c>GET /api/games?page=1&amp;pageSize=20</c>: the games the caller plays in, most recently changed first.
/// </summary>
public static class ListMyGamesEndpoint
{
    /// <summary>Maps the endpoint.</summary>
    /// <param name="app">The route builder.</param>
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/games", HandleAsync)
            .RequireAuthorization()
            .WithName(nameof(ListMyGamesEndpoint))
            .WithTags("Games")
            .Produces<ListMyGamesResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

    /// <summary>Lists the caller's games.</summary>
    /// <param name="page">One-based page number; 1 when omitted.</param>
    /// <param name="pageSize">Items per page (1–50); 20 when omitted.</param>
    /// <param name="mediator">The mediator.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>One page of games and the total count.</returns>
    /// <response code="200">The page.</response>
    /// <response code="400">Page or page size out of range.</response>
    /// <response code="401">Missing or invalid access token.</response>
    private static async Task<IResult> HandleAsync(int? page, int? pageSize, IMediatorService mediator, CancellationToken cancellationToken)
    {
        var request = new ListMyGamesRequest(page ?? 1, pageSize ?? ListMyGamesRequest.DefaultPageSize);
        var result = await mediator.SendAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}

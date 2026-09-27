using Govern.Api.Extensions;
using Govern.Application.Users.Contracts;
using Govern.Utils.Mediator.Abstractions;

namespace Govern.Api.Features.Users;

/// <summary>
/// <c>GET /api/users/me</c>: the authenticated caller's account.
/// </summary>
public static class GetCurrentUserEndpoint
{
    /// <summary>Maps the endpoint.</summary>
    /// <param name="app">The route builder.</param>
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/users/me", HandleAsync)
            .RequireAuthorization()
            .WithName(nameof(GetCurrentUserEndpoint))
            .WithTags("Users")
            .Produces<GetCurrentUserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

    /// <summary>Returns the caller's account.</summary>
    /// <param name="mediator">The mediator.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The account.</returns>
    /// <response code="200">The caller's account.</response>
    /// <response code="401">Missing or invalid access token.</response>
    /// <response code="404">The account no longer exists.</response>
    private static async Task<IResult> HandleAsync(IMediatorService mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.SendAsync(new GetCurrentUserRequest(), cancellationToken);
        return result.ToHttpResult();
    }
}

using Govern.Api.Extensions;
using Govern.Application.Users.Contracts;
using Govern.Utils.Mediator.Abstractions;

namespace Govern.Api.Features.Users;

/// <summary>
/// <c>POST /api/users/sign-out-everywhere</c>: revokes all of the caller's refresh tokens (ends every session).
/// </summary>
public static class SignOutEverywhereEndpoint
{
    /// <summary>Maps the endpoint.</summary>
    /// <param name="app">The route builder.</param>
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/users/sign-out-everywhere", HandleAsync)
            .RequireAuthorization()
            .WithName(nameof(SignOutEverywhereEndpoint))
            .WithTags("Users")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

    /// <summary>Revokes every active refresh token of the caller; access tokens stay valid until they expire.</summary>
    /// <param name="mediator">The mediator.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>No content.</returns>
    /// <response code="204">Signed out on all devices.</response>
    /// <response code="401">Missing or invalid access token.</response>
    private static async Task<IResult> HandleAsync(IMediatorService mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.SendAsync(new SignOutEverywhereRequest(), cancellationToken);
        return result.ToHttpResult(static _ => TypedResults.NoContent());
    }
}

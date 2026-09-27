using Govern.Api.Extensions;
using Govern.Application.Users.Contracts;
using Govern.Utils.Mediator.Abstractions;

namespace Govern.Api.Features.Users;

/// <summary>
/// <c>POST /api/users/sign-out</c>: revokes one of the caller's refresh tokens (ends that device's session).
/// </summary>
public static class SignOutEndpoint
{
    /// <summary>Maps the endpoint.</summary>
    /// <param name="app">The route builder.</param>
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/users/sign-out", HandleAsync)
            .RequireAuthorization()
            .WithName(nameof(SignOutEndpoint))
            .WithTags("Users")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

    /// <summary>Revokes the given refresh token; the access token stays valid until it expires.</summary>
    /// <param name="request">The refresh token of the session to end.</param>
    /// <param name="mediator">The mediator.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>No content.</returns>
    /// <response code="204">Signed out (also for unknown or already revoked tokens).</response>
    /// <response code="400">Missing refresh token.</response>
    /// <response code="401">Missing or invalid access token.</response>
    private static async Task<IResult> HandleAsync(SignOutRequest request, IMediatorService mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.SendAsync(request, cancellationToken);
        return result.ToHttpResult(static _ => TypedResults.NoContent());
    }
}

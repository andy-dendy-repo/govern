using Govern.Api.Extensions;
using Govern.Api.Models;
using Govern.Application.Users.Contracts;
using Govern.Utils.Mediator.Abstractions;

namespace Govern.Api.Features.Users;

/// <summary>
/// <c>POST /api/users/refresh</c>: rotates a refresh token into a new token pair (anonymous, rate limited).
/// </summary>
public static class RefreshTokenEndpoint
{
    /// <summary>Maps the endpoint.</summary>
    /// <param name="app">The route builder.</param>
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/users/refresh", HandleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingPolicies.Auth)
            .WithName(nameof(RefreshTokenEndpoint))
            .WithTags("Users")
            .Produces<RefreshTokenResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    /// <summary>Exchanges a refresh token; the presented token stops working.</summary>
    /// <param name="request">The refresh token.</param>
    /// <param name="mediator">The mediator.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>A new access token and refresh token.</returns>
    /// <response code="200">Tokens rotated.</response>
    /// <response code="400">Missing refresh token.</response>
    /// <response code="401">Unknown, expired or revoked refresh token.</response>
    /// <response code="429">Too many attempts from this client.</response>
    private static async Task<IResult> HandleAsync(RefreshTokenRequest request, IMediatorService mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.SendAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}

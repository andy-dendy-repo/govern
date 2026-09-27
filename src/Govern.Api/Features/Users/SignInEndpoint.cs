using Govern.Api.Extensions;
using Govern.Api.Models;
using Govern.Application.Users.Contracts;
using Govern.Utils.Mediator.Abstractions;

namespace Govern.Api.Features.Users;

/// <summary>
/// <c>POST /api/users/sign-in</c>: exchanges email and password for tokens (anonymous, rate limited).
/// </summary>
public static class SignInEndpoint
{
    /// <summary>Maps the endpoint.</summary>
    /// <param name="app">The route builder.</param>
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/users/sign-in", HandleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingPolicies.Auth)
            .WithName(nameof(SignInEndpoint))
            .WithTags("Users")
            .Produces<SignInResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    /// <summary>Signs the user in.</summary>
    /// <param name="request">Email and password.</param>
    /// <param name="mediator">The mediator.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>An access token and a refresh token.</returns>
    /// <response code="200">Signed in.</response>
    /// <response code="400">Missing email or password.</response>
    /// <response code="401">Invalid email or password.</response>
    /// <response code="429">
    /// Too many requests from this client (IPv6 counted per /64), or too many failed sign-ins for this email
    /// (<c>user.too_many_sign_in_attempts</c>).
    /// </response>
    private static async Task<IResult> HandleAsync(SignInRequest request, IMediatorService mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.SendAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}

using Govern.Api.Extensions;
using Govern.Api.Models;
using Govern.Application.Users.Contracts;
using Govern.Utils.Mediator.Abstractions;

namespace Govern.Api.Features.Users;

/// <summary>
/// <c>POST /api/users/sign-up</c>: creates an account (anonymous, rate limited).
/// </summary>
public static class SignUpEndpoint
{
    /// <summary>Maps the endpoint.</summary>
    /// <param name="app">The route builder.</param>
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/users/sign-up", HandleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingPolicies.Auth)
            .WithName(nameof(SignUpEndpoint))
            .WithTags("Users")
            .Produces<SignUpResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    /// <summary>Creates a user account and sends a welcome email.</summary>
    /// <param name="request">Email and password.</param>
    /// <param name="mediator">The mediator.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The created user id.</returns>
    /// <response code="201">Account created.</response>
    /// <response code="400">Invalid email or password.</response>
    /// <response code="409">An account with this email already exists.</response>
    /// <response code="429">Too many attempts from this client.</response>
    private static async Task<IResult> HandleAsync(SignUpRequest request, IMediatorService mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.SendAsync(request, cancellationToken);
        return result.ToHttpResult(response => TypedResults.Created("/api/users/me", response));
    }
}

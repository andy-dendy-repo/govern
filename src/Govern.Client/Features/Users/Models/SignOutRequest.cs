namespace Govern.Client.Features.Users.Models;

/// <summary>
/// The request body for <c>POST /api/users/sign-out</c>: the session to revoke.
/// </summary>
/// <param name="RefreshToken">This session's refresh token.</param>
public sealed record SignOutRequest(string RefreshToken);

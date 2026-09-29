namespace Govern.Client.Features.Users.Models;

/// <summary>
/// The account created by <c>POST /api/users/sign-up</c>.
/// </summary>
/// <param name="UserId">The new user's id.</param>
public sealed record SignUpResponse(Guid UserId);

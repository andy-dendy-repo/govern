namespace Govern.Client.Features.Users.Models;

/// <summary>
/// The signed-in user, from <c>GET /api/users/me</c>.
/// </summary>
/// <param name="Id">The user's id.</param>
/// <param name="Email">The email address.</param>
/// <param name="CreatedAt">When the account was created.</param>
public sealed record GetCurrentUserResponse(Guid Id, string Email, DateTimeOffset CreatedAt);

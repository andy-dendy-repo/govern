using Govern.Application.Common.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Govern.Api.Services;

/// <summary>
/// Reads the caller's user id from the <c>sub</c> claim of the validated access token.
/// </summary>
internal sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    /// <inheritdoc />
    public Guid? UserId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId)
            ? userId
            : null;
}

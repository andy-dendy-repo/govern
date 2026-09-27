using System.Globalization;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Govern.Api.Extensions;

/// <summary>
/// Reads values of the signed-in user's access token.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <param name="user">The authenticated user.</param>
    extension(ClaimsPrincipal user)
    {
        /// <summary>
        /// When the access token expires (its <c>exp</c> claim), or <see langword="null"/> when the claim is missing or invalid.
        /// Token validation requires the claim, so it is always present on an authenticated request.
        /// </summary>
        public DateTimeOffset? AccessTokenExpiresAt =>
            user.FindFirst(JwtRegisteredClaimNames.Exp)?.Value is { } exp
            && long.TryParse(exp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            && seconds <= DateTimeOffset.MaxValue.ToUnixTimeSeconds()
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : null;
    }
}

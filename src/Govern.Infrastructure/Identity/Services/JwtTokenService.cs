using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Govern.Application.Common.Abstractions;
using Govern.Application.Common.Models;
using Govern.Application.Common.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Govern.Infrastructure.Identity.Services;

/// <summary>
/// Issues HS256-signed JWT access tokens and random refresh tokens (64 bytes, base64url) stored as SHA-256 hashes.
/// </summary>
internal sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : ITokenService
{
    private readonly JsonWebTokenHandler _handler = new();

    /// <inheritdoc />
    public AccessToken CreateAccessToken(Guid userId, string email)
    {
        var settings = options.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now + settings.AccessTokenLifetime;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            ]),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }

    /// <inheritdoc />
    public GeneratedRefreshToken CreateRefreshToken()
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(64));
        return new GeneratedRefreshToken(token, HashRefreshToken(token));
    }

    /// <inheritdoc />
    public string HashRefreshToken(string refreshToken)
    {
        ArgumentNullException.ThrowIfNull(refreshToken);
        return Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
    }
}

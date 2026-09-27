using Govern.Domain.EntityBase;

namespace Govern.Domain.Entities;

/// <summary>
/// A refresh token issued to a <see cref="User"/>. Only the SHA-256 hash of the token is stored; the raw value is
/// returned to the client once. Tokens are rotated on use: the used token is revoked and points to its replacement.
/// Created and revoked only through <see cref="User"/>.
/// </summary>
public sealed class RefreshToken : BaseAuditableEntity
{
    private RefreshToken()
    {
    }

    /// <summary>
    /// The owning user.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Base64url SHA-256 hash of the raw token.
    /// </summary>
    public string TokenHash { get; private set; } = string.Empty;

    /// <summary>
    /// When the token stops being accepted.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>
    /// When the token was revoked (rotated, or revoked because of reuse); <see langword="null"/> while usable.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>
    /// The token that replaced this one on rotation, if any.
    /// </summary>
    public Guid? ReplacedByTokenId { get; private set; }

    /// <summary>
    /// Whether the token can still be exchanged at <paramref name="now"/>.
    /// </summary>
    /// <param name="now">The current time.</param>
    /// <returns><see langword="true"/> when not revoked and not expired.</returns>
    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    internal static RefreshToken Create(Guid userId, string tokenHash, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        return new RefreshToken { UserId = userId, TokenHash = tokenHash, ExpiresAt = expiresAt };
    }

    internal void Revoke(DateTimeOffset now, Guid? replacedByTokenId)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now;
        ReplacedByTokenId = replacedByTokenId;
    }
}

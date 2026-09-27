using Govern.Domain.EntityBase;
using Govern.Domain.Events;
using Govern.Domain.ValueObjects;

namespace Govern.Domain.Entities;

/// <summary>
/// A user account identified by email, authenticated by password and able to hold several refresh tokens
/// (one per signed-in device). The aggregate root for <see cref="RefreshToken"/>.
/// </summary>
public sealed class User : BaseAuditableEntity
{
    private readonly List<RefreshToken> _refreshTokens = [];

    private User()
    {
    }

    /// <summary>
    /// The normalised email address; unique across users.
    /// </summary>
    public Email Email { get; private set; } = null!;

    /// <summary>
    /// The password hash produced by the password hashing service; never the password itself.
    /// </summary>
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>
    /// Refresh tokens loaded with the user (queries may load only the active ones).
    /// </summary>
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    /// <summary>
    /// Creates a new account and raises <see cref="UserSignedUpEvent"/>.
    /// </summary>
    /// <param name="email">The email address.</param>
    /// <param name="passwordHash">The already hashed password.</param>
    /// <returns>The new user.</returns>
    public static User SignUp(Email email, string passwordHash)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var user = new User { Email = email, PasswordHash = passwordHash };
        user.AddDomainEvent(new UserSignedUpEvent(user.Id, user.Email));
        return user;
    }

    /// <summary>
    /// Issues a new refresh token (e.g. on sign-in).
    /// </summary>
    /// <param name="tokenHash">Hash of the raw token handed to the client.</param>
    /// <param name="expiresAt">Expiry of the new token.</param>
    /// <returns>The new token.</returns>
    public RefreshToken IssueRefreshToken(string tokenHash, DateTimeOffset expiresAt)
    {
        var token = RefreshToken.Create(Id, tokenHash, expiresAt);
        _refreshTokens.Add(token);
        return token;
    }

    /// <summary>
    /// Exchanges <paramref name="current"/> for a new token and revokes the current one.
    /// </summary>
    /// <param name="current">The token presented by the client; must belong to this user.</param>
    /// <param name="newTokenHash">Hash of the replacement token.</param>
    /// <param name="expiresAt">Expiry of the replacement token.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The replacement token.</returns>
    /// <exception cref="InvalidOperationException">The token belongs to another user.</exception>
    public RefreshToken RotateRefreshToken(RefreshToken current, string newTokenHash, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.UserId != Id)
        {
            throw new InvalidOperationException("The refresh token belongs to another user.");
        }

        var replacement = IssueRefreshToken(newTokenHash, expiresAt);
        current.Revoke(now, replacement.Id);
        return replacement;
    }

    /// <summary>
    /// Revokes every loaded, still active refresh token, e.g. when a revoked token is reused (possible theft).
    /// </summary>
    /// <param name="now">The current time.</param>
    public void RevokeAllRefreshTokens(DateTimeOffset now)
    {
        foreach (var token in _refreshTokens.Where(token => token.IsActive(now)))
        {
            token.Revoke(now, replacedByTokenId: null);
        }
    }
}

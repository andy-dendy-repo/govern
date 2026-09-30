namespace Govern.Client.Auth;

/// <summary>
/// The outcome of <see cref="AuthSession.RefreshAsync"/>.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="AccessToken">The access token to use now; set only when <paramref name="Outcome"/> is <see cref="RefreshOutcome.Refreshed"/>.</param>
public sealed record RefreshResult(RefreshOutcome Outcome, string? AccessToken)
{
    /// <summary>The session ended: the Api rejected the refresh token, or no session was stored.</summary>
    public static RefreshResult SignedOut { get; } = new(RefreshOutcome.SignedOut, null);

    /// <summary>The Api could not refresh right now (rate limit, server or network error); the session is kept.</summary>
    public static RefreshResult Unavailable { get; } = new(RefreshOutcome.Unavailable, null);

    /// <summary>A fresh access token is available.</summary>
    /// <param name="accessToken">The access token.</param>
    /// <returns>The result.</returns>
    public static RefreshResult Refreshed(string accessToken) => new(RefreshOutcome.Refreshed, accessToken);
}

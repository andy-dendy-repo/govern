namespace Govern.Client.Auth;

/// <summary>
/// What a session refresh achieved.
/// </summary>
public enum RefreshOutcome
{
    /// <summary>A fresh access token is stored (by this tab or, just before, by another one).</summary>
    Refreshed,

    /// <summary>The session ended and the tokens were cleared.</summary>
    SignedOut,

    /// <summary>The refresh could not be done now; the tokens are kept so a later call can try again.</summary>
    Unavailable,
}

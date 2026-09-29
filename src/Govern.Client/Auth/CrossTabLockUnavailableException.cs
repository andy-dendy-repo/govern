namespace Govern.Client.Auth;

/// <summary>
/// The cross-tab lock could not be taken (the lock module failed to load, or the browser refused the lock). A refresh then
/// gives up for now instead of risking a second tab presenting the same single-use refresh token.
/// </summary>
public sealed class CrossTabLockUnavailableException : Exception
{
    /// <summary>Creates the exception with a default message.</summary>
    public CrossTabLockUnavailableException()
        : base("The cross-tab lock is unavailable.")
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What failed.</param>
    public CrossTabLockUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What failed.</param>
    /// <param name="innerException">The JavaScript error.</param>
    public CrossTabLockUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

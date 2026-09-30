namespace Govern.Client.Auth;

/// <summary>
/// Guards the <c>returnUrl</c> that login and register navigate to after signing in, so a crafted link cannot send the user
/// to another site (open redirect).
/// </summary>
public static class ReturnUrl
{
    /// <summary>The page to use when the return URL is missing or not local.</summary>
    public const string Home = "/";

    /// <summary>Returns <paramref name="url"/> when it is a local, app-relative path; otherwise <see cref="Home"/>.</summary>
    /// <param name="url">The requested return URL, e.g. <c>/profile</c> or <c>/games?page=2</c>.</param>
    /// <returns>A path starting with a single <c>/</c>, without a scheme, host, backslash or control character.</returns>
    public static string Sanitize(string? url)
    {
        if (string.IsNullOrEmpty(url)
            || url[0] != '/'
            || url.StartsWith("//", StringComparison.Ordinal)
            || url.Contains('\\', StringComparison.Ordinal)
            || url.Any(char.IsControl))
        {
            return Home;
        }

        return url;
    }
}

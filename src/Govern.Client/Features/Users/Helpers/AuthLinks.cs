using Govern.Client.Auth;
using Microsoft.AspNetCore.Components;

namespace Govern.Client.Features.Users.Helpers;

/// <summary>
/// Builds the links between login and register and the page to go to afterwards. The return path travels in the query
/// (<c>returnUrl</c>); the current URL's fragment (e.g. an invite code) is carried as a fragment, so it is never sent to a server.
/// </summary>
public static class AuthLinks
{
    /// <summary>Where to go after signing in: the sanitized return path plus the current fragment.</summary>
    /// <param name="returnPath">The requested return path, or <see langword="null"/>.</param>
    /// <param name="navigation">The navigation manager (for the current fragment).</param>
    /// <returns>A local URL.</returns>
    public static string Destination(string? returnPath, NavigationManager navigation)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        return ReturnUrl.Sanitize(returnPath) + Fragment(navigation);
    }

    /// <summary>A link to <paramref name="page"/> that keeps the return path and the current fragment.</summary>
    /// <param name="page">The relative page, e.g. <c>register</c> or <c>login?registered=true</c>.</param>
    /// <param name="returnPath">The requested return path, or <see langword="null"/>.</param>
    /// <param name="navigation">The navigation manager (for the current fragment).</param>
    /// <returns>The relative link.</returns>
    public static string WithReturn(string page, string? returnPath, NavigationManager navigation)
    {
        ArgumentException.ThrowIfNullOrEmpty(page);
        ArgumentNullException.ThrowIfNull(navigation);

        var query = string.IsNullOrEmpty(returnPath)
            ? string.Empty
            : (page.Contains('?', StringComparison.Ordinal) ? "&" : "?") + "returnUrl=" + Uri.EscapeDataString(ReturnUrl.Sanitize(returnPath));
        return page + query + Fragment(navigation);
    }

    private static string Fragment(NavigationManager navigation) => new Uri(navigation.Uri).Fragment;
}

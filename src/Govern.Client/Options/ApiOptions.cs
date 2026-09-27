namespace Govern.Client.Options;

/// <summary>
/// Location of the Govern Api, bound from the <c>Api</c> section of <c>wwwroot/appsettings*.json</c>.
/// </summary>
/// <remarks>
/// Client configuration is downloaded by every visitor, so this holds public values only (see
/// <c>.claude/rules/security.md</c>). Production values are written by the release workflow.
/// </remarks>
public sealed class ApiOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Api";

    /// <summary>
    /// Absolute base URL of the Api, e.g. <c>https://api.example.com</c>. Must use HTTPS unless it points to a loopback host,
    /// and must not carry credentials, a query or a fragment.
    /// </summary>
    public Uri? BaseUrl { get; set; }

    /// <summary>
    /// Whether <see cref="BaseUrl"/> is an absolute URL that is HTTPS or targets a loopback host (local development),
    /// without user info, query or fragment.
    /// </summary>
    public bool IsValid =>
        BaseUrl is { IsAbsoluteUri: true, UserInfo: "", Query: "", Fragment: "" } url
        && (url.Scheme == Uri.UriSchemeHttps || (url.Scheme == Uri.UriSchemeHttp && url.IsLoopback));
}

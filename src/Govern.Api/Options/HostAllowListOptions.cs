namespace Govern.Api.Options;

/// <summary>
/// Host names the Api answers to, bound from the <c>HostFiltering</c> section. Requests with any other <c>Host</c>
/// header get 400, so a spoofed host can't reach generated links, caches or logs. Empty disables the check, which startup
/// validation allows only in Development (local runs and tests). The built-in <c>AllowedHosts</c> filter stays <c>*</c> because it can't exempt the liveness probe.
/// </summary>
public sealed class HostAllowListOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "HostFiltering";

    /// <summary>
    /// Host names without scheme or port, e.g. <c>govern-prod-api-x.azurewebsites.net</c> or <c>*.example.com</c>
    /// (subdomains only, at least two labels after <c>*.</c>); never <c>*</c>. IP literals are not supported.
    /// </summary>
    public IReadOnlyList<string> AllowedHosts { get; set; } = [];

    /// <summary>
    /// Whether every entry is a plain host name or a <c>*.</c> subdomain wildcard over at least two labels: no bare or
    /// inner <c>*</c>, scheme, port, path, whitespace, empty label or trailing dot.
    /// </summary>
    /// <returns><see langword="true"/> when all entries are valid.</returns>
    public bool HasValidEntries() => AllowedHosts.All(IsValidEntry);

    private static bool IsValidEntry(string? host)
    {
        if (string.IsNullOrWhiteSpace(host) || host.IndexOfAny([':', '/', ' ', '@']) >= 0)
        {
            return false;
        }

        var name = host.StartsWith("*.", StringComparison.Ordinal) ? host[2..] : host;
        var labels = name.Split('.');
        return !name.Contains('*', StringComparison.Ordinal)
            && labels.All(static label => label.Length > 0)
            && (name.Length == host.Length || labels.Length >= 2);
    }
}

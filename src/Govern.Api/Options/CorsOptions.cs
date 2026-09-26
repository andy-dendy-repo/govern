namespace Govern.Api.Options;

/// <summary>
/// Browser origins allowed to call the Api (the Static Web App and local client), bound from the <c>Cors</c> section.
/// </summary>
public sealed class CorsOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Cors";

    /// <summary>Exact origins, e.g. <c>https://app.example.com</c>; never <c>*</c>.</summary>
    public IReadOnlyList<string> AllowedOrigins { get; set; } = [];
}

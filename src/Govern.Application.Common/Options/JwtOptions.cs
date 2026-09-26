using System.ComponentModel.DataAnnotations;

namespace Govern.Application.Common.Options;

/// <summary>
/// Token settings bound from the <c>Jwt</c> configuration section and validated at startup.
/// The signing key comes from user-secrets locally and Key Vault in Azure, never from appsettings.
/// </summary>
public sealed class JwtOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Jwt";

    /// <summary>Token issuer (<c>iss</c>).</summary>
    [Required]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Token audience (<c>aud</c>).</summary>
    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>HMAC-SHA256 signing key; at least 32 characters (256 bits).</summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Access token lifetime (default 15 minutes).</summary>
    [Range(typeof(TimeSpan), "00:01:00", "01:00:00")]
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Refresh token lifetime (default 14 days).</summary>
    [Range(typeof(TimeSpan), "01:00:00", "90.00:00:00")]
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(14);
}

using System.ComponentModel.DataAnnotations;

namespace Govern.Api.Options;

/// <summary>
/// Limits for the <see cref="Models.RateLimitingPolicies.Auth"/> policy, bound from the <c>RateLimiting</c> section.
/// </summary>
public sealed class RateLimitingOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "RateLimiting";

    /// <summary>Requests allowed per client IP (IPv6: per /64) and window (default 10).</summary>
    [Range(1, 10_000)]
    public int AuthPermitLimit { get; set; } = 10;

    /// <summary>Window length (default 1 minute).</summary>
    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan AuthWindow { get; set; } = TimeSpan.FromMinutes(1);
}

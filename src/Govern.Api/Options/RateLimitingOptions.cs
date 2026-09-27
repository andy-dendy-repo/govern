using System.ComponentModel.DataAnnotations;

namespace Govern.Api.Options;

/// <summary>
/// Limits for the <see cref="Models.RateLimitingPolicies"/> policies, bound from the <c>RateLimiting</c> section.
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

    /// <summary>Readiness checks allowed per client IP (IPv6: per /64) and window (default 30).</summary>
    [Range(1, 10_000)]
    public int HealthPermitLimit { get; set; } = 30;

    /// <summary>Readiness window length (default 1 minute).</summary>
    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan HealthWindow { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Game creates, joins and starts allowed per user and window (default 20).</summary>
    [Range(1, 10_000)]
    public int GamesPermitLimit { get; set; } = 20;

    /// <summary>Game window length (default 1 minute).</summary>
    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan GamesWindow { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Game commands allowed per user and window (default 120).</summary>
    [Range(1, 10_000)]
    public int GameCommandsPermitLimit { get; set; } = 120;

    /// <summary>Game command window length (default 1 minute).</summary>
    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan GameCommandsWindow { get; set; } = TimeSpan.FromMinutes(1);
}

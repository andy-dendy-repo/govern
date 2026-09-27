using System.ComponentModel.DataAnnotations;

namespace Govern.Application.Common.Options;

/// <summary>
/// Per-account sign-in throttling, bound from the <c>SignInThrottling</c> section and validated at startup.
/// </summary>
public sealed class SignInThrottlingOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "SignInThrottling";

    /// <summary>
    /// Sign-in attempts allowed per account and window without a success; the next one returns 429 (default 5).
    /// </summary>
    [Range(1, 100)]
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>Window length, starting at the first attempt after a success or an expired window (default 15 minutes).</summary>
    [Range(typeof(TimeSpan), "00:01:00", "01:00:00")]
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Accounts tracked at once (default 100 000, about 100 MB at most). Tracked counters are never evicted early; when
    /// full, new accounts are not tracked (fail open) until entries expire, and the per-IP rate limit still applies.
    /// </summary>
    [Range(50_000, 1_000_000)]
    public int MaxTrackedAccounts { get; set; } = 100_000;
}

namespace Govern.Api.Models;

/// <summary>
/// Rate limiting policy names used with <c>RequireRateLimiting</c>.
/// </summary>
public static class RateLimitingPolicies
{
    /// <summary>
    /// Fixed window per client IP for anonymous credential endpoints (sign-up, sign-in).
    /// </summary>
    public const string Auth = "auth";
}

namespace Govern.Api.Models;

/// <summary>
/// Rate limiting policy names used with <c>RequireRateLimiting</c>.
/// </summary>
public static class RateLimitingPolicies
{
    /// <summary>
    /// Fixed window per client IP shared by the anonymous credential endpoints (sign-up, sign-in, refresh).
    /// </summary>
    public const string Auth = "auth";

    /// <summary>
    /// Fixed window per client IP for <see cref="HealthCheckRoutes.Ready"/>, which queries the database anonymously.
    /// </summary>
    public const string Health = "health";

    /// <summary>
    /// Fixed window per signed-in user shared by the game-creating endpoints (create, join, start).
    /// </summary>
    public const string Games = "games";

    /// <summary>
    /// Fixed window per signed-in user for game commands.
    /// </summary>
    public const string GameCommands = "game-commands";
}

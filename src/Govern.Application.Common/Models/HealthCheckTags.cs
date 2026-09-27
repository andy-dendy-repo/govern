namespace Govern.Application.Common.Models;

/// <summary>
/// Health check tags shared by the layer that registers checks (Infrastructure) and the one that maps the routes (Api).
/// </summary>
public static class HealthCheckTags
{
    /// <summary>Checks run by the readiness endpoint; anything that touches a dependency (database, cache…).</summary>
    public const string Ready = "ready";
}

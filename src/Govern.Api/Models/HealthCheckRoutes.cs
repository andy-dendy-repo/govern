using Govern.Application.Common.Models;

namespace Govern.Api.Models;

/// <summary>
/// Health check routes. Both are anonymous.
/// </summary>
public static class HealthCheckRoutes
{
    /// <summary>
    /// Liveness: runs no checks and touches no dependency. The App Service Health check probes it every minute (on the
    /// default host), so it is cheap and not rate limited. It is also exempt from host filtering as a safeguard in case a
    /// platform probe uses another <c>Host</c> header.
    /// </summary>
    public const string Live = "/health/live";

    /// <summary>
    /// Readiness: runs the checks tagged <see cref="HealthCheckTags.Ready"/> (the database). Rate limited per client IP;
    /// used by the release workflow and for diagnostics.
    /// </summary>
    public const string Ready = "/health/ready";
}

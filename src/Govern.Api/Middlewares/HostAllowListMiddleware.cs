using Govern.Api.Models;
using Govern.Api.Options;
using Microsoft.Extensions.Options;

namespace Govern.Api.Middlewares;

/// <summary>
/// Rejects requests whose <c>Host</c> header is not in <see cref="HostAllowListOptions.AllowedHosts"/> with 400
/// (<c>UseStatusCodePages</c> adds the ProblemDetails body). The App Service front end already routes by host, so this is
/// defence in depth, and it also covers custom domains that are bound but not listed. Only
/// <see cref="HealthCheckRoutes.Live"/> is exempt: it exposes nothing, and a platform probe might use another host. An
/// empty list disables the check, which startup validation allows only in Development.
/// </summary>
internal sealed partial class HostAllowListMiddleware : IMiddleware
{
    private readonly HashSet<string> _exactHosts;
    private readonly string[] _wildcardSuffixes;
    private readonly ILogger<HostAllowListMiddleware> _logger;

    /// <summary>Precomputes the allow-list.</summary>
    /// <param name="options">The allowed hosts.</param>
    /// <param name="logger">Logs rejected hosts at Debug.</param>
    public HostAllowListMiddleware(IOptions<HostAllowListOptions> options, ILogger<HostAllowListMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        var hosts = options.Value.AllowedHosts;
        _exactHosts = new HashSet<string>(hosts.Where(static host => !host.StartsWith("*.", StringComparison.Ordinal)), StringComparer.OrdinalIgnoreCase);
        _wildcardSuffixes = [.. hosts.Where(static host => host.StartsWith("*.", StringComparison.Ordinal)).Select(static host => host[1..])];
        _logger = logger;
    }

    /// <inheritdoc />
    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (_exactHosts.Count == 0 && _wildcardSuffixes.Length == 0
            || context.Request.Path.Equals(HealthCheckRoutes.Live, StringComparison.OrdinalIgnoreCase)
            || IsAllowed(context.Request.Host.Host))
        {
            return next(context);
        }

        Log.HostRejected(_logger, context.Request.Host.Host);
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return Task.CompletedTask;
    }

    private bool IsAllowed(string host) =>
        _exactHosts.Contains(host)
        || _wildcardSuffixes.Any(suffix => host.Length > suffix.Length && host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

    private static partial class Log
    {
        [LoggerMessage(EventId = 3100, Level = LogLevel.Debug, Message = "Rejected request for host {Host}: not in HostFiltering:AllowedHosts")]
        public static partial void HostRejected(ILogger logger, string host);
    }
}

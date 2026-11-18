using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Govern.Api.Extensions;
using Govern.Api.Helpers;
using Govern.Api.Middlewares;
using Govern.Api.Models;
using Govern.Api.Options;
using Govern.Api.Services;
using Govern.Application.Common.Abstractions;
using Govern.Application.Common.Options;
using Govern.Utils.Networking;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Govern.Api;

/// <summary>
/// Registers the HTTP layer: ProblemDetails, OpenAPI (with the Bearer security scheme), JSON (enums as camelCase strings, matching
/// <c>GameSerializer</c>), JWT authentication with a deny-by-default authorization fallback, rate limiting, the game stream settings,
/// CORS and the current-user accessor.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Maximum tolerated clock difference when validating token lifetimes.</summary>
    public static readonly TimeSpan TokenClockSkew = TimeSpan.FromSeconds(30);

    /// <param name="services">The service collection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds all Api services.
        /// </summary>
        /// <param name="configuration">
        /// Application configuration (<c>Cors</c>, <c>RateLimiting</c>, <c>GameStream</c>, <c>HostFiltering</c>, <c>Jwt</c>).
        /// </param>
        /// <returns>The service collection, for chaining.</returns>
        public IServiceCollection AddApi(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            services.AddProblemDetails();
            services.AddOpenApi(static options => options.AddBearerSecurity());
            services.ConfigureHttpJsonOptions(static json => json.SerializerOptions.Converters.Add(
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)));
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddTransient<ExceptionHandlingMiddleware>();

            services.AddJwtAuthentication();
            services.AddRateLimiting(configuration);
            services.AddGameStream(configuration);
            services.AddHostAllowList(configuration);
            services.AddAllowListCors(configuration);

            return services;
        }

        private void AddJwtAuthentication()
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
            services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IOptions<JwtOptions>>(static (bearer, jwt) =>
                {
                    bearer.MapInboundClaims = false;
                    bearer.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidIssuer = jwt.Value.Issuer,
                        ValidAudience = jwt.Value.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.SigningKey)),
                        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                        ClockSkew = TokenClockSkew,
                        NameClaimType = JwtRegisteredClaimNames.Sub,
                    };
                });

            // Deny by default: endpoints without RequireAuthorization/AllowAnonymous still require a signed-in user.
            services.AddAuthorizationBuilder()
                .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        }

        private void AddRateLimiting(IConfiguration configuration)
        {
            services.AddOptions<RateLimitingOptions>().Bind(configuration.GetSection(RateLimitingOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
            services.AddRateLimiter(static limiter =>
            {
                limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                limiter.AddPolicy(RateLimitingPolicies.Auth, static context => PerClientIp(
                    context, RateLimitingPolicies.Auth, static settings => (settings.AuthPermitLimit, settings.AuthWindow)));
                limiter.AddPolicy(RateLimitingPolicies.Health, static context => PerClientIp(
                    context, RateLimitingPolicies.Health, static settings => (settings.HealthPermitLimit, settings.HealthWindow)));
                limiter.AddPolicy(RateLimitingPolicies.Games, static context => PerUser(
                    context, RateLimitingPolicies.Games, static settings => (settings.GamesPermitLimit, settings.GamesWindow)));
                limiter.AddPolicy(RateLimitingPolicies.GameCommands, static context => PerUser(
                    context, RateLimitingPolicies.GameCommands, static settings => (settings.GameCommandsPermitLimit, settings.GameCommandsWindow)));
                limiter.AddPolicy(RateLimitingPolicies.GameStreams, static context => GameStreamsPerUser(context));
            });
        }

        private void AddGameStream(IConfiguration configuration) =>
            services.AddOptions<GameStreamOptions>().Bind(configuration.GetSection(GameStreamOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();

        private void AddHostAllowList(IConfiguration configuration)
        {
            services.AddOptions<HostAllowListOptions>()
                .Bind(configuration.GetSection(HostAllowListOptions.SectionName))
                .Validate(static options => options.HasValidEntries(), "HostFiltering:AllowedHosts entries must be host names or '*.' wildcards over two or more labels, without '*', scheme, port or path.")
                .Validate<IHostEnvironment>(static (options, environment) => environment.IsDevelopment() || options.AllowedHosts.Count > 0, "HostFiltering:AllowedHosts is required outside Development (Bicep sets it).")
                .ValidateOnStart();
            services.AddTransient<HostAllowListMiddleware>();
        }

        private void AddAllowListCors(IConfiguration configuration)
        {
            var origins = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()?.AllowedOrigins ?? [];
            if (origins.Contains("*"))
            {
                throw new InvalidOperationException("Cors:AllowedOrigins must list exact origins; '*' is not allowed.");
            }

            services.AddCors(cors => cors.AddDefaultPolicy(policy => policy.WithOrigins([.. origins]).AllowAnyHeader().AllowAnyMethod()));
        }
    }

    // Fixed window per client IP (IPv6 per /64); options are read per request so tests can override them. The limiters come
    // from IRateLimiterService, which counts in Redis when it is configured, so the limits hold across Api instances.
    private static RateLimitPartition<string> PerClientIp(
        HttpContext context, string policy, Func<RateLimitingOptions, (int PermitLimit, TimeSpan Window)> limits) =>
        FixedWindow(context, policy, IpPartitionKey.From(context.Connection.RemoteIpAddress), limits);

    // Per user: at most N open streams (the lease is held while the stream is open), then a fixed window on opening them.
    // Concurrency comes first, so a stream the window rejects gives its concurrency permit back at once. A stream ends at the
    // access token's expiry, so a shared open-stream lease never needs to outlive one token lifetime (plus clock skew).
    private static RateLimitPartition<string> GameStreamsPerUser(HttpContext context)
    {
        var settings = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
        var leaseLifetime = context.RequestServices.GetRequiredService<IOptions<JwtOptions>>().Value.AccessTokenLifetime + TimeSpan.FromMinutes(1);
        var limiters = context.RequestServices.GetRequiredService<IRateLimiterService>();
        var partition = UserPartitionKey(context);
        return RateLimitPartition.Get(partition, _ => new OwningChainedRateLimiter(
            limiters.CreateConcurrency($"{RateLimitingPolicies.GameStreams}:open:{partition}", settings.GameStreamsConcurrentLimit, leaseLifetime),
            limiters.CreateFixedWindow($"{RateLimitingPolicies.GameStreams}:window:{partition}", settings.GameStreamsPermitLimit, settings.GameStreamsWindow)));
    }

    // Fixed window per signed-in user (the JWT subject).
    private static RateLimitPartition<string> PerUser(
        HttpContext context, string policy, Func<RateLimitingOptions, (int PermitLimit, TimeSpan Window)> limits) =>
        FixedWindow(context, policy, UserPartitionKey(context), limits);

    private static RateLimitPartition<string> FixedWindow(
        HttpContext context, string policy, string partition, Func<RateLimitingOptions, (int PermitLimit, TimeSpan Window)> limits)
    {
        var (permitLimit, window) = limits(context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value);
        var limiters = context.RequestServices.GetRequiredService<IRateLimiterService>();
        return RateLimitPartition.Get(partition, _ => limiters.CreateFixedWindow($"{policy}:{partition}", permitLimit, window));
    }

    // The signed-in user (JWT subject) for per-user policies. The limiter runs after authorization, so a protected endpoint
    // never gets here without a user; the client-IP key only guards against a per-user policy on an anonymous endpoint.
    private static string UserPartitionKey(HttpContext context) =>
        context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value is { Length: > 0 } userId
            ? $"user:{userId}"
            : IpPartitionKey.From(context.Connection.RemoteIpAddress);
}

using System.Text;
using System.Threading.RateLimiting;
using Govern.Api.Extensions;
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
/// Registers the HTTP layer: ProblemDetails, OpenAPI (with the Bearer security scheme), JWT authentication with a deny-by-default authorization
/// fallback, rate limiting, CORS and the current-user accessor.
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
        /// Application configuration (<c>Cors</c>, <c>RateLimiting</c>, <c>HostFiltering</c>, <c>Jwt</c>).
        /// </param>
        /// <returns>The service collection, for chaining.</returns>
        public IServiceCollection AddApi(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            services.AddProblemDetails();
            services.AddOpenApi(static options => options.AddBearerSecurity());
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddTransient<ExceptionHandlingMiddleware>();

            services.AddJwtAuthentication();
            services.AddIpRateLimiting(configuration);
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

        private void AddIpRateLimiting(IConfiguration configuration)
        {
            services.AddOptions<RateLimitingOptions>().Bind(configuration.GetSection(RateLimitingOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
            services.AddRateLimiter(static limiter =>
            {
                limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                limiter.AddPolicy(RateLimitingPolicies.Auth, static context => PerClientIp(
                    context, static settings => new FixedWindowRateLimiterOptions { PermitLimit = settings.AuthPermitLimit, Window = settings.AuthWindow }));
                limiter.AddPolicy(RateLimitingPolicies.Health, static context => PerClientIp(
                    context, static settings => new FixedWindowRateLimiterOptions { PermitLimit = settings.HealthPermitLimit, Window = settings.HealthWindow }));
            });
        }

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

    // Fixed window per client IP (IPv6 per /64); options are read per request so tests can override them.
    private static RateLimitPartition<string> PerClientIp(HttpContext context, Func<RateLimitingOptions, FixedWindowRateLimiterOptions> limits)
    {
        var settings = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
        return RateLimitPartition.GetFixedWindowLimiter(IpPartitionKey.From(context.Connection.RemoteIpAddress), _ => limits(settings));
    }
}

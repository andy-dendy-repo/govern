using System.Text;
using System.Threading.RateLimiting;
using Govern.Api.Middlewares;
using Govern.Api.Models;
using Govern.Api.Options;
using Govern.Api.Services;
using Govern.Application.Common.Abstractions;
using Govern.Application.Common.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Govern.Api;

/// <summary>
/// Registers the HTTP layer: ProblemDetails, OpenAPI, JWT authentication with a deny-by-default authorization
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
        /// <param name="configuration">Application configuration (<c>Cors</c>, <c>RateLimiting</c>, <c>Jwt</c>).</param>
        /// <returns>The service collection, for chaining.</returns>
        public IServiceCollection AddApi(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            services.AddProblemDetails();
            services.AddOpenApi();
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddTransient<ExceptionHandlingMiddleware>();

            services.AddJwtAuthentication();
            services.AddAuthRateLimiting(configuration);
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

        private void AddAuthRateLimiting(IConfiguration configuration)
        {
            services.AddOptions<RateLimitingOptions>().Bind(configuration.GetSection(RateLimitingOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
            services.AddRateLimiter(limiter =>
            {
                limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                limiter.AddPolicy(RateLimitingPolicies.Auth, context =>
                {
                    var settings = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
                    return RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = settings.AuthPermitLimit, Window = settings.AuthWindow });
                });
            });
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
}

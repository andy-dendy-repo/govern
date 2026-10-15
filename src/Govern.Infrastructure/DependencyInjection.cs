using Govern.Application.Common.Abstractions;
using Govern.Application.Common.Models;
using Govern.Application.Common.Options;
using Govern.Infrastructure.Background;
using Govern.Infrastructure.Caching.Services;
using Govern.Infrastructure.Emailing.Services;
using Govern.Infrastructure.Identity.Services;
using Govern.Infrastructure.Options;
using Govern.Infrastructure.Persistence;
using Govern.Infrastructure.Persistence.Interceptors;
using Govern.Infrastructure.Persistence.Services;
using Govern.Infrastructure.RateLimiting.Services;
using Govern.Infrastructure.Realtime.Services;
using Govern.Infrastructure.Redis.Helpers;
using Govern.Infrastructure.Redis.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Govern.Infrastructure;

/// <summary>
/// Registers Infrastructure implementations: persistence (PostgreSQL), identity (JWT, password hashing),
/// email (mocked), sign-in throttling, rate limiters and the game change hub (in process, or shared through Redis when
/// <c>ConnectionStrings:Redis</c> is set), the expired-lobby sweep and the health checks.
/// </summary>
public static class DependencyInjection
{
    /// <param name="services">The service collection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds all Infrastructure services.
        /// </summary>
        /// <param name="configuration">
        /// Application configuration (connection strings <c>Govern</c> and optional <c>Redis</c>, <c>Jwt</c>, <c>Database</c>,
        /// <c>SignInThrottling</c>, <c>Redis</c>, <c>LobbyCleanup</c>).
        /// </param>
        /// <returns>The service collection, for chaining.</returns>
        /// <exception cref="InvalidOperationException">The <c>Govern</c> connection string is missing.</exception>
        public IServiceCollection AddInfrastructure(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            services.TryAddSingleton(TimeProvider.System);
            services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
            services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.SectionName));
            services.AddOptions<SignInThrottlingOptions>().Bind(configuration.GetSection(SignInThrottlingOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();

            services.AddPersistence(configuration);

            services.AddSingleton<ITokenService, JwtTokenService>();
            services.AddSingleton<IPasswordHashingService, PasswordHashingService>();
            services.AddSingleton<IEmailService, LoggingEmailService>();
            services.AddSingleton<MemorySignInThrottlingService>();
            services.AddSingleton<InMemoryGameChangeService>();
            services.AddSharedState(configuration);

            services.AddOptions<LobbyCleanupOptions>().Bind(configuration.GetSection(LobbyCleanupOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
            services.AddHostedService<ExpiredLobbyCleanupService>();

            return services;
        }

        // Per-instance state (throttle, rate limits, game changes) stays in process unless Redis is configured.
        private void AddSharedState(IConfiguration configuration)
        {
            var redisConnectionString = configuration.GetConnectionString(RedisOptions.ConnectionStringName);
            if (string.IsNullOrWhiteSpace(redisConnectionString))
            {
                services.AddSingleton<ISignInThrottlingService>(static provider => provider.GetRequiredService<MemorySignInThrottlingService>());
                services.AddSingleton<IRateLimiterService, MemoryRateLimiterService>();
                services.AddSingleton<IGameChangeService>(static provider => provider.GetRequiredService<InMemoryGameChangeService>());
                return;
            }

            // AbortOnConnectFail=false: the Api starts and serves while Redis is down, and reconnects in the background.
            // FailFast: while disconnected, commands fail at once (callers fall back to in-process state) instead of queueing.
            var redisConfiguration = ConfigurationOptions.Parse(redisConnectionString);
            redisConfiguration.AbortOnConnectFail = false;
            redisConfiguration.BacklogPolicy = BacklogPolicy.FailFast;
            ShortenRedisTimeouts(redisConfiguration, redisConnectionString);

            // Production Redis is an external service: its password and the throttle counters must not cross the internet in clear.
            services.AddOptions<RedisOptions>()
                .Bind(configuration.GetSection(RedisOptions.SectionName))
                .ValidateDataAnnotations()
                .Validate<IHostEnvironment>((_, environment) => environment.IsDevelopment() || redisConfiguration.Ssl, "ConnectionStrings:Redis must use TLS (ssl=true) outside Development.")
                .ValidateOnStart();
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConfiguration));

            services.AddSingleton<ISignInThrottlingService, RedisSignInThrottlingService>();
            services.AddSingleton<IRateLimiterService, RedisRateLimiterService>();
            services.AddSingleton<RedisGameChangeService>();
            services.AddSingleton<IGameChangeService>(static provider => provider.GetRequiredService<RedisGameChangeService>());
            services.AddHostedService(static provider => provider.GetRequiredService<RedisGameChangeService>());
            services.AddHealthChecks().AddCheck<RedisHealthCheck>("redis", tags: [HealthCheckTags.Ready]);
        }

        // A Redis call sits in front of every rate-limited request, so on a link that silently drops packets the library's 5 s
        // defaults would add 5 s to each request until the socket is declared dead. Shorter defaults unless the string sets them.
        private static void ShortenRedisTimeouts(ConfigurationOptions redisConfiguration, string connectionString)
        {
            if (!connectionString.Contains("syncTimeout", StringComparison.OrdinalIgnoreCase))
            {
                redisConfiguration.SyncTimeout = 1000;
            }

            if (!connectionString.Contains("asyncTimeout", StringComparison.OrdinalIgnoreCase))
            {
                redisConfiguration.AsyncTimeout = 1000;
            }

            if (!connectionString.Contains("connectTimeout", StringComparison.OrdinalIgnoreCase))
            {
                redisConfiguration.ConnectTimeout = 2000;
            }
        }

        private void AddPersistence(IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{DatabaseOptions.ConnectionStringName}' is missing. Set ConnectionStrings:{DatabaseOptions.ConnectionStringName} (user-secrets locally).");

            services.AddScoped<AuditableEntityInterceptor>();
            services.AddScoped<DispatchDomainEventsInterceptor>();

            services.AddDbContextFactory<ApplicationDbContext>(
                (provider, options) => options
                    .UseNpgsql(connectionString)
                    .UseSnakeCaseNamingConvention()
                    .AddInterceptors(
                        provider.GetRequiredService<AuditableEntityInterceptor>(),
                        provider.GetRequiredService<DispatchDomainEventsInterceptor>()),
                ServiceLifetime.Scoped);

            services.AddScoped<IApplicationDbContextFactory, ApplicationDbContextFactory>();
            services.AddSingleton<IDbExceptionClassificationService, PostgresExceptionClassificationService>();
            services.AddHostedService<DatabaseMigrationService>();
            services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>("postgres", tags: [HealthCheckTags.Ready]);
        }
    }
}

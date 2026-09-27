using Govern.Application.Common.Abstractions;
using Govern.Application.Common.Models;
using Govern.Application.Common.Options;
using Govern.Infrastructure.Caching.Services;
using Govern.Infrastructure.Emailing.Services;
using Govern.Infrastructure.Identity.Services;
using Govern.Infrastructure.Options;
using Govern.Infrastructure.Persistence;
using Govern.Infrastructure.Persistence.Interceptors;
using Govern.Infrastructure.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Govern.Infrastructure;

/// <summary>
/// Registers Infrastructure implementations: persistence (PostgreSQL), identity (JWT, password hashing),
/// email (mocked), in-memory sign-in throttling and the database health check.
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
        /// Application configuration (connection string, <c>Jwt</c>, <c>Database</c>, <c>SignInThrottling</c>).
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
            services.AddSingleton<ISignInThrottlingService, MemorySignInThrottlingService>();

            return services;
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

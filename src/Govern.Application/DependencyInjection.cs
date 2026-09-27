using FluentValidation;
using Govern.Application.Users.Services;
using Govern.Utils.Mediator.Extensions;
using Govern.Utils.Mediator.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.Application;

/// <summary>
/// Registers the Application layer: the mediator with its pipelines, all handlers and validators, and feature services.
/// </summary>
public static class DependencyInjection
{
    /// <param name="services">The service collection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds the mediator (error handling → logging → validation → handler), handlers and validators from this
        /// assembly, and feature services.
        /// </summary>
        /// <returns>The service collection, for chaining.</returns>
        public IServiceCollection AddApplication()
        {
            var assembly = typeof(DependencyInjection).Assembly;

            services.AddMediator(
                options => options
                    .AddPipeline(typeof(ErrorHandlingPipeline<,>))
                    .AddPipeline(typeof(LoggingPipeline<,>))
                    .AddPipeline(typeof(ValidationPipeline<,>)),
                assembly);

            services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
            services.AddScoped<UserTokenIssuingService>();
            services.AddScoped<RefreshTokenReuseService>();

            return services;
        }
    }
}

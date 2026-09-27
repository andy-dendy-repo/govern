using System.Reflection;
using Govern.Utils.Mediator.Abstractions;
using Govern.Utils.Mediator.Options;
using Govern.Utils.Mediator.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Govern.Utils.Mediator.Extensions;

/// <summary>
/// Registers the mediator, its pipelines and all handlers found in the given assemblies.
/// </summary>
public static class MediatorServiceCollectionExtensions
{
    /// <param name="services">The service collection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="IMediatorService"/> (scoped), the configured pipelines (scoped, in order) and every
        /// non-abstract <see cref="IRequestHandler{TRequest, TResponse}"/> and <see cref="INotificationHandler{TNotification}"/>
        /// (public or internal) in <paramref name="assemblies"/> (scoped).
        /// </summary>
        /// <param name="configure">Configures options, e.g. <c>o =&gt; o.AddPipeline(typeof(LoggingPipeline&lt;,&gt;))</c>.</param>
        /// <param name="assemblies">Assemblies scanned for handlers.</param>
        /// <returns>The service collection, for chaining.</returns>
        public IServiceCollection AddMediator(Action<MediatorOptions>? configure, params Assembly[] assemblies)
        {
            ArgumentNullException.ThrowIfNull(assemblies);

            var options = new MediatorOptions();
            configure?.Invoke(options);

            services.AddSingleton(Microsoft.Extensions.Options.Options.Create(options));
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddScoped<IMediatorService, MediatorService>();

            foreach (var pipeline in options.Pipelines)
            {
                services.AddScoped(typeof(IPipeline<,>), pipeline);
            }

            foreach (var (serviceType, implementationType) in assemblies.Distinct().SelectMany(FindHandlers))
            {
                services.AddScoped(serviceType, implementationType);
            }

            return services;
        }
    }

    private static IEnumerable<(Type Service, Type Implementation)> FindHandlers(Assembly assembly) =>
        from type in assembly.GetTypes()
        where type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
        from contract in type.GetInterfaces()
        where contract.IsGenericType
            && (contract.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)
                || contract.GetGenericTypeDefinition() == typeof(INotificationHandler<>))
        select (contract, type);
}

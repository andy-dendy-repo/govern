using System.Reflection;

namespace Govern.Api.Extensions;

/// <summary>
/// Discovers endpoints by convention: every <c>static class *Endpoint</c> in <c>Govern.Api.Features</c> exposing
/// <c>public static void Map(IEndpointRouteBuilder)</c> (enforced by the architecture tests).
/// </summary>
public static class EndpointExtensions
{
    private const string FeaturesNamespace = "Govern.Api.Features";

    /// <param name="app">The route builder.</param>
    extension(IEndpointRouteBuilder app)
    {
        /// <summary>
        /// Maps all feature endpoints.
        /// </summary>
        /// <returns>The route builder, for chaining.</returns>
        /// <exception cref="InvalidOperationException">An endpoint class has no <c>Map(IEndpointRouteBuilder)</c> method.</exception>
        public IEndpointRouteBuilder MapEndpoints()
        {
            foreach (var endpoint in EndpointTypes())
            {
                var map = endpoint.GetMethod("Map", BindingFlags.Public | BindingFlags.Static, [typeof(IEndpointRouteBuilder)])
                    ?? throw new InvalidOperationException($"{endpoint.FullName} must declare public static void Map(IEndpointRouteBuilder app).");

                map.Invoke(null, [app]);
            }

            return app;
        }
    }

    private static IEnumerable<Type> EndpointTypes() =>
        typeof(EndpointExtensions).Assembly.GetTypes()
            .Where(static type => type is { IsAbstract: true, IsSealed: true, IsNested: false }
                && type.Namespace?.StartsWith(FeaturesNamespace, StringComparison.Ordinal) == true
                && type.Name.EndsWith("Endpoint", StringComparison.Ordinal))
            .OrderBy(static type => type.FullName, StringComparer.Ordinal);
}

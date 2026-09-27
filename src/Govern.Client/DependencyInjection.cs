using Govern.Client.Options;
using Radzen;

namespace Govern.Client;

/// <summary>
/// Registers the client services: Radzen and the validated <see cref="ApiOptions"/>.
/// </summary>
public static class DependencyInjection
{
    /// <param name="services">The service collection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds all client services. Components never get a bare <see cref="HttpClient"/>; Api calls go through typed
        /// clients in <c>Features/[Feature]/Clients</c>.
        /// </summary>
        /// <param name="configuration">Client configuration (<c>Api</c>).</param>
        /// <returns>The service collection, for chaining.</returns>
        /// <exception cref="InvalidOperationException"><c>Api:BaseUrl</c> is missing, relative, not HTTPS, or has credentials, a query or a fragment.</exception>
        public IServiceCollection AddClient(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var api = configuration.GetSection(ApiOptions.SectionName).Get<ApiOptions>() ?? new ApiOptions();
            if (!api.IsValid)
            {
                throw new InvalidOperationException(
                    "Api:BaseUrl must be an absolute HTTPS URL without credentials, query or fragment (HTTP is allowed for loopback hosts only).");
            }

            services.AddSingleton(Microsoft.Extensions.Options.Options.Create(api));
            services.AddRadzenComponents();

            return services;
        }
    }
}

using Govern.Client.Auth;
using Govern.Client.Features.Games.Clients;
using Govern.Client.Features.Games.Services;
using Govern.Client.Features.Users.Clients;
using Govern.Client.Services;
using Govern.Client.Options;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Radzen;

namespace Govern.Client;

/// <summary>
/// Registers the client services: Radzen, the validated <see cref="ApiOptions"/>, the auth session and the typed Api clients.
/// </summary>
public static class DependencyInjection
{
    /// <param name="services">The service collection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds all client services. Components never get a bare <see cref="HttpClient"/>; Api calls go through typed
        /// clients in <c>Features/[Feature]/Clients</c>, which send the bearer token through <see cref="AuthorizationMessageHandler"/>.
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
            AddAuth(services, api);
            AddApiClient<UsersClient>(services, api);
            AddApiClient<GamesClient>(services, api);
            services.AddTransient<GameWatchService>();
            services.AddScoped<ClipboardService>();

            // AddHttpClient also registers a plain HttpClient; components must use typed clients instead.
            services.RemoveAll<HttpClient>();

            return services;
        }
    }

    private static void AddAuth(IServiceCollection services, ApiOptions api)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<TokenStore>();
        services.AddSingleton<ICrossTabLock, WebLocksCrossTabLock>();
        services.AddSingleton<JwtAuthenticationStateProvider>();
        services.AddSingleton<AuthenticationStateProvider>(static provider => provider.GetRequiredService<JwtAuthenticationStateProvider>());
        services.AddSingleton<AuthSession>();
        services.AddAuthorizationCore();
        services.AddCascadingAuthenticationState();
        services.AddTransient<AuthorizationMessageHandler>();
        services.AddHttpClient(AuthSession.HttpClientName, client => client.BaseAddress = api.BaseUrl);
    }

    private static void AddApiClient<TClient>(IServiceCollection services, ApiOptions api)
        where TClient : class =>
        services.AddHttpClient<TClient>(client => client.BaseAddress = api.BaseUrl)
            .AddHttpMessageHandler<AuthorizationMessageHandler>();
}

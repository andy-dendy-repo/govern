using System.Net;
using System.Net.Http.Headers;
using Govern.Client.Options;
using Microsoft.Extensions.Options;

namespace Govern.Client.Auth;

/// <summary>
/// Adds the bearer token to Api calls, refreshes it ahead of expiry, and on a 401 refreshes once and retries the request.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Only requests to the Api origin (<see cref="ApiOptions.BaseUrl"/>) get the token; any other URL passes through
/// without it.</item>
/// <item>Requests marked with <see cref="SendAsIs"/> pass through untouched: sign-up and sign-in (their 401 means wrong
/// credentials, not an expired token) and sign-out, which carries the tokens <see cref="AuthSession"/> chose under its lock.</item>
/// <item>Without a session the request is not sent; the caller gets a 401.</item>
/// <item>When the Api cannot refresh right now (rate limit, network), the caller gets a 503 and the session is kept.</item>
/// <item>The request content is buffered before the first send, so the retry can send it again.</item>
/// </list>
/// Depends only on singletons, because <c>IHttpClientFactory</c> creates handlers in its own scope.
/// </remarks>
public sealed class AuthorizationMessageHandler(AuthSession session, IOptions<ApiOptions> apiOptions) : DelegatingHandler
{
    /// <summary>Marks a request that the handler sends as is: no bearer token added, no refresh, no retry.</summary>
    public static readonly HttpRequestOptionsKey<bool> SendAsIs = new("Govern.SendAsIs");

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if ((request.Options.TryGetValue(SendAsIs, out var sendAsIs) && sendAsIs) || !IsApiRequest(request))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var accessToken = await session.GetAccessTokenAsync(cancellationToken);
        if (accessToken is null)
        {
            return Synthetic(HttpStatusCode.Unauthorized, request);
        }

        if (request.Content is not null)
        {
            await request.Content.LoadIntoBufferAsync(cancellationToken);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        var refresh = await session.RefreshAsync(accessToken, cancellationToken);
        switch (refresh.Outcome)
        {
            case RefreshOutcome.Refreshed:
                response.Dispose();
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refresh.AccessToken);
                return await base.SendAsync(request, cancellationToken);
            case RefreshOutcome.Unavailable:
                response.Dispose();
                return Synthetic(HttpStatusCode.ServiceUnavailable, request);
            default:
                return response;
        }
    }

    private bool IsApiRequest(HttpRequestMessage request) =>
        request.RequestUri is { IsAbsoluteUri: true } uri
        && apiOptions.Value.BaseUrl is { } api
        && Uri.Compare(uri, api, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0;

    private static HttpResponseMessage Synthetic(HttpStatusCode status, HttpRequestMessage request) =>
        new(status) { RequestMessage = request };
}

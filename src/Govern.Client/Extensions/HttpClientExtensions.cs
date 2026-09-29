using System.Net.Http.Json;
using System.Text.Json;
using Govern.Client.Models;

namespace Govern.Client.Extensions;

/// <summary>
/// Sends Api requests for the typed clients in <c>Features/[Feature]/Clients</c> and turns the response into an
/// <see cref="ApiResult"/>: a ProblemDetails body becomes an <see cref="ApiError"/>, and network failures, unreadable JSON and
/// timeouts become <see cref="ApiError.Network"/>. Cancellation by the caller still throws.
/// </summary>
public static class HttpClientExtensions
{
    /// <param name="http">A typed client's <see cref="HttpClient"/>.</param>
    extension(HttpClient http)
    {
        /// <summary>Sends <paramref name="request"/> (and disposes it) and reads a <typeparamref name="T"/> body.</summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">Cancels the call.</param>
        /// <typeparam name="T">The response body type.</typeparam>
        /// <returns>The body, or the error.</returns>
        public async Task<ApiResult<T>> SendForResultAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            using (request)
            {
                try
                {
                    using var response = await http.SendAsync(request, cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        return ApiResult.Fail<T>(await ReadErrorAsync(response, cancellationToken));
                    }

                    return await response.Content.ReadFromJsonAsync<T>(ApiJson.Options, cancellationToken) is { } value
                        ? ApiResult.Ok(value)
                        : ApiResult.Fail<T>(ApiError.FromStatus(response.StatusCode));
                }
                catch (Exception exception) when (IsNetworkFailure(exception, cancellationToken))
                {
                    return ApiResult.Fail<T>(ApiError.Network);
                }
            }
        }

        /// <summary>Sends <paramref name="request"/> (and disposes it), ignoring any success body.</summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">Cancels the call.</param>
        /// <returns>Success, or the error.</returns>
        public async Task<ApiResult> SendForResultAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            using (request)
            {
                try
                {
                    using var response = await http.SendAsync(request, cancellationToken);
                    return response.IsSuccessStatusCode
                        ? ApiResult.Ok()
                        : ApiResult.Fail(await ReadErrorAsync(response, cancellationToken));
                }
                catch (Exception exception) when (IsNetworkFailure(exception, cancellationToken))
                {
                    return ApiResult.Fail(ApiError.Network);
                }
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="exception"/> is a failure to reach or read the Api rather than the caller cancelling: a network
    /// error, unreadable JSON, or a timeout (a <see cref="TaskCanceledException"/> while the caller's token is not cancelled).
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <returns>Whether to report it as a network error.</returns>
    public static bool IsNetworkFailure(Exception exception, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested
        && exception is HttpRequestException or JsonException or IOException or TaskCanceledException;

    private static async Task<ApiError> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await ApiError.ReadAsync(response.StatusCode, body, cancellationToken);
    }
}

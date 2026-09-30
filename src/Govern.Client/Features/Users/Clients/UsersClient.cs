using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Govern.Client.Auth;
using Govern.Client.Extensions;
using Govern.Client.Features.Users.Models;
using Govern.Client.Models;

namespace Govern.Client.Features.Users.Clients;

/// <summary>
/// Calls the Api's <c>/api/users</c> endpoints. Sign-in starts the session in <see cref="AuthSession"/>; sign-out ends it
/// even when the Api call fails.
/// </summary>
public sealed class UsersClient(HttpClient http, AuthSession session)
{
    /// <summary>Creates an account. Does not sign in.</summary>
    /// <param name="request">The email and password.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new account, or the error (e.g. 409 <c>user.email_taken</c>, 400 with field errors).</returns>
    public Task<ApiResult<SignUpResponse>> SignUpAsync(SignUpRequest request, CancellationToken cancellationToken) =>
        http.SendForResultAsync<SignUpResponse>(SentAsIs(HttpMethod.Post, "api/users/sign-up", request), cancellationToken);

    /// <summary>Signs in and stores the session.</summary>
    /// <param name="request">The email and password.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or the error (e.g. 401 <c>user.invalid_credentials</c>, 429).</returns>
    public async Task<ApiResult> SignInAsync(SignInRequest request, CancellationToken cancellationToken)
    {
        var result = await http.SendForResultAsync<SignInResponse>(SentAsIs(HttpMethod.Post, "api/users/sign-in", request), cancellationToken);
        if (!result.IsSuccess)
        {
            return ApiResult.Fail(result.Error);
        }

        var tokens = result.Value;
        await session.StartAsync(
            new TokenPair(tokens.AccessToken, tokens.AccessTokenExpiresAt, tokens.RefreshToken, tokens.RefreshTokenExpiresAt),
            cancellationToken);
        return ApiResult.Ok();
    }

    /// <summary>
    /// Revokes this session on the Api and clears it locally (always, even when the Api call fails). Runs under the session
    /// lock, after any due refresh, so the request carries the current refresh token: a rotated one would count as reuse and
    /// make the Api revoke every session of the user.
    /// </summary>
    /// <param name="cancellationToken">Stops waiting; the sign-out still completes once started.</param>
    /// <returns>The Api call's result.</returns>
    public Task<ApiResult> SignOutAsync(CancellationToken cancellationToken) =>
        session.SignOutAsync(
            (accessToken, refreshToken) => http.SendForResultAsync(SignOutRequestMessage(accessToken, refreshToken), CancellationToken.None),
            cancellationToken);

    /// <summary>
    /// Revokes every session of the user on the Api. The local session is cleared only when that worked (or the Api no longer
    /// accepts this session), so a failure can be shown and retried.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The Api call's result.</returns>
    public async Task<ApiResult> SignOutEverywhereAsync(CancellationToken cancellationToken)
    {
        var result = await http.SendForResultAsync(new HttpRequestMessage(HttpMethod.Post, "api/users/sign-out-everywhere"), cancellationToken);
        if (result.IsSuccess || result.Error.Status == (int)HttpStatusCode.Unauthorized)
        {
            await session.EndAsync(CancellationToken.None);
        }

        return result;
    }

    /// <summary>Reads the signed-in user.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The user, or the error.</returns>
    public Task<ApiResult<GetCurrentUserResponse>> GetCurrentUserAsync(CancellationToken cancellationToken) =>
        http.SendForResultAsync<GetCurrentUserResponse>(new HttpRequestMessage(HttpMethod.Get, "api/users/me"), cancellationToken);

    private static HttpRequestMessage Json<TBody>(HttpMethod method, string path, TBody body) =>
        new(method, path) { Content = JsonContent.Create(body, options: ApiJson.Options) };

    private static HttpRequestMessage SentAsIs<TBody>(HttpMethod method, string path, TBody body)
    {
        var request = Json(method, path, body);
        request.Options.Set(AuthorizationMessageHandler.SendAsIs, true);
        return request;
    }

    // The session lock chose these tokens; the handler must neither swap the bearer nor refresh.
    private static HttpRequestMessage SignOutRequestMessage(string accessToken, string refreshToken)
    {
        var request = Json(HttpMethod.Post, "api/users/sign-out", new SignOutRequest(refreshToken));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Options.Set(AuthorizationMessageHandler.SendAsIs, true);
        return request;
    }
}

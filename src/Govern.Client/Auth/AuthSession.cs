using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Govern.Client.Models;

namespace Govern.Client.Auth;

/// <summary>
/// Owns the signed-in session: stores the tokens from sign-in, hands out access tokens, refreshes them ahead of expiry or
/// after a 401, and signs out. Every change to the stored session runs one at a time, in this tab (a semaphore) and across
/// tabs (<see cref="ICrossTabLock"/>), because the refresh token is single-use: presenting a rotated one again (to refresh or
/// to sign out) makes the Api revoke every session of the user.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A singleton: <see cref="AuthorizationMessageHandler"/> is created by <c>IHttpClientFactory</c> in its own scope, so
/// everything it depends on must be a singleton.</item>
/// <item>The refresh call uses the <see cref="HttpClientName"/> client, which has no <see cref="AuthorizationMessageHandler"/>,
/// so refreshing never recurses.</item>
/// <item>Once started, session work is not cancelled: the Api may already have rotated the tokens, and dropping the answer
/// would leave a rotated token in storage. A caller's token only stops it from waiting.</item>
/// </list>
/// </remarks>
public sealed class AuthSession(
    IHttpClientFactory httpClientFactory,
    TokenStore tokenStore,
    JwtAuthenticationStateProvider authenticationState,
    ICrossTabLock crossTabLock,
    TimeProvider timeProvider) : IDisposable
{
    /// <summary>The name of the <see cref="HttpClient"/> used for refresh (Api base address, no auth handler).</summary>
    public const string HttpClientName = "Govern.Auth";

    /// <summary>How long before the access token expires it is refreshed (at most half its lifetime).</summary>
    public static readonly TimeSpan RenewMargin = TimeSpan.FromMinutes(1);

    /// <summary>
    /// After a refresh was unavailable (rate limit, server or network error), how long this tab waits before calling the Api
    /// again. Refresh shares the per-IP <c>auth</c> rate limit with sign-in.
    /// </summary>
    public static readonly TimeSpan UnavailableBackoff = TimeSpan.FromSeconds(30);

    // Used when an access token's lifetime can't be read, so an unexpected token format can't make every request refresh.
    private static readonly TimeSpan FallbackLifetime = TimeSpan.FromMinutes(2);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _nextRefreshAttempt = DateTimeOffset.MinValue;

    /// <summary>Stores the tokens of a new session (sign-in) and notifies components that the user changed.</summary>
    /// <param name="tokens">The tokens returned by the Api.</param>
    /// <param name="cancellationToken">Stops waiting; the tokens are still stored once storing has started.</param>
    /// <returns>A task that completes when the session is stored.</returns>
    public async Task StartAsync(TokenPair tokens, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        await RunExclusiveAsync(() => StoreAsync(tokens), whenLockUnavailable: null).WaitAsync(cancellationToken);
        await authenticationState.NotifyChangedAsync();
    }

    /// <summary>Clears the local session without calling the Api, and notifies components.</summary>
    /// <param name="cancellationToken">Stops waiting; the session is still cleared once clearing has started.</param>
    /// <returns>A task that completes when the session is cleared.</returns>
    public async Task EndAsync(CancellationToken cancellationToken)
    {
        await RunExclusiveAsync(ClearAsync, whenLockUnavailable: null).WaitAsync(cancellationToken);
        await authenticationState.NotifyChangedAsync();
    }

    /// <summary>
    /// Signs this session out: refreshes first when the access token is due, then calls <paramref name="revoke"/> with the
    /// current access and refresh tokens, and clears the local session whatever the Api answers.
    /// </summary>
    /// <param name="revoke">Sends the sign-out request with the given access token (bearer) and refresh token (body).</param>
    /// <param name="cancellationToken">Stops waiting; the sign-out still completes once started.</param>
    /// <returns>The result of <paramref name="revoke"/>, or success when there was no session.</returns>
    public async Task<ApiResult> SignOutAsync(Func<string, string, Task<ApiResult>> revoke, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(revoke);
        var result = await RunExclusiveAsync(() => SignOutLockedAsync(revoke), whenLockUnavailable: null).WaitAsync(cancellationToken);
        await authenticationState.NotifyChangedAsync();
        return result;
    }

    /// <summary>
    /// The access token to send, refreshed first when it is due. When the refresh is unavailable the current token is returned,
    /// and a 401 from the Api triggers the next attempt. Also notifies components when another tab changed the user.
    /// </summary>
    /// <param name="cancellationToken">Stops reading or waiting for a refresh.</param>
    /// <returns>The access token, or <see langword="null"/> when signed out.</returns>
    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var tokens = await tokenStore.GetAsync(cancellationToken);
        authenticationState.NotifyIfUserChanged(tokens);
        if (tokens is null || tokens.RenewAt > timeProvider.GetUtcNow())
        {
            return tokens?.AccessToken;
        }

        var result = await RefreshAsync(tokens.AccessToken, cancellationToken);
        return result.Outcome switch
        {
            RefreshOutcome.Refreshed => result.AccessToken,
            RefreshOutcome.Unavailable => tokens.AccessToken,
            _ => null,
        };
    }

    /// <summary>
    /// Refreshes the session after <paramref name="failedAccessToken"/> was rejected or became due. When another caller or tab
    /// has already replaced that token with a fresh one, the fresh token is returned without calling the Api.
    /// </summary>
    /// <param name="failedAccessToken">The access token that was rejected or is due, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops waiting; a refresh that has started still completes and stores its tokens.</param>
    /// <returns>The outcome.</returns>
    public Task<RefreshResult> RefreshAsync(string? failedAccessToken, CancellationToken cancellationToken) =>
        RunExclusiveAsync(() => RefreshLockedAsync(failedAccessToken), () => RefreshResult.Unavailable).WaitAsync(cancellationToken);

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    // Runs session work under the in-tab gate and the cross-tab lock. When the lock can't be taken, refresh gives up
    // (whenLockUnavailable) rather than risk presenting a token another tab is rotating; local writes run unlocked.
    private async Task<T> RunExclusiveAsync<T>(Func<Task<T>> action, Func<T>? whenLockUnavailable)
    {
        await _gate.WaitAsync();
        try
        {
            try
            {
                return await crossTabLock.RunAsync(action, CancellationToken.None);
            }
            catch (CrossTabLockUnavailableException)
            {
                return whenLockUnavailable is null ? await action() : whenLockUnavailable();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<RefreshResult> RefreshLockedAsync(string? failedAccessToken)
    {
        var tokens = await tokenStore.GetAsync(CancellationToken.None);
        if (tokens is null)
        {
            await authenticationState.NotifyChangedAsync();
            return RefreshResult.SignedOut;
        }

        var now = timeProvider.GetUtcNow();
        if (tokens.AccessToken != failedAccessToken && tokens.RenewAt > now)
        {
            return RefreshResult.Refreshed(tokens.AccessToken);
        }

        if (now < _nextRefreshAttempt)
        {
            return RefreshResult.Unavailable;
        }

        var result = await PostRefreshAsync(tokens.RefreshToken);
        _nextRefreshAttempt = result.Outcome == RefreshOutcome.Unavailable ? now + UnavailableBackoff : DateTimeOffset.MinValue;
        return result;
    }

    private async Task<ApiResult> SignOutLockedAsync(Func<string, string, Task<ApiResult>> revoke)
    {
        var tokens = await tokenStore.GetAsync(CancellationToken.None);
        if (tokens is null)
        {
            return ApiResult.Ok();
        }

        if (tokens.RenewAt <= timeProvider.GetUtcNow())
        {
            // Refresh first: the bearer must be valid, and the body must carry the refresh token that is current after it.
            if ((await PostRefreshAsync(tokens.RefreshToken)).Outcome == RefreshOutcome.SignedOut)
            {
                return ApiResult.Ok();
            }

            tokens = await tokenStore.GetAsync(CancellationToken.None) ?? tokens;
        }

        try
        {
            return await revoke(tokens.AccessToken, tokens.RefreshToken);
        }
        finally
        {
            await ClearAsync();
        }
    }

    private async Task<RefreshResult> PostRefreshAsync(string refreshToken)
    {
        var http = httpClientFactory.CreateClient(HttpClientName);
        try
        {
            using var response = await http.PostAsJsonAsync("api/users/refresh", new RefreshTokenRequest(refreshToken), ApiJson.Options, CancellationToken.None);
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            {
                return await EndIfCurrentAsync(refreshToken);
            }

            if (!response.IsSuccessStatusCode
                || await response.Content.ReadFromJsonAsync<TokenPair>(ApiJson.Options, CancellationToken.None) is not { } tokens)
            {
                return RefreshResult.Unavailable;
            }

            return await StoreIfCurrentAsync(refreshToken, tokens);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            // TaskCanceledException here is the client timeout: no caller token reaches the request.
            return RefreshResult.Unavailable;
        }
    }

    // Compare-and-set: only the session that presented the refresh token is replaced. When the stored session changed
    // meanwhile (another tab signed out or in without Web Locks), it is left alone and returned as is.
    private async Task<RefreshResult> StoreIfCurrentAsync(string presentedRefreshToken, TokenPair tokens)
    {
        var stored = await tokenStore.GetAsync(CancellationToken.None);
        if (stored?.RefreshToken != presentedRefreshToken)
        {
            return stored is null ? RefreshResult.SignedOut : RefreshResult.Refreshed(stored.AccessToken);
        }

        // Same user, new tokens: no auth state notification, so the current page is not re-rendered from scratch.
        await StoreAsync(tokens);
        return RefreshResult.Refreshed(tokens.AccessToken);
    }

    private async Task<RefreshResult> EndIfCurrentAsync(string presentedRefreshToken)
    {
        var stored = await tokenStore.GetAsync(CancellationToken.None);
        if (stored is not null && stored.RefreshToken != presentedRefreshToken)
        {
            return RefreshResult.Refreshed(stored.AccessToken);
        }

        await ClearAsync();
        await authenticationState.NotifyChangedAsync();
        return RefreshResult.SignedOut;
    }

    private async Task<bool> StoreAsync(TokenPair tokens)
    {
        await tokenStore.SetAsync(ToStored(tokens), CancellationToken.None);
        _nextRefreshAttempt = DateTimeOffset.MinValue;
        return true;
    }

    private async Task<bool> ClearAsync()
    {
        await tokenStore.ClearAsync(CancellationToken.None);
        return true;
    }

    private StoredTokens ToStored(TokenPair tokens)
    {
        var lifetime = JwtPayload.TryRead(tokens.AccessToken)?.Lifetime ?? FallbackLifetime;
        var margin = lifetime / 2 < RenewMargin ? lifetime / 2 : RenewMargin;
        var renewAt = timeProvider.GetUtcNow() + lifetime - margin;
        return new StoredTokens(tokens.AccessToken, tokens.RefreshToken, tokens.RefreshTokenExpiresAt, renewAt);
    }

    private sealed record RefreshTokenRequest(string RefreshToken);
}

using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Govern.Client.Auth;

/// <summary>
/// The signed-in user, from the stored access token's claims (name = <c>email</c>). The user counts as signed in while an
/// unexpired refresh token is stored, even when the access token has expired: the next Api call refreshes it.
/// </summary>
public sealed class JwtAuthenticationStateProvider(TokenStore tokenStore, TimeProvider timeProvider) : AuthenticationStateProvider
{
    /// <summary>The authentication type of signed-in identities.</summary>
    public const string AuthenticationType = "Govern.Jwt";

    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private bool _observed;
    private string? _lastSubject;

    /// <inheritdoc />
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var state = StateFor(await tokenStore.GetAsync(CancellationToken.None));
        Observe(state);
        return state;
    }

    /// <summary>Tells components that the user changed (signed in or signed out).</summary>
    /// <remarks>
    /// The new state is read first and passed as a completed task: <c>AuthorizeRouteView</c> shows its <c>Authorizing</c>
    /// content while the task is pending, which would tear down and re-create the current page.
    /// </remarks>
    /// <returns>A task that completes when components have been notified.</returns>
    public async Task NotifyChangedAsync()
    {
        var state = await GetAuthenticationStateAsync();
        NotifyAuthenticationStateChanged(Task.FromResult(state));
    }

    /// <summary>
    /// Notifies components when <paramref name="tokens"/> (just read from storage) belong to a different user (<c>sub</c>) than
    /// the state components last received, e.g. because another tab signed out or signed in as someone else.
    /// </summary>
    /// <param name="tokens">The stored tokens, or <see langword="null"/> when none are stored.</param>
    public void NotifyIfUserChanged(StoredTokens? tokens)
    {
        var state = StateFor(tokens);
        var changed = _observed && Subject(state) != _lastSubject;
        Observe(state);
        if (changed)
        {
            NotifyAuthenticationStateChanged(Task.FromResult(state));
        }
    }

    private AuthenticationState StateFor(StoredTokens? tokens)
    {
        if (tokens is null
            || tokens.RefreshTokenExpiresAt <= timeProvider.GetUtcNow()
            || JwtPayload.TryRead(tokens.AccessToken) is not { } payload)
        {
            return Anonymous;
        }

        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(payload.Claims, AuthenticationType, "email", "role")));
    }

    private void Observe(AuthenticationState state)
    {
        _observed = true;
        _lastSubject = Subject(state);
    }

    private static string? Subject(AuthenticationState state) => state.User.FindFirst("sub")?.Value;
}

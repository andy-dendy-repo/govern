using System.Net;
using Govern.Client.Auth;
using Govern.ClientTests.Support;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.ClientTests.Auth;

public class AuthSessionTests : ClientTestContext
{
    private const string RefreshPath = "/api/users/refresh";

    private AuthSession Session => Services.GetRequiredService<AuthSession>();

    [Fact]
    public async Task RefreshAsync_ApiRefreshes_StoresTheNewPairAndReturnsTheAccessToken()
    {
        var old = SignIn();
        var fresh = TestTokens.Pair("player@example.com", Time.GetUtcNow());
        Api.On(HttpMethod.Post, RefreshPath, () => FakeApiHandler.Json(fresh));

        var result = await Session.RefreshAsync(old.AccessToken, Ct);

        result.ShouldBe(RefreshResult.Refreshed(fresh.AccessToken));
        StoredTokens().ShouldNotBeNull().RefreshToken.ShouldBe(fresh.RefreshToken);
        var request = Api.RequestsTo(RefreshPath).ShouldHaveSingleItem();
        request.Body.ShouldBe($"{{\"refreshToken\":\"{old.RefreshToken}\"}}");
        request.Authorization.ShouldBeNull();
        CrossTabLock.Taken.ShouldBe(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task RefreshAsync_ApiRejectsTheRefreshToken_ClearsTheSessionAndSignsOut(HttpStatusCode status)
    {
        var old = SignIn();
        Api.On(HttpMethod.Post, RefreshPath, () => FakeApiHandler.Problem(status, "user.invalid_refresh_token", "The refresh token is invalid or expired."));

        var result = await Session.RefreshAsync(old.AccessToken, Ct);

        result.ShouldBe(RefreshResult.SignedOut);
        StoredTokens().ShouldBeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task RefreshAsync_ApiCannotRefreshNow_KeepsTheSession(HttpStatusCode status)
    {
        var old = SignIn();
        Api.On(HttpMethod.Post, RefreshPath, () => FakeApiHandler.Empty(status));

        var result = await Session.RefreshAsync(old.AccessToken, Ct);

        result.ShouldBe(RefreshResult.Unavailable);
        StoredTokens().ShouldBe(old);
    }

    [Fact]
    public async Task RefreshAsync_NetworkError_KeepsTheSession()
    {
        var old = SignIn();
        Api.On(HttpMethod.Post, RefreshPath, _ => throw new HttpRequestException("offline"));

        var result = await Session.RefreshAsync(old.AccessToken, Ct);

        result.ShouldBe(RefreshResult.Unavailable);
        StoredTokens().ShouldBe(old);
    }

    [Fact]
    public async Task RefreshAsync_ConcurrentCallsForTheSameToken_CallTheApiOnce()
    {
        var old = SignIn();
        var fresh = TestTokens.Pair("player@example.com", Time.GetUtcNow());
        var release = new TaskCompletionSource();
        Api.On(HttpMethod.Post, RefreshPath, async _ =>
        {
            await release.Task;
            return FakeApiHandler.Json(fresh);
        });

        var first = Session.RefreshAsync(old.AccessToken, Ct);
        var second = Session.RefreshAsync(old.AccessToken, Ct);
        release.SetResult();

        (await first).ShouldBe(RefreshResult.Refreshed(fresh.AccessToken));
        (await second).ShouldBe(RefreshResult.Refreshed(fresh.AccessToken));
        Api.RequestsTo(RefreshPath).Count.ShouldBe(1);
    }

    [Fact]
    public async Task RefreshAsync_AnotherTabAlreadyRefreshed_ReturnsItsTokenWithoutCallingTheApi()
    {
        var old = SignIn();
        var fresh = TestTokens.Pair("player@example.com", Time.GetUtcNow());
        StoreTokens(new StoredTokens(fresh.AccessToken, fresh.RefreshToken, fresh.RefreshTokenExpiresAt, Time.GetUtcNow().AddMinutes(14)));

        var result = await Session.RefreshAsync(old.AccessToken, Ct);

        result.ShouldBe(RefreshResult.Refreshed(fresh.AccessToken));
        Api.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RefreshAsync_NothingStored_SignsOutWithoutCallingTheApi()
    {
        var result = await Session.RefreshAsync("access", Ct);

        result.ShouldBe(RefreshResult.SignedOut);
        Api.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RefreshAsync_CallerStopsWaitingAfterTheRequestWasSent_StillStoresTheRotatedTokens()
    {
        var old = SignIn();
        var fresh = TestTokens.Pair("player@example.com", Time.GetUtcNow());
        var sent = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        Api.On(HttpMethod.Post, RefreshPath, async _ =>
        {
            sent.SetResult();
            await release.Task;
            return FakeApiHandler.Json(fresh);
        });
        using var navigatedAway = new CancellationTokenSource();

        var abandoned = Session.RefreshAsync(old.AccessToken, navigatedAway.Token);
        await sent.Task;
        await navigatedAway.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => abandoned);
        release.SetResult();

        // The next refresh waits for the abandoned one and finds its tokens already stored.
        (await Session.RefreshAsync(old.AccessToken, Ct)).ShouldBe(RefreshResult.Refreshed(fresh.AccessToken));
        StoredTokens().ShouldNotBeNull().RefreshToken.ShouldBe(fresh.RefreshToken);
        Api.RequestsTo(RefreshPath).Count.ShouldBe(1);
    }

    [Fact]
    public async Task RefreshAsync_UnavailableRecently_WaitsForTheBackoffBeforeCallingTheApiAgain()
    {
        var old = SignIn();
        Api.On(HttpMethod.Post, RefreshPath, () => FakeApiHandler.Empty(HttpStatusCode.TooManyRequests));
        await Session.RefreshAsync(old.AccessToken, Ct);

        Time.Advance(AuthSession.UnavailableBackoff - TimeSpan.FromSeconds(1));
        (await Session.RefreshAsync(old.AccessToken, Ct)).ShouldBe(RefreshResult.Unavailable);
        Api.RequestsTo(RefreshPath).Count.ShouldBe(1);

        Time.Advance(TimeSpan.FromSeconds(1));
        await Session.RefreshAsync(old.AccessToken, Ct);
        Api.RequestsTo(RefreshPath).Count.ShouldBe(2);
    }

    [Fact]
    public async Task RefreshAsync_CrossTabLockUnavailable_ReturnsUnavailableWithoutCallingTheApi()
    {
        var old = SignIn();
        CrossTabLock.Unavailable = true;

        var result = await Session.RefreshAsync(old.AccessToken, Ct);

        result.ShouldBe(RefreshResult.Unavailable);
        Api.Requests.ShouldBeEmpty();
        StoredTokens().ShouldBe(old);
    }

    [Fact]
    public async Task RefreshAsync_SessionReplacedWhileRefreshing_KeepsTheNewSession()
    {
        var old = SignIn();
        var other = new StoredTokens(TestTokens.AccessToken("other@example.com", Time.GetUtcNow()), "other-refresh",
            Time.GetUtcNow().AddDays(14), Time.GetUtcNow().AddMinutes(14));
        Api.On(HttpMethod.Post, RefreshPath, _ =>
        {
            StoreTokens(other);
            return Task.FromResult(FakeApiHandler.Json(TestTokens.Pair("player@example.com", Time.GetUtcNow())));
        });

        var result = await Session.RefreshAsync(old.AccessToken, Ct);

        result.ShouldBe(RefreshResult.Refreshed(other.AccessToken));
        StoredTokens().ShouldBe(other);
    }

    [Fact]
    public async Task RefreshAsync_RejectedAfterTheSessionWasReplaced_KeepsTheNewSession()
    {
        var old = SignIn();
        var other = new StoredTokens(TestTokens.AccessToken("other@example.com", Time.GetUtcNow()), "other-refresh",
            Time.GetUtcNow().AddDays(14), Time.GetUtcNow().AddMinutes(14));
        Api.On(HttpMethod.Post, RefreshPath, _ =>
        {
            StoreTokens(other);
            return Task.FromResult(FakeApiHandler.Empty(HttpStatusCode.Unauthorized));
        });

        var result = await Session.RefreshAsync(old.AccessToken, Ct);

        result.ShouldBe(RefreshResult.Refreshed(other.AccessToken));
        StoredTokens().ShouldBe(other);
    }

    [Fact]
    public async Task StartAsync_UnreadableAccessToken_RenewsAfterOneMinuteInsteadOfOnEveryRequest()
    {
        var pair = TestTokens.Pair("player@example.com", Time.GetUtcNow()) with { AccessToken = "opaque-token" };

        await Session.StartAsync(pair, Ct);

        StoredTokens().ShouldNotBeNull().RenewAt.ShouldBe(Time.GetUtcNow() + TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetAccessTokenAsync_AnotherTabSignedOut_NotifiesComponentsOfTheAnonymousUser()
    {
        SignIn();
        await Services.GetRequiredService<JwtAuthenticationStateProvider>().GetAuthenticationStateAsync();
        Storage[TokenStore.Key] = null;
        var notifications = RecordNotifications();

        (await Session.GetAccessTokenAsync(Ct)).ShouldBeNull();

        (await notifications.ShouldHaveSingleItem()).User.Identity!.IsAuthenticated.ShouldBeFalse();
    }

    [Fact]
    public async Task GetAccessTokenAsync_AnotherTabSignedInAsSomeoneElse_NotifiesComponentsOfTheNewUser()
    {
        SignIn("player@example.com");
        await Services.GetRequiredService<JwtAuthenticationStateProvider>().GetAuthenticationStateAsync();
        SignIn("someone-else@example.com");
        var notifications = RecordNotifications();

        await Session.GetAccessTokenAsync(Ct);

        (await notifications.ShouldHaveSingleItem()).User.Identity!.Name.ShouldBe("someone-else@example.com");
    }

    [Fact]
    public async Task GetAccessTokenAsync_SameUser_DoesNotNotifyComponents()
    {
        SignIn();
        await Services.GetRequiredService<JwtAuthenticationStateProvider>().GetAuthenticationStateAsync();
        var notifications = RecordNotifications();

        await Session.GetAccessTokenAsync(Ct);

        notifications.ShouldBeEmpty();
    }

    [Fact]
    public async Task RefreshAsync_ApiRefreshes_DoesNotNotifyComponentsBecauseTheUserIsTheSame()
    {
        var old = SignIn();
        Api.On(HttpMethod.Post, RefreshPath, () => FakeApiHandler.Json(TestTokens.Pair("player@example.com", Time.GetUtcNow())));
        var notifications = RecordNotifications();

        await Session.RefreshAsync(old.AccessToken, Ct);

        notifications.ShouldBeEmpty();
    }

    [Fact]
    public async Task StartAsync_NewSession_NotifiesComponentsWithAnAlreadyCompletedSignedInState()
    {
        var notifications = RecordNotifications();

        await Session.StartAsync(TestTokens.Pair("player@example.com", Time.GetUtcNow()), Ct);

        var notification = notifications.ShouldHaveSingleItem();
        notification.IsCompletedSuccessfully.ShouldBeTrue("a pending state makes AuthorizeRouteView re-create the current page");
        (await notification).User.Identity!.Name.ShouldBe("player@example.com");
    }

    [Fact]
    public async Task EndAsync_SignedIn_ClearsTheSessionAndNotifiesAnAnonymousState()
    {
        SignIn();
        var notifications = RecordNotifications();

        await Session.EndAsync(Ct);

        StoredTokens().ShouldBeNull();
        (await notifications.ShouldHaveSingleItem()).User.Identity!.IsAuthenticated.ShouldBeFalse();
    }

    [Fact]
    public async Task StartAsync_ServerClockAhead_RenewsOneMinuteBeforeTheTokenLifetimeEndsByTheLocalClock()
    {
        var serverNow = Time.GetUtcNow().AddHours(3);

        await Session.StartAsync(TestTokens.Pair("player@example.com", serverNow), Ct);

        StoredTokens().ShouldNotBeNull().RenewAt.ShouldBe(Time.GetUtcNow() + TestTokens.AccessTokenLifetime - AuthSession.RenewMargin);
    }

    [Fact]
    public async Task GetAccessTokenAsync_BeforeRenewal_ReturnsTheStoredTokenWithoutRefreshing()
    {
        var tokens = SignIn();
        Time.Advance(TimeSpan.FromMinutes(13));

        (await Session.GetAccessTokenAsync(Ct)).ShouldBe(tokens.AccessToken);

        Api.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAccessTokenAsync_RenewalDue_RefreshesFirst()
    {
        SignIn();
        Time.Advance(TimeSpan.FromMinutes(14));
        var fresh = TestTokens.Pair("player@example.com", Time.GetUtcNow());
        Api.On(HttpMethod.Post, RefreshPath, () => FakeApiHandler.Json(fresh));

        (await Session.GetAccessTokenAsync(Ct)).ShouldBe(fresh.AccessToken);
    }

    [Fact]
    public async Task GetAccessTokenAsync_RenewalDueButApiUnavailable_ReturnsTheCurrentToken()
    {
        var tokens = SignIn();
        Time.Advance(TimeSpan.FromMinutes(14));
        Api.On(HttpMethod.Post, RefreshPath, () => FakeApiHandler.Empty(HttpStatusCode.TooManyRequests));

        (await Session.GetAccessTokenAsync(Ct)).ShouldBe(tokens.AccessToken);
    }

    [Fact]
    public async Task GetAccessTokenAsync_SignedOut_ReturnsNull()
    {
        (await Session.GetAccessTokenAsync(Ct)).ShouldBeNull();
    }

    private List<Task<AuthenticationState>> RecordNotifications()
    {
        var notifications = new List<Task<AuthenticationState>>();
        Services.GetRequiredService<JwtAuthenticationStateProvider>().AuthenticationStateChanged += notifications.Add;
        return notifications;
    }
}

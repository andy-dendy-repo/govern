using Govern.Client.Auth;
using Govern.ClientTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.ClientTests.Auth;

public class JwtAuthenticationStateProviderTests : ClientTestContext
{
    private JwtAuthenticationStateProvider Provider => Services.GetRequiredService<JwtAuthenticationStateProvider>();

    [Fact]
    public async Task GetAuthenticationState_SignedIn_UsesTheTokenClaims()
    {
        SignIn("player@example.com");

        var user = (await Provider.GetAuthenticationStateAsync()).User;

        user.Identity.ShouldNotBeNull().IsAuthenticated.ShouldBeTrue();
        user.Identity.Name.ShouldBe("player@example.com");
        user.FindFirst("sub").ShouldNotBeNull();
    }

    [Fact]
    public async Task GetAuthenticationState_NothingStored_IsAnonymous()
    {
        var user = (await Provider.GetAuthenticationStateAsync()).User;

        user.Identity.ShouldNotBeNull().IsAuthenticated.ShouldBeFalse();
    }

    [Fact]
    public async Task GetAuthenticationState_AccessTokenExpiredButRefreshTokenValid_IsStillSignedIn()
    {
        SignIn();
        Time.Advance(TimeSpan.FromHours(2));

        var user = (await Provider.GetAuthenticationStateAsync()).User;

        user.Identity.ShouldNotBeNull().IsAuthenticated.ShouldBeTrue();
    }

    [Fact]
    public async Task GetAuthenticationState_RefreshTokenExpired_IsAnonymous()
    {
        SignIn();
        Time.Advance(TimeSpan.FromDays(15));

        var user = (await Provider.GetAuthenticationStateAsync()).User;

        user.Identity.ShouldNotBeNull().IsAuthenticated.ShouldBeFalse();
    }

    [Fact]
    public async Task GetAuthenticationState_MalformedAccessToken_IsAnonymous()
    {
        StoreTokens(new StoredTokens("not-a-jwt", "refresh", Time.GetUtcNow().AddDays(14), Time.GetUtcNow()));

        var user = (await Provider.GetAuthenticationStateAsync()).User;

        user.Identity.ShouldNotBeNull().IsAuthenticated.ShouldBeFalse();
    }
}

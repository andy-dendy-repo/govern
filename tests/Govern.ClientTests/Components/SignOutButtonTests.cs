using System.Net;
using Govern.Client.Components;
using Govern.ClientTests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.ClientTests.Components;

public class SignOutButtonTests : ClientTestContext
{
    [Fact]
    public void Click_SignedIn_RevokesTheSessionClearsItAndGoesHome()
    {
        var tokens = SignIn();
        Api.On(HttpMethod.Post, "/api/users/sign-out", () => FakeApiHandler.Empty(HttpStatusCode.NoContent));
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("profile");
        var cut = Render<SignOutButton>();

        cut.Find("button").Click();

        cut.WaitForAssertion(() => navigation.Uri.ShouldBe("http://localhost/"));
        Api.Requests.ShouldHaveSingleItem().Body.ShouldBe($"{{\"refreshToken\":\"{tokens.RefreshToken}\"}}");
        StoredTokens().ShouldBeNull();
    }

    [Fact]
    public void Click_ApiUnreachable_StillClearsTheSession()
    {
        SignIn();
        Api.On(HttpMethod.Post, "/api/users/sign-out", _ => throw new HttpRequestException("offline"));
        var cut = Render<SignOutButton>();

        cut.Find("button").Click();

        cut.WaitForAssertion(() => StoredTokens().ShouldBeNull());
    }
}

using Govern.Client;
using Govern.ClientTests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.ClientTests;

// Uses the real JwtAuthenticationStateProvider (no bUnit authorization fake), so AuthorizeRouteView decides from storage.
public class AppTests() : ClientTestContext(fakeAuthorization: false)
{
    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    [Fact]
    public void Render_AnonymousOnAnAuthorizedPage_RedirectsToLoginKeepingTheReturnUrlAndFragment()
    {
        Navigation.NavigateTo("profile#section");

        var cut = Render<App>();

        cut.WaitForAssertion(() => Navigation.Uri.ShouldBe("http://localhost/login?returnUrl=%2Fprofile#section"));
    }

    [Fact]
    public void Render_SignedInOnAnAuthorizedPage_ShowsThePageAndTheUser()
    {
        SignIn("player@example.com");
        Api.On(HttpMethod.Get, "/api/users/me",
            () => FakeApiHandler.Json(new { id = Guid.CreateVersion7(), email = "player@example.com", createdAt = Time.GetUtcNow() }));
        Navigation.NavigateTo("profile");

        var cut = Render<App>();

        cut.WaitForAssertion(() => cut.Find("h1").TextContent.ShouldBe("Profile"));
        cut.Find(".rz-header .user-name").TextContent.ShouldBe("player@example.com");
        Navigation.Uri.ShouldBe("http://localhost/profile");
    }
}

using System.Net;
using Govern.Client.Features.Users.Pages;
using Govern.ClientTests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.ClientTests.Features.Users.Pages;

public class ProfilePageTests : ClientTestContext
{
    [Fact]
    public void Render_SignedIn_ShowsTheCurrentUser()
    {
        var tokens = SignIn("player@example.com");
        Api.On(HttpMethod.Get, "/api/users/me",
            () => FakeApiHandler.Json(new { id = Guid.CreateVersion7(), email = "player@example.com", createdAt = Time.GetUtcNow() }));

        var cut = Render<ProfilePage>();

        cut.WaitForAssertion(() => cut.Find(".email").TextContent.ShouldBe("player@example.com"));
        cut.Find(".member-since").TextContent.ShouldStartWith("Member since");
        Api.Requests.ShouldHaveSingleItem().Authorization.ShouldBe($"Bearer {tokens.AccessToken}");
    }

    [Fact]
    public void Render_ApiUnreachable_ShowsTheError()
    {
        SignIn();
        Api.On(HttpMethod.Get, "/api/users/me", _ => throw new HttpRequestException("offline"));

        var cut = Render<ProfilePage>();

        cut.WaitForAssertion(() => cut.Find(".api-error").TextContent.ShouldContain("Could not reach the server"));
    }

    [Fact]
    public void SignOutEverywhere_Click_RevokesAllSessionsClearsThisOneAndGoesHome()
    {
        SignIn();
        Api.On(HttpMethod.Get, "/api/users/me",
            () => FakeApiHandler.Json(new { id = Guid.CreateVersion7(), email = "player@example.com", createdAt = Time.GetUtcNow() }));
        Api.On(HttpMethod.Post, "/api/users/sign-out-everywhere", () => FakeApiHandler.Empty(HttpStatusCode.NoContent));
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("profile");
        var cut = Render<ProfilePage>();

        cut.Find(".sign-out-everywhere").Click();

        cut.WaitForAssertion(() => navigation.Uri.ShouldBe("http://localhost/"));
        Api.RequestsTo("/api/users/sign-out-everywhere").ShouldHaveSingleItem();
        StoredTokens().ShouldBeNull();
    }

    [Fact]
    public void SignOutEverywhere_ApiFails_ShowsTheErrorAndStaysSignedIn()
    {
        var tokens = SignIn();
        Api.On(HttpMethod.Get, "/api/users/me",
            () => FakeApiHandler.Json(new { id = Guid.CreateVersion7(), email = "player@example.com", createdAt = Time.GetUtcNow() }));
        Api.On(HttpMethod.Post, "/api/users/sign-out-everywhere", () => FakeApiHandler.Empty(HttpStatusCode.InternalServerError));
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("profile");
        var cut = Render<ProfilePage>();

        cut.Find(".sign-out-everywhere").Click();

        cut.WaitForAssertion(() => cut.Find(".api-error").TextContent.ShouldContain("Something went wrong on the server"));
        navigation.Uri.ShouldBe("http://localhost/profile");
        StoredTokens().ShouldBe(tokens);
    }
}

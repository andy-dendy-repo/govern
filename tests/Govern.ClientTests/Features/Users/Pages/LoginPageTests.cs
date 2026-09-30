using System.Net;
using Bunit.TestDoubles;
using Govern.Client.Features.Users.Pages;
using Govern.ClientTests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.ClientTests.Features.Users.Pages;

public class LoginPageTests : ClientTestContext
{
    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    [Fact]
    public void Submit_EmptyForm_ShowsRequiredMessagesWithoutCallingTheApi()
    {
        var cut = Render<LoginPage>();

        cut.Find("form").Submit();

        cut.Markup.ShouldContain("Enter your email.");
        cut.Markup.ShouldContain("Enter your password.");
        Api.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void Submit_InvalidCredentials_ShowsTheApiMessage()
    {
        Api.On(HttpMethod.Post, "/api/users/sign-in",
            () => FakeApiHandler.Problem(HttpStatusCode.Unauthorized, "user.invalid_credentials", "Invalid email or password."));
        var cut = Render<LoginPage>();

        FillAndSubmit(cut);

        cut.WaitForAssertion(() => cut.Find(".api-error").TextContent.ShouldContain("Invalid email or password."));
        StoredTokens().ShouldBeNull();
    }

    [Fact]
    public void Submit_RateLimited_ShowsTheRateLimitMessage()
    {
        Api.On(HttpMethod.Post, "/api/users/sign-in", () => FakeApiHandler.Empty(HttpStatusCode.TooManyRequests));
        var cut = Render<LoginPage>();

        FillAndSubmit(cut);

        cut.WaitForAssertion(() => cut.Find(".api-error").TextContent.ShouldContain("Too many requests"));
    }

    [Theory]
    [InlineData("login", "http://localhost/")]
    [InlineData("login?returnUrl=%2Fprofile", "http://localhost/profile")]
    [InlineData("login?returnUrl=%2Fgames%3Fpage%3D2", "http://localhost/games?page=2")]
    [InlineData("login?returnUrl=https%3A%2F%2Fevil.example", "http://localhost/")]
    [InlineData("login?returnUrl=%2F%2Fevil.example", "http://localhost/")]
    [InlineData("login?returnUrl=%2Fjoin#invite-code", "http://localhost/join#invite-code")]
    public void Submit_ValidCredentials_StoresTheSessionAndGoesToTheLocalReturnUrl(string loginUrl, string expected)
    {
        Navigation.NavigateTo(loginUrl);
        Api.On(HttpMethod.Post, "/api/users/sign-in", () => FakeApiHandler.Json(TestTokens.Pair("player@example.com", Time.GetUtcNow())));
        var cut = Render<LoginPage>();

        FillAndSubmit(cut);

        cut.WaitForAssertion(() => Navigation.Uri.ShouldBe(expected));
        StoredTokens().ShouldNotBeNull();
    }

    [Fact]
    public void Submit_ValidCredentials_ReplacesTheLoginEntryThatCarriedTheFragment()
    {
        Navigation.NavigateTo("login?returnUrl=%2Fjoin#invite-code");
        Api.On(HttpMethod.Post, "/api/users/sign-in", () => FakeApiHandler.Json(TestTokens.Pair("player@example.com", Time.GetUtcNow())));
        var cut = Render<LoginPage>();

        FillAndSubmit(cut);

        cut.WaitForAssertion(() => Navigation.Uri.ShouldBe("http://localhost/join#invite-code"));
        var history = ((BunitNavigationManager)Navigation).History;
        history.Select(static entry => entry.Uri).ShouldBe(["/join#invite-code"], "the login entry is replaced, so Back skips it");
        history.Single().Options.ReplaceHistoryEntry.ShouldBeTrue();
    }

    [Fact]
    public void Render_WithReturnUrlAndFragment_RegisterLinkKeepsBoth()
    {
        Navigation.NavigateTo("login?returnUrl=%2Fjoin#invite-code");

        var cut = Render<LoginPage>();

        cut.Find("a[href^='register']").GetAttribute("href").ShouldBe("register?returnUrl=%2Fjoin#invite-code");
    }

    [Fact]
    public void Render_AfterRegistering_ShowsTheAccountCreatedNotice()
    {
        Navigation.NavigateTo("login?registered=true");

        var cut = Render<LoginPage>();

        cut.Find(".registered").TextContent.ShouldContain("Your account was created.");
    }

    private static void FillAndSubmit(IRenderedComponent<LoginPage> cut)
    {
        cut.Find("input[name=Email]").Change("player@example.com");
        cut.Find("input[name=Password]").Change("password1");
        cut.Find("form").Submit();
    }
}

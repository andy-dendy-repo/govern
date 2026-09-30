using System.Net;
using Govern.Client.Features.Users.Pages;
using Govern.ClientTests.Support;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.ClientTests.Features.Users.Pages;

public class RegisterPageTests : ClientTestContext
{
    private BunitNavigationManager Navigation => (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

    [Theory]
    [InlineData("not-an-email", "password1", "password1", "Enter a valid email address.")]
    [InlineData("new@example.com", "short", "short", "Use 8 to 128 characters.")]
    [InlineData("new@example.com", "password1", "password2", "The passwords do not match.")]
    public void Submit_InvalidForm_ShowsTheMessageWithoutCallingTheApi(string email, string password, string confirm, string message)
    {
        var cut = Render<RegisterPage>();

        FillAndSubmit(cut, email, password, confirm);

        cut.Markup.ShouldContain(message);
        Api.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void Submit_EmailTaken_ShowsTheApiMessage()
    {
        Api.On(HttpMethod.Post, "/api/users/sign-up",
            () => FakeApiHandler.Problem(HttpStatusCode.Conflict, "user.email_taken", "An account with this email already exists."));
        var cut = Render<RegisterPage>();

        FillAndSubmit(cut);

        cut.WaitForAssertion(() => cut.Find(".api-error").TextContent.ShouldContain("An account with this email already exists."));
    }

    [Fact]
    public void Submit_Valid_SignsUpThenSignsInAndGoesToTheReturnUrl()
    {
        Navigation.NavigateTo("register?returnUrl=%2Fprofile");
        Api.On(HttpMethod.Post, "/api/users/sign-up", () => FakeApiHandler.Json(new { userId = Guid.CreateVersion7() }, HttpStatusCode.Created));
        Api.On(HttpMethod.Post, "/api/users/sign-in", () => FakeApiHandler.Json(TestTokens.Pair("new@example.com", Time.GetUtcNow())));
        var cut = Render<RegisterPage>();

        FillAndSubmit(cut);

        cut.WaitForAssertion(() => Navigation.Uri.ShouldBe("http://localhost/profile"));
        Api.Requests.Select(static request => request.Path).ShouldBe(["/api/users/sign-up", "/api/users/sign-in"]);
        StoredTokens().ShouldNotBeNull();
        Navigation.History.Single().Options.ReplaceHistoryEntry.ShouldBeTrue("Back must not return to the register form");
    }

    [Fact]
    public void Submit_AccountCreatedButSignInFails_GoesToLoginKeepingTheReturnUrl()
    {
        Navigation.NavigateTo("register?returnUrl=%2Fjoin#invite-code");
        Api.On(HttpMethod.Post, "/api/users/sign-up", () => FakeApiHandler.Json(new { userId = Guid.CreateVersion7() }, HttpStatusCode.Created));
        Api.On(HttpMethod.Post, "/api/users/sign-in", () => FakeApiHandler.Empty(HttpStatusCode.TooManyRequests));
        var cut = Render<RegisterPage>();

        FillAndSubmit(cut);

        cut.WaitForAssertion(() => Navigation.Uri.ShouldBe("http://localhost/login?registered=true&returnUrl=%2Fjoin#invite-code"));
        Navigation.History.Single().Options.ReplaceHistoryEntry.ShouldBeTrue("the register entry carried the invite code");
    }

    private static void FillAndSubmit(IRenderedComponent<RegisterPage> cut, string email = "new@example.com",
        string password = "password1", string confirm = "password1")
    {
        cut.Find("input[name=Email]").Change(email);
        cut.Find("input[name=Password]").Change(password);
        cut.Find("input[name=ConfirmPassword]").Change(confirm);
        cut.Find("form").Submit();
    }
}

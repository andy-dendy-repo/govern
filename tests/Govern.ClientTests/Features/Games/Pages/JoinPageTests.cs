using System.Net;
using Bunit.TestDoubles;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Pages;
using Govern.ClientTests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.ClientTests.Features.Games.Pages;

public class JoinPageTests : ClientTestContext
{
    private static readonly string GameUri = $"http://localhost/games/{TestGames.Id}";

    public JoinPageTests() => SignIn();

    private BunitNavigationManager Navigation => (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

    [Fact]
    public void Render_CodeInFragment_DropsItFromTheUrlAndFillsTheFormWithoutJoining()
    {
        Navigation.NavigateTo($"join#{TestGames.InviteCode}");

        var cut = Render<JoinPage>();

        cut.Find("input[name=InviteCode]").GetAttribute("value").ShouldBe(TestGames.InviteCode);
        cut.Find(".invited").TextContent.ShouldContain("You were invited");
        Api.Requests.ShouldBeEmpty("opening a link (which another site can do) must not join a game on its own");
        // The navigation replaced the entry that carried the code, so no entry with a fragment is left.
        Navigation.History.Select(static entry => entry.Uri).ShouldBe(["join"]);
        Navigation.History.Single().Options.ReplaceHistoryEntry.ShouldBeTrue();
    }

    [Fact]
    public void Submit_AfterOpeningTheLink_JoinsWithTheCodeInTheBodyAndReplacesTheEntry()
    {
        Navigation.NavigateTo($"join#{TestGames.InviteCode}");
        string? urlAtJoin = null;
        Api.On(HttpMethod.Post, "/api/games/join", _ =>
        {
            urlAtJoin = Navigation.Uri;
            return Task.FromResult(FakeApiHandler.Json(new JoinGameResponse(TestGames.Id)));
        });
        var cut = Render<JoinPage>();

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Navigation.Uri.ShouldBe(GameUri));
        urlAtJoin.ShouldBe("http://localhost/join");
        Api.Requests.Single().Body.ShouldBe($$"""{"inviteCode":"{{TestGames.InviteCode}}"}""");
        Navigation.History.Select(static entry => entry.Uri).ShouldBe([$"games/{TestGames.Id}"]);
    }

    [Fact]
    public void Submit_CodeRejected_ShowsTheErrorAndKeepsTheCode()
    {
        Navigation.NavigateTo($"join#{TestGames.InviteCode}");
        Api.On(HttpMethod.Post, "/api/games/join",
            () => FakeApiHandler.Problem(HttpStatusCode.NotFound, "game.invite_not_found", "No game uses this invite code."));
        var cut = Render<JoinPage>();

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Find(".api-error").TextContent.ShouldContain("No game uses this invite code."));
        cut.Find("input[name=InviteCode]").GetAttribute("value").ShouldBe(TestGames.InviteCode);
        Navigation.Uri.ShouldBe("http://localhost/join");
    }

    [Fact]
    public void Render_MalformedFragment_ShowsTheInvalidLinkNoticeAndLeavesTheFormEmpty()
    {
        Navigation.NavigateTo("join#Contact%20support%20at%20evil.example");

        var cut = Render<JoinPage>();

        cut.Find(".invalid-link").TextContent.ShouldContain("not valid");
        cut.Find("input[name=InviteCode]").GetAttribute("value").ShouldBeNullOrEmpty();
        Api.Requests.ShouldBeEmpty();
        Navigation.Uri.ShouldBe("http://localhost/join");
    }

    [Fact]
    public void Render_NoFragment_JoinsNothing()
    {
        Navigation.NavigateTo("join");

        var cut = Render<JoinPage>();

        cut.FindAll(".invited, .invalid-link").ShouldBeEmpty();
        Api.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void Submit_PastedLink_JoinsWithItsCode()
    {
        Navigation.NavigateTo("join");
        Api.On(HttpMethod.Post, "/api/games/join", () => FakeApiHandler.Json(new JoinGameResponse(TestGames.Id)));
        var cut = Render<JoinPage>();

        cut.Find("input[name=InviteCode]").Change($" https://govern.example/join#{TestGames.InviteCode} ");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Navigation.Uri.ShouldBe(GameUri));
        Api.Requests.Single().Body.ShouldBe($$"""{"inviteCode":"{{TestGames.InviteCode}}"}""");
    }

    [Theory]
    [InlineData("", "Paste the invite link or code.")]
    [InlineData("not a code", "This is not an invite link or code.")]
    public void Submit_Invalid_ShowsValidationWithoutJoining(string input, string message)
    {
        Navigation.NavigateTo("join");
        var cut = Render<JoinPage>();

        cut.Find("input[name=InviteCode]").Change(input);
        cut.Find("form").Submit();

        cut.Markup.ShouldContain(message);
        Api.Requests.ShouldBeEmpty();
    }
}

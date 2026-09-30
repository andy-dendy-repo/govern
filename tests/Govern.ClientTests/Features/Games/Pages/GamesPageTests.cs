using System.Net;
using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Pages;
using Govern.ClientTests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.ClientTests.Features.Games.Pages;

public class GamesPageTests : ClientTestContext
{
    private const string FirstPage = "/api/games?page=1&pageSize=20";

    public GamesPageTests() => SignIn();

    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    [Fact]
    public void Render_Games_ShowsOneRowPerGame()
    {
        Api.On(HttpMethod.Get, FirstPage, () => FakeApiHandler.Json(new ListMyGamesResponse(
            [
                new MyGameItem(TestGames.Id, GamePhase.InProgress, 2, false, true, TestGames.CreatedAt),
                new MyGameItem(Guid.CreateVersion7(), GamePhase.Lobby, 1, true, false, TestGames.CreatedAt),
            ], 2)));

        var cut = Render<GamesPage>();

        cut.WaitForAssertion(() => cut.FindAll("a.open-game").Count.ShouldBe(2));
        var first = cut.FindAll("tr.rz-data-row")[0].TextContent;
        first.ShouldContain("In progress");
        first.ShouldContain("2/2");
        first.ShouldContain("Joined");
        first.ShouldContain("Your turn");
        cut.Find("a.open-game").GetAttribute("href").ShouldBe($"games/{TestGames.Id}");
    }

    [Fact]
    public void Render_NoGames_ShowsTheEmptyText()
    {
        Api.On(HttpMethod.Get, FirstPage, () => FakeApiHandler.Json(new ListMyGamesResponse([], 0)));

        var cut = Render<GamesPage>();

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No games yet."));
    }

    [Fact]
    public void Pager_NextPage_LoadsPageTwo()
    {
        var items = Enumerable.Range(0, 20)
            .Select(_ => new MyGameItem(Guid.CreateVersion7(), GamePhase.Lobby, 1, true, false, TestGames.CreatedAt)).ToList();
        Api.On(HttpMethod.Get, FirstPage, () => FakeApiHandler.Json(new ListMyGamesResponse(items, 21)));
        Api.On(HttpMethod.Get, "/api/games?page=2&pageSize=20", () => FakeApiHandler.Json(new ListMyGamesResponse([items[0]], 21)));
        var cut = Render<GamesPage>();
        cut.WaitForAssertion(() => cut.FindAll("a.open-game").Count.ShouldBe(20));

        cut.Find(".rz-pager-next").Click();

        cut.WaitForAssertion(() => cut.FindAll("a.open-game").Count.ShouldBe(1));
    }

    [Fact]
    public void NewGame_Created_GoesToTheGame()
    {
        Api.On(HttpMethod.Get, FirstPage, () => FakeApiHandler.Json(new ListMyGamesResponse([], 0)));
        Api.On(HttpMethod.Post, "/api/games", () => FakeApiHandler.Json(new CreateGameResponse(TestGames.Id, TestGames.InviteCode), HttpStatusCode.Created));
        var cut = Render<GamesPage>();

        cut.Find("button.new-game").Click();

        cut.WaitForAssertion(() => Navigation.Uri.ShouldBe($"http://localhost/games/{TestGames.Id}"));
    }

    [Fact]
    public void NewGame_CapReached_ShowsTheApiMessage()
    {
        Api.On(HttpMethod.Get, FirstPage, () => FakeApiHandler.Json(new ListMyGamesResponse([], 0)));
        Api.On(HttpMethod.Post, "/api/games", () => FakeApiHandler.Problem(HttpStatusCode.Conflict, "game.too_many_open_lobbies",
            "You already have 3 open lobbies. Start or cancel one before creating another."));
        var cut = Render<GamesPage>();

        cut.Find("button.new-game").Click();

        cut.WaitForAssertion(() => cut.Find(".api-error").TextContent.ShouldContain("3 open lobbies"));
        Navigation.Uri.ShouldBe("http://localhost/");
    }
}

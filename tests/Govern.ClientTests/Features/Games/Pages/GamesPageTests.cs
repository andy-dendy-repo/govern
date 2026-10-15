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
                new MyGameItem(TestGames.Id, GamePhase.InProgress, 2, 2, false, true, false, TestGames.CreatedAt),
                new MyGameItem(Guid.CreateVersion7(), GamePhase.Lobby, 1, 2, true, false, false, TestGames.CreatedAt),
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
            .Select(_ => new MyGameItem(Guid.CreateVersion7(), GamePhase.Lobby, 1, 2, true, false, false, TestGames.CreatedAt)).ToList();
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
    public void NewGame_ByDefault_AsksForARandomPoolMap()
    {
        Api.On(HttpMethod.Get, FirstPage, () => FakeApiHandler.Json(new ListMyGamesResponse([], 0)));
        Api.On(HttpMethod.Post, "/api/games", () => FakeApiHandler.Json(new CreateGameResponse(TestGames.Id, TestGames.InviteCode), HttpStatusCode.Created));
        var cut = Render<GamesPage>();

        cut.Find("button.new-game").Click();

        cut.WaitForAssertion(() => Api.RequestsTo("/api/games").Single(request => request.Method == HttpMethod.Post).Body.ShouldBe("""{"map":"random","seats":2,"turnMode":"sequential"}"""));
    }

    [Fact]
    public void NewGame_EveryoneAtOnce_AsksForSimultaneousTurns()
    {
        Api.On(HttpMethod.Get, FirstPage, () => FakeApiHandler.Json(new ListMyGamesResponse([], 0)));
        Api.On(HttpMethod.Post, "/api/games", () => FakeApiHandler.Json(new CreateGameResponse(TestGames.Id, TestGames.InviteCode), HttpStatusCode.Created));
        var cut = Render<GamesPage>();
        cut.FindAll(".turn-choice .rz-dropdown-item").Select(static item => item.TextContent.Trim())
            .ShouldBe(["One at a time", "Everyone at once"]);

        cut.FindAll(".turn-choice .rz-dropdown-item")[1].Click();
        cut.Find("button.new-game").Click();

        cut.WaitForAssertion(() => Api.RequestsTo("/api/games").Single(request => request.Method == HttpMethod.Post).Body
            .ShouldBe("""{"map":"random","seats":2,"turnMode":"simultaneous"}"""));
    }

    [Fact]
    public void NewGame_APickedMap_AsksForIt()
    {
        Api.On(HttpMethod.Get, FirstPage, () => FakeApiHandler.Json(new ListMyGamesResponse([], 0)));
        Api.On(HttpMethod.Post, "/api/games", () => FakeApiHandler.Json(new CreateGameResponse(TestGames.Id, TestGames.InviteCode), HttpStatusCode.Created));
        var cut = Render<GamesPage>();
        cut.FindAll(".map-choice .rz-dropdown-item").Select(static item => item.TextContent.Trim())
            .ShouldBe(["Random map from the pool", "The Basin", "Generated map"]);

        cut.FindAll(".map-choice .rz-dropdown-item")[1].Click();
        cut.Find("button.new-game").Click();

        cut.WaitForAssertion(() => Api.RequestsTo("/api/games").Single(request => request.Method == HttpMethod.Post).Body.ShouldBe("""{"map":"basin","seats":2,"turnMode":"sequential"}"""));
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

    [Fact]
    public void NewGame_ForThreePlayers_OffersOnlyMapsForThreeAndAsksForThem()
    {
        Api.On(HttpMethod.Get, FirstPage, () => FakeApiHandler.Json(new ListMyGamesResponse([], 0)));
        Api.On(HttpMethod.Post, "/api/games", () => FakeApiHandler.Json(new CreateGameResponse(TestGames.Id, TestGames.InviteCode), HttpStatusCode.Created));
        var cut = Render<GamesPage>();
        cut.FindAll(".seat-choice .rz-dropdown-item").Select(static item => item.TextContent.Trim()).ShouldBe(["2 players", "3 players", "4 players"]);
        cut.FindAll(".map-choice .rz-dropdown-item")[1].Click();

        cut.FindAll(".seat-choice .rz-dropdown-item")[1].Click();
        cut.Find("button.new-game").Click();

        cut.FindAll(".map-choice .rz-dropdown-item").Select(static item => item.TextContent.Trim()).ShouldBe(["Random map from the pool", "Generated map"]);
        cut.WaitForAssertion(() => Api.RequestsTo("/api/games").Single(request => request.Method == HttpMethod.Post).Body.ShouldBe("""{"map":"random","seats":3,"turnMode":"sequential"}"""));
    }

    [Fact]
    public void Render_AGameYouLeftWhileItGoesOn_SaysYoureOut()
    {
        Api.On(HttpMethod.Get, FirstPage, () => FakeApiHandler.Json(new ListMyGamesResponse(
            [new MyGameItem(TestGames.Id, GamePhase.InProgress, 3, 3, false, false, true, DateTimeOffset.UnixEpoch)], 1)));

        var cut = Render<GamesPage>();

        cut.WaitForAssertion(() => cut.Find("tbody").TextContent.ShouldContain("You're out"));
        cut.Find("tbody").TextContent.ShouldContain("3/3");
    }
}

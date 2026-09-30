using System.Net;
using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Pages;
using Govern.ClientTests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.ClientTests.Features.Games.Pages;

public class GamePageTests : ClientTestContext
{
    public GamePageTests() => SignIn();

    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    [Fact]
    public async Task Render_CreatorLobby_ShowsTheInviteLinkAndLiveBadge()
    {
        var stream = await ServeAsync(TestGames.CreatorLobby());

        var cut = RenderGame();

        cut.WaitForAssertion(() => cut.Find(".connection").TextContent.Trim().ShouldBe("Live"));
        cut.Find("h1").TextContent.ShouldBe("Lobby");
        cut.Find(".invite-link").TextContent.ShouldBe($"http://localhost/join#{TestGames.InviteCode}");
        cut.Find("button.start").HasAttribute("disabled").ShouldBeTrue();
        stream.Complete();
    }

    [Fact]
    public async Task Render_JoinerLobby_OffersLeaveWithoutTheCode()
    {
        await ServeAsync(TestGames.JoinerLobby());

        var cut = RenderGame();

        cut.WaitForAssertion(() => cut.FindAll("button.leave").Count.ShouldBe(1));
        cut.FindAll(".invite-share, button.start, button.cancel").ShouldBeEmpty();
    }

    [Fact]
    public async Task Stream_SecondPlayerJoins_EnablesStart()
    {
        var stream = await ServeAsync(TestGames.CreatorLobby());
        var cut = RenderGame();
        cut.WaitForAssertion(() => cut.Find(".connection").TextContent.Trim().ShouldBe("Live"));

        await stream.WriteGameAsync(TestGames.CreatorLobby(players: 2));

        cut.WaitForAssertion(() => cut.FindAll(".player:not(.waiting)").Count.ShouldBe(2));
        cut.Find("button.start").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public async Task Stream_GameStarted_ShowsTheBoard()
    {
        var stream = await ServeAsync(TestGames.CreatorLobby(players: 2));
        var cut = RenderGame();
        cut.WaitForAssertion(() => cut.Find(".connection").TextContent.Trim().ShouldBe("Live"));

        await stream.WriteGameAsync(TestGames.Started());

        cut.WaitForAssertion(() => cut.FindAll("use.tile").Count.ShouldBe(TestViews.Width * TestViews.Height));
        cut.Find("h1").TextContent.ShouldBe("Game in progress");
        cut.Find(".turn").TextContent.ShouldContain("your turn");
        cut.FindAll(".lobby-actions, .invite-share").ShouldBeEmpty();
    }

    [Fact]
    public async Task Stream_NewSnapshot_UpdatesTheBoard()
    {
        var stream = await ServeAsync(TestGames.Started());
        var cut = RenderGame();
        cut.WaitForAssertion(() => cut.FindAll("use.unit").Count.ShouldBe(3));

        await stream.WriteGameAsync(TestGames.Started(view: TestViews.Start() with { CurrentPlayer = TestViews.Opponent, Units = [] }));

        cut.WaitForAssertion(() => cut.FindAll("use.unit").ShouldBeEmpty());
        cut.Find(".turn").TextContent.ShouldContain("opponent's turn");
    }

    [Fact]
    public async Task Command_Applied_ShowsTheAnsweredView()
    {
        await ServeAsync(TestGames.Started());
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () =>
            FakeApiHandler.Json(TestGames.Started(view: TestViews.Start() with { CurrentPlayer = TestViews.Opponent })));
        var cut = RenderGame();
        cut.WaitForAssertion(() => cut.Find("button.end-turn"));

        cut.Find("button.end-turn").Click();

        cut.WaitForAssertion(() => cut.Find(".turn").TextContent.ShouldContain("opponent's turn"));
    }

    [Fact]
    public async Task Render_Finished_ShowsGameOver()
    {
        await ServeAsync(TestGames.Started(GamePhase.Finished));

        var cut = RenderGame();

        cut.WaitForAssertion(() => cut.Find(".game-over .result").TextContent.ShouldBe("You won!"));
        cut.Find("h1").TextContent.ShouldBe("Game over");
    }

    [Fact]
    public async Task Cancel_Confirmed_GoesToMyGames()
    {
        await ServeAsync(TestGames.CreatorLobby());
        Api.On(HttpMethod.Delete, TestGames.GamePath(), () => FakeApiHandler.Empty(HttpStatusCode.NoContent));
        var cut = RenderGame();
        cut.WaitForAssertion(() => cut.Find("button.cancel"));

        cut.Find("button.cancel").Click();
        cut.Find("button.confirm").Click();

        cut.WaitForAssertion(() => Navigation.Uri.ShouldBe("http://localhost/games"));
    }

    [Fact]
    public void Stream_GameDeleted_ShowsGone()
    {
        Api.On(HttpMethod.Get, TestGames.GamePath(), () => FakeApiHandler.Json(TestGames.JoinerLobby()));
        Api.On(HttpMethod.Get, TestGames.StreamPath(), () => FakeApiHandler.Empty(HttpStatusCode.NotFound));

        var cut = RenderGame();

        cut.WaitForAssertion(() => cut.Find(".gone").TextContent.ShouldContain("cancelled or expired"));
        cut.Find(".connection").TextContent.Trim().ShouldBe("Gone");
    }

    [Fact]
    public void Render_UnknownGame_ShowsNotFoundWithoutWatching()
    {
        Api.On(HttpMethod.Get, TestGames.GamePath(), () => FakeApiHandler.Problem(HttpStatusCode.NotFound, "game.not_found", "The game was not found."));

        var cut = RenderGame();

        cut.WaitForAssertion(() => cut.Find(".gone").TextContent.ShouldContain("does not exist"));
        Api.RequestsTo(TestGames.StreamPath()).ShouldBeEmpty();
    }

    [Fact]
    public async Task GameId_Changed_WatchesTheNewGameAndDropsTheOldStream()
    {
        var otherId = Guid.CreateVersion7();
        var first = await ServeAsync(TestGames.CreatorLobby());
        await ServeAsync(TestGames.CreatorLobby(id: otherId), otherId);
        var cut = RenderGame();
        cut.WaitForAssertion(() => cut.Find(".connection").TextContent.Trim().ShouldBe("Live"));

        cut.Render(parameters => parameters.Add(static page => page.GameId, otherId));

        cut.WaitForAssertion(() => Api.RequestsTo(TestGames.StreamPath(otherId)).Count.ShouldBe(1));
        first.Complete();
        Time.Advance(TimeSpan.FromMinutes(1));
        await Eventually.NeverAsync(() => Api.RequestsTo(TestGames.StreamPath()).Count > 1, "the old game's watch was stopped");
    }

    [Fact]
    public async Task GameId_ChangedWhileACommandIsInFlight_ShowsOnlyTheNewGame()
    {
        var otherId = Guid.CreateVersion7();
        var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        await ServeAsync(TestGames.Started());
        await ServeAsync(TestGames.Started(view: TestViews.Create()) with { Id = otherId }, otherId);
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), _ => response.Task);
        var cut = RenderGame();
        cut.WaitForAssertion(() => cut.Find("button.end-turn"));
        cut.Find("button.end-turn").Click();
        await Eventually.HoldsAsync(() => Api.RequestsTo(TestGames.CommandsPath()).Count == 1, "the command is sent");

        cut.Render(parameters => parameters.Add(static page => page.GameId, otherId));
        cut.WaitForAssertion(() => cut.FindAll("use.unit").ShouldBeEmpty());
        response.SetResult(FakeApiHandler.Json(TestGames.Started()));

        await Eventually.NeverAsync(() => cut.FindAll("use.unit").Count > 0, "the old game's answer must not replace the new game");
        cut.Find("button.end-turn").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public async Task Dispose_WhileTheGameIsLoading_NeverOpensTheStream()
    {
        var game = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        Api.On(HttpMethod.Get, TestGames.GamePath(), _ => game.Task);
        Api.On(HttpMethod.Get, TestGames.StreamPath(), () => new SseResponse().Response);
        RenderGame();
        await Eventually.HoldsAsync(() => Api.RequestsTo(TestGames.GamePath()).Count == 1, "the page reads the game");

        await DisposeComponentsAsync();
        game.SetResult(FakeApiHandler.Json(TestGames.CreatorLobby()));

        await Eventually.NeverAsync(() => Api.RequestsTo(TestGames.StreamPath()).Count > 0, "a left page must not start watching");
    }

    [Fact]
    public async Task Dispose_StopsWatching()
    {
        var stream = await ServeAsync(TestGames.CreatorLobby());
        var cut = RenderGame();
        cut.WaitForAssertion(() => cut.Find(".connection").TextContent.Trim().ShouldBe("Live"));

        await DisposeComponentsAsync();
        stream.Complete();
        Time.Advance(TimeSpan.FromMinutes(1));

        await Eventually.NeverAsync(() => Api.RequestsTo(TestGames.StreamPath()).Count > 1, "a left page no longer watches the game");
    }

    private async Task<SseResponse> ServeAsync(GetGameResponse game, Guid? id = null)
    {
        var stream = new SseResponse();
        Api.On(HttpMethod.Get, TestGames.GamePath(id), () => FakeApiHandler.Json(game));
        Api.On(HttpMethod.Get, TestGames.StreamPath(id), () => stream.Response);
        await stream.WriteGameAsync(game);
        return stream;
    }

    private IRenderedComponent<GamePage> RenderGame() =>
        Render<GamePage>(parameters => parameters.Add(static page => page.GameId, TestGames.Id));
}

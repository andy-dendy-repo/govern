using System.Net;
using System.Text.Json.Nodes;
using AngleSharp.Dom;
using Govern.Client.Features.Games.Components;
using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.ClientTests.Support;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components.Web;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Components;

public class GameTableTests : ClientTestContext
{
    private readonly List<GetGameResponse> _changed = [];

    public GameTableTests() => SignIn();

    [Fact]
    public void Move_SettlerToAReachableTile_SendsTheMoveWithTheViewVersion()
    {
        var after = TestGames.Started(view: Create([Settler(1, You, At(1, 1), movementLeft: 1)]));
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(after));
        var cut = RenderTable(TestGames.Started());

        Tile(cut, At(2, 2)).Click();
        cut.FindAll("use.overlay.reachable").ShouldNotBeEmpty();
        Tile(cut, At(1, 1)).Click();

        cut.WaitForAssertion(() => _changed.Count.ShouldBe(1));
        _changed.Single().View!.Units.Single().Position.ShouldBe(At(1, 1));
        var expected = JsonNode.Parse(
            $$"""{"expectedVersion":{{TestViews.Version}},"command":{"type":"move_unit","unitId":{"value":1},"target":{"q":{{At(1, 1).Q}},"r":{{At(1, 1).R}} } } }""");
        JsonNode.DeepEquals(Command(0), expected).ShouldBeTrue(Command(0).ToJsonString());
    }

    [Fact]
    public void Move_Applied_KeepsTheUnitSelectedOnTheNewView()
    {
        var after = TestGames.Started(view: Create([Settler(1, You, At(1, 1), movementLeft: 1)]));
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(after));
        var cut = RenderTable(TestGames.Started());
        Tile(cut, At(2, 2)).Click();
        Tile(cut, At(1, 1)).Click();
        cut.WaitForAssertion(() => _changed.Count.ShouldBe(1));

        cut.Render(parameters => parameters.Add(static table => table.Game, after));

        cut.Find(".unit-panel .movement").TextContent.ShouldContain("1 of 2");
    }

    [Fact]
    public void Command_Stale_ReloadsTheGameAndSaysNothingWasApplied()
    {
        var current = TestGames.Started(view: Start() with { CurrentPlayer = Opponent });
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Problem(HttpStatusCode.Conflict, "game.stale_version", "The game changed."));
        Api.On(HttpMethod.Get, TestGames.GamePath(), () => FakeApiHandler.Json(current));
        var cut = RenderTable(TestGames.Started());

        cut.Find("button.end-turn").Click();

        cut.WaitForAssertion(() => _changed.Count.ShouldBe(1));
        cut.Find(".stale").TextContent.ShouldContain("nothing was applied");
        cut.FindAll(".api-error").ShouldBeEmpty();
        _changed.Single().View!.CurrentPlayer.ShouldBe(Opponent);
    }

    [Fact]
    public void Command_RuleViolation_ShowsTheError()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Problem(HttpStatusCode.Conflict, "game.not_your_turn", "It is not your turn."));
        var cut = RenderTable(TestGames.Started());

        cut.Find("button.end-turn").Click();

        cut.WaitForAssertion(() => cut.Find(".api-error").TextContent.ShouldContain("It is not your turn."));
        _changed.ShouldBeEmpty();
    }

    [Fact]
    public async Task Command_InFlight_BlocksAnotherOne()
    {
        var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), _ => response.Task);
        var cut = RenderTable(TestGames.Started());

        cut.Find("button.end-turn").Click();
        cut.WaitForAssertion(() => cut.Find("button.end-turn").HasAttribute("disabled").ShouldBeTrue());
        Tile(cut, At(2, 2)).Click();
        Tile(cut, At(1, 1)).Click();

        Api.RequestsTo(TestGames.CommandsPath()).Count.ShouldBe(1);
        cut.Find("svg.board").ClassList.ShouldContain("busy");
        response.SetResult(FakeApiHandler.Json(TestGames.Started()));
        await Eventually.HoldsAsync(() => _changed.Count == 1, "the first command finishes");
        cut.WaitForAssertion(() => cut.Find("button.end-turn").HasAttribute("disabled").ShouldBeFalse());
    }

    [Fact]
    public void Command_StaleAndTheReloadFails_ShowsTheReloadError()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Problem(HttpStatusCode.Conflict, "game.stale_version", "The game changed."));
        Api.On(HttpMethod.Get, TestGames.GamePath(), () => FakeApiHandler.Empty(HttpStatusCode.ServiceUnavailable));
        var cut = RenderTable(TestGames.Started());

        cut.Find("button.end-turn").Click();

        cut.WaitForAssertion(() => cut.FindAll(".api-error").Count.ShouldBe(1));
        cut.FindAll(".stale").ShouldBeEmpty();
        _changed.ShouldBeEmpty();
    }

    [Fact]
    public void Render_NewSnapshotAfterAnError_ClearsTheError()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Problem(HttpStatusCode.Conflict, "game.not_your_turn", "It is not your turn."));
        var cut = RenderTable(TestGames.Started());
        cut.Find("button.end-turn").Click();
        cut.WaitForAssertion(() => cut.FindAll(".api-error").Count.ShouldBe(1));

        cut.Render(parameters => parameters.Add(static table => table.Game, TestGames.Started(view: Start() with { Turn = 4 })));

        cut.FindAll(".api-error").ShouldBeEmpty();
    }

    [Fact]
    public async Task Dispose_WhileACommandIsInFlight_NeverReportsItsAnswer()
    {
        var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), _ => response.Task);
        var cut = RenderTable(TestGames.Started());
        cut.Find("button.end-turn").Click();
        await Eventually.HoldsAsync(() => Api.RequestsTo(TestGames.CommandsPath()).Count == 1, "the command is sent");

        await DisposeComponentsAsync();
        response.SetResult(FakeApiHandler.Json(TestGames.Started()));

        await Eventually.NeverAsync(() => _changed.Count > 0, "a left table must not report an answer");
    }

    [Fact]
    public void EndTurn_Clicked_SendsEndTurn()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(TestGames.Started()));
        var cut = RenderTable(TestGames.Started());

        cut.Find("button.end-turn").Click();

        cut.WaitForAssertion(() => Command(0)["command"]!["type"]!.GetValue<string>().ShouldBe("end_turn"));
    }

    [Fact]
    public void Render_OpponentsTurn_DisablesEndTurnButAllowsResign()
    {
        var cut = RenderTable(TestGames.Started(view: Start() with { CurrentPlayer = Opponent }));

        cut.Find(".turn").TextContent.ShouldContain("opponent's turn");
        cut.Find("button.end-turn").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("button.resign").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void Resign_NeedsASecondClick()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(TestGames.Started(GamePhase.Finished)));
        var cut = RenderTable(TestGames.Started());

        cut.Find("button.resign").Click();
        Api.Requests.ShouldBeEmpty();
        cut.Find(".confirm-text").TextContent.ShouldContain("Resign the game?");
        cut.Find("button.confirm-resign").Click();

        cut.WaitForAssertion(() => Command(0)["command"]!["type"]!.GetValue<string>().ShouldBe("resign"));
    }

    [Fact]
    public void Resign_KeepPlaying_SendsNothing()
    {
        var cut = RenderTable(TestGames.Started());

        cut.Find("button.resign").Click();
        cut.Find("button.dismiss").Click();

        cut.FindAll("button.end-turn").Count.ShouldBe(1);
        Api.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void FoundCity_TooCloseToACity_IsDisabledWithTheReason()
    {
        var cut = RenderTable(TestGames.Started(view: Create([Settler(1, You, At(2, 2))], [City(7, You, At(4, 2))])));

        Tile(cut, At(2, 2)).Click();

        cut.Find("button.found-city").HasAttribute("disabled").ShouldBeTrue();
        cut.Find(".found-city-reason").TextContent.ShouldBe("Another city is too close.");
    }

    [Fact]
    public void FoundCity_Allowed_SendsIt()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(TestGames.Started()));
        var cut = RenderTable(TestGames.Started());

        Tile(cut, At(2, 2)).Click();
        cut.Find("button.found-city").Click();

        cut.WaitForAssertion(() => Command(0)["command"]!.ToJsonString().ShouldBe("""{"type":"found_city","unitId":{"value":1}}"""));
    }

    [Fact]
    public void Render_OwnCitySelected_ShowsProductionAndChangesIt()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(TestGames.Started()));
        var cut = RenderTable(TestGames.Started(view: Create(cities: [City(7, You, At(2, 2), "Home")])));

        Tile(cut, At(2, 2)).Click();

        cut.Find(".city-panel .city-name").TextContent.ShouldBe("Home");
        cut.Find(".production").TextContent.ShouldBe("Producing a Settler: 2 of 6");
        cut.Find("button.produce-settler").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("button.produce-soldier").Click();
        cut.WaitForAssertion(() => Command(0)["command"]!.ToJsonString()
            .ShouldBe("""{"type":"set_production","cityId":{"value":7},"unitType":"soldier"}"""));
    }

    [Fact]
    public void Render_EnemyCitySelected_ShowsOnlyItsName()
    {
        var cut = RenderTable(TestGames.Started(view: Create(cities: [City(8, Opponent, At(5, 4), "Aria")])));

        Tile(cut, At(5, 4)).Click();

        cut.Find(".city-panel .city-name").TextContent.ShouldBe("Aria");
        cut.FindAll(".enemy-city").Count.ShouldBe(1);
        cut.FindAll(".production, button.produce-settler, button.produce-soldier").ShouldBeEmpty();
    }

    [Fact]
    public void Render_EnemyUnitSelected_ShowsNoActions()
    {
        var cut = RenderTable(TestGames.Started());

        Tile(cut, At(5, 2)).Click();

        cut.Find(".unit-name").TextContent.Trim().ShouldBe("Enemy Soldier");
        cut.FindAll(".movement, button.found-city").ShouldBeEmpty();
        cut.FindAll("use.overlay").ShouldBeEmpty();
    }

    [Fact]
    public void Render_NewViewWithoutTheSelectedUnit_DropsTheSelection()
    {
        var cut = RenderTable(TestGames.Started());
        Tile(cut, At(3, 2)).Click();
        cut.FindAll(".unit-panel").Count.ShouldBe(1);

        cut.Render(parameters => parameters.Add(static table => table.Game, TestGames.Started(view: Create([Settler(1, You, At(2, 2))]))));

        cut.FindAll(".unit-panel").ShouldBeEmpty();
        cut.FindAll("use.overlay").ShouldBeEmpty();
    }

    [Fact]
    public void Render_Finished_ShowsTheResultAndNoTurnActions()
    {
        var cut = RenderTable(TestGames.Started(GamePhase.Finished));

        cut.Find(".game-over .result").TextContent.ShouldBe("You won!");
        cut.FindAll("button.end-turn, button.resign").ShouldBeEmpty();
    }

    [Fact]
    public void Render_Started_ShowsTheBoardWithTheOverlaySlotsAndTheIconSet()
    {
        var cut = RenderTable(TestGames.Started());

        cut.FindAll(".game-screen .hud-board svg.board").Count.ShouldBe(1);
        cut.FindAll(".game-screen .hud-top .top-bar").Count.ShouldBe(1);
        cut.FindAll(".game-screen .hud-notices").Count.ShouldBe(1);
        cut.FindAll(".game-screen .hud-selection").Count.ShouldBe(1);
        cut.FindAll(".game-screen .hud-log").ShouldBeEmpty("the log drawer is closed until the Log button opens it");
        cut.FindAll("svg.game-sprite symbol#unit-settler").Count.ShouldBe(1);
        cut.Find(".top-bar .seat-swatch").ClassList.ShouldContain("seat-1");
        cut.Find(".selection-hint").TextContent.ShouldContain("Select a unit or a city");
    }

    [Fact]
    public void Escape_WithAUnitSelected_ClearsTheSelection()
    {
        var cut = RenderTable(TestGames.Started());
        Tile(cut, At(2, 2)).Click();
        cut.FindAll(".unit-panel").Count.ShouldBe(1);

        cut.Find(".game-screen").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        cut.FindAll(".unit-panel, .action-bar, use.overlay").ShouldBeEmpty();
        cut.FindAll(".selection-hint").Count.ShouldBe(1);
    }

    [Fact]
    public void Render_OwnUnitSelected_ShowsItsStatsAndItsActionsInTheActionBar()
    {
        var cut = RenderTable(TestGames.Started());

        Tile(cut, At(2, 2)).Click();

        var settler = Ruleset.Unit(SettlerType);
        cut.Find(".unit-panel .movement .stat-value").TextContent.ShouldContain($"{settler.Movement} of {settler.Movement}");
        cut.Find(".unit-panel .sight .stat-value .number").TextContent.ShouldBe($"{settler.Sight}");
        cut.Find(".unit-panel .cost .stat-value .number").TextContent.ShouldBe($"{settler.ProductionCost}");
        cut.FindAll(".action-bar button.found-city").Count.ShouldBe(1);
    }

    [Fact]
    public void NextUnit_Clicked_SelectsTheUnitsThatCanStillMoveInTurn()
    {
        var cut = RenderTable(TestGames.Started());

        cut.Find("button.next-unit").TextContent.ShouldContain("2 units can still move");
        cut.Find("button.next-unit").Click();
        cut.Find(".unit-panel .unit-name").TextContent.ShouldContain("Settler");
        cut.Find("button.next-unit").Click();

        cut.Find(".unit-panel .unit-name").TextContent.ShouldContain("Soldier");
    }

    [Fact]
    public void Render_NoUnitCanMove_HidesNextUnit()
    {
        var cut = RenderTable(TestGames.Started(view: Create([Settler(1, You, At(2, 2), movementLeft: 0)])));

        cut.FindAll("button.next-unit").ShouldBeEmpty();
    }

    [Fact]
    public void Render_OpponentsTurn_HidesNextUnit()
    {
        var cut = RenderTable(TestGames.Started(view: Start() with { CurrentPlayer = Opponent }));

        cut.FindAll("button.next-unit").ShouldBeEmpty();
    }

    [Fact]
    public void Render_OpponentsTurn_DisablesFoundCity()
    {
        var cut = RenderTable(TestGames.Started(view: Start() with { CurrentPlayer = Opponent }));

        Tile(cut, At(2, 2)).Click();

        cut.Find("button.found-city").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void Render_OwnCityProducing_ShowsTurnsLeftAndCosts()
    {
        var cut = RenderTable(TestGames.Started(view: Create(cities: [City(7, You, At(2, 2), "Home")])));

        Tile(cut, At(2, 2)).Click();

        // TestViews: your city produces a settler with 2 production so far.
        var left = (Ruleset.Unit(SettlerType).ProductionCost - 2 + Ruleset.Cities.ProductionPerTurn - 1) / Ruleset.Cities.ProductionPerTurn;
        cut.Find(".city-panel .progress-turns").TextContent.ShouldBe(left == 1 ? "1 turn left" : $"{left} turns left");
        cut.Find("button.produce-soldier .cost-amount").TextContent.ShouldBe($"{Ruleset.Unit(SoldierType).ProductionCost}");
    }

    [Fact]
    public void Render_EnemyUnitSelected_ShowsItsHealthAndStrengthButNoMovementOrActionBar()
    {
        var cut = RenderTable(TestGames.Started(view: Create([Soldier(2, You, At(3, 2)), Soldier(3, Opponent, At(5, 2), health: 70)])));

        Tile(cut, At(5, 2)).Click();

        cut.FindAll(".unit-panel").Count.ShouldBe(1);
        cut.Find(".unit-panel use").GetAttribute("href").ShouldBe("#unit-soldier");
        cut.Find(".unit-panel .health-text").TextContent.ShouldBe("70 of 100");
        cut.Find(".unit-panel .strength .stat-value .number").TextContent.ShouldBe("20");
        cut.FindAll(".unit-panel .movement, .unit-panel .cost, .action-bar").ShouldBeEmpty();
    }

    [Fact]
    public void Render_OwnArcherSelected_ShowsItsRangedStrengthAndRange()
    {
        var cut = RenderTable(TestGames.Started(view: Create([Unit(2, You, ArcherType, At(3, 2))])));

        Tile(cut, At(3, 2)).Click();

        cut.Find(".unit-panel .ranged .stat-value .number").TextContent.ShouldBe("22, range 2");
        cut.Find(".unit-panel .strength .stat-value .number").TextContent.ShouldBe("10");
    }

    [Fact]
    public void Render_OwnSettlerSelected_ShowsItCantFight()
    {
        var cut = RenderTable(TestGames.Started());

        Tile(cut, At(2, 2)).Click();

        cut.Find(".unit-panel .strength .stat-value").TextContent.ShouldContain("—");
        cut.Find(".unit-panel .strength .number-tooltip").GetAttribute("title")!.ShouldContain("can't fight");
    }

    [Fact]
    public void Render_HurtUnit_ShowsAHealthBarOnTheBoard()
    {
        var cut = RenderTable(TestGames.Started(view: Create([Soldier(2, You, At(3, 2), health: 25), Soldier(3, Opponent, At(5, 2))])));

        var fill = cut.Find("g.health rect.health-fill");
        fill.GetAttribute("width").ShouldBe("6");
        fill.ClassList.ShouldContain("low");
        cut.FindAll("g.health").Count.ShouldBe(1);
    }

    [Fact]
    public void Attack_ClickEnemy_ShowsTheForecastAndMarksTheTarget()
    {
        var cut = RenderTable(TestGames.Started(view: AdjacentSoldiers()));
        Tile(cut, At(3, 2)).Click();

        Tile(cut, At(4, 2)).Click();

        Api.RequestsTo(TestGames.CommandsPath()).ShouldBeEmpty();
        cut.Find(".combat-forecast .combat-title").TextContent.ShouldContain("Attack the enemy Soldier?");
        cut.Find(".combat-attacker .combat-outcome").TextContent.ShouldBe("Takes 24–36 damage.");
        cut.Find(".combat-defender .combat-outcome").TextContent.ShouldBe("Takes 24–36 damage.");
        cut.Find(".combat-defender .combat-strength .number").TextContent.ShouldBe("20");
        cut.FindAll("use.overlay.target").Count.ShouldBe(1);
    }

    [Fact]
    public void Attack_ForecastAttackButton_SendsTheAttack()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(TestGames.Started(view: AdjacentSoldiers())));
        var cut = RenderTable(TestGames.Started(view: AdjacentSoldiers()));
        Tile(cut, At(3, 2)).Click();
        Tile(cut, At(4, 2)).Click();

        cut.Find("button.combat-attack").Click();

        cut.WaitForAssertion(() => _changed.Count.ShouldBe(1));
        Command(0)["command"]!["type"]!.GetValue<string>().ShouldBe("attack");
    }

    [Fact]
    public void Attack_SecondClickOnTheTarget_KeepsTheForecastAndSendsNothing()
    {
        var cut = RenderTable(TestGames.Started(view: AdjacentSoldiers()));
        Tile(cut, At(3, 2)).Click();
        Tile(cut, At(4, 2)).Click();

        Tile(cut, At(4, 2)).Click();

        cut.FindAll(".combat-forecast").Count.ShouldBe(1);
        Api.RequestsTo(TestGames.CommandsPath()).ShouldBeEmpty();
    }

    [Fact]
    public void Attack_CancelOrEscape_ClosesTheForecastAndKeepsTheUnit()
    {
        var cut = RenderTable(TestGames.Started(view: AdjacentSoldiers()));
        Tile(cut, At(3, 2)).Click();
        Tile(cut, At(4, 2)).Click();

        cut.Find("button.combat-cancel").Click();

        cut.FindAll(".combat-forecast").ShouldBeEmpty();
        cut.FindAll(".unit-panel").Count.ShouldBe(1);

        Tile(cut, At(4, 2)).Click();
        cut.Find(".game-screen").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        cut.FindAll(".combat-forecast").ShouldBeEmpty();
        cut.FindAll(".unit-panel").Count.ShouldBe(1);
        Api.RequestsTo(TestGames.CommandsPath()).ShouldBeEmpty();
    }

    [Fact]
    public void Attack_ArcherForecast_SaysTheEnemyCantHitBack()
    {
        var cut = RenderTable(TestGames.Started(view: Create([Unit(2, You, ArcherType, At(3, 2)), Unit(3, Opponent, HorsemanType, At(5, 2), health: 40)])));
        Tile(cut, At(3, 2)).Click();

        Tile(cut, At(5, 2)).Click();

        cut.Find(".combat-title").TextContent.ShouldContain("Shoot at the enemy Horseman?");
        cut.Find(".combat-attacker .combat-outcome").TextContent.ShouldBe("Takes no damage.");
        cut.Find(".combat-defender .health-text").TextContent.ShouldBe("40 of 100");
        cut.Find(".combat-defender .combat-strength .number-tooltip").GetAttribute("title").ShouldBe("20 base, -6 for being hurt, +5 against Ranged = 19.");
        cut.FindAll(".combat-note").Count.ShouldBe(1);
    }

    [Fact]
    public void Attack_AcrossARiver_ShowsTheDefendersRiverBonus()
    {
        var view = Create(
            [Soldier(2, You, At(3, 2)), Soldier(3, Opponent, At(4, 2))],
            rivers: static coord => coord == At(3, 2) ? new TileRivers(East: River) : null);
        var cut = RenderTable(TestGames.Started(view: view));
        Tile(cut, At(3, 2)).Click();

        Tile(cut, At(4, 2)).Click();

        cut.Find(".combat-defender .combat-strength .number-tooltip").GetAttribute("title").ShouldBe("20 base, +3 behind River = 23.");
    }

    [Fact]
    public void Render_FinishedWithOwnUnitSelected_ShowsNoActionBar()
    {
        var cut = RenderTable(TestGames.Started());
        Tile(cut, At(2, 2)).Click();
        cut.FindAll(".action-bar").Count.ShouldBe(1);

        cut.Render(parameters => parameters.Add(static table => table.Game, TestGames.Started(view: Start() with { Status = GameStatus.Finished })));

        cut.FindAll(".action-bar").ShouldBeEmpty();
    }

    [Fact]
    public void Escape_WhileConfirmingResign_BacksOutAndKeepsTheSelection()
    {
        var cut = RenderTable(TestGames.Started());
        Tile(cut, At(2, 2)).Click();
        cut.Find("button.resign").Click();

        cut.Find(".confirm-button").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        cut.FindAll("button.resign").Count.ShouldBe(1);
        cut.FindAll(".unit-panel").Count.ShouldBe(1);
        Api.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void Render_OwnUnitOnHills_ShowsSightWithTheHillsBonus()
    {
        var view = Create([Settler(1, You, At(2, 2))], terrain: static coord => coord == At(2, 2) ? Hills : Plains);
        var cut = RenderTable(TestGames.Started(view: view));

        Tile(cut, At(2, 2)).Click();

        var sight = Ruleset.Unit(SettlerType).Sight;
        cut.Find(".unit-panel .sight .stat-value .number").TextContent.ShouldBe($"{sight + 1}");
        cut.Find(".unit-panel .sight .number-tooltip").GetAttribute("title").ShouldBe($"Sees {sight + 1} tiles around it: {sight}, plus 1 for standing on Hills.");
    }

    [Fact]
    public void LensToolbar_Toggle_DrawsTheLensAndPressesTheButton()
    {
        var cut = RenderTable(TestGames.Started());
        var button = cut.Find(".lens-toolbar button[data-lens=movement-cost]");
        button.GetAttribute("aria-pressed").ShouldBe("false");

        button.Click();

        cut.Find(".lens-toolbar button[data-lens=movement-cost]").GetAttribute("aria-pressed").ShouldBe("true");
        cut.FindAll("g[data-layer=lens] g.lens-mark").Count.ShouldBe(Width * Height);
    }

    [Fact]
    public void LensToolbar_ToggleTwice_RemovesTheLens()
    {
        var cut = RenderTable(TestGames.Started());
        cut.Find(".lens-toolbar button[data-lens=movement-cost]").Click();

        cut.Find(".lens-toolbar button[data-lens=movement-cost]").Click();

        cut.FindAll("g[data-layer=lens]").ShouldBeEmpty();
        cut.Find(".lens-toolbar button[data-lens=movement-cost]").GetAttribute("aria-pressed").ShouldBe("false");
    }

    [Fact]
    public void BoardModeToggle_Clicked_ShowsThe3DBoardAndBack()
    {
        var cut = RenderTable(TestGames.Started());

        cut.Find("button.board-mode").Click();

        cut.FindAll(".board3d").Count.ShouldBe(1);
        cut.FindAll("svg.board").ShouldBeEmpty();
        cut.Find("button.board-mode").Click();
        cut.FindAll("svg.board").Count.ShouldBe(1);
        cut.FindAll(".board3d").ShouldBeEmpty();
    }

    [Fact]
    public async Task Click_On3DBoard_MovesTheSelectedUnit()
    {
        var after = TestGames.Started(view: Create([Settler(1, You, At(1, 1), movementLeft: 1)]));
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(after));
        var module = JSInterop.SetupModule(Board3DInteropService.ModulePath);
        module.Setup<bool>("mount", _ => true).SetResult(true);
        foreach (var call in new[] { "render", "renderOverlay", "renderMotion", "fit", "focus", "unmount" })
        {
            module.SetupVoid(call, _ => true).SetVoidResult();
        }

        var cut = RenderTable(TestGames.Started());
        cut.Find("button.board-mode").Click();
        var relay = module.Invocations["mount"].Single().Arguments[1]!;
        var target = relay.GetType().GetProperty("Value")!.GetValue(relay)!;
        var click = target.GetType().GetMethod("OnTileClicked")!;

        await cut.InvokeAsync(() => (Task)click.Invoke(target, [At(2, 2).Q, At(2, 2).R])!);
        await cut.InvokeAsync(() => (Task)click.Invoke(target, [At(1, 1).Q, At(1, 1).R])!);

        cut.WaitForAssertion(() => _changed.Count.ShouldBe(1));
        Command(0)["command"]!["type"]!.GetValue<string>().ShouldBe("move_unit");
    }

    private static PlayerView AdjacentSoldiers() => Create([Settler(1, You, At(1, 1)), Soldier(2, You, At(3, 2)), Soldier(3, Opponent, At(4, 2))]);

    private IRenderedComponent<GameTable> RenderTable(GetGameResponse game) =>
        Render<GameTable>(parameters => parameters
            .Add(static table => table.Game, game)
            .Add(static table => table.GameChanged, changed => _changed.Add(changed)));

    private static IElement Tile(IRenderedComponent<GameTable> cut, HexCoord coord) =>
        cut.Find($"use.tile[data-q='{coord.Q}'][data-r='{coord.R}']");

    private JsonNode Command(int index) => JsonNode.Parse(Api.RequestsTo(TestGames.CommandsPath())[index].Body!)!;
}

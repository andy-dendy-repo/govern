using Govern.Client.Features.Games.Helpers;
using Govern.Client.Features.Games.Models;
using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Engine;
using Govern.Core.State;
using Govern.Core.Units;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Helpers;

public class BoardClickResolverTests
{
    private static readonly UnitId YourSettler = new(1);
    private static readonly UnitId YourSoldier = new(2);
    private static readonly UnitId EnemySoldier = new(3);

    [Fact]
    public void Resolve_OwnUnit_SelectsIt()
    {
        BoardClickResolver.Resolve(Start(), BoardSelection.None, At(2, 2)).ShouldBe(new BoardClick(BoardSelection.Of(YourSettler), null));
    }

    [Fact]
    public void Resolve_SelectedUnitAndReachableTile_Moves()
    {
        var selection = BoardSelection.Of(YourSettler);

        var click = BoardClickResolver.Resolve(Start(), selection, At(1, 1));

        click.ShouldBe(new BoardClick(selection, new MoveUnitCommand(YourSettler, At(1, 1))));
    }

    [Fact]
    public void Resolve_SelectedSoldierAndAdjacentEnemy_Attacks()
    {
        var view = Create([Soldier(2, You, At(3, 2)), Soldier(3, Opponent, At(4, 2))]);
        var selection = BoardSelection.Of(YourSoldier);

        var click = BoardClickResolver.Resolve(view, selection, At(4, 2));

        click.ShouldBe(new BoardClick(selection, new AttackCommand(YourSoldier, At(4, 2))));
    }

    [Fact]
    public void Resolve_OpponentsTurn_OnlySelects()
    {
        var view = Start() with { CurrentPlayer = Opponent };

        BoardClickResolver.Resolve(view, BoardSelection.Of(YourSettler), At(1, 1)).ShouldBe(new BoardClick(BoardSelection.None, null));
        BoardClickResolver.Resolve(view, BoardSelection.None, At(3, 2)).ShouldBe(new BoardClick(BoardSelection.Of(YourSoldier), null));
    }

    [Fact]
    public void Resolve_FinishedGame_OnlySelects()
    {
        var view = Finished(You);

        BoardClickResolver.Resolve(view, BoardSelection.Of(YourSettler), At(1, 1)).Command.ShouldBeNull();
    }

    [Fact]
    public void Resolve_SelectedUnitOnOwnCity_TogglesBetweenThem()
    {
        var view = Create([Settler(1, You, At(2, 2))], [City(7, You, At(2, 2))]);

        var toCity = BoardClickResolver.Resolve(view, BoardSelection.Of(YourSettler), At(2, 2));
        var toUnit = BoardClickResolver.Resolve(view, toCity.Next, At(2, 2));

        toCity.ShouldBe(new BoardClick(BoardSelection.Of(new CityId(7)), null));
        toUnit.ShouldBe(new BoardClick(BoardSelection.Of(YourSettler), null));
    }

    [Fact]
    public void Resolve_EnemyUnitOrCity_SelectsItForInspection()
    {
        var view = Create([Soldier(3, Opponent, At(5, 2))], [City(8, Opponent, At(5, 4))]);

        BoardClickResolver.Resolve(view, BoardSelection.None, At(5, 2)).Next.ShouldBe(BoardSelection.Of(EnemySoldier));
        BoardClickResolver.Resolve(view, BoardSelection.None, At(5, 4)).Next.ShouldBe(BoardSelection.Of(new CityId(8)));
    }

    [Fact]
    public void Resolve_EmptyUnreachableTile_Deselects()
    {
        BoardClickResolver.Resolve(Start(), BoardSelection.Of(YourSettler), At(7, 5)).ShouldBe(new BoardClick(BoardSelection.None, null));
    }

    [Fact]
    public void Prune_UnitStillInView_KeepsIt()
    {
        BoardClickResolver.Prune(Start(), BoardSelection.Of(YourSoldier)).ShouldBe(BoardSelection.Of(YourSoldier));
    }

    [Fact]
    public void Prune_UnitOrCityGone_DropsIt()
    {
        var view = Create([Settler(1, You, At(2, 2))]);

        BoardClickResolver.Prune(view, BoardSelection.Of(YourSoldier)).ShouldBe(BoardSelection.None);
        BoardClickResolver.Prune(view, BoardSelection.Of(new CityId(7))).ShouldBe(BoardSelection.None);
    }

    [Fact]
    public void Highlights_OwnUnit_AreTheCorePreviews()
    {
        var view = Create([Soldier(2, You, At(3, 2)), Soldier(3, Opponent, At(4, 2))]);

        var highlights = BoardClickResolver.Highlights(view, BoardSelection.Of(YourSoldier));

        highlights.Reachable.ShouldBe(GameEngine.ReachableTiles(view, YourSoldier), ignoreOrder: true);
        highlights.Reachable.ShouldNotBeEmpty();
        highlights.Attackable.ShouldBe([At(4, 2)]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Highlights_NoUnitOrEnemyUnit_AreEmpty(int unit)
    {
        var selection = unit == 0 ? BoardSelection.None : BoardSelection.Of(new UnitId(unit));

        var highlights = BoardClickResolver.Highlights(Start(), selection);

        highlights.Reachable.ShouldBeEmpty();
        highlights.Attackable.ShouldBeEmpty();
    }

    [Fact]
    public void Highlights_OpponentsTurn_AreEmpty()
    {
        var highlights = BoardClickResolver.Highlights(Start() with { CurrentPlayer = Opponent }, BoardSelection.Of(YourSettler));

        highlights.Reachable.ShouldBeEmpty();
        highlights.Attackable.ShouldBeEmpty();
    }

    [Fact]
    public void Highlights_FinishedGame_AreEmpty()
    {
        var view = Start() with { Status = GameStatus.Finished };

        BoardClickResolver.Highlights(view, BoardSelection.Of(YourSettler)).Reachable.ShouldBeEmpty();
    }
}

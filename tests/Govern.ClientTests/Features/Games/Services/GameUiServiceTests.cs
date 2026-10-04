using Govern.Client.Features.Games.Lenses;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.Core.Cities;
using Govern.Core.Map;
using Govern.Core.Units;
using Govern.Core.Visibility;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Services;

public class GameUiServiceTests
{
    private readonly GameUiService _ui = new();
    private int _changes;

    public GameUiServiceTests() => _ui.Changed += () => _changes++;

    [Fact]
    public void Select_OwnUnit_HighlightsItsMovesAndNotifies()
    {
        _ui.Select(Ruleset, Start(), BoardSelection.Of(new UnitId(1)));

        _ui.Selection.ShouldBe(BoardSelection.Of(new UnitId(1)));
        _ui.Highlights.Reachable.ShouldNotBeEmpty();
        _changes.ShouldBe(1);
    }

    [Fact]
    public void Refresh_SelectedUnitGone_DropsTheSelection()
    {
        _ui.Select(Ruleset, Start(), BoardSelection.Of(new UnitId(2)));

        _ui.Refresh(Ruleset, Create([Settler(1, You, At(2, 2))]));

        _ui.Selection.ShouldBe(BoardSelection.None);
        _ui.Highlights.ShouldBe(BoardHighlights.None);
    }

    [Fact]
    public void Refresh_SelectedCityStillThere_KeepsIt()
    {
        var view = Create(cities: [City(7, You, At(2, 2))]);
        _ui.Select(Ruleset, view, BoardSelection.Of(new CityId(7)));

        _ui.Refresh(Ruleset, view with { Turn = 4 });

        _ui.Selection.ShouldBe(BoardSelection.Of(new CityId(7)));
    }

    [Fact]
    public void Escape_WithASelection_ClearsItAndNotifies()
    {
        _ui.Select(Ruleset, Start(), BoardSelection.Of(new UnitId(1)));

        _ui.Escape(Ruleset, Start()).ShouldBeTrue();

        _ui.Selection.ShouldBe(BoardSelection.None);
        _ui.Highlights.ShouldBe(BoardHighlights.None);
        _changes.ShouldBe(2);
    }

    [Fact]
    public void Escape_NothingSelected_ChangesNothing()
    {
        _ui.Escape(Ruleset, Start()).ShouldBeFalse();

        _changes.ShouldBe(0);
    }

    [Fact]
    public void ToggleLens_Off_TurnsItOnAndNotifies()
    {
        var lens = new MovementCostLens(new RulesetService());

        _ui.ToggleLens(lens);

        _ui.ActiveLens.ShouldBeSameAs(lens);
        _changes.ShouldBe(1);
    }

    [Fact]
    public void ToggleLens_Active_TurnsItOff()
    {
        var lens = new MovementCostLens(new RulesetService());
        _ui.ToggleLens(lens);

        _ui.ToggleLens(lens);

        _ui.ActiveLens.ShouldBeNull();
        _changes.ShouldBe(2);
    }

    [Fact]
    public void ToggleLens_AnotherLensActive_SwitchesToTheNewOne()
    {
        _ui.ToggleLens(new MovementCostLens(new RulesetService()));
        var other = new OtherLens();

        _ui.ToggleLens(other);

        _ui.ActiveLens.ShouldBeSameAs(other);
    }

    [Fact]
    public void Escape_WithALensOn_KeepsTheLens()
    {
        var lens = new MovementCostLens(new RulesetService());
        _ui.ToggleLens(lens);
        _ui.Select(Ruleset, Start(), BoardSelection.Of(new UnitId(1)));

        _ui.Escape(Ruleset, Start());

        _ui.ActiveLens.ShouldBeSameAs(lens);
    }

    private sealed class OtherLens : IMapLens
    {
        public string Id => "other";

        public string Name => "Other";

        public string Icon => "ui-sight";

        public LensMark? MarkFor(TileView tile) => null;
    }

    [Fact]
    public void Escape_WithAPendingAttack_CancelsOnlyTheAttack()
    {
        var view = Create([Soldier(2, You, At(3, 2)), Soldier(3, Opponent, At(4, 2))]);
        _ui.Select(Ruleset, view, BoardSelection.Of(new UnitId(2)) with { Target = At(4, 2) });

        _ui.Escape(Ruleset, view).ShouldBeTrue();

        _ui.Selection.ShouldBe(BoardSelection.Of(new UnitId(2)));
        _ui.Highlights.Attackable.ShouldBe([At(4, 2)]);
    }

    [Fact]
    public void ToggleBridge_SelectedUnit_MarksTheBridgeTargetsAndEscapeLeavesBridgeMode()
    {
        var view = Create([Soldier(2, You, At(3, 2))], rivers: static coord => coord == At(3, 2) ? new TileRivers(East: River) : null);
        _ui.Select(Ruleset, view, BoardSelection.Of(new UnitId(2)));

        _ui.ToggleBridge(Ruleset, view);
        _ui.Highlights.Bridgeable.ShouldBe([At(4, 2)]);

        _ui.Escape(Ruleset, view).ShouldBeTrue();
        _ui.Selection.ShouldBe(BoardSelection.Of(new UnitId(2)));
        _ui.Highlights.Bridgeable.ShouldBeEmpty();
        _ui.Highlights.Reachable.ShouldNotBeEmpty();
    }

    [Fact]
    public void PlaceBuilding_SelectedCity_MarksTheSitesAndEscapeStopsPlacing()
    {
        var view = Create(cities: [City(7, You, At(2, 2))]);
        _ui.Select(Ruleset, view, BoardSelection.Of(new CityId(7)));

        _ui.PlaceBuilding(Ruleset, view, Campus);
        _ui.Selection.Placing.ShouldBe(Campus);
        _ui.Highlights.Placeable.Count.ShouldBe(6);

        _ui.Escape(Ruleset, view).ShouldBeTrue();
        _ui.Selection.ShouldBe(BoardSelection.Of(new CityId(7)));
        _ui.Highlights.Placeable.ShouldBeEmpty();
    }

    [Fact]
    public void PlaceBuilding_SameBuildingAgain_StopsPlacingAndNothingSelected_DoesNothing()
    {
        var view = Create(cities: [City(7, You, At(2, 2))]);
        _ui.PlaceBuilding(Ruleset, view, Campus);
        _ui.Selection.ShouldBe(BoardSelection.None);

        _ui.Select(Ruleset, view, BoardSelection.Of(new CityId(7)));
        _ui.PlaceBuilding(Ruleset, view, Campus);
        _ui.PlaceBuilding(Ruleset, view, Campus);

        _ui.Selection.ShouldBe(BoardSelection.Of(new CityId(7)));
    }

    [Fact]
    public void ToggleBridge_NothingSelected_DoesNothing()
    {
        _ui.ToggleBridge(Ruleset, Start());

        _ui.Selection.ShouldBe(BoardSelection.None);
        _changes.ShouldBe(0);
    }

    [Fact]
    public void CancelAttack_NothingPending_DoesNotNotify()
    {
        _ui.Select(Ruleset, Start(), BoardSelection.Of(new UnitId(2)));

        _ui.CancelAttack();

        _changes.ShouldBe(1);
    }

    [Fact]
    public void Refresh_TargetNoLongerAttackable_DropsIt()
    {
        var view = Create([Soldier(2, You, At(3, 2)), Soldier(3, Opponent, At(4, 2))]);
        _ui.Select(Ruleset, view, BoardSelection.Of(new UnitId(2)) with { Target = At(4, 2) });

        _ui.Refresh(Ruleset, Create([Soldier(2, You, At(3, 2), movementLeft: 0), Soldier(3, Opponent, At(4, 2), health: 60)]));

        _ui.Selection.ShouldBe(BoardSelection.Of(new UnitId(2)));
    }

    [Fact]
    public void ToggleDestination_SelectedUnit_ChoosesUntilEscape()
    {
        var view = Create([Soldier(2, You, At(3, 2))]);
        _ui.Select(Ruleset, view, BoardSelection.Of(new UnitId(2)));

        _ui.ToggleDestination(Ruleset, view);

        _ui.Selection.Destination.ShouldBeTrue();
        _ui.Highlights.Reachable.ShouldBeEmpty();
        _ui.Escape(Ruleset, view).ShouldBeTrue();
        _ui.Selection.ShouldBe(BoardSelection.Of(new UnitId(2)));
    }

    [Fact]
    public void IdleUnits_LeavesOutEnemiesSpentUnitsWorkersAndUnitsOnOrders()
    {
        var view = Create(
        [
            Soldier(4, You, At(1, 1)),
            Settler(2, You, At(2, 2)),
            Soldier(3, You, At(3, 2), movementLeft: 0),
            Settler(5, You, At(4, 2)) with { Job = new UnitJob(0, 3, Improvement: Farm) },
            Soldier(6, Opponent, At(5, 2)),
            Soldier(7, You, At(6, 4)) with { Destination = At(8, 4) },
            Soldier(8, You, At(7, 4)) with { Exploring = true },
        ]);

        GameUiService.IdleUnits(view).Select(static unit => unit.Id.Value).ShouldBe([2, 4]);
    }

    [Fact]
    public void SelectNextIdleUnit_GoesThroughThemAndWrapsRound()
    {
        var view = Create([Settler(1, You, At(2, 2)), Soldier(2, You, At(3, 2), movementLeft: 0), Soldier(3, You, At(4, 2))]);

        _ui.SelectNextIdleUnit(Ruleset, view);
        _ui.Selection.Unit.ShouldBe(new UnitId(1));
        _ui.SelectNextIdleUnit(Ruleset, view);
        _ui.Selection.Unit.ShouldBe(new UnitId(3));
        _ui.SelectNextIdleUnit(Ruleset, view);
        _ui.Selection.Unit.ShouldBe(new UnitId(1));
    }

    [Fact]
    public void SelectNextIdleUnit_NoneLeft_KeepsTheSelection()
    {
        _ui.Select(Ruleset, Start(), BoardSelection.Of(new CityId(7)));
        _changes = 0;

        _ui.SelectNextIdleUnit(Ruleset, Create([Settler(1, You, At(2, 2), movementLeft: 0)]));

        _ui.Selection.ShouldBe(BoardSelection.Of(new CityId(7)));
        _changes.ShouldBe(0);
    }

    [Fact]
    public void ToggleLog_Opened_MarksTheLogSeenAndCountsOnlyLaterEntries()
    {
        GameLogItem[] log = [new(1, 1, []), new(2, 1, [])];
        _ui.UnreadLog(log).ShouldBe(2);

        _ui.ToggleLog(log);

        _ui.LogOpen.ShouldBeTrue();
        _ui.UnreadLog(log).ShouldBe(0);
        _ui.ToggleLog(log);
        _ui.UnreadLog([.. log, new(3, 2, [])]).ShouldBe(1);
    }

    [Fact]
    public void Escape_LogOpen_ClosesTheLogAndKeepsTheSelection()
    {
        _ui.Select(Ruleset, Start(), BoardSelection.Of(new UnitId(1)));
        _ui.ToggleLog([]);

        _ui.Escape(Ruleset, Start()).ShouldBeTrue();

        _ui.LogOpen.ShouldBeFalse();
        _ui.Selection.ShouldBe(BoardSelection.Of(new UnitId(1)));
    }

    [Fact]
    public void Select_AnotherUnit_OpensTheCollapsedCardAgain()
    {
        _ui.Select(Ruleset, Start(), BoardSelection.Of(new UnitId(1)));
        _ui.ToggleSelectionCollapsed();

        _ui.Select(Ruleset, Start(), BoardSelection.Of(new UnitId(1)) with { Bridge = true });
        _ui.SelectionCollapsed.ShouldBeTrue("the same unit in another mode keeps the card as it is");

        _ui.Select(Ruleset, Start(), BoardSelection.Of(new UnitId(2)));
        _ui.SelectionCollapsed.ShouldBeFalse();
    }

    [Fact]
    public void SetFullScreen_Changed_NotifiesOnlyOnAChange()
    {
        _ui.SetFullScreen(true);
        _ui.SetFullScreen(true);

        _ui.FullScreen.ShouldBeTrue();
        _changes.ShouldBe(1);
    }
}

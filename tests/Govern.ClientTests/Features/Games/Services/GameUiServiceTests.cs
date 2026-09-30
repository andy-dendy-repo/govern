using Govern.Client.Features.Games.Lenses;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.Core.Cities;
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

        _ui.Escape().ShouldBeTrue();

        _ui.Selection.ShouldBe(BoardSelection.None);
        _ui.Highlights.ShouldBe(BoardHighlights.None);
        _changes.ShouldBe(2);
    }

    [Fact]
    public void Escape_NothingSelected_ChangesNothing()
    {
        _ui.Escape().ShouldBeFalse();

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

        _ui.Escape();

        _ui.ActiveLens.ShouldBeSameAs(lens);
    }

    private sealed class OtherLens : IMapLens
    {
        public string Id => "other";

        public string Name => "Other";

        public string Icon => "ui-sight";

        public LensMark? MarkFor(TileView tile) => null;
    }
}

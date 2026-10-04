using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Visibility;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Engine;

// City territory: founding claims the tiles around a city, and a city that holds the price of its next tile in culture claims
// it at the end of its owner's turn. Default ruleset: initial radius 1, maximum radius 3, price 4 culture + 2 per tile gained,
// and every city makes 1 culture a turn.
public class TerritoryRuleTests
{
    [Fact]
    public void FoundCity_ClaimsTheTilesAroundItAndStartsWithTheStartingStock()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var settler = builder.AddUnit(P1, TestRulesets.Settler, 5, 5);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new FoundCityCommand(settler, "Ambermoor")).ShouldSucceed();

        var city = update.State.CityAt(At(5, 5))!;
        update.State.Map.Tiles.Where(tile => tile.Territory == city.Id).Select(static tile => tile.Coord)
            .ShouldBe(At(5, 5).WithinRadius(1), ignoreOrder: true);
        city.StockList.ShouldBe([.. TestRulesets.Default.Cities.StartingStock]);
        city.TilesGained.ShouldBe(0);
    }

    [Fact]
    public void FoundCity_NextToAnotherTerritory_LeavesItsTilesAlone()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var enemy = builder.AddCity(P2, 8, 5);
        builder.WithTerritory(enemy, (6, 5));
        var settler = builder.AddUnit(P1, TestRulesets.Settler, 5, 5);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new FoundCityCommand(settler, "Ambermoor")).ShouldSucceed();

        update.State.Map.TileAt(At(6, 5)).Territory.ShouldBe(enemy);
        update.State.Map.Tiles.Count(tile => tile.Territory == update.State.CityAt(At(5, 5))!.Id).ShouldBe(6);
    }

    [Fact]
    public void FoundCity_InsideAnotherTerritory_IsRefused()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var enemy = builder.AddCity(P2, 9, 5);
        builder.WithTerritory(enemy, (6, 5));
        var settler = builder.AddUnit(P1, TestRulesets.Settler, 6, 5);
        var state = builder.Build();

        GameEngine.Execute(TestRulesets.Default, state, P1, new FoundCityCommand(settler, "Ambermoor")).ShouldFailWith(RuleViolations.CityInTerritory);
        GameEngine.CheckFoundCity(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), settler)
            .ShouldBe(RuleViolations.CityInTerritory, "the settler's own tile shows its territory");
    }

    [Fact]
    public void EndTurn_CultureReachesThePrice_PaysItAndClaimsTheRichestTile()
    {
        var builder = new GameStateBuilder().WithCapitals().WithTerrain(TestRulesets.Lake, (7, 5)).WithTerrain(TestRulesets.Hills, (3, 5));
        var city = builder.AddCity(P1, 5, 5, stock: [new ResourceAmount(TestRulesets.Culture, 3)]);

        var update = EndTurn(builder.Build());

        // Plains give 3 in total and hills 3; the lake gives 4.
        update.State.Map.TileAt(At(7, 5)).Territory.ShouldBe(city);
        var grown = update.State.FindCity(city)!;
        grown.TilesGained.ShouldBe(1);
        YieldCalculator.AmountOf(grown.StockList, TestRulesets.Culture).ShouldBe(0);
        update.Events.ShouldContain(new TerritoryGrewEvent(city, P1, At(7, 5)));
    }

    [Fact]
    public void EndTurn_EqualTiles_TakesTheFirstByRowThenColumn()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P1, 5, 5, stock: [new ResourceAmount(TestRulesets.Culture, 3)]);

        var update = EndTurn(builder.Build());

        var claimed = update.Events.OfType<TerritoryGrewEvent>().ShouldHaveSingleItem();
        claimed.CityId.ShouldBe(city);
        claimed.Tile.ShouldBe(At(5, 5).WithinRadius(2).Where(coord => At(5, 5).DistanceTo(coord) == 2).OrderBy(static coord => coord.Row).ThenBy(static coord => coord.Column).First());
    }

    [Fact]
    public void EndTurn_NotEnoughCulture_ClaimsNothingAndKeepsIt()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P1, 5, 5, stock: [new ResourceAmount(TestRulesets.Culture, 2)]);

        var update = EndTurn(builder.Build());

        update.Events.OfType<TerritoryGrewEvent>().ShouldBeEmpty();
        YieldCalculator.AmountOf(update.State.FindCity(city)!.StockList, TestRulesets.Culture).ShouldBe(3);
    }

    [Fact]
    public void EndTurn_TilesAlreadyGained_RaiseThePrice()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P1, 5, 5, stock: [new ResourceAmount(TestRulesets.Culture, 6)], tilesGained: 2);

        var first = EndTurn(builder.Build());
        first.Events.OfType<TerritoryGrewEvent>().ShouldBeEmpty("2 tiles gained: the next costs 8");

        var second = EndTurn(EndTurn(first.State, P2).State);
        second.Events.OfType<TerritoryGrewEvent>().ShouldHaveSingleItem();
        second.State.FindCity(city)!.TilesGained.ShouldBe(3);
        YieldCalculator.AmountOf(second.State.FindCity(city)!.StockList, TestRulesets.Culture).ShouldBe(0);
    }

    [Fact]
    public void EndTurn_EveryTileWithinTheMaximumRadiusOwned_ClaimsNothingAndKeepsTheCulture()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P1, 5, 5, stock: [new ResourceAmount(TestRulesets.Culture, 50)]);
        builder.WithTerritory(city, [.. At(5, 5).WithinRadius(3).Select(static coord => (coord.Column, coord.Row))]);

        var update = EndTurn(builder.Build());

        update.Events.OfType<TerritoryGrewEvent>().ShouldBeEmpty();
        YieldCalculator.AmountOf(update.State.FindCity(city)!.StockList, TestRulesets.Culture).ShouldBe(51);
    }

    [Fact]
    public void EndTurn_NeighbourTilesOwnedByAnotherCity_AreNeverTaken()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P1, 5, 5, stock: [new ResourceAmount(TestRulesets.Culture, 50)]);
        var enemy = builder.AddCity(P2, 9, 9);
        builder.WithTerritory(enemy, [.. At(5, 5).WithinRadius(2).Where(coord => At(5, 5).DistanceTo(coord) == 2).Select(static coord => (coord.Column, coord.Row))]);

        var update = EndTurn(builder.Build());

        update.Events.OfType<TerritoryGrewEvent>().ShouldBeEmpty();
        update.State.Map.Tiles.Count(tile => tile.Territory == city).ShouldBe(7);
    }

    [Fact]
    public void Capture_KeepsTheTerritoryAndTheStockWithTheCity()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P2, 6, 5, stock: [new ResourceAmount(TestRulesets.Gold, 9)]);
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 5, 5);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new AttackCommand(attacker, At(6, 5))).ShouldSucceed();

        var captured = update.State.FindCity(city)!;
        captured.Owner.ShouldBe(P1);
        captured.StockList.ShouldBe([new ResourceAmount(TestRulesets.Gold, 9)]);
        update.State.Map.Tiles.Count(tile => tile.Territory == city).ShouldBe(7);
        var view = GameEngine.ViewFor(TestRulesets.Default, update.State, P1);
        view.TileAt(At(7, 5)).TerritoryCity.ShouldBe(city);
        view.TileAt(At(7, 5)).TerritoryOwner.ShouldBe(P1);
    }

    [Fact]
    public void CityIncome_FromTheView_IsWhatTheEndOfTurnAdds()
    {
        var builder = new GameStateBuilder().WithCapitals()
            .WithTerrain(TestRulesets.Lake, (6, 4))
            .WithTerrain(TestRulesets.Desert, (4, 5))
            .WithTerrain(TestRulesets.Forest, (5, 6))
            .WithRiver(TestRulesets.Stream, ((5, 5), (6, 5)))
            .WithRiver(TestRulesets.DeepRiver, ((6, 5), (7, 5)))
            .WithImprovement(TestRulesets.Farm, (6, 5));
        var city = builder.AddCity(P1, 5, 5, paid: true, buildings: [TestRulesets.TradePost, TestRulesets.Harbour]);
        builder.WithTerritory(city, (7, 5));
        var state = builder.Build();

        var income = GameEngine.CityIncome(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), city);
        var after = EndTurn(state).State.FindCity(city)!.StockList;

        income.Where(static amount => amount.Resource != TestRulesets.Science).ShouldBe(after, "science goes to the player's budget, not the stock");
        YieldCalculator.AmountOf(income, TestRulesets.Science).ShouldBe(1);
        GameEngine.CityIncome(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P2), city).ShouldBeEmpty("not the viewer's city");
    }

    [Fact]
    public void CityIncome_RiverOnAnEdgeStoredOutsideSight_IsStillCounted()
    {
        // (3, 5) is in the territory at the largest radius; its west edge is stored by (2, 5), which no city or unit sees.
        var builder = new GameStateBuilder().WithCapitals().WithRiver(TestRulesets.Stream, ((3, 5), (2, 5)));
        var city = builder.AddCity(P1, 6, 5, paid: true);
        builder.WithTerritory(city, [.. At(6, 5).WithinRadius(3).Select(static coord => (coord.Column, coord.Row))]);
        var state = builder.Build();
        var view = GameEngine.ViewFor(TestRulesets.Default, state, P1);

        view.TileAt(At(2, 5)).Visibility.ShouldBe(TileVisibility.Explored, "next to the territory, known but not in sight");
        GameEngine.CityIncome(TestRulesets.Default, view, city).Where(static amount => amount.Resource != TestRulesets.Science)
            .ShouldBe(EndTurn(state).State.FindCity(city)!.StockList);
    }

    private static GameUpdate EndTurn(GameState state, PlayerId? player = null) =>
        GameEngine.Execute(TestRulesets.Default, state, player ?? P1, new EndTurnCommand()).ShouldSucceed();
}

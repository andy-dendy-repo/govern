using Govern.Core.Actions;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Engine;

// Jobs and yields at the end of a turn. Production: builder 2, soldier 1, city 1 (+1 with an army camp); farm 4, mine 6, bridge 6.
public class ConstructionRuleTests
{
    [Fact]
    public void EndTurn_BuilderInTerritory_UsesItsOwnAndTheCitysProduction()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P1, 6, 4, progress: 1);
        var unit = builder.AddUnit(P1, TestRulesets.Builder, 5, 5, job: UnitJob.ForImprovement(TestRulesets.Mine, 1));
        var state = builder.WithTerrain(TestRulesets.Hills, (5, 5)).Build();

        var update = EndTurn(state);

        update.State.FindUnit(unit)!.Job!.Progress.ShouldBe(3);
        update.State.FindCity(city)!.ProductionProgress.ShouldBe(1, "the city's production went to the job");
    }

    [Fact]
    public void EndTurn_ProgressReachesTheCost_BuildsTheImprovementAndFreesTheUnit()
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddCity(P1, 6, 4);
        var unit = builder.AddUnit(P1, TestRulesets.Builder, 5, 5, job: new UnitJob(1, 1, Improvement: TestRulesets.Farm));

        var update = EndTurn(builder.Build());

        update.State.Map.TileAt(At(5, 5)).Improvement.ShouldBe(TestRulesets.Farm);
        update.State.FindUnit(unit)!.Job.ShouldBeNull();
        update.Events.ShouldContain(new ImprovementBuiltEvent(unit, P1, At(5, 5), TestRulesets.Farm));
    }

    [Fact]
    public void EndTurn_FarmTakesTwoTurnsFromScratch()
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddCity(P1, 6, 4);
        var unit = builder.AddUnit(P1, TestRulesets.Builder, 5, 5);
        var state = builder.Build();

        state = GameEngine.Execute(TestRulesets.Default, state, P1, new BuildImprovementCommand(unit, TestRulesets.Farm)).ShouldSucceed().State;
        state = EndTurn(state).State;
        state.Map.TileAt(At(5, 5)).Improvement.ShouldBeNull();
        state = EndTurn(state, P2).State;
        state = EndTurn(state).State;

        state.Map.TileAt(At(5, 5)).Improvement.ShouldBe(TestRulesets.Farm);
    }

    [Fact]
    public void EndTurn_CityFeedsOnlyTheOldestJob()
    {
        var builder = new GameStateBuilder().WithCapitals()
            .WithRiver(TestRulesets.River, ((6, 5), (7, 5)))
            .WithCurrentPlayer(P1, turn: 4);
        builder.AddCity(P1, 6, 4);
        var newer = builder.AddUnit(P1, TestRulesets.Builder, 5, 5, job: UnitJob.ForImprovement(TestRulesets.Farm, 3));
        var older = builder.AddUnit(P1, TestRulesets.Soldier, 6, 5, job: UnitJob.ForBridge(At(7, 5), 2));

        var update = EndTurn(builder.Build());

        update.State.FindUnit(older)!.Job!.Progress.ShouldBe(2, "soldier 1 + city 1");
        update.State.FindUnit(newer)!.Job!.Progress.ShouldBe(2, "builder 2 only");
    }

    [Fact]
    public void EndTurn_BridgeOutsideEveryTerritory_UsesOnlyTheUnitsProduction()
    {
        var builder = new GameStateBuilder().WithCapitals().WithRiver(TestRulesets.DeepRiver, ((5, 5), (6, 5)));
        var unit = builder.AddUnit(P1, TestRulesets.Soldier, 5, 5, job: new UnitJob(4, 1, BridgeTo: At(6, 5)));

        var update = EndTurn(builder.Build());

        update.State.FindUnit(unit)!.Job!.Progress.ShouldBe(5);

        var built = EndTurn(EndTurn(update.State, P2).State);
        built.State.Map.BridgeBetween(At(6, 5), At(5, 5)).ShouldBeTrue();
        built.State.FindUnit(unit)!.Job.ShouldBeNull();
        built.Events.ShouldContain(new BridgeBuiltEvent(unit, P1, At(5, 5), At(6, 5)));
    }

    [Fact]
    public void EndTurn_BridgeFromTheCityTile_UsesTheCitysProduction()
    {
        var builder = new GameStateBuilder().WithCapitals().WithRiver(TestRulesets.River, ((5, 5), (6, 5)));
        builder.AddCity(P1, 5, 5);
        var unit = builder.AddUnit(P1, TestRulesets.Soldier, 5, 5, job: UnitJob.ForBridge(At(6, 5), 1));

        EndTurn(builder.Build()).State.FindUnit(unit)!.Job!.Progress.ShouldBe(2);
    }

    [Fact]
    public void EndTurn_SiteNoLongerAllowsTheJob_DropsIt()
    {
        // The bridge was finished from the other bank, and the farm's city now belongs to the enemy.
        var builder = new GameStateBuilder().WithCapitals()
            .WithRiver(TestRulesets.River, ((5, 5), (6, 5)))
            .WithBridge(((5, 5), (6, 5)));
        builder.AddCity(P2, 3, 2);
        var bridger = builder.AddUnit(P1, TestRulesets.Soldier, 5, 5, job: new UnitJob(5, 1, BridgeTo: At(6, 5)));
        var farmer = builder.AddUnit(P1, TestRulesets.Builder, 3, 3, job: new UnitJob(3, 1, Improvement: TestRulesets.Farm));

        var update = EndTurn(builder.Build());

        update.State.FindUnit(bridger)!.Job.ShouldBeNull();
        update.State.FindUnit(farmer)!.Job.ShouldBeNull();
        update.State.Map.TileAt(At(3, 3)).Improvement.ShouldBeNull();
    }

    [Fact]
    public void EndTurn_WorkingUnit_DoesNotHeal()
    {
        var builder = new GameStateBuilder().WithCapitals().WithRiver(TestRulesets.River, ((5, 5), (6, 5)));
        var unit = builder.AddUnit(P1, TestRulesets.Soldier, 5, 5, damage: 30, job: UnitJob.ForBridge(At(6, 5), 1));

        EndTurn(builder.Build()).State.FindUnit(unit)!.Damage.ShouldBe(30);
    }

    [Fact]
    public void Move_DropsTheJob()
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddCity(P1, 6, 4);
        var unit = builder.AddUnit(P1, TestRulesets.Builder, 5, 5, job: new UnitJob(3, 1, Improvement: TestRulesets.Farm));

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new MoveUnitCommand(unit, At(4, 5))).ShouldSucceed();

        update.State.FindUnit(unit)!.Job.ShouldBeNull();
    }

    [Fact]
    public void Attack_DropsTheJob()
    {
        var builder = new GameStateBuilder().WithCapitals().WithRiver(TestRulesets.River, ((5, 5), (6, 5)));
        var unit = builder.AddUnit(P1, TestRulesets.Soldier, 5, 5, job: new UnitJob(3, 1, BridgeTo: At(6, 5)));
        builder.AddUnit(P2, TestRulesets.Settler, 4, 5);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new AttackCommand(unit, At(4, 5))).ShouldSucceed();

        update.State.FindUnit(unit)!.Job.ShouldBeNull();
    }

    [Fact]
    public void EndTurn_TerritoryImprovementsAndBuildings_AddTheirYieldsToTheCitysStockInRulesetOrder()
    {
        var builder = new GameStateBuilder().WithCapitals()
            .WithImprovement(TestRulesets.Farm, (5, 5), (7, 4))
            .WithImprovement(TestRulesets.Mine, (6, 3))
            .WithImprovement(TestRulesets.Farm, (9, 9));
        var own = builder.AddCity(P1, 6, 4, paid: true, buildings: [TestRulesets.Campus, TestRulesets.Harbour]);
        var enemy = builder.AddCity(P2, 8, 7, buildings: [TestRulesets.TradePost]);

        var update = EndTurn(builder.Build());

        // 7 plains tiles (2 food, 1 water each), two farms (+2 food each), a mine (+2 stone), the city's own culture, the campus
        // (2 science) and the harbour (2 food).
        update.State.FindCity(own)!.StockList.ShouldBe([
            new ResourceAmount(TestRulesets.Food, 20),
            new ResourceAmount(TestRulesets.Water, 7),
            new ResourceAmount(TestRulesets.Stone, 2),
            new ResourceAmount(TestRulesets.Science, 2),
            new ResourceAmount(TestRulesets.Culture, 1),
        ]);
        update.State.FindCity(enemy)!.Stock.ShouldBeNull("only the ending player's cities yield");

        var next = EndTurn(EndTurn(update.State, P2).State);
        YieldCalculator.AmountOf(next.State.FindCity(own)!.StockList, TestRulesets.Food).ShouldBe(40);
        YieldCalculator.AmountOf(next.State.FindCity(enemy)!.StockList, TestRulesets.Gold).ShouldBe(2);
    }

    [Fact]
    public void EndTurn_ImprovementOutsideEveryTerritory_YieldsNothing()
    {
        var without = new GameStateBuilder().WithCapitals();
        var with = new GameStateBuilder().WithCapitals().WithImprovement(TestRulesets.Farm, (5, 5));

        EndTurn(with.Build()).State.Cities.Select(static city => city.StockList.Sum(static amount => amount.Amount))
            .ShouldBe(EndTurn(without.Build()).State.Cities.Select(static city => city.StockList.Sum(static amount => amount.Amount)));
    }

    [Fact]
    public void EndTurn_RiverAroundATile_AddsItsWaterOnceWhateverTheNumberOfRivers()
    {
        var builder = new GameStateBuilder().WithCapitals().WithTerrain(TestRulesets.Desert, (5, 4), (4, 4), (6, 4), (5, 3), (4, 3), (5, 5), (4, 5));
        var city = builder.AddCity(P1, 5, 4);
        builder.WithRiver(TestRulesets.River, ((5, 4), (6, 4)), ((5, 4), (4, 4)));

        // Desert gives nothing: the water comes from the rivers (1 for each tile beside one, however many edges it has).
        var stock = EndTurn(builder.Build()).State.FindCity(city)!.StockList;
        YieldCalculator.AmountOf(stock, TestRulesets.Water).ShouldBe(3);
        YieldCalculator.AmountOf(stock, TestRulesets.Food).ShouldBe(0);
    }

    [Fact]
    public void EndTurn_ArmyCamp_AddsProductionToItsCity()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P1, 5, 5, buildings: [TestRulesets.ArmyCamp]);

        EndTurn(builder.Build()).State.FindCity(city)!.ProductionProgress.ShouldBe(2);
    }

    [Fact]
    public void EndTurn_BuildingFinished_JoinsTheCityStandsOnItsSiteAndProductionGoesBackToTheUnit()
    {
        var builder = new GameStateBuilder().WithCapitals().WithBuilding(TestRulesets.TradePost, (6, 5));
        var city = builder.AddCity(P1, 5, 5, TestRulesets.Archer, progress: 7, producingBuilding: TestRulesets.Campus, paid: true, buildingSite: (5, 4), buildings: [TestRulesets.TradePost]);

        var update = EndTurn(builder.Build());

        var finished = update.State.FindCity(city)!;
        finished.BuildingList.ShouldBe([TestRulesets.TradePost, TestRulesets.Campus]);
        finished.ProducingBuilding.ShouldBeNull();
        finished.ProducingBuildingSite.ShouldBeNull();
        finished.Producing.ShouldBe(TestRulesets.Archer);
        finished.ProductionProgress.ShouldBe(0);
        update.State.Map.TileAt(At(5, 4)).Building.ShouldBe(TestRulesets.Campus);
        update.State.Map.TileAt(At(5, 5)).Building.ShouldBeNull("the city tile never holds a building");
        update.Events.ShouldContain(new BuildingCompletedEvent(city, TestRulesets.Campus, At(5, 4)));
        YieldCalculator.AmountOf(update.State.FindCity(city)!.StockList, TestRulesets.Gold).ShouldBe(2);
        YieldCalculator.AmountOf(update.State.FindCity(city)!.StockList, TestRulesets.Science).ShouldBe(0, "a new building yields from the next turn");
    }

    [Fact]
    public void Capture_KeepsTheBuildingsOnTheirTilesAndStopsTheBuildingInProduction()
    {
        var builder = new GameStateBuilder().WithCapitals().WithTerrain(TestRulesets.Lake, (7, 4)).WithBuilding(TestRulesets.Campus, (7, 5));
        var city = builder.AddCity(P2, 6, 5, progress: 3, producingBuilding: TestRulesets.Harbour, buildingSite: (6, 4), buildings: [TestRulesets.Campus]);
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 5, 5);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new AttackCommand(attacker, At(6, 5))).ShouldSucceed();

        var captured = update.State.FindCity(city)!;
        captured.Owner.ShouldBe(P1);
        captured.BuildingList.ShouldBe([TestRulesets.Campus]);
        captured.ProducingBuilding.ShouldBeNull();
        captured.ProducingBuildingSite.ShouldBeNull();
        update.State.Map.TileAt(At(7, 5)).Building.ShouldBe(TestRulesets.Campus);
        update.State.Map.TileAt(At(7, 5)).Territory.ShouldBe(city);
    }

    private static GameUpdate EndTurn(Govern.Core.State.GameState state, Govern.Core.State.PlayerId? player = null) =>
        GameEngine.Execute(TestRulesets.Default, state, player ?? P1, new EndTurnCommand()).ShouldSucceed();
}

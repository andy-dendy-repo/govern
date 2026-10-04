using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.Core.Visibility;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Actions;

public class UpgradeBuildingActionTests
{
    [Fact]
    public void Execute_BuilderOnACampus_UpgradesItIntoAUniversityWhenTheJobIsDone()
    {
        var (state, city, builder) = Campus(stone: 6);

        var started = GameEngine.Execute(TestRulesets.Default, state, P1, new UpgradeBuildingCommand(builder)).ShouldSucceed();
        started.State.FindUnit(builder)!.Job.ShouldBe(UnitJob.ForUpgrade(TestRulesets.University, 1));

        // The builder's 2 and the city's 1 a turn: 10 production takes 4 turns.
        var done = Turns(started.State, 4);

        var after = done.State.FindCity(city)!;
        after.BuildingList.ShouldBe([TestRulesets.University]);
        after.DamageOf(TestRulesets.University).ShouldBe(0);
        YieldCalculator.AmountOf(after.StockList, TestRulesets.Stone).ShouldBe(0, "the university's 6 stone are paid");
        done.State.Map.TileAt(At(6, 5)).Building.ShouldBe(TestRulesets.University);
        done.State.FindUnit(builder)!.Job.ShouldBeNull();
        done.Events.ShouldContain(new BuildingUpgradedEvent(builder, P1, At(6, 5), TestRulesets.Campus, TestRulesets.University, city));
        GameEngine.ViewFor(TestRulesets.Default, done.State, P1).Science!.Income.ShouldBe(6, "the capital's 1, the city's 1 and the university's 4");
    }

    [Fact]
    public void Execute_CityCantPayTheUpgrade_TheFinishedJobWaits()
    {
        var (state, city, builder) = Campus(stone: 2);
        var started = GameEngine.Execute(TestRulesets.Default, state, P1, new UpgradeBuildingCommand(builder)).ShouldSucceed();

        var waiting = Turns(started.State, 5);

        waiting.State.FindCity(city)!.BuildingList.ShouldBe([TestRulesets.Campus]);
        waiting.State.FindUnit(builder)!.Job!.Progress.ShouldBe(10);
    }

    [Fact]
    public void Execute_UpgradedTradePost_StillTrades()
    {
        var builder = new GameStateBuilder().WithCapitals().WithNodes(P1, "trade").WithBuilding(TestRulesets.Market, (6, 5));
        var city = builder.AddCity(P1, 5, 5, buildings: [TestRulesets.Market]);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new SetProductionCommand(city, TestRulesets.Trader)).ShouldSucceed();
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Execute_Refused_WithTheReasonTheViewGivesToo(string scenario, string code)
    {
        var (state, unit) = Build(scenario);

        GameEngine.Execute(TestRulesets.Default, state, P1, new UpgradeBuildingCommand(unit)).Violation!.Code.ShouldBe(code);
        SciencePreviewCalculator.CheckUpgradeBuilding(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), unit)!.Code.ShouldBe(code);
    }

    [Fact]
    public void CheckUpgradeBuilding_AllowedUpgrade_IsNull()
    {
        var (state, unit) = Build("allowed");

        SciencePreviewCalculator.CheckUpgradeBuilding(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), unit).ShouldBeNull();
        GameEngine.Execute(TestRulesets.Default, state, P1, new UpgradeBuildingCommand(unit)).ShouldSucceed();
    }

    public static TheoryData<string, string> Scenarios => new()
    {
        { "not a builder", "unit.cannot_build_improvements" },
        { "no building", "upgrade.none" },
        { "building without an upgrade", "upgrade.none" },
        { "node not owned", "science.locked" },
        { "enemy building", "upgrade.not_in_territory" },
        { "no movement", "unit.no_movement" },
        { "already upgrading", "build.already_building" },
    };

    private static (GameState State, UnitId Unit) Build(string scenario)
    {
        var builder = new GameStateBuilder().WithCapitals()
            .WithBuilding(TestRulesets.Campus, (6, 5))
            .WithBuilding(TestRulesets.CultureCentre, (5, 4))
            .WithBuilding(TestRulesets.Campus, (8, 5));
        if (scenario != "node not owned")
        {
            builder.WithNodes(P1, "education");
        }

        builder.AddCity(P1, 5, 5, buildings: [TestRulesets.Campus, TestRulesets.CultureCentre]);
        builder.AddCity(P2, 9, 5, buildings: [TestRulesets.Campus]);
        var unit = scenario switch
        {
            "not a builder" => builder.AddUnit(P1, TestRulesets.Soldier, 6, 5),
            "no building" => builder.AddUnit(P1, TestRulesets.Builder, 4, 5),
            "building without an upgrade" => builder.AddUnit(P1, TestRulesets.Builder, 5, 4),
            "enemy building" => builder.AddUnit(P1, TestRulesets.Builder, 8, 5),
            "no movement" => builder.AddUnit(P1, TestRulesets.Builder, 6, 5, movementLeft: 0),
            "already upgrading" => builder.AddUnit(P1, TestRulesets.Builder, 6, 5, job: UnitJob.ForUpgrade(TestRulesets.University, 1)),
            _ => builder.AddUnit(P1, TestRulesets.Builder, 6, 5),
        };
        return (builder.Build(), unit);
    }

    private static (GameState State, CityId City, UnitId Builder) Campus(int stone)
    {
        var builder = new GameStateBuilder().WithCapitals().WithNodes(P1, "education").WithBuilding(TestRulesets.Campus, (6, 5));
        var city = builder.AddCity(P1, 5, 5, stock: [new ResourceAmount(TestRulesets.Gold, 20), new ResourceAmount(TestRulesets.Stone, stone)], buildings: [TestRulesets.Campus]);
        var unit = builder.AddUnit(P1, TestRulesets.Builder, 6, 5);
        return (builder.Build(), city, unit);
    }

    // Ends P1's turn, then P2's, the given number of times; returns P1's last end of turn.
    private static GameUpdate Turns(GameState state, int count)
    {
        GameUpdate? last = null;
        for (var turn = 0; turn < count; turn++)
        {
            last = GameEngine.Execute(TestRulesets.Default, state, P1, new EndTurnCommand()).ShouldSucceed();
            state = GameEngine.Execute(TestRulesets.Default, last.State, P2, new EndTurnCommand()).ShouldSucceed().State;
        }

        return last!;
    }
}

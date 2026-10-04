using Govern.Core.Actions;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Units;
using Govern.Core.Visibility;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Actions;

public class UpgradeUnitActionTests
{
    [Fact]
    public void Execute_OldUnitOnOwnTerritory_BecomesTheNewOneAndTheCityPays()
    {
        var builder = new GameStateBuilder().WithCapitals().WithNodes(P1, "bronze_working");
        var city = builder.AddCity(P1, 5, 5, stock: [new ResourceAmount(TestRulesets.Stone, 3)]);
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 6, 5, damage: 30, destination: (8, 5));

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new UpgradeUnitCommand(soldier)).ShouldSucceed();

        var upgraded = update.State.FindUnit(soldier)!;
        upgraded.Type.ShouldBe(TestRulesets.Swordsman);
        upgraded.Damage.ShouldBe(30, "it keeps its health");
        upgraded.MovementLeft.ShouldBe(0);
        upgraded.Destination.ShouldBeNull();
        update.State.FindCity(city)!.StockList.ShouldBe([new ResourceAmount(TestRulesets.Stone, 1)]);
        update.Events.ShouldBe([new UnitUpgradedEvent(soldier, P1, At(6, 5), TestRulesets.Soldier, TestRulesets.Swordsman)]);
    }

    [Fact]
    public void Execute_TwoVersionsAhead_GoesToTheNewestUnlocked()
    {
        var document = TestRulesets.Document(json =>
        {
            TestRulesets.AddUnit(json, "legionary", 2, 2, 8, "attack");
            var units = json["units"]!.AsArray();
            units[units.Count - 1]!["replaces"] = "swordsman";
            units[units.Count - 1]!["upgradeCost"] = new System.Text.Json.Nodes.JsonArray();
            json["science"]!["nodes"]![4]!["units"]!.AsArray().Add("legionary");
        });
        var ruleset = Govern.Core.Rulesets.Ruleset.ParseValid(document.ToJsonString());
        var builder = new GameStateBuilder().WithCapitals().WithNodes(P1, "bronze_working");
        builder.AddCity(P1, 5, 5);
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 6, 5);

        var update = GameEngine.Execute(ruleset, builder.Build(), P1, new UpgradeUnitCommand(soldier)).ShouldSucceed();

        update.State.FindUnit(soldier)!.Type.ShouldBe(new UnitType("legionary"));
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Execute_Refused_WithTheReasonTheViewGivesToo(string scenario, string code)
    {
        var (state, unit) = Build(scenario);

        GameEngine.Execute(TestRulesets.Default, state, P1, new UpgradeUnitCommand(unit)).Violation!.Code.ShouldBe(code);
        SciencePreviewCalculator.CheckUpgradeUnit(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), unit)!.Code.ShouldBe(code);
    }

    [Fact]
    public void CheckUpgradeUnit_AllowedUpgrade_IsNull()
    {
        var (state, unit) = Build("allowed");

        SciencePreviewCalculator.CheckUpgradeUnit(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), unit).ShouldBeNull();
        GameEngine.Execute(TestRulesets.Default, state, P1, new UpgradeUnitCommand(unit)).ShouldSucceed();
    }

    public static TheoryData<string, string> Scenarios => new()
    {
        { "node not owned", "upgrade.none" },
        { "nothing newer", "upgrade.none" },
        { "outside territory", "upgrade.not_in_territory" },
        { "enemy territory", "upgrade.not_in_territory" },
        { "no stone", "upgrade.missing_resources" },
        { "no movement", "unit.no_movement" },
        { "enemy unit", "unit.not_found" },
    };

    private static (Govern.Core.State.GameState State, UnitId Unit) Build(string scenario)
    {
        var builder = new GameStateBuilder().WithCapitals();
        if (scenario != "node not owned")
        {
            builder.WithNodes(P1, "bronze_working");
        }

        builder.AddCity(P1, 5, 5, stock: scenario == "no stone" ? [new ResourceAmount(TestRulesets.Stone, 1)] : [new ResourceAmount(TestRulesets.Stone, 2)]);
        builder.AddCity(P2, 9, 5);
        var unit = scenario switch
        {
            "nothing newer" => builder.AddUnit(P1, TestRulesets.Swordsman, 6, 5),
            "outside territory" => builder.AddUnit(P1, TestRulesets.Soldier, 7, 2),
            "enemy territory" => builder.AddUnit(P1, TestRulesets.Soldier, 8, 5),
            "no movement" => builder.AddUnit(P1, TestRulesets.Soldier, 6, 5, movementLeft: 0),
            "enemy unit" => builder.AddUnit(P2, TestRulesets.Soldier, 6, 5),
            _ => builder.AddUnit(P1, TestRulesets.Soldier, 6, 5),
        };
        return (builder.Build(), unit);
    }
}

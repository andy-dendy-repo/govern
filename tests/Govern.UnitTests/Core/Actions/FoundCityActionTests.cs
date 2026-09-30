using System.Text.Json.Nodes;
using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Actions;

public class FoundCityActionTests
{
    [Fact]
    public void Execute_Settler_FoundsCityAndConsumesSettler()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var settler = builder.AddUnit(P1, TestRulesets.Settler, 5, 5);
        var state = builder.Build();

        var update = GameEngine.Execute(TestRulesets.Default, state, P1, new FoundCityCommand(settler)).ShouldSucceed();

        update.State.FindUnit(settler).ShouldBeNull();
        var city = update.State.CityAt(At(5, 5)).ShouldNotBeNull();
        city.Owner.ShouldBe(P1);
        city.Name.ShouldBe("City 2");
        city.Producing.ShouldBe(TestRulesets.Soldier);
        city.ProductionProgress.ShouldBe(0);
        state.Units.ShouldNotContain(unit => unit.Id.Value == city.Id.Value);
        state.Cities.ShouldNotContain(existing => existing.Id.Value == city.Id.Value);
        update.State.RngState.ShouldNotBe(state.RngState);
        update.Events.ShouldHaveSingleItem().ShouldBe(new CityFoundedEvent(city, settler));
    }

    [Fact]
    public void Execute_Soldier_ReturnsCannotFoundCity()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 5, 5);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new FoundCityCommand(soldier));

        result.ShouldFailWith(RuleViolations.CannotFoundCity);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public void Execute_DistanceToNearestCity_EnforcesMinimumSpacing(int distance, bool allowed)
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddCity(P2, 5, 5);
        var settler = builder.AddUnit(P1, TestRulesets.Settler, 5 - distance, 5);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new FoundCityCommand(settler));

        if (allowed)
        {
            result.ShouldSucceed();
        }
        else
        {
            result.ShouldFailWith(RuleViolations.CityTooClose);
        }
    }

    [Fact]
    public void Execute_SettlerInsideOwnCity_ReturnsTileTaken()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var settler = builder.AddUnit(P1, TestRulesets.Settler, 0, 0);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new FoundCityCommand(settler));

        result.ShouldFailWith(RuleViolations.CityTileTaken);
    }

    [Fact]
    public void Execute_SettlerWithoutMovement_ReturnsNoMovement()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var settler = builder.AddUnit(P1, TestRulesets.Settler, 5, 5, movementLeft: 0);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new FoundCityCommand(settler));

        result.ShouldFailWith(RuleViolations.NoMovementLeft);
    }

    [Fact]
    public void Execute_RulesetCityRules_NameTheCityAndSetItsProduction()
    {
        var ruleset = TestRulesets.With(json =>
        {
            json["texts"]!["city.default_name"] = "Town {n}";
            json["cities"]!["initialProduction"] = "settler";
        });
        var builder = new GameStateBuilder().WithCapitals();
        var settler = builder.AddUnit(P1, TestRulesets.Settler, 5, 5);

        var update = GameEngine.Execute(ruleset, builder.Build(), P1, new FoundCityCommand(settler)).ShouldSucceed();

        var city = update.State.CityAt(At(5, 5)).ShouldNotBeNull();
        city.Name.ShouldBe("Town 2");
        city.Producing.ShouldBe(TestRulesets.Settler);
    }

    [Fact]
    public void Execute_RulesetSpacing_IsEnforced()
    {
        var ruleset = TestRulesets.With(json =>
        {
            json["cities"]!["minimumSpacing"] = 4;
            json["units"]![0]!["sight"] = 3;
        });
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddCity(P2, 5, 5);
        var settler = builder.AddUnit(P1, TestRulesets.Settler, 2, 5);

        var result = GameEngine.Execute(ruleset, builder.Build(), P1, new FoundCityCommand(settler));

        result.ShouldFailWith(RuleViolations.CityTooClose);
    }

    [Fact]
    public void Execute_UnitWithFoundCityAbility_FoundsACity()
    {
        var ruleset = TestRulesets.With(json => json["units"]![1]!["abilities"] = new JsonArray("attack", "found_city"));
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 5, 5);

        GameEngine.Execute(ruleset, builder.Build(), P1, new FoundCityCommand(soldier)).ShouldSucceed();
    }
}

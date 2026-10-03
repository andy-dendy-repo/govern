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

        var update = GameEngine.Execute(TestRulesets.Default, state, P1, new FoundCityCommand(settler, "Ambermoor")).ShouldSucceed();

        update.State.FindUnit(settler).ShouldBeNull();
        var city = update.State.CityAt(At(5, 5)).ShouldNotBeNull();
        city.Owner.ShouldBe(P1);
        city.Name.ShouldBe("Ambermoor");
        city.Producing.ShouldBe(TestRulesets.Soldier);
        city.ProductionProgress.ShouldBe(0);
        state.Units.ShouldNotContain(unit => unit.Id.Value == city.Id.Value);
        state.Cities.ShouldNotContain(existing => existing.Id.Value == city.Id.Value);
        update.State.RngState.ShouldNotBe(state.RngState);
        update.Events.ShouldHaveSingleItem().ShouldBe(new CityFoundedEvent(city.Id, P1, At(5, 5), "Ambermoor", settler));
    }

    [Fact]
    public void Execute_Soldier_ReturnsCannotFoundCity()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 5, 5);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new FoundCityCommand(soldier, "Ambermoor"));

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

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new FoundCityCommand(settler, "Ambermoor"));

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

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new FoundCityCommand(settler, "Ambermoor"));

        result.ShouldFailWith(RuleViolations.CityTileTaken);
    }

    [Fact]
    public void Execute_SettlerWithoutMovement_ReturnsNoMovement()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var settler = builder.AddUnit(P1, TestRulesets.Settler, 5, 5, movementLeft: 0);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new FoundCityCommand(settler, "Ambermoor"));

        result.ShouldFailWith(RuleViolations.NoMovementLeft);
    }

    [Fact]
    public void Execute_RulesetInitialProduction_IsWhatTheCityBuilds()
    {
        var ruleset = TestRulesets.With(json => json["cities"]!["initialProduction"] = "settler");
        var builder = new GameStateBuilder().WithCapitals();
        var settler = builder.AddUnit(P1, TestRulesets.Settler, 5, 5);

        var update = GameEngine.Execute(ruleset, builder.Build(), P1, new FoundCityCommand(settler, "Ambermoor")).ShouldSucceed();

        update.State.CityAt(At(5, 5)).ShouldNotBeNull().Producing.ShouldBe(TestRulesets.Settler);
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

        var result = GameEngine.Execute(ruleset, builder.Build(), P1, new FoundCityCommand(settler, "Ambermoor"));

        result.ShouldFailWith(RuleViolations.CityTooClose);
    }

    [Fact]
    public void Execute_UnitWithFoundCityAbility_FoundsACity()
    {
        var ruleset = TestRulesets.With(json => json["units"]![1]!["abilities"] = new JsonArray("attack", "build_bridge", "found_city"));
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 5, 5);

        GameEngine.Execute(ruleset, builder.Build(), P1, new FoundCityCommand(soldier, "Ambermoor")).ShouldSucceed();
    }

    [Theory]
    [InlineData("  Old   Harbour ", "Old Harbour")]
    [InlineData("Saint-Éloi", "Saint-Éloi")]
    [InlineData("O'Neil Fort. 2", "O'Neil Fort. 2")]
    [InlineData("\tKyiv\n", "Kyiv")]
    public void Execute_Name_IsTrimmedWithSpacesCollapsed(string name, string expected)
    {
        var builder = new GameStateBuilder().WithCapitals();
        var settler = builder.AddUnit(P1, TestRulesets.Settler, 5, 5);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new FoundCityCommand(settler, name)).ShouldSucceed();

        update.State.CityAt(At(5, 5)).ShouldNotBeNull().Name.ShouldBe(expected);
        update.Events.OfType<CityFoundedEvent>().Single().Name.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A name that is far too long")]
    [InlineData("<script>")]
    [InlineData("Tab\u0000City")]
    [InlineData("Home!")]
    public void Execute_InvalidName_ReturnsNameInvalidAndChangesNothing(string? name)
    {
        var builder = new GameStateBuilder().WithCapitals();
        var settler = builder.AddUnit(P1, TestRulesets.Settler, 5, 5);
        var state = builder.Build();

        var result = GameEngine.Execute(TestRulesets.Default, state, P1, new FoundCityCommand(settler, name!));

        result.ShouldFailWith(RuleViolations.CityNameInvalid);
    }

    [Fact]
    public void Execute_NameOfLength24_IsAccepted()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var settler = builder.AddUnit(P1, TestRulesets.Settler, 5, 5);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new FoundCityCommand(settler, new string('a', FoundCityCommand.MaxNameLength))).ShouldSucceed();
    }

    [Theory]
    [InlineData("City 1")]
    [InlineData("city 1")]
    [InlineData(" CITY   1 ")]
    public void Execute_NameOfAnOwnCity_ReturnsNameTaken(string name)
    {
        var builder = new GameStateBuilder().WithCapitals();
        var settler = builder.AddUnit(P1, TestRulesets.Settler, 5, 5);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new FoundCityCommand(settler, name));

        result.ShouldFailWith(RuleViolations.CityNameTaken);
    }

    [Fact]
    public void Execute_NameOfAnEnemyCity_IsAccepted()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var settler = builder.AddUnit(P1, TestRulesets.Settler, 5, 5);

        // P2's capital is "City 2" and stands far out of P1's sight.
        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new FoundCityCommand(settler, "City 2")).ShouldSucceed();
    }

    [Fact]
    public void Execute_TooCloseAndInvalidName_ReturnsThePlacementProblemFirst()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var settler = builder.AddUnit(P1, TestRulesets.Settler, 1, 0);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new FoundCityCommand(settler, "")).ShouldFailWith(RuleViolations.CityTooClose);
    }
}

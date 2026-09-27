using Govern.Core.Commands;
using Govern.Core.Engine;
using Govern.Core.Errors;
using Govern.Core.Events;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Rules;

public class FoundCityRuleTests
{
    [Fact]
    public void Execute_Settler_FoundsCityAndConsumesSettler()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var settler = builder.AddUnit(P1, UnitType.Settler, 5, 5);
        var state = builder.Build();

        var update = GameEngine.Execute(state, P1, new FoundCityCommand(settler)).ShouldSucceed();

        update.State.FindUnit(settler).ShouldBeNull();
        var city = update.State.CityAt(At(5, 5)).ShouldNotBeNull();
        city.Owner.ShouldBe(P1);
        city.Name.ShouldBe("City 2");
        city.Producing.ShouldBe(UnitType.Soldier);
        city.ProductionProgress.ShouldBe(0);
        update.State.NextId.ShouldBe(state.NextId + 1);
        update.Events.ShouldHaveSingleItem().ShouldBe(new CityFoundedEvent(city, settler));
    }

    [Fact]
    public void Execute_Soldier_ReturnsCannotFoundCity()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, UnitType.Soldier, 5, 5);

        var result = GameEngine.Execute(builder.Build(), P1, new FoundCityCommand(soldier));

        result.ShouldFailWith(GameErrors.CannotFoundCity);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public void Execute_DistanceToNearestCity_EnforcesMinimumSpacing(int distance, bool allowed)
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddCity(P2, 5, 5);
        var settler = builder.AddUnit(P1, UnitType.Settler, 5 - distance, 5);

        var result = GameEngine.Execute(builder.Build(), P1, new FoundCityCommand(settler));

        if (allowed)
        {
            result.ShouldSucceed();
        }
        else
        {
            result.ShouldFailWith(GameErrors.CityTooClose);
        }
    }

    [Fact]
    public void Execute_SettlerInsideOwnCity_ReturnsTileTaken()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var settler = builder.AddUnit(P1, UnitType.Settler, 0, 0);

        var result = GameEngine.Execute(builder.Build(), P1, new FoundCityCommand(settler));

        result.ShouldFailWith(GameErrors.CityTileTaken);
    }

    [Fact]
    public void Execute_SettlerWithoutMovement_ReturnsNoMovement()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var settler = builder.AddUnit(P1, UnitType.Settler, 5, 5, movementLeft: 0);

        var result = GameEngine.Execute(builder.Build(), P1, new FoundCityCommand(settler));

        result.ShouldFailWith(GameErrors.NoMovementLeft);
    }
}

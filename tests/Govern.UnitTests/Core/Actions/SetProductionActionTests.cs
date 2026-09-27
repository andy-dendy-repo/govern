using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Actions;

public class SetProductionActionTests
{
    [Fact]
    public void Execute_OwnCity_ChangesProductionAndKeepsProgress()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P1, 5, 5, UnitType.Soldier, progress: 2);

        var update = GameEngine.Execute(builder.Build(), P1, new SetProductionCommand(city, UnitType.Settler)).ShouldSucceed();

        var changed = update.State.FindCity(city)!;
        changed.Producing.ShouldBe(UnitType.Settler);
        changed.ProductionProgress.ShouldBe(2);
        update.Events.ShouldHaveSingleItem().ShouldBe(new ProductionChangedEvent(city, UnitType.Settler));
    }

    [Fact]
    public void Execute_UnknownCity_ReturnsNotFound()
    {
        var state = new GameStateBuilder().WithCapitals().Build();

        var result = GameEngine.Execute(state, P1, new SetProductionCommand(new CityId(999), UnitType.Settler));

        result.ShouldFailWith(RuleViolations.CityNotFound);
    }

    [Fact]
    public void Execute_EnemyCity_ReturnsNotFoundLikeAMissingId()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P2, 5, 5);

        var result = GameEngine.Execute(builder.Build(), P1, new SetProductionCommand(city, UnitType.Settler));

        result.ShouldFailWith(RuleViolations.CityNotFound);
    }

    [Fact]
    public void Execute_UndefinedUnitType_ReturnsUnknownUnitType()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P1, 5, 5);

        var result = GameEngine.Execute(builder.Build(), P1, new SetProductionCommand(city, (UnitType)99));

        result.ShouldFailWith(RuleViolations.UnknownUnitType);
    }
}

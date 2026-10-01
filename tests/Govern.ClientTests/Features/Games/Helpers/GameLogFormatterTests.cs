using Govern.Client.Features.Games.Helpers;
using Govern.Core.Cities;
using Govern.Core.Events;
using Govern.Core.Units;
using Govern.ClientTests.Support;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Helpers;

public class GameLogFormatterTests
{
    private static readonly Govern.Core.Visibility.PlayerView View = Create(
        units: [Soldier(1, You, At(2, 2))],
        cities: [City(7, You, At(3, 3), "Rome")]);

    [Fact]
    public void Describe_OwnMove_NamesYourUnitAndItsDestination()
    {
        var line = GameLogFormatter.Describe(Ruleset, View, new UnitMovedEvent(new UnitId(1), You, SoldierType, [At(2, 3), At(2, 4)]))!;

        line.Text.ShouldBe("Your Soldier moved");
        line.Tile.ShouldBe(At(2, 4));
        line.IsYours.ShouldBeTrue();
    }

    [Fact]
    public void Describe_EnemyAttackOnYourUnit_NamesTheSeatAndTheDamage()
    {
        var attacked = new UnitAttackedEvent(new UnitId(9), Opponent, ArcherType, At(4, 2), new UnitId(1), You, SoldierType, At(2, 2), 22, 0);

        var line = GameLogFormatter.Describe(Ruleset, View, attacked)!;

        line.Text.ShouldBe("Seat 2's Archer hit your Soldier: 22 damage");
        line.Tile.ShouldBe(At(2, 2));
        line.IsYours.ShouldBeFalse();
    }

    [Fact]
    public void Describe_AttackFromAnUnseenTile_SaysSoAndCountsDamageTaken()
    {
        var attacked = new UnitAttackedEvent(null, Opponent, SoldierType, null, new UnitId(1), You, SoldierType, At(2, 2), 18, 7);

        GameLogFormatter.Describe(Ruleset, View, attacked)!.Text.ShouldBe("An unseen Seat 2 Soldier hit your Soldier: 18 damage, 7 taken");
    }

    [Fact]
    public void Describe_OwnCityEvents_UseTheCityName()
    {
        var line = GameLogFormatter.Describe(Ruleset, View, new BuildingCompletedEvent(new CityId(7), Campus))!;

        line.Text.ShouldBe("Rome finished a Campus");
        line.Tile.ShouldBe(At(3, 3));
    }

    [Fact]
    public void Describe_UpkeepPriority_SaysWhatIsPaidFirst() =>
        GameLogFormatter.Describe(Ruleset, View, new UpkeepPrioritySetEvent(new CityId(7), UpkeepPriority.UnitsFirst))!.Text
            .ShouldBe("Rome now pays for units first");

    [Fact]
    public void Describe_TurnPassedToYou_SaysItIsYourTurnWithoutATile()
    {
        var line = GameLogFormatter.Describe(Ruleset, View, new TurnEndedEvent(Opponent, You, 3))!;

        line.Text.ShouldBe("Turn 3: your turn");
        line.Tile.ShouldBeNull();
    }

    [Fact]
    public void Describe_CapturedCity_NamesBothSides() =>
        GameLogFormatter.Describe(Ruleset, View, new CityCapturedEvent(new CityId(8), "Aria", At(5, 4), Opponent, You, new UnitId(1)))!.Text
            .ShouldBe("You captured Aria from Seat 2");

    [Theory]
    [InlineData("Archer", "an Archer")]
    [InlineData("Soldier", "a Soldier")]
    public void Article_ChoosesByTheFirstLetter(string name, string expected) => GameLogFormatter.Article(name).ShouldBe(expected);
}

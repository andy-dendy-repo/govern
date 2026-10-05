using Govern.Client.Features.Games.Helpers;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Events;
using Govern.Core.Science;
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

    [Theory]
    [InlineData(OrderEndReason.Arrived, "Your Soldier arrived at its destination")]
    [InlineData(OrderEndReason.EnemySighted, "Your Soldier stopped: an enemy came into sight")]
    [InlineData(OrderEndReason.Blocked, "Your Soldier stopped: there is no known way to its destination")]
    [InlineData(OrderEndReason.Explored, "Your Soldier finished exploring: no known way leads to anything unexplored")]
    public void Describe_OrderEnded_SaysWhy(OrderEndReason reason, string text)
    {
        var line = GameLogFormatter.Describe(Ruleset, View, new UnitOrderEndedEvent(new UnitId(1), You, SoldierType, At(2, 2), At(6, 2), reason))!;

        line.Text.ShouldBe(text);
        line.Tile.ShouldBe(At(2, 2));
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
    public void Describe_BuildingCompleted_UsesTheCityNameAndPointsAtTheBuildingsTile()
    {
        var line = GameLogFormatter.Describe(Ruleset, View, new BuildingCompletedEvent(new CityId(7), Campus, At(3, 2)))!;

        line.Text.ShouldBe("Rome finished a Campus");
        line.Tile.ShouldBe(At(3, 2));
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
    [InlineData(TradeChange.Proposed, "Seat 2's trader from a city offered Rome a deal")]
    [InlineData(TradeChange.Countered, "New terms were offered for the deal between a city and Rome")]
    [InlineData(TradeChange.Accepted, "The deal between a city and Rome was accepted: the trader sets off for Rome")]
    [InlineData(TradeChange.Started, "Seat 2's trader reached Rome: the deal with a city is active")]
    [InlineData(TradeChange.Broken, "The deal between a city and Rome broke: its trader was lost")]
    [InlineData(TradeChange.Ended, "The deal between a city and Rome ended: a city changed hands")]
    public void Describe_TradeDealChange_NamesTheCitiesItKnows(TradeChange change, string text)
    {
        var line = GameLogFormatter.Describe(Ruleset, View, new TradeDealChangedEvent(new TradeDealId(4), Opponent, You, new CityId(9), new CityId(7), change))!;

        line.Text.ShouldBe(text);
        line.Tile.ShouldBe(At(3, 3));
        line.IsYours.ShouldBeFalse();
    }

    [Theory]
    [InlineData(false, false, "Rome gave 3 food to Tyre for 1 stone")]
    [InlineData(false, true, "No trade between Rome and Tyre this turn: Tyre lacked 1 stone")]
    [InlineData(true, true, "No trade between Rome and Tyre this turn: neither city had its part")]
    public void Describe_TradeExchanged_SaysWhatMovedOrWhoLackedIt(bool homeShort, bool hostShort, string text)
    {
        var route = new Govern.Core.Visibility.TradeDealView(new TradeDealId(4), You, new CityId(7), "Rome", new CityId(9), "Tyre", Opponent, new TradeTerms([new(Food, 3)], [new(Stone, 1)]), TradeDealStatus.Active);
        var view = View with { TradeDeals = [route] };
        var exchanged = new TradeExchangedEvent(route.Id, You, Opponent, route.Home, route.Host, [new(Food, 3)], [new(Stone, 1)], homeShort, hostShort);

        var line = GameLogFormatter.Describe(Ruleset, view, exchanged)!;

        line.Text.ShouldBe(text);
        line.IsYours.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Archer", "an Archer")]
    [InlineData("Soldier", "a Soldier")]
    [InlineData("University", "a University")]
    public void Article_ChoosesByTheFirstSound(string name, string expected) => GameLogFormatter.Article(name).ShouldBe(expected);

    [Theory]
    [InlineData(ScienceNodeChange.Unlocked, "You unlocked Archery")]
    [InlineData(ScienceNodeChange.Disabled, "Archery switched off: your cities make too little science")]
    [InlineData(ScienceNodeChange.Enabled, "Archery works again")]
    public void Describe_ScienceNodeChanged_SaysWhatHappened(ScienceNodeChange change, string text)
    {
        var line = GameLogFormatter.Describe(Ruleset, View, new ScienceNodeChangedEvent(You, "archery", change))!;

        line.Text.ShouldBe(text);
        line.Tile.ShouldBeNull();
        line.IsYours.ShouldBeTrue();
    }

    [Fact]
    public void Describe_UnitUpgraded_NamesBothVersions()
    {
        var line = GameLogFormatter.Describe(Ruleset, View, new UnitUpgradedEvent(new UnitId(1), You, At(2, 2), SoldierType, new UnitType("swordsman")))!;

        line.Text.ShouldBe("You upgraded a Soldier to a Swordsman");
        line.Tile.ShouldBe(At(2, 2));
    }

    [Fact]
    public void Describe_EnemyBuildingUpgraded_NamesTheSeat()
    {
        var upgraded = new BuildingUpgradedEvent(new UnitId(9), Opponent, At(5, 2), Campus, new BuildingType("university"), null);

        var line = GameLogFormatter.Describe(Ruleset, View, upgraded)!;

        line.Text.ShouldBe("Seat 2 upgraded a Campus to a University");
        line.IsYours.ShouldBeFalse();
    }

    [Fact]
    public void Describe_UpgradeStarted_NamesTheUpgrade()
    {
        var line = GameLogFormatter.Describe(Ruleset, View, new BuildStartedEvent(new UnitId(1), UnitJob.ForUpgrade(new BuildingType("university"), 3)))!;

        line.Text.ShouldBe("Your Soldier started an upgrade to a University");
    }
}

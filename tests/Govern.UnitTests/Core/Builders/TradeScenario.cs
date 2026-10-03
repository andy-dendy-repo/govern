using System.Collections.Immutable;
using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.State;
using Govern.Core.Units;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Builders;

/// <summary>
/// A trading scenario on row 2: P1's home city at (2, 2) with its trade post at (3, 2), and a host city at (8, 2) (P2's, or P1's
/// own) with its trade post at (7, 2). P1's trader stands on the home city; with an enemy host, P1's scout at (8, 4) watches the
/// host and its post. Both cities have plenty of every resource and have paid for their production, and their markets match:
/// home sells up to 6 Food and buys up to 4 Stone, the host buys up to 6 Food and sells up to 4 Stone.
/// </summary>
internal sealed class TradeScenario
{
    public static readonly ImmutableArray<ResourceAmount> Plenty =
    [
        new(TestRulesets.Food, 30), new(TestRulesets.Water, 30), new(TestRulesets.Wood, 30), new(TestRulesets.Stone, 30), new(TestRulesets.Gold, 30),
    ];

    public TradeScenario(PlayerId? hostOwner = null)
    {
        var host = hostOwner ?? P2;
        Builder = new GameStateBuilder().WithMapExplored(P1).WithMapExplored(P2);
        Home = Builder.AddCity(P1, 2, 2, stock: Plenty, paid: true, buildings: TestRulesets.TradePost);
        Host = Builder.AddCity(host, 8, 2, stock: Plenty, paid: true, buildings: TestRulesets.TradePost);
        Builder.WithBuilding(TestRulesets.TradePost, (3, 2)).WithBuilding(TestRulesets.TradePost, (7, 2));
        Trader = Builder.AddUnit(P1, TestRulesets.Trader, 2, 2);
        if (host == P2)
        {
            Builder.AddUnit(P1, TestRulesets.Scout, 8, 4);
        }
        else
        {
            Builder.AddCity(P2, 11, 9);
        }

        Builder.WithCity(Home, static city => city with { Market = [Order(TestRulesets.Food, MarketSide.Sell, 6), Order(TestRulesets.Stone, MarketSide.Buy, 4)] });
        Builder.WithCity(Host, static city => city with { Market = [Order(TestRulesets.Food, MarketSide.Buy, 6), Order(TestRulesets.Stone, MarketSide.Sell, 4)] });
    }

    public GameStateBuilder Builder { get; }

    public CityId Home { get; }

    public CityId Host { get; }

    public UnitId Trader { get; }

    public static MarketOrder Order(ResourceType resource, MarketSide side, int cap) => new(resource, side, cap);

    public static ImmutableArray<ResourceAmount> Amounts(params (ResourceType Resource, int Amount)[] amounts) =>
        [.. amounts.Select(static amount => new ResourceAmount(amount.Resource, amount.Amount))];

    public static int StockOf(GameState state, CityId city, ResourceType resource) =>
        YieldCalculator.AmountOf(state.FindCity(city)!.StockList, resource);

    public static GameUpdate Execute(GameState state, PlayerId actor, GameCommand command) =>
        GameEngine.Execute(TestRulesets.Default, state, actor, command).ShouldSucceed();

    public static GameState EndRound(GameState state) =>
        Execute(Execute(state, P1, new EndTurnCommand()).State, P2, new EndTurnCommand()).State;

    // P1's turn of round 2, the trader next to the host's post and the route open.
    public (GameState State, TradeRouteId Route) Opened()
    {
        var sent = Execute(Builder.Build(), P1, new OpenTradeRouteCommand(Trader, Home, Host)).State;
        var state = EndRound(sent);
        return (state, state.TradeRouteList.ShouldHaveSingleItem().Id);
    }

    // As Opened, with P1's proposal of 3 Food for 1 Stone waiting (accepted at once when P1 hosts it too).
    public (GameState State, TradeRouteId Route) Proposed()
    {
        var (state, route) = Opened();
        return (Execute(state, P1, new ProposeDealCommand(route, Amounts((TestRulesets.Food, 3)), Amounts((TestRulesets.Stone, 1)))).State, route);
    }

    // P2's turn of round 2 with the deal of 3 Food for 1 Stone accepted (P2 hosts it).
    public (GameState State, TradeRouteId Route) Accepted()
    {
        var (state, route) = Proposed();
        var p2Turn = Execute(state, P1, new EndTurnCommand()).State;
        return (Execute(p2Turn, P2, new AnswerDealCommand(route, Accept: true)).State, route);
    }
}

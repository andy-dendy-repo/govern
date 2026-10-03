using System.Collections.Immutable;
using Govern.Core.Actions;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.Core.Visibility;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;
using static Govern.UnitTests.Core.Builders.TradeScenario;

namespace Govern.UnitTests.Core.Visibility;

// The trade previews over a PlayerView must answer exactly what Execute decides.
public class TradePreviewCalculatorTests
{
    [Fact]
    public void CheckOpenTradeRoute_EveryUnitAndCityPair_MatchesExecute()
    {
        var scenario = new TradeScenario();
        scenario.Builder.AddUnit(P1, TestRulesets.Soldier, 3, 3);
        scenario.Builder.AddCity(P1, 2, 7, stock: Plenty, paid: true);
        var hidden = scenario.Builder.AddCity(P2, 10, 7, stock: Plenty, paid: true, buildings: TestRulesets.TradePost);
        scenario.Builder.WithBuilding(TestRulesets.TradePost, (10, 6));
        var sent = Execute(scenario.Builder.Build(), P1, new OpenTradeRouteCommand(scenario.Trader, scenario.Home, scenario.Host)).State;

        foreach (var state in new[] { scenario.Builder.Build(), sent })
        {
            var view = GameEngine.ViewFor(TestRulesets.Default, state, P1);
            foreach (var unit in state.Units.Select(static unit => unit.Id).Append(new UnitId(999)))
            {
                foreach (var home in state.Cities.Select(static city => city.Id))
                {
                    foreach (var host in state.Cities.Select(static city => city.Id))
                    {
                        TradePreviewCalculator.CheckOpenTradeRoute(TestRulesets.Default, view, unit, home, host)
                            .ShouldBe(Violation(state, P1, new OpenTradeRouteCommand(unit, home, host)), $"{unit} {home} {host}");
                    }
                }
            }
        }

        hidden.ShouldNotBe(scenario.Host);
    }

    [Fact]
    public void CheckProposeDeal_ManyTerms_MatchExecute()
    {
        var scenario = new TradeScenario();
        var sent = Execute(scenario.Builder.Build(), P1, new OpenTradeRouteCommand(scenario.Trader, scenario.Home, scenario.Host)).State;
        var (opened, route) = scenario.Opened();
        var (proposed, _) = scenario.Proposed();
        var p2Turn = Execute(proposed, P1, new EndTurnCommand()).State;

        foreach (var (state, actor) in new[] { (sent, P1), (opened, P1), (proposed, P1), (p2Turn, P2) })
        {
            var view = GameEngine.ViewFor(TestRulesets.Default, state, actor);
            foreach (var (gives, receives) in Terms())
            {
                foreach (var id in new[] { state.TradeRouteList.Single().Id, new TradeRouteId(4242) })
                {
                    TradePreviewCalculator.CheckProposeDeal(TestRulesets.Default, view, id, gives, receives)
                        .ShouldBe(Violation(state, actor, new ProposeDealCommand(id, gives, receives)));
                }
            }
        }

        route.ShouldBe(opened.TradeRouteList.Single().Id);
    }

    [Fact]
    public void CheckAcceptDeal_WithAndWithoutCancelling_MatchesExecute()
    {
        var scenario = new TradeScenario();
        var p2Trader = scenario.Builder.AddUnit(P2, TestRulesets.Trader, 8, 3);
        var p2Home = scenario.Builder.AddCity(P2, 8, 7, stock: Plenty, paid: true, buildings: TestRulesets.TradePost);
        scenario.Builder.WithBuilding(TestRulesets.TradePost, (8, 6))
            .WithCity(p2Home, static city => city with { Market = [Order(TestRulesets.Gold, MarketSide.Sell, 5), Order(TestRulesets.Stone, MarketSide.Buy, 4)] })
            .WithCity(scenario.Host, static city => city with { Market = [Order(TestRulesets.Food, MarketSide.Buy, 6), Order(TestRulesets.Gold, MarketSide.Buy, 5), Order(TestRulesets.Stone, MarketSide.Sell, 4)] });
        var (proposed, route) = scenario.Proposed();
        var p2Turn = Execute(proposed, P1, new EndTurnCommand()).State;
        var sent = Execute(p2Turn, P2, new OpenTradeRouteCommand(p2Trader, p2Home, scenario.Host));
        var own = sent.State.TradeRouteList.Single(other => other.Owner == P2).Id;
        var withOwnDeal = Execute(sent.State, P2, new ProposeDealCommand(own, Amounts((TestRulesets.Gold, 2)), Amounts((TestRulesets.Stone, 4)))).State;

        foreach (var (state, actor) in new[] { (proposed, P1), (p2Turn, P2), (withOwnDeal, P2) })
        {
            var view = GameEngine.ViewFor(TestRulesets.Default, state, actor);
            foreach (var cancel in new ImmutableArray<TradeRouteId>[] { [], [own], [route], [new TradeRouteId(4242)] })
            {
                foreach (var id in new[] { route, own, new TradeRouteId(4242) })
                {
                    TradePreviewCalculator.CheckAcceptDeal(TestRulesets.Default, view, id, cancel)
                        .ShouldBe(Violation(state, actor, new AnswerDealCommand(id, Accept: true, cancel)), $"{id} {cancel.Length}");
                }
            }
        }
    }

    private static IEnumerable<(ImmutableArray<ResourceAmount> Gives, ImmutableArray<ResourceAmount> Receives)> Terms()
    {
        ResourceType[] resources = [TestRulesets.Food, TestRulesets.Stone, TestRulesets.Gold, TestRulesets.Water];
        foreach (var give in resources)
        {
            foreach (var take in resources)
            {
                foreach (var amount in new[] { 0, 1, 4, 7 })
                {
                    yield return (Amounts((give, amount == 0 ? 3 : amount)), amount == 0 ? [] : Amounts((take, amount)));
                }
            }
        }
    }

    private static RuleViolation? Violation(GameState state, PlayerId actor, GameCommand command) =>
        GameEngine.Execute(TestRulesets.Default, state, actor, command).Violation;
}

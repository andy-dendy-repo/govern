using System.Collections.Immutable;
using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
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
    public void CheckProposeDeal_EveryTraderHostAndTerms_MatchesExecute()
    {
        // An own city without a post, an own city with one, and an enemy city whose post P1 never saw.
        var scenario = new TradeScenario();
        scenario.Builder.AddUnit(P1, TestRulesets.Soldier, 3, 3);
        scenario.Builder.AddUnit(P1, TestRulesets.Trader, 2, 3);
        scenario.Builder.AddCity(P1, 2, 7, stock: Plenty, paid: true);
        var ownPost = scenario.Builder.AddCity(P1, 5, 8, stock: Plenty, paid: true, buildings: TestRulesets.TradePost);
        var hidden = scenario.Builder.AddCity(P2, 10, 7, stock: Plenty, paid: true, buildings: TestRulesets.TradePost);
        scenario.Builder.WithBuilding(TestRulesets.TradePost, (10, 6)).WithBuilding(TestRulesets.TradePost, (5, 9))
            .WithCity(ownPost, static city => city with { Market = [Order(TestRulesets.Food, MarketSide.Buy, 2), Order(TestRulesets.Stone, MarketSide.Sell, 1)] });
        var (proposed, _) = scenario.Proposed();
        var p2Turn = Execute(proposed, P1, new EndTurnCommand()).State;

        foreach (var (state, actor) in new[] { (scenario.Builder.Build(), P1), (proposed, P1), (p2Turn, P2) })
        {
            var view = GameEngine.ViewFor(TestRulesets.Default, state, actor);
            foreach (var unit in state.Units.Select(static unit => unit.Id).Append(new UnitId(999)))
            {
                foreach (var host in state.Cities.Select(static city => city.Id).Append(new CityId(999)))
                {
                    foreach (var (gives, receives) in Terms())
                    {
                        TradePreviewCalculator.CheckProposeDeal(TestRulesets.Default, view, unit, host, new TradeTerms(gives, receives))
                            .ShouldBe(Violation(state, actor, new ProposeDealCommand(unit, host, gives, receives)), $"{unit} {host}");
                    }
                }
            }
        }

        hidden.ShouldNotBe(scenario.Host);
    }

    [Fact]
    public void CheckAnswerDeal_AcceptWithAndWithoutCancellingAndCounters_MatchExecute()
    {
        var scenario = new TradeScenario();
        var p2Home = scenario.Builder.AddCity(P2, 8, 7, stock: Plenty, paid: true, buildings: TestRulesets.TradePost);
        var p2Trader = scenario.Builder.AddUnit(P2, TestRulesets.Trader, 8, 7, homeCity: p2Home);
        scenario.Builder.WithBuilding(TestRulesets.TradePost, (8, 6))
            .WithCity(p2Home, static city => city with { Market = [Order(TestRulesets.Gold, MarketSide.Sell, 5), Order(TestRulesets.Stone, MarketSide.Buy, 4)] })
            .WithCity(scenario.Host, static city => city with { Market = [Order(TestRulesets.Food, MarketSide.Buy, 6), Order(TestRulesets.Gold, MarketSide.Buy, 5), Order(TestRulesets.Stone, MarketSide.Sell, 4)] });
        var (proposed, deal) = scenario.Proposed();
        var p2Turn = Execute(proposed, P1, new EndTurnCommand()).State;
        var withOwnDeal = Execute(p2Turn, P2, new ProposeDealCommand(p2Trader, scenario.Host, Amounts((TestRulesets.Gold, 2)), Amounts((TestRulesets.Stone, 4)))).State;
        var own = withOwnDeal.TradeDealList.Single(other => other.Owner == P2).Id;
        var countered = Execute(p2Turn, P2, new AnswerDealCommand(deal, DealAnswer.Counter, Amounts((TestRulesets.Food, 4)), Amounts((TestRulesets.Stone, 1)))).State;
        var p1Answers = Execute(countered, P2, new EndTurnCommand()).State;

        foreach (var (state, actor) in new[] { (proposed, P1), (p2Turn, P2), (withOwnDeal, P2), (p1Answers, P1) })
        {
            var view = GameEngine.ViewFor(TestRulesets.Default, state, actor);
            foreach (var id in new[] { deal, own, new TradeDealId(4242) })
            {
                foreach (var cancel in new ImmutableArray<TradeDealId>[] { [], [own], [deal], [new TradeDealId(4242)] })
                {
                    TradePreviewCalculator.CheckAnswerDeal(TestRulesets.Default, view, id, null, cancel)
                        .ShouldBe(Violation(state, actor, new AnswerDealCommand(id, DealAnswer.Accept, CancelDeals: cancel)), $"{id} {cancel.Length}");
                }

                foreach (var (gives, receives) in Terms())
                {
                    TradePreviewCalculator.CheckAnswerDeal(TestRulesets.Default, view, id, new TradeTerms(gives, receives), [])
                        .ShouldBe(Violation(state, actor, new AnswerDealCommand(id, DealAnswer.Counter, gives, receives)), $"{id} counter");
                }
            }
        }
    }

    [Fact]
    public void FreeMarket_OwnAndPartnerCities_MatchTheRules()
    {
        // P1's agreed deal takes 3 Food and 1 Stone from both markets; the view works that out from the viewer's own deals.
        var scenario = new TradeScenario();
        var (accepted, _) = scenario.Accepted();
        var p1Turn = Execute(accepted, P2, new EndTurnCommand()).State;

        var view = GameEngine.ViewFor(TestRulesets.Default, p1Turn, P1);

        TradePreviewCalculator.FreeMarket(view, scenario.Home).ShouldBe([Order(TestRulesets.Food, MarketSide.Sell, 3), Order(TestRulesets.Stone, MarketSide.Buy, 3)]);
        TradePreviewCalculator.FreeMarket(view, scenario.Host).ShouldBe([Order(TestRulesets.Food, MarketSide.Buy, 3), Order(TestRulesets.Stone, MarketSide.Sell, 3)]);
        TradePreviewCalculator.Targets(view, scenario.Home).ShouldBe([(scenario.Host, p1Turn.FindCity(scenario.Host)!.Name, P2)]);
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

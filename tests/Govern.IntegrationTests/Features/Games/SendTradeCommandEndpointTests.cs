using System.Net;
using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;

namespace Govern.IntegrationTests.Features.Games;

// The trade commands (G17, G17b) over POST /api/games/{id}/commands: the happy path, malformed bodies and rule violations. Auth,
// turn and version checks are the endpoint's own and covered by SendGameCommandEndpointTests.
public class SendTradeCommandEndpointTests(GovernApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Post_SetMarketOrder_Returns200AndTheCityShowsItsMarket()
    {
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var (city, version) = await FoundCityAsync(game.Id, creator);
        var json = $$"""
            { "expectedVersion": {{version}},
              "command": { "type": "set_market_order", "cityId": { "value": {{city.Value}} }, "resource": "stone", "side": "buy", "cap": 4 } }
            """;

        var response = await GamesApi.SendCommandJsonAsync(Client, creator.AccessToken, game.Id, json, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = (await GamesApi.ReadGameAsync(response, Ct)).View!.Cities.Single(c => c.Id == city);
        updated.MarketList.ShouldBe([new MarketOrder(new ResourceType("stone"), MarketSide.Buy, 4)]);
    }

    [Fact]
    public async Task Post_SetMarketOrderForWater_Returns409WithTheRuleViolationCode()
    {
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var (city, version) = await FoundCityAsync(game.Id, creator);

        var response = await GamesApi.SendCommandAsync(
            Client, creator.AccessToken, game.Id, version, new SetMarketOrderCommand(city, new ResourceType("water"), MarketSide.Sell, 3), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, RuleViolations.NotTradable.Code, Ct);
    }

    [Theory]
    [InlineData("""{ "type": "set_market_order", "cityId": { "value": 1 }, "resource": "stone", "side": "steal", "cap": 4 }""")]
    [InlineData("""{ "type": "propose_deal", "traderId": { "value": 1 }, "hostId": { "value": 2 }, "gives": [{ "resource": "food" }], "receives": [] }""")]
    [InlineData("""{ "type": "answer_deal", "dealId": { "value": 1 } }""")]
    [InlineData("""{ "type": "answer_deal", "dealId": { "value": 1 }, "answer": "haggle" }""")]
    [InlineData("""{ "type": "open_trade_route", "traderId": { "value": 1 } }""")]
    public async Task Post_MalformedTradeCommand_Returns400(string command)
    {
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);

        var response = await GamesApi.SendCommandJsonAsync(Client, creator.AccessToken, game.Id, $$"""{ "expectedVersion": 0, "command": {{command}} }""", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_ProposeDealWithASoldier_Returns409WithTheRuleViolationCode()
    {
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var (city, version) = await FoundCityAsync(game.Id, creator);
        var view = (await GamesApi.GetGameAsync(Client, creator, game.Id, Ct)).View!;
        var soldier = view.Units.First(u => u.Owner == view.Viewer && TestRulesets.Default.Unit(u.Type).CanAttack);

        var response = await GamesApi.SendCommandAsync(Client, creator.AccessToken, game.Id, version, new ProposeDealCommand(soldier.Id, city, [new ResourceAmount(new ResourceType("food"), 3)], [new ResourceAmount(new ResourceType("stone"), 1)]), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, RuleViolations.CannotTrade.Code, Ct);
    }

    public static TheoryData<GameCommand> DealCommands => new()
    {
        new AnswerDealCommand(new TradeDealId(4242), DealAnswer.Accept),
        new AnswerDealCommand(new TradeDealId(4242), DealAnswer.Counter, [new ResourceAmount(new ResourceType("food"), 3)], [new ResourceAmount(new ResourceType("stone"), 1)]),
        new CancelDealCommand(new TradeDealId(4242)),
    };

    [Theory]
    [MemberData(nameof(DealCommands))]
    public async Task Post_CommandOnAnUnknownDeal_Returns409DealNotFound(GameCommand command)
    {
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var version = (await GamesApi.GetGameAsync(Client, creator, game.Id, Ct)).View!.Version;

        var response = await GamesApi.SendCommandAsync(Client, creator.AccessToken, game.Id, version, command, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, RuleViolations.DealNotFound.Code, Ct);
    }

    // The creator's settler founds a city; returns the city and the creator's view version after it.
    private async Task<(CityId City, long Version)> FoundCityAsync(Guid gameId, GamesApi.Player creator)
    {
        var view = (await GamesApi.GetGameAsync(Client, creator, gameId, Ct)).View!;
        var settler = view.Units.First(u => u.Owner == view.Viewer && TestRulesets.Default.Unit(u.Type).CanFoundCity);
        var response = await GamesApi.SendCommandAsync(Client, creator.AccessToken, gameId, view.Version, new FoundCityCommand(settler.Id, "Ambermoor"), Ct);
        response.EnsureSuccessStatusCode();
        var after = (await GamesApi.ReadGameAsync(response, Ct)).View!;
        return (after.Cities.Single(c => c.Owner == after.Viewer).Id, after.Version);
    }
}

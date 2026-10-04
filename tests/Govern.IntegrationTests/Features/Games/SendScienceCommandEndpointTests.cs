using System.Net;
using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.Units;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;

namespace Govern.IntegrationTests.Features.Games;

// The science commands (G11a) over POST /api/games/{id}/commands: the happy path, malformed bodies, rule violations and the
// science staying private. Auth, turn and version checks are the endpoint's own and covered by SendGameCommandEndpointTests.
public class SendScienceCommandEndpointTests(GovernApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Post_UnlockNode_Returns200AndTheViewShowsTheNodeToItsOwnerOnly()
    {
        var (game, creator, joiner) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var version = await FoundCityAsync(game.Id, creator);

        var response = await GamesApi.SendCommandAsync(Client, creator.AccessToken, game.Id, version, new UnlockNodeCommand("archery"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var science = (await GamesApi.ReadGameAsync(response, Ct)).View!.Science!;
        science.Income.ShouldBe(1);
        science.NodeList.Select(static node => node.Node).ShouldBe(["archery"]);
        var opponent = (await GamesApi.GetGameAsync(Client, joiner, game.Id, Ct)).View!;
        (opponent.Science?.NodeList ?? []).ShouldBeEmpty();
    }

    [Fact]
    public async Task Post_UnlockNodeAboveTheIncome_Returns409NotEnoughScience()
    {
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var version = await FoundCityAsync(game.Id, creator);

        var response = await GamesApi.SendCommandAsync(Client, creator.AccessToken, game.Id, version, new UnlockNodeCommand("education"), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, RuleViolations.NotEnoughScience.Code, Ct);
    }

    [Fact]
    public async Task Post_UnlockUnknownNode_Returns409UnknownNode()
    {
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var version = (await GamesApi.GetGameAsync(Client, creator, game.Id, Ct)).View!.Version;

        var response = await GamesApi.SendCommandAsync(Client, creator.AccessToken, game.Id, version, new UnlockNodeCommand("time_travel"), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, RuleViolations.UnknownNode.Code, Ct);
    }

    [Fact]
    public async Task Post_UpgradeUnitWithoutANewerVersion_Returns409NoUpgrade()
    {
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var view = (await GamesApi.GetGameAsync(Client, creator, game.Id, Ct)).View!;
        var soldier = OwnUnit(view, static definition => definition.CanAttack);

        var response = await GamesApi.SendCommandAsync(Client, creator.AccessToken, game.Id, view.Version, new UpgradeUnitCommand(soldier), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, RuleViolations.NoUpgrade.Code, Ct);
    }

    [Fact]
    public async Task Post_UpgradeBuildingWithASoldier_Returns409CannotBuildImprovements()
    {
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var view = (await GamesApi.GetGameAsync(Client, creator, game.Id, Ct)).View!;
        var soldier = OwnUnit(view, static definition => definition.CanAttack);

        var response = await GamesApi.SendCommandAsync(Client, creator.AccessToken, game.Id, view.Version, new UpgradeBuildingCommand(soldier), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, RuleViolations.CannotBuildImprovements.Code, Ct);
    }

    [Theory]
    [InlineData("""{ "type": "unlock_node" }""")]
    [InlineData("""{ "type": "upgrade_unit" }""")]
    [InlineData("""{ "type": "upgrade_building", "unitId": "one" }""")]
    public async Task Post_MalformedScienceCommand_Returns400(string command)
    {
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);

        var response = await GamesApi.SendCommandJsonAsync(Client, creator.AccessToken, game.Id, $$"""{ "expectedVersion": 0, "command": {{command}} }""", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static UnitId OwnUnit(Core.Visibility.PlayerView view, Func<UnitDefinition, bool> kind) =>
        view.Units.First(unit => unit.Owner == view.Viewer && kind(TestRulesets.Default.Unit(unit.Type))).Id;

    // The creator's settler founds a city (1 science a turn); returns the creator's view version after it.
    private async Task<long> FoundCityAsync(Guid gameId, GamesApi.Player creator)
    {
        var view = (await GamesApi.GetGameAsync(Client, creator, gameId, Ct)).View!;
        var settler = OwnUnit(view, static definition => definition.CanFoundCity);
        var response = await GamesApi.SendCommandAsync(Client, creator.AccessToken, gameId, view.Version, new FoundCityCommand(settler, "Ambermoor"), Ct);
        response.EnsureSuccessStatusCode();
        return (await GamesApi.ReadGameAsync(response, Ct)).View!.Version;
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.State;
using Govern.Domain.Enums;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace Govern.IntegrationTests.Features.Games;

public class SendGameCommandEndpointTests(GovernApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Post_EndTurn_Returns200WithTheOpponentToMoveAndPersists()
    {
        var (game, creator, joiner) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var before = await GamesApi.GetGameAsync(Client, creator, game.Id, Ct);

        var response = await GamesApi.SendCommandAsync(Client, creator.AccessToken, game.Id, before.View!.Version, new EndTurnCommand(), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await GamesApi.ReadGameAsync(response, Ct);
        var opponent = body.Players.Single(p => !p.IsYou);
        body.View.ShouldNotBeNull().Version.ShouldNotBe(before.View.Version);
        body.View.CurrentPlayer.ShouldBe(new PlayerId(opponent.PlayerId));

        var (scope, db) = await Fixture.OpenDbAsync();
        await using (scope)
        await using (db)
        {
            var stored = await db.Games.AsNoTracking().SingleAsync(g => g.Id == game.Id, Ct);
            stored.CurrentPlayerId.ShouldBe(opponent.PlayerId);
            stored.Revision.ShouldBe(3);
            var you = new PlayerId(body.Players.Single(p => p.IsYou).PlayerId);
            GameEngine.ViewFor(TestRulesets.Default, stored.State.ShouldNotBeNull(), you).Version.ShouldBe(body.View.Version);
        }

        (await GamesApi.GetGameAsync(Client, joiner, game.Id, Ct)).View!.CurrentPlayer.ShouldBe(new PlayerId(opponent.PlayerId));
    }

    [Fact]
    public async Task Post_EndTurn_PutsTheTurnInBothPlayersLogsAndStoresOneEntryPerSeat()
    {
        var (game, creator, joiner) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var before = await GamesApi.GetGameAsync(Client, creator, game.Id, Ct);
        before.Log.ShouldBeEmpty();

        var response = await GamesApi.SendCommandAsync(Client, creator.AccessToken, game.Id, before.View!.Version, new EndTurnCommand(), Ct);

        var own = (await GamesApi.ReadGameAsync(response, Ct)).Log.ShouldHaveSingleItem();
        own.Sequence.ShouldBe(1);
        own.Turn.ShouldBe(1);
        own.Events.OfType<TurnEndedEvent>().ShouldHaveSingleItem();
        var theirs = (await GamesApi.GetGameAsync(Client, joiner, game.Id, Ct)).Log.ShouldHaveSingleItem();
        theirs.Events.ShouldAllBe(gameEvent => gameEvent is TurnEndedEvent);

        var (scope, db) = await Fixture.OpenDbAsync();
        await using (scope)
        await using (db)
        {
            (await db.GameLogEntries.CountAsync(entry => entry.GameId == game.Id, Ct)).ShouldBe(2);
        }
    }

    [Fact]
    public async Task Post_MoveOutOfTheOpponentsSight_LogsItOnlyForTheMover()
    {
        var (game, creator, joiner) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var view = (await GamesApi.GetGameAsync(Client, creator, game.Id, Ct)).View!;
        var unit = view.Units.First(u => u.Owner == view.Viewer);
        var target = GameEngine.ReachableTiles(TestRulesets.Default, await StoredStateAsync(game.Id), view.Viewer, unit.Id).First();

        var response = await GamesApi.SendCommandAsync(Client, creator.AccessToken, game.Id, view.Version, new MoveUnitCommand(unit.Id, target), Ct);

        var moved = (await GamesApi.ReadGameAsync(response, Ct)).Log.ShouldHaveSingleItem().Events.ShouldHaveSingleItem().ShouldBeOfType<UnitMovedEvent>();
        moved.UnitId.ShouldBe(unit.Id);
        moved.Path[^1].ShouldBe(target);
        (await GamesApi.GetGameAsync(Client, joiner, game.Id, Ct)).Log.ShouldBeEmpty();
    }

    [Fact]
    public async Task Post_MoveUnitWithTypeLast_Returns200AndMovesTheUnit()
    {
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var view = (await GamesApi.GetGameAsync(Client, creator, game.Id, Ct)).View!;
        var unit = view.Units.First(u => u.Owner == view.Viewer);
        var state = await StoredStateAsync(game.Id);
        var target = GameEngine.ReachableTiles(TestRulesets.Default, state, view.Viewer, unit.Id).First();
        var json = $$"""
            { "expectedVersion": {{view.Version}},
              "command": { "unitId": { "value": {{unit.Id.Value}} }, "target": { "q": {{target.Q}}, "r": {{target.R}} }, "type": "move_unit" } }
            """;

        var response = await GamesApi.SendCommandJsonAsync(Client, creator.AccessToken, game.Id, json, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var moved = (await GamesApi.ReadGameAsync(response, Ct)).View!.Units.Single(u => u.Id == unit.Id);
        moved.Position.ShouldBe(target);
    }

    [Fact]
    public async Task Post_ProduceBuilding_Returns200AndTheCityProducesIt()
    {
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var (city, version) = await FoundCityAsync(game.Id, creator);
        var json = $$"""
            { "expectedVersion": {{version}},
              "command": { "type": "produce_building", "cityId": { "value": {{city.Value}} }, "building": "campus" } }
            """;

        var response = await GamesApi.SendCommandJsonAsync(Client, creator.AccessToken, game.Id, json, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var produced = (await GamesApi.ReadGameAsync(response, Ct)).View!.Cities.Single(c => c.Id == city);
        produced.ProducingBuilding.ShouldBe(new BuildingType("campus"));
    }

    [Fact]
    public async Task Post_ProduceUnknownBuilding_Returns409WithTheRuleViolationCode()
    {
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var (city, version) = await FoundCityAsync(game.Id, creator);

        var response = await GamesApi.SendCommandAsync(
            Client, creator.AccessToken, game.Id, version, new ProduceBuildingCommand(city, new BuildingType("castle")), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, RuleViolations.UnknownBuilding.Code, Ct);
    }

    [Fact]
    public async Task Post_SetUpkeepPriority_Returns200AndTheCityPaysUnitsFirst()
    {
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var (city, version) = await FoundCityAsync(game.Id, creator);
        var json = $$"""
            { "expectedVersion": {{version}},
              "command": { "type": "set_upkeep_priority", "cityId": { "value": {{city.Value}} }, "priority": "unitsFirst" } }
            """;

        var response = await GamesApi.SendCommandJsonAsync(Client, creator.AccessToken, game.Id, json, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = (await GamesApi.ReadGameAsync(response, Ct)).View!.Cities.Single(c => c.Id == city);
        updated.UpkeepPriority.ShouldBe(UpkeepPriority.UnitsFirst);
        (await StoredStateAsync(game.Id)).FindCity(city).ShouldNotBeNull().UpkeepPriority.ShouldBe(UpkeepPriority.UnitsFirst);
    }

    [Fact]
    public async Task Post_SetUnknownUpkeepPriority_Returns400()
    {
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var (city, version) = await FoundCityAsync(game.Id, creator);
        var json = $$"""
            { "expectedVersion": {{version}},
              "command": { "type": "set_upkeep_priority", "cityId": { "value": {{city.Value}} }, "priority": "goldFirst" } }
            """;

        var response = await GamesApi.SendCommandJsonAsync(Client, creator.AccessToken, game.Id, json, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_Resign_FinishesTheGameAndLaterCommandsReturnGameFinished()
    {
        var (game, creator, joiner) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var version = (await GamesApi.GetGameAsync(Client, joiner, game.Id, Ct)).View!.Version;

        var response = await GamesApi.SendCommandAsync(Client, joiner.AccessToken, game.Id, version, new ResignCommand(), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await GamesApi.ReadGameAsync(response, Ct);
        body.Phase.ShouldBe(GamePhase.Finished);
        body.View.ShouldNotBeNull().Status.ShouldBe(GameStatus.Finished);
        body.View.Winner.ShouldBe(new PlayerId(body.Players.Single(p => !p.IsYou).PlayerId));

        var creatorVersion = (await GamesApi.GetGameAsync(Client, creator, game.Id, Ct)).View!.Version;
        var after = await GamesApi.SendCommandAsync(Client, creator.AccessToken, game.Id, creatorVersion, new EndTurnCommand(), Ct);
        await after.ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.finished", Ct);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("""{ "expectedVersion": 0, "command": {} }""")]
    [InlineData("""{ "expectedVersion": 0, "command": { "type": "teleport" } }""")]
    [InlineData("""{ "command": { "type": "end_turn" } }""")]
    [InlineData("""{ "expectedVersion": 0, "command": { "type": "move_unit" } }""")]
    public async Task Post_MalformedBody_Returns400(string json)
    {
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);

        var response = await GamesApi.SendCommandJsonAsync(Client, creator.AccessToken, game.Id, json, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, code: null, Ct);
    }

    [Fact]
    public async Task Post_NegativeExpectedVersion_Returns400()
    {
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);

        var response = await GamesApi.SendCommandAsync(Client, creator.AccessToken, game.Id, -1, new EndTurnCommand(), Ct);

        await response.ShouldBeValidationProblemAsync(Ct, "ExpectedVersion");
    }

    [Fact]
    public async Task Post_EmptyGameId_Returns400()
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.SendCommandAsync(Client, creator.AccessToken, Guid.Empty, 0, new EndTurnCommand(), Ct);

        await response.ShouldBeValidationProblemAsync(Ct, "GameId");
    }

    [Fact]
    public async Task Post_BodyOver4KiB_Returns413()
    {
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var json = $$"""{ "expectedVersion": 0, "command": { "type": "end_turn" } }{{new string(' ', 4096)}}""";

        var response = await GamesApi.SendCommandJsonAsync(Client, creator.AccessToken, game.Id, json, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.RequestEntityTooLarge, code: null, Ct);
    }

    [Fact]
    public async Task Post_StreamedBodyOver4KiBWithoutContentLength_Returns413()
    {
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var json = $$"""{ "expectedVersion": 0, "command": { "type": "end_turn" } }{{new string(' ', 8192)}}""";
        using var content = new UnknownLengthContent(Encoding.UTF8.GetBytes(json), "application/json");

        var response = await GamesApi.SendCommandContentAsync(Client, creator.AccessToken, game.Id, content, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.RequestEntityTooLarge, code: null, Ct);
    }

    [Fact]
    public async Task Post_WithoutContentType_Returns415()
    {
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        using var content = new UnknownLengthContent(
            Encoding.UTF8.GetBytes("""{ "expectedVersion": 0, "command": { "type": "end_turn" } }"""), mediaType: null);

        var response = await GamesApi.SendCommandContentAsync(Client, creator.AccessToken, game.Id, content, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.UnsupportedMediaType, code: null, Ct);
    }

    [Fact]
    public async Task Post_NotJson_Returns415()
    {
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);

        var response = await GamesApi.SendCommandJsonAsync(
            Client, creator.AccessToken, game.Id, """{ "expectedVersion": 0, "command": { "type": "end_turn" } }""", Ct, "text/plain");

        await response.ShouldBeProblemAsync(HttpStatusCode.UnsupportedMediaType, code: null, Ct);
    }

    [Fact]
    public async Task Post_WithoutToken_Returns401()
    {
        var (game, _, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);

        var response = await GamesApi.SendCommandAsync(Client, accessToken: null, game.Id, 0, new EndTurnCommand(), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Outsider_Returns404()
    {
        var (game, _, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var outsider = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.SendCommandAsync(Client, outsider.AccessToken, game.Id, 0, new EndTurnCommand(), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "game.not_found", Ct);
    }

    [Fact]
    public async Task Post_UnknownGame_Returns404()
    {
        var player = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.SendCommandAsync(Client, player.AccessToken, Guid.CreateVersion7(), 0, new EndTurnCommand(), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "game.not_found", Ct);
    }

    [Fact]
    public async Task Post_Lobby_Returns409NotStarted()
    {
        var (game, creator, _) = await GamesApi.CreateFullLobbyAsync(Client, Ct);

        var response = await GamesApi.SendCommandAsync(Client, creator.AccessToken, game.Id, 0, new EndTurnCommand(), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.not_started", Ct);
    }

    [Fact]
    public async Task Post_VersionFromBeforeTheOpponentsTurnEnded_Returns409StaleVersion()
    {
        var (game, creator, joiner) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var joinerVersion = (await GamesApi.GetGameAsync(Client, joiner, game.Id, Ct)).View!.Version;
        var creatorVersion = (await GamesApi.GetGameAsync(Client, creator, game.Id, Ct)).View!.Version;
        (await GamesApi.SendCommandAsync(Client, creator.AccessToken, game.Id, creatorVersion, new EndTurnCommand(), Ct)).EnsureSuccessStatusCode();

        var response = await GamesApi.SendCommandAsync(Client, joiner.AccessToken, game.Id, joinerVersion, new EndTurnCommand(), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.stale_version", Ct);
    }

    [Fact]
    public async Task Post_AnotherPlayersVersion_Returns409StaleVersion()
    {
        var (game, creator, joiner) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var joinerVersion = (await GamesApi.GetGameAsync(Client, joiner, game.Id, Ct)).View!.Version;

        var response = await GamesApi.SendCommandAsync(Client, creator.AccessToken, game.Id, joinerVersion, new EndTurnCommand(), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.stale_version", Ct);
    }

    [Fact]
    public async Task Post_NotYourTurn_Returns409WithTheRuleViolationCode()
    {
        var (game, _, joiner) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var version = (await GamesApi.GetGameAsync(Client, joiner, game.Id, Ct)).View!.Version;

        var response = await GamesApi.SendCommandAsync(Client, joiner.AccessToken, game.Id, version, new EndTurnCommand(), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, RuleViolations.NotYourTurn.Code, Ct);
    }

    [Fact]
    public async Task Post_SameVersionConcurrently_AppliesOnce()
    {
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var version = (await GamesApi.GetGameAsync(Client, creator, game.Id, Ct)).View!.Version;
        var commandsBefore = (await StoredStateAsync(game.Id)).Version;

        var responses = await GameRowLock.RaceAsync(Fixture.ConnectionString, game.Id, 3, () => Task.WhenAll(
            Enumerable.Range(0, 3).Select(_ => GamesApi.SendCommandAsync(Client, creator.AccessToken, game.Id, version, new EndTurnCommand(), Ct))), Ct);

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        foreach (var rejected in responses.Where(r => r.StatusCode != HttpStatusCode.OK))
        {
            await rejected.ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.stale_version", Ct);
        }

        (await StoredStateAsync(game.Id)).Version.ShouldBe(commandsBefore + 1);
    }

    [Fact]
    public async Task Post_OverRateLimit_Returns429ForThatUserOnly()
    {
        using var factory = Fixture.Factory.WithWebHostBuilder(static builder => builder.UseSetting("RateLimiting:GameCommandsPermitLimit", "2"));
        using var client = factory.CreateClient();
        var (game, creator, joiner) = await GamesApi.CreateStartedGameAsync(client, Ct);

        for (var i = 0; i < 2; i++)
        {
            (await GamesApi.SendCommandAsync(client, creator.AccessToken, game.Id, 0, new EndTurnCommand(), Ct)).StatusCode
                .ShouldNotBe(HttpStatusCode.TooManyRequests);
        }

        var limited = await GamesApi.SendCommandAsync(client, creator.AccessToken, game.Id, 0, new EndTurnCommand(), Ct);
        var other = await GamesApi.SendCommandAsync(client, joiner.AccessToken, game.Id, 0, new EndTurnCommand(), Ct);

        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        await other.ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.stale_version", Ct);
    }

    // The creator's settler founds a city; returns the city and the creator's view version after it.
    private async Task<(CityId City, long Version)> FoundCityAsync(Guid gameId, GamesApi.Player creator)
    {
        var view = (await GamesApi.GetGameAsync(Client, creator, gameId, Ct)).View!;
        var settler = view.Units.First(u => u.Owner == view.Viewer && TestRulesets.Default.Unit(u.Type).CanFoundCity);
        var response = await GamesApi.SendCommandAsync(Client, creator.AccessToken, gameId, view.Version, new FoundCityCommand(settler.Id), Ct);
        response.EnsureSuccessStatusCode();
        var after = (await GamesApi.ReadGameAsync(response, Ct)).View!;
        return (after.Cities.Single(c => c.Owner == after.Viewer).Id, after.Version);
    }

    private async Task<GameState> StoredStateAsync(Guid gameId)
    {
        var (scope, db) = await Fixture.OpenDbAsync();
        await using (scope)
        await using (db)
        {
            return (await db.Games.AsNoTracking().SingleAsync(g => g.Id == gameId, Ct)).State.ShouldNotBeNull();
        }
    }

    // A body whose length is not known up front (sent chunked), so only the read loop can enforce the size cap.
    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly byte[] _bytes;

        public UnknownLengthContent(byte[] bytes, string? mediaType)
        {
            _bytes = bytes;
            if (mediaType is not null)
            {
                Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            }
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(_bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}

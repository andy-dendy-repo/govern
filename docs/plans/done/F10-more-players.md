---
title: "Plan: F10 more than two players"
tags: [plan, done, f10, players, lobby, seats, spawns, map-pool, resign, free-for-all]
code: [src/Govern.Core/Map/SpawnPlanner.cs, src/Govern.Core/State/GameFactory.cs, src/Govern.Core/Engine/VictoryRule.cs, src/Govern.Domain/Entities/Game.cs, src/Govern.Application/Games, src/Govern.Client/Features/Games, rulesets/default/v15.json, maps/basin.json]
updated: 2026-10-15
---

# Plan: F10 more than two players (free-for-all, 2–4 seats)

Status: done 2026-10-15. Approved by the owner on 2026-10-15 with every default in "Risks & open questions" (F8 later, start only when full, seat order, a resigned player's cities stay capturable, at most 4 players). Plan, implementation and close-out are one PR.

## Context
Games are 1-vs-1 only. The owner wants lobbies of more than two players. Turn order, victory, fog of war, the per-viewer views, the game log, seats, SSE streams and quotas are already written for a list of players; what is hard-coded for two is the lobby size, the spawn layout (left/right), the predefined map format (`firstSpawns`/`secondSpawns`), a few rules that only work because the game ends when one of two players leaves, and the client's "opponent" wording. Outcome: a creator picks 2, 3 or 4 seats, the game starts when the lobby is full, and plays as a sequential-turn free-for-all until one player is left.

## Scope
- In:
  - Seats chosen at create time: 2 (default), 3 or 4.
  - Sequential turns in seat order (today's model), free-for-all, last player standing wins.
  - Spawn layout and default map size per player count, for generated maps and the map pool.
  - Correct mid-game departures: the current player resigning passes the turn; a player who is out stops counting as an unfinished game.
  - Client: seat count in create, "n/max" in lists, a players strip with seat colours, "Seat 3's turn", game-over results for everyone.
- Out (explicitly deferred):
  - F8 turn timers and absent players (a 4-player game waits on its slowest player; see risks).
  - F9 simultaneous turns.
  - G16 diplomacy, teams, alliances (free-for-all only; ganging up and kingmaking are accepted).
  - F7 invite code rotation / kick from the lobby.
  - New 3- and 4-player hand-made maps (the format supports them; The Basin stays a 2-player map).
  - Spectating after you are out with a full map reveal (an out player keeps their own view; see "As built").

## Affected areas
- Core
  - `Rulesets/SetupRules.cs`, `Rulesets/MapRules.cs`, `Rulesets/RulesetValidator.cs`: `setup.players` (exactly 2) becomes `setup.minPlayers`/`setup.maxPlayers` (2 and 4); `map.sizes` gives the default width and height per player count.
  - `State/GameFactory.cs`: N spawns instead of `isLeftSide`.
  - `Map/MapGenerator.cs` and `Map/RiverGenerator.cs`: unchanged for 2 players; reserved tiles for N spawns.
  - `Map/PredefinedMap.cs`, `PredefinedMapDocument.cs`, `PredefinedMapParser`, `PredefinedMapValidator.cs`, `PredefinedMapBalanceValidator.cs`: `spawns` as one candidate group per seat.
  - `Actions/ResignAction.cs`, `Actions/EndTurnAction.cs` (`PassTurn`), `Engine/VictoryRule.cs`, `Engine/TradeRule.cs` (`Reconcile`).
- `rulesets/default/v15.json` (new version; v14 untouched), `maps/basin.json` (new spawn format).
- Domain: `Entities/Game.cs` (`MaxPlayers` constant → `Seats` property, `Join` takes the lowest free seat), `Entities/GamePlayer.cs` (`IsOut`).
- Application: `Games/Contracts/CreateGameContract.cs` (`Seats`), `StartGameContract.cs`, `JoinGameContract.cs`, `ListMyGamesContract.cs` and `Models/MyGameItem.cs` (`Seats`), `Services/GameSeatLimitService.cs` (out seats don't count), `Services/MapPoolService.cs` (maps by seat count).
- Infrastructure: `Persistence/Configurations/GameConfiguration.cs`, `GamePlayerConfiguration.cs`; migration `AddGameSeats`.
- Api: `Features/Games/CreateGameEndpoint.cs` (body gains `seats`; route and auth unchanged).
- Client: `Features/Games/Pages/GamesPage.razor` (create form seat picker, "n/max"), `Components/LobbyPanel.razor`, `Components/PlayerList.razor`, `Components/GameOverPanel.razor`, `Components/Screen/TopBar.razor` (turn text, resign question, players strip), `Components/TradeProposal.razor` (wording), `Models/MyGameItem.cs`, `Services/MapPoolService.cs`.
- Reuse: `Client/Helpers/Seats.cs` (seat number of a player), the four seat colour tokens already in `wwwroot/css/tokens.css` (`--gv-seat-1..4`) and `board.css` (`.seat-1..4`), the start-value measure in `PredefinedMapBalanceValidator`.

## Steps
1. Core: ruleset v15
   - `setup.minPlayers: 2`, `setup.maxPlayers: 4` replace `setup.players`; the validator requires 2 ≤ min ≤ max ≤ 4. The cap of 4 is a validator limit because the client has four seat colours.
   - `map.sizes`: `{ "2": [20, 14], "3": [26, 18], "4": [30, 20] }`, about 140–150 tiles per player as today; each within `minSize`/`maxSize`, one entry per count from min to max.
   - `GameFactory.ValidatePlayers` accepts a count in the range; `GameSetup` width/height default from `map.sizes`.
2. Core: spawns for N players
   - Generated maps: 2 players keep today's left/right zones and draw order, so `GameFactoryTests.Create_KnownSeed_GivesTheRecordedState` stays as recorded. 3 players: left edge, top-right and bottom-right; 4 players: the four corners. Each zone is a box of `spawn.zoneColumnsPerMille` of the width and half the height, inset by `edgeMargin`.
   - The terrain stays point-symmetric (`GameMap.MirrorOf`), which pairs opposite corners but not neighbouring ones. So for 3–4 players the factory draws each zone's spawn (hashed) only among tiles whose start value is within 10% of the mean over all zones' candidates, the same measure as the map pool's balance check. It runs after terrain generation, and the row of starting units must stay on passable default terrain.
   - Starting units stand in a row from the spawn towards the map's centre column (generalises `SpawnRow(isLeftSide)`).
3. Core: map pool format
   - `maps/<key>.json`: `firstSpawns`/`secondSpawns` become `spawns`, an array of candidate groups, one per seat; the number of groups is the player count the map is for. The parser keeps reading the old two keys as two groups, so an old file still loads.
   - `PredefinedMapValidator`: at least 2 candidates per group. `PredefinedMapBalanceValidator`: every group's average start value within 5% of the mean over groups; each group's starting row passable and on its own side of the centre (nearest to its group's centre rather than "left half").
   - `maps/basin.json` moves to `spawns` with two groups.
4. Core: departures with more than two players
   - `ResignRule`: when the actor resigns on their own turn, the turn passes to the next active player (shared `PassTurn`), so the game never waits on someone who left.
   - The resigned player's cities stay where they are with their territory, produce nothing and have no defenders, so any other player can capture them (capture rules unchanged). Their trade deals end (`TradeRule.Reconcile`, like a capture).
   - `VictoryRule` is unchanged: it already finishes the game when at most one active player is left.
5. Domain
   - `Game.Seats` (2–4, set by `Create`) replaces the `MaxPlayers` constant; `IsFull` uses it; `Join` takes the lowest free seat (the comment in `Leave` already says this is needed).
   - `GamePlayer.IsOut`, set by `Game.Execute` from the state when a player resigns or is eliminated, so lists and limits never read the snapshot.
   - `Start` passes the seats in seat order; `MapChoice.Fits` also checks the map's seat count.
6. Application.Common and Application
   - `CreateGameRequest(string? Map, int Seats = 2)`; the validator allows the ruleset's min..max, and a named pool map must be for exactly that many seats (`game.map_seats_mismatch`, 400). Random picks among pool maps for that seat count and falls back to Generated when there is none (today that is every 3- and 4-seat game).
   - `MyGameItem` and the game response gain `Seats`; "your turn" is unchanged.
   - `GameSeatLimitService`: a seat that is out no longer counts as an unfinished game (today resigning frees a slot only because the 1-vs-1 game ends).
7. Infrastructure
   - `games.seats integer not null default 2` and `game_players.is_out boolean not null default false`; migration `AddGameSeats`. Existing rows read as 2-seat games.
8. Api
   - `POST /api/games` body gains optional `seats` (2–4, default 2). 201 / 400 / 401 / 409 / 429 as today. No new endpoints.
9. Client
   - Create: a seat picker (2, 3, 4) next to the map picker; the map list shows only maps for that seat count, plus Random and Generated.
   - Lists and lobby: "n/max"; Start is enabled when full; the invite text says how many seats are free.
   - In game: a players strip in the top bar (seat colour, "You" or "Seat n", whose turn, resigned or eliminated); "Seat 3's turn" instead of "opponent's turn"; the resign question says the game goes on for the others when more than two are active; the game-over panel lists every player's result; "opponent" wording in `TradeProposal` becomes "another player's".

## Tests
- Unit (Core):
  - Ruleset validator: min/max range, `map.sizes` coverage and bounds.
  - `GameFactory`: 3 and 4 players get distinct, passable, in-zone spawns with balanced start values over many seeds; 2 players unchanged (recorded state).
  - Map pool: `spawns` groups; old keys still parse; balance check over 3 groups; `PredefinedMapTests` for Basin.
  - Turn order with 3–4 players: wrap-around increments the turn; skips eliminated and resigned seats.
  - Current player resigns with 3 active: the next player gets the turn, units removed, game continues; out-of-turn resign also continues.
  - Elimination mid-game with 3 players; the last active player wins.
  - Fog with 3 players: each viewer sees only their own; an attack between two others is seen by the third only on visible tiles; trade partners and deals between two others stay hidden.
  - A resigned player's undefended city can be captured; their deals end.
- Integration:
  - Create with `seats` 3 → 201 and `seats` in the response; 1 or 5 → 400; Basin with 3 seats → 400; unauthenticated → 401.
  - Join fills seats 1–3, a fifth user → 409 `game.full`; leaving frees the lowest seat and the next join takes it.
  - Start below full → 409; full 3-seat lobby starts and each player gets their own view.
  - A player who resigned a 3-player game no longer counts toward the unfinished-games limit.
- Client (bUnit): seat picker and filtered map list; "2/4" in lists; players strip; turn text names the seat; game-over lists all players.
- Architecture: none; existing rules cover it.

## Docs & rules
- `docs/game/rules.md` (players, turn order, leaving a game; its "Game numbers" table gains min/max players), `docs/game/game-logic.md` (spawns for N, resigning on your turn), `docs/game/game-data.md` (`setup.minPlayers`/`maxPlayers`, `map.sizes`), `docs/game/maps.md` (`spawns` groups, balance checks), `docs/features/games.md` (seats, lobby, limits).
- `.claude/rules/game-core.md`: the predefined map paragraph (spawn groups instead of first/second, "each side" fairness).
- Backlog: F10 to Recently done, plan to `docs/plans/done/`, and the F10 section in `docs/plans/deferred/follow-ups.md` points at the plan.

## Verification
- `dotnet build Govern.slnx` and `dotnet test Govern.slnx` green, summary shown.
- `dotnet format Govern.slnx --verify-no-changes`.
- Manual: in `Govern.Api.http`, create a 3-seat game, join with two more users, start, and play a round of end turns; then the client with Playwright: a 3-player game at 1400 and 390 widths, light and dark, screenshots in `/mnt/project-files/screenshots/f10-more-players/`.

## Risks & open questions
Defaults are picked so work can start; the owner decides:
1. **Waiting without turn timers.** With sequential turns, each player waits for up to three others every round, and one absent player stalls everyone. Default: ship F10 first, among friends, and do F8 (turn timers) next. Alternative: F8 first.
2. **Start rule.** Default: the lobby starts only when every seat is taken (today's rule). Alternative: the creator can start once at least 2 have joined (the map is then sized for the joined count).
3. **Turn order.** Default: seat order, creator first (as today). Alternative: a random order drawn at start, which also changes who moves first in 1-vs-1.
4. **A resigned player's cities.** Default: they stay, undefended, for anyone to capture. Alternative: they are razed and their territory goes neutral.
5. **Player cap of 4.** It matches the four seat colours and keeps maps within the 40×40 limit; more would need new colours and bigger maps.

Real risk: balanced spawns on a point-symmetric generated map for 3–4 players. Corners next to each other aren't mirrors, so fairness comes from the start-value filter; if that filter finds too few candidates on small maps, the fallback is a larger `map.sizes` entry (tested over many seeds).

## As built (differences from the plan)
- **Map pool format:** the parser does not read the old `firstSpawns`/`secondSpawns` keys; `maps/basin.json` moved to `spawns` instead (pool maps may be edited, so no reader for the old shape is needed).
- **Spawn zones:** the corner zones are a third of the map's height (`SpawnRules.CornerRows`), not half, so the left zone of a 3-player game and the right zones never share rows with each other.
- **Fair generated starts:** with 3–4 players the terrain is generated first (no reserved tiles), then `Map/SpawnPlanner` keeps each zone's tiles whose row of starting units is passable (a zone with none gets its first row cleared), finds the start value every zone can come closest to, and draws each zone's spawn among its candidates within 5% of it (or its closest ones). That replaced "within 10% of the mean", which still let 79 against 99 through. `GameFactoryTests.Create_ThreeOrFourPlayers_StartsAreWorthAboutTheSame` checks over 30 seeds that the best start is within 11% of the worst.
- **Departures:** handled once in `VictoryRule` after every command (not in `ResignRule` and `TradeRule.Reconcile`), so an elimination is covered too: the deals hosted by a player who is out end, and the turn passes when the current player is out.
- **Map choice:** no new error code. A pool map made for another number of players is a 400 on `Map` from `CreateGameValidator`; `MapChoice.Fits` accepts a random choice that resolved to a generated map.
- **An out player's view** keeps updating like any other view (their cities stay on the map and still see), with the game-over panel saying they are out. So a player who is out could still tell a remaining player what their cities see; that is part of the accepted free-for-all kingmaking until spectating is designed.
- **Review fixes:** a pool map is also rejected when two players' starting units could share a tile (any candidates), and on a two-player map the first group must lie in the left half and the second in the right. A player who is out trades with nobody: their cities are no trade partners, in the rule and in the view.

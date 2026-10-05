---
title: "Plan G17b: trading rework (trader deals, counter-offers, no routes, free-walking traders)"
tags: [plan, done, game, trading, trade, trader, deal, counter-offer, market, ruleset-v13]
code: [src/Govern.Core/Engine/TradeRule.cs, src/Govern.Core/Economy, src/Govern.Core/Actions, src/Govern.Core/Engine/AutoMoveRule.cs, src/Govern.Client/Features/Games/Components/CityMarket.razor, rulesets/default/v13.json]
updated: 2026-10-05
---

# Plan: G17b trading rework

*Done 2026-10-05 (PR #PRNUM). Draft 1, 2026-10-05, from Andrii's request ("the trading system is a bit confusing"). Approved the same day ("yes"), with one addition: two or more units on one tile stand side by side on the board. Builds on [G17](G17-trading.md).*

## Context

G17 trading works through routes: a trader walks to another city's trade post to open a route, only then proposes a deal, and the host can only accept or decline. Andrii finds it confusing. His model: the trader is the thing you manage. Each trader sends a deal to a target city; the target accepts, declines or suggests other rates; once accepted the trader walks to the target's trade post, the deal starts when it arrives, and from then on it shuttles between the two posts by the fastest way. Traders never block and are never blocked. The Market tab lists the city's traders with their deals.

## Scope

- In:
  - routes removed: the deal belongs to a trader; a city's traders are the ones it trained (its home city);
  - propose from home: a trader sends a deal to any known city with a trade post (own or the opponent's), no walk first;
  - answer: accept, decline, or counter (other amounts or resources); a counter goes back to the proposer, who can accept, decline or counter again;
  - accepted: the trader walks to the target's trade post; the deal turns active when it gets there; then it shuttles post to post by the fastest way;
  - traders ignore units and cities when walking (they share tiles, stand on posts, pass through cities), and nothing is blocked by a trader;
  - the Market tab: one row per trader (where it is, its deal and its state) with Suggest deal and Cancel deal; incoming proposals and counters to answer; the city panel gets wider;
  - ruleset v13 (`tradersPerCity` replaces `routesPerCity`), events, the game log, rules and docs.
- Out: market orders, demand levels, caps, trade values and the end-of-turn swap stay as G17 built them; no cargo per trip, no neutral merchants, no new trade resources.

## Defaults where Andrii's ask forks

1. **Seeing the target's market.** A trader can't learn the host's rates by walking there first any more, so a city's market (sell/buy orders, free caps) and demand levels are visible to any player who has seen the city. Otherwise a fair offer can't be made from home.
2. **Counter-offers** go back and forth with no limit; each side can withdraw. Only the latest offer is open; it waits until answered or withdrawn (no expiry).
3. **One deal per trader**, at most `tradersPerCity` (2) traders per city; a third can't be trained. Hosting is limited only by the host's market caps (accepting can still cancel older deals to make room, as in G17).
4. **Goods swap per turn, not per trip:** each turn the deal is active and the trader isn't stuck, at the end of the host owner's turn, as today.
5. **A trader is killed** by attacking it where it stands alone; on a tile shared with a soldier the attack hits the soldier. Its death ends the deal (nothing to re-staff now).
6. **Cancel deal** (either side, any time) frees the trader; it walks home to its own post and waits there for its next deal.
7. **Capture**: a deal ends when either city changes hands; a trader whose home is captured is rehomed to its owner's nearest city with a trade post, or disbanded if there is none.
8. **Old saves**: running games drop their routes when loaded; traders get the nearest own trading city as home and go idle. No production deploy exists, so this is acceptable.

## Affected areas

- Ruleset: `rulesets/default/v13.json` (copy of v12; `trade.tradersPerCity: 2` replaces `routesPerCity`); `Core/Rulesets/TradeRules.cs`, `TradeRulesValidator.cs`; the default ruleset service loads v13.
- Core:
  - `Economy/`: `TradeDeal` gains a state (`Proposed`, `Countered`, `Travelling`, `Active`), the target city, who must answer next and the turn it went active; it is keyed by the trader (`Unit.Deal` or `GameState.Deals` by `UnitId`, decided while implementing by what serializes simplest). `TradeRoute`, `TradeRouteId` go away; `TradeCalculator` takes deals instead of routes.
  - `Units/Unit.cs`: `HomeCity` (set when trained), `RouteId` removed.
  - `Actions/`: `ProposeDealAction` (trader, target city, gives, receives), `AnswerDealAction` (accept, decline, or counter with new amounts and resources), `CancelDealAction` (by trader); `OpenTradeRouteAction`, `CloseTradeRouteAction` removed; `SetProductionAction` checks `tradersPerCity`.
  - `Engine/TradeRule.cs`: walk to the target post, turn active on arrival, shuttle, exchange, reconcile on death and capture, load-time cleanup of old routes.
  - `Engine/AutoMoveRule.cs`, `Actions/MoveUnitAction.cs` and `GameState.UnitAt`: a trader ignores units and cities; `UnitAt` returns the blocking unit, a new `TradersAt` the traders on a tile; combat, capture, founding and production placement treat traders as not there.
  - `Events/`: `TradeRouteChangedEvent` becomes `TradeDealChangedEvent` (proposed, countered, accepted, declined, cancelled, arrived, ended); `TradeExchangedEvent` keyed by trader.
  - `Visibility/`: `TradeDealView` replaces `TradeRouteView`; `CityView` shows market and demand of seen cities; `TradePreviewCalculator` checks propose, answer and counter.
- Client:
  - `Components/CityMarket.razor` (orders as today, then Traders and Incoming sections); `CityTraders` replaces `CityTradeRoutes`; `TradeProposal` is reused for suggest and counter, with a target city picker;
  - `Screen/TraderOrders`: Open route removed (a trader's deal is managed in its city's Market tab; the trader's card links there);
  - board: several pieces on a tile (a trader drawn beside the unit it shares a tile with);
  - `wwwroot/css/game-ui.css`: the selection panel holding the city panel widens from 24rem to 32rem on wide screens (phones unchanged);
  - `Helpers/TradeLogFormatter.cs`.

## Steps

1. Ruleset v13 and `TradeRules`.
2. Core state: deals on traders, `HomeCity`, routes removed, load-time cleanup.
3. Actions: propose, answer (accept, decline, counter), cancel; `tradersPerCity` in production.
4. Walking: traders pass units and cities; `UnitAt` / `TradersAt`; combat and capture rules.
5. End turn: walk to the target, activate on arrival, shuttle, exchange, reconcile.
6. Visibility: deal views, market of seen cities, previews.
7. Client: Market tab with traders and incoming offers, counter form, wider panel, board stacking, log lines.
8. Docs, rules and backlog close-out in the same PR.

## Tests

- Unit (Core):
  - propose, answer and cancel: happy paths and every rejection (not a trader, busy trader, no post, unknown or unseen city, water, over caps);
  - counter ping-pong: proposer counters a counter, then accepts; withdrawing an open counter;
  - accept starts the walk; active only on arrival; shuttle between posts; swap per turn while active; nothing swapped while travelling or stuck;
  - a trader passes through and stops on tiles with units, cities and posts; units and attacks ignore a trader on a shared tile; a lone trader can be killed;
  - trader death and city capture end the deal; home captured rehomes the trader;
  - `tradersPerCity` blocks a third trader;
  - old save with routes loads clean;
  - deal views and event visibility for both sides, serializer round trip; preview parity with `Execute`.
- Integration: `SendTradeCommandEndpointTests` updated (propose 200, counter 200, water 409, malformed 400, a soldier proposing 409).
- Client (bUnit): Market tab with traders and their deals, suggest, cancel, answer and counter; the wider panel class; trader beside another piece; log lines.
- Architecture: `RulesReferenceTests` covers the changed rules tables; no new conventions.

## Docs & rules

- `docs/game/rules.md` (Trading rewritten, game numbers), `docs/game/game-logic-trade.md`, `docs/game/game-logic.md`, `docs/game/game-data.md`, `game-data-economy.md`, `docs/game/design-roadmap.md` (G17b).
- `docs/features/game-core.md`, `docs/features/game-ui.md`, `docs/README.md`.
- `docs/plans/backlog.md`: G17b row, then Recently done; this plan to `done/`.
- `.claude/rules/game-core.md`: the Trading bullet.

## Verification

- `dotnet build Govern.slnx`, `dotnet format Govern.slnx --verify-no-changes`, `dotnet test Govern.slnx` all green.
- Playwright on the running client: train a trader, suggest a deal to the opponent's city, counter it, accept, watch the trader walk through a unit and become active; screenshots of the Market tab light, dark and 390px.

## Risks & open questions

- Traders sharing tiles touches every place that assumes one unit per tile (17 `UnitAt` callers, the board). This is the largest part of the work.
- Making markets of seen cities public widens what the opponent learns (G17 already leaked the free market to a route's owner).

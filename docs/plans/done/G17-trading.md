---
title: "Plan G17: trading (city markets, traders, trade routes and deals)"
tags: [plan, done, game, trading, trade, trader, trade-route, deal, market, demand, ruleset-v10]
code: [src/Govern.Core/Engine/TradeRule.cs, src/Govern.Core/Economy/TradeCalculator.cs, src/Govern.Core/Actions, src/Govern.Core/Visibility/TradePreviewCalculator.cs, src/Govern.Client/Features/Games/Components/CityMarket.razor, rulesets/default/v10.json]
updated: 2026-10-03
---

# Plan: G17 trading (city markets, traders, trade routes and deals)

*Done 2026-10-03. Built from Andrii's request and his corrections of the same day, approved with "Go" on draft 2; this file records the plan as built. Not a Civ 6 copy.*

## Context

Cities keep their own stocks (G7) and pay resource costs and upkeep (G8a), so one city can sit on stone while another starves for food, and surplus had no use. Trading lets a city swap what it has too much of for what it lacks, with its own cities or the opponent's. Andrii's model: there is no money (gold is a resource), water can't be traded, rates follow need and proposition, a host accepts a better proposal and cancels an older deal, and a trader walks to the other city's trade post to open a route, then keeps the deal alive by walking back and forth; killing it breaks the deal.

## Scope

- In: market orders (sell or buy, a cap per turn) and demand levels per city; the trader; routes (opening, serving, closing); proposals, accept with cancel-to-make-room, decline, cancel; the shuttle walk; the exchange at the end of the host owner's turn; pausing, breaking on the trader's death, ending on capture; events and the game log; fog of war; the Market tab, the proposal form, the trader's orders; ruleset v10; rules and docs.
- Out (later ideas, not agreed): neutral merchants, more routes from upgraded trade posts, trading science or culture, cargo per trip, a trade route board lens, demand history.

## Defaults Andrii approved

1. Goods are swapped every turn the deal is active and its trader can walk, not carried as cargo.
2. A killed trader breaks the deal but the route stays; a new trader serves it and the host accepts a new proposal.
3. If a side is short of its part, nothing is swapped that turn.
4. Both cities need a trade post. Tradable: food, wood, stone, gold. Caps up to 20 a turn, 2 routes per city, demand moves at most 20 % a turn within 25 %..400 %.

## As built (differences from draft 2)

- The trader has sight 3, not 2 (the validator keeps sight ≥ movement), and no strength (a civilian, like the settler).
- A trader is **at** a post when it stands next to it; it never steps onto the post, so it never blocks the host's tile.
- Value = `tradeValue` × demand level; tradable means `tradeValue` ≥ 1 (no separate `tradable` flag).
- Two events instead of seven: `trade_route_changed` (opened, proposed, accepted, declined, cancelled, broken, closed) and `trade_exchanged` (what was due and who was short).
- A deal with the player's own city is accepted at once.
- `CityForecast` ignores trade: the other side's stock is hidden, so it can't be exact.

## Affected areas

- Ruleset: `rulesets/default/v10.json` (`resources[].tradeValue`, the `trade` section, the `trader` unit with the `trade` ability).
- Core:
  - `Rulesets/TradeRules.cs`, `TradeRulesValidator.cs`;
  - `Economy/` `MarketOrder`, `MarketSide`, `ResourceDemand`, `TradeRoute`, `TradeRouteId`, `TradeDeal`, `TradeChange`, `DealProblem`, `TradeCalculator`;
  - `Actions/` `SetMarketOrderAction`, `OpenTradeRouteAction`, `ProposeDealAction`, `AnswerDealAction`, `CancelDealAction`, `CloseTradeRouteAction`;
  - `Engine/TradeRule.cs` (dispatch, walk, exchange, demand, breaking), wired into `GameEngine`, `EndTurnAction` and `UpkeepRule` (unpaid upkeep as need);
  - guards in `MoveUnitAction`, `SetDestinationAction`, `SetProductionAction`;
  - `Events/TradeRouteChangedEvent.cs`, `TradeExchangedEvent.cs`;
  - `Visibility/` `TradeRouteView`, `TradePreviewCalculator`, and the `CityView`, `UnitView`, `PlayerView` and event visibility changes.
- Application: the default ruleset service loads v10.
- Client:
  - `Components/CityMarket`, `CityTradeRoutes`, `TradeProposal`, `Screen/TraderOrders`;
  - `CityPanel` (Market tab), `ActionBar`;
  - `Helpers/TradeLogFormatter`;
  - the trader sprite and board model.

## Steps

1. Ruleset v10, `TradeRules` and its validator.
2. Core state and the six actions.
3. End turn: the walk, the exchange and the demand step; breaking on death and capture.
4. Visibility and the trade previews.
5. Client: Market tab, proposals, trader orders, log lines.
6. Docs, rules and the backlog close-out in the same PR.

## Tests

- Unit (Core):
  - every action's happy path and each rejection;
  - `TradeRuleTests`:
    - the exchange;
    - a short side;
    - the shuttle;
    - a paused route;
    - a killed trader breaks the deal;
    - capture closes the route;
    - demand moves with need, offer and unpaid upkeep;
    - no post means no demand;
  - `TradeCalculatorTests`;
  - `TradeRouteViewTests`, including a serializer round trip;
  - `TradePreviewCalculatorTests`, parity with `Execute`;
  - the ruleset validator cases;
  - event visibility for both sides.
- Integration: `SendTradeCommandEndpointTests`:
  - a market order (200);
  - water (409);
  - malformed commands (400);
  - a soldier opening a route (409);
  - an unknown route (409).
- Client (bUnit): `GameTableTradeTests` (Market tab, proposals, accept and cancel, trader orders), the city tabs, the log lines.
- Architecture: `RulesReferenceTests` checks the new rules tables. There are no new conventions.

## Docs & rules

- Rules and design:
  - `docs/game/rules.md`: Trading, Trade values, the trader, game numbers;
  - `docs/game/game-logic.md`: End turn step 5 is Trade;
  - the new `docs/game/game-logic-trade.md`;
  - `docs/game/game-data.md` and `game-data-economy.md`;
  - `docs/game/design-roadmap.md`: G17, with G10b and G16 pointing here.
- Features and index: `docs/features/game-core.md`, `docs/features/game-ui.md`, `docs/README.md`.
- Backlog: `docs/plans/backlog.md`.
- `.claude/rules/game-core.md`: a Trading bullet.

## Verification

- `dotnet build Govern.slnx`
- `dotnet format Govern.slnx --verify-no-changes`
- `dotnet test Govern.slnx`, all green.

## Risks & open questions

- An accepted leak: the owner of a route sees the host's free market, which hints at what the host's other deals use.
- Balance (trade values, demand step, caps) is a first guess and will change in a later ruleset version.

---
title: Game logic, trading (markets, traders, trade routes and deals)
tags: [game, core, rules, algorithms, trading, trade, trader, trade-route, deal, market, demand, exchange, fog-of-war, previews]
code: [src/Govern.Core/Engine/TradeRule.cs, src/Govern.Core/Economy/TradeCalculator.cs, src/Govern.Core/Actions, src/Govern.Core/Visibility/TradeRouteView.cs, src/Govern.Core/Visibility/TradePreviewCalculator.cs]
updated: 2026-10-03
---

# Game logic: trading

*G17, decided 2026-10-03 (the owner's model; not a Civ copy).* How `Govern.Core` lets cities swap resources, step by step, in the style of [game logic](game-logic.md): every number is a ruleset key in brackets ([game data: trade](game-data-economy.md#trade-trade)). The player's version is [Trading in the rules reference](rules.md#trading). There is no money: gold is a resource like the others.

## Trading terms

| Term | Meaning |
|---|---|
| Tradable resource | A resource with [`resources.*.tradeValue`] ≥ 1 (food, wood, stone, gold); never [`territory.growthResource`] |
| Trade post | The tile of a city's territory holding [`trade.building`]. Only a city with one trades: it has a market, produces traders and opens or hosts routes |
| Market order | Per city and tradable resource: sell or buy, up to a cap of 1..[`trade.maxCap`] a turn over all its deals |
| Demand level | Per city and tradable resource, in percent, starting at 100 and kept within [`trade.minDemandPercent`]..[`trade.maxDemandPercent`]; only levels other than 100 are stored |
| Value | A resource's [`tradeValue`] × the city's demand level. The fair rate between two resources in a city is the ratio of their values |
| Trade route | From a home city (its owner's) to a host city (anyone's, never the same city), served by one trader. It is open once its trader first reached the host's trade post |
| Deal | On a route: what the home city gives each turn and what the host gives back, each a list of tradable resources with amounts 1..[`trade.maxCap`], each resource once. A proposal until the host accepts it, then active |
| Committed | What a city's deals use of an order's cap: the amounts of every accepted deal it is a side of, plus its own proposals as a home city |
| Free market | A city's orders with the committed amounts taken off each cap |

## The trade commands

Each command is checked in order; the first failing check is the answer (codes in brackets). A route the actor is not a side of is always `trade.route_not_found`.

1. **Set a market order** (city, resource, side or none, cap): the city is the actor's (`city.not_found`); the resource is tradable (`trade.not_tradable`); with a side, the cap is 1..[`trade.maxCap`] (`market.cap_out_of_range`); for each side, what the city's deals commit is at most the new cap on that side, 0 for the other side (`market.below_deals`). The orders are kept in [`resources`] order.
2. **Open a route** (trader, home, host): the trader is the actor's (`unit.not_found`) and has `trade` (`trade.cannot_trade`) and no route (`trade.on_route`); the home city is the actor's (`city.not_found`) with a trade post (`trade.no_trade_post`); the host is another city with a trade post, and for an enemy host both its city tile and its post are visible to the actor (`trade.no_partner`; a hidden city reads the same as none). If the actor already has a route between the two cities: it must have lost its trader (`trade.route_has_trader`), and the new trader serves it. Otherwise the home city must have fewer than [`trade.routesPerCity`] routes (`trade.too_many_routes`) and a new route gets a random id. In both cases the trader needs a known way to a tile next to the host's post (`move.no_path`; planned as [Set a destination](game-logic.md#set-a-destination-auto-moves), the post itself allowed as the target even with a unit on it). The trader's job is dropped, its destination becomes the host's post, and it [walks](#the-traders-walk) at once.
3. **Propose a deal** (route, gives, receives): the route is the actor's (`trade.route_not_found`) without an active deal (`trade.deal_active`); it is open and its trader stands next to the host's post (`trade.trader_not_at_post`). The terms ([deal check](#the-deal-check)) must fit the home city's and the host's free markets, both without this route's own deal. The deal is stored (amounts summed per resource, in [`resources`] order) as a proposal, replacing one still waiting (`trade_route_changed`: proposed). When the actor also owns the host, it is accepted at once (step 4's last part).
4. **Answer a deal** (route, accept, routes to cancel): the host city is the actor's (`trade.route_not_found`) and the route carries a proposal (`trade.no_proposal`). Decline drops the proposal (declined). Accept first cancels each listed route's active deal (a route listed twice counts once); each must be active and hosted by the same city, or opened by the actor from that city (`trade.cannot_cancel`). Then only the host's side is checked ([deal check](#the-deal-check), the home side being the proposal itself, already committed): `trade.host_market` if it doesn't fit. The deal becomes active with the round it was accepted in (accepted), and its trader turns towards the home city's post.
5. **Cancel a deal** (route): either side (`trade.route_not_found`), with a deal or proposal (`trade.no_deal`). The deal goes (cancelled) and the trader, if alive, walks back to the host's post to wait for a new proposal.
6. **Close a route** (route): the owner only (`trade.route_not_found`). The route goes (closed) and its trader is free where it stands.

A trader on a route takes no other order: move and set destination answer `trade.on_route`. A unit with `trade` can be produced only by a city that has [`trade.building`] (`trade.no_trade_post`).

### The deal check

1. Both sides give at least one resource (`trade.empty_deal`).
2. No item is missing (a null in the command's lists) (`trade.bad_amount`).
3. Every resource is tradable (`trade.not_tradable`).
4. Each amount is 1..[`trade.maxCap`] and no resource appears twice on one side (`trade.bad_amount`).
5. The home city sells everything it gives and buys everything it receives, each within its free cap (`trade.home_market`).
6. The host buys everything the home city gives and sells everything it receives, within its free caps (`trade.host_market`).

## The trader's walk

At the start of each of its owner's turns (step 10 of [End turn](game-logic.md#end-turn)) and when it is sent, a trader on a route walks like a unit with a destination, except:
1. Its destination is a trade post: the host's while the route has no active deal, otherwise the post it is heading for.
2. It is **at** a post when it stands next to it (or on it). It never steps onto the post: the walk stops on the first tile of the path next to it.
3. It doesn't stop when an enemy comes into sight.
4. With no known way, the route is **paused** (an active deal swaps nothing) and the trader stays; the next walk that finds a way unpauses it.
5. On arriving: with an active deal it turns round for the other post (so it shuttles between the two for as long as the deal lasts); otherwise it waits there, and its first arrival opens the route (opened).

## Exchange at the end of the host owner's turn

Step 5 of [End turn](game-logic.md#end-turn), after upkeep (a city never trades away what its units needed) and before territory and production (goods received can start an item that turn):
1. Take every active, unpaused deal with a trader whose host city belongs to the actor, oldest first (the round it was accepted, then the route id).
2. For each: the home city is short when its stock lacks any of what it gives; the host is short when its stock lacks any of what it gives back. A `trade_exchanged` event says what was due and who was short.
3. When neither is short, the home city's stock loses what it gives and gains what it receives, and the host's the other way round. Otherwise nothing moves this turn (no half deals).
4. **Demand:** then each of the actor's cities with a trade post, by id, moves each tradable resource's level:
   - need = the free cap of its buy order (0 without one) + the upkeep of that resource it couldn't pay in step 4;
   - offer = the free cap of its sell order (0 without one);
   - new level = level + level × [`trade.demandStepPercent`] × (need − offer) ÷ (100 × (need + offer + [`trade.demandSmoothing`])), integer maths truncated toward zero, clamped to the bounds. Without need or offer it stays.

## Breaking, capture and the trader's death

After every successful command (with the elimination and victory checks):
1. A route whose home city no longer belongs to its owner, or whose host changed hands, ends (closed); its trader is free where it stands.
2. A route whose trader is gone loses its trader and its deal (broken, when it had one). The route stays: a new trader sent along it serves it again, and the host has to accept a new proposal.
3. A unit pointing at a route that no longer has it as its trader is freed.

## What each side sees

- The route's owner sees each of their routes from the moment it is sent: the cities, the deal, whether it is open or paused, its trader and the host's post, and, while the route is open and its trader stands at the host's post, the host's **free market** (its orders less what its other deals use) and its demand levels. Accepted leaks while the trader is there: the free market tells the owner roughly what the host's other deals use, and a rising demand level can show the host is short of upkeep in that resource.
- The host's owner sees a route at their city once it is open: who opened it, from which city (by name), and its deal, but not the trader's id, the post, whether the route is paused or anything about the home city's market.
- A city's own market and demand levels are its owner's only. An enemy city's trade post is shown while its tile and the city are visible, so a route can be opened to it.
- `trade_route_changed` and `trade_exchanged` events reach the two sides of the route only, and the host's owner never gets one for a route closed before it was opened. `trade_exchanged` says which side was short, so each side learns when the other couldn't pay its part (accepted: the swap visibly didn't happen). Traders are ordinary units on the board.

## Trade previews

From the view alone the client checks Open route, Propose a deal and Accept (with the deals to cancel), each giving exactly the engine's answer: the view holds every route the player is a side of, their own markets and every deal at their cities, the trade post of every city they may trade with, and, for their own routes, the host's free market. The parity is tested over every unit and city pair and many terms (`TradePreviewCalculatorTests`).

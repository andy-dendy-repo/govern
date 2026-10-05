---
title: Game logic, trading (markets, traders and deals)
tags: [game, core, rules, algorithms, trading, trade, trader, home-city, deal, counter-offer, market, demand, exchange, fog-of-war, previews, shared-tiles]
code: [src/Govern.Core/Engine/TradeRule.cs, src/Govern.Core/Economy/TradeCalculator.cs, src/Govern.Core/Economy/TradeDeal.cs, src/Govern.Core/Actions, src/Govern.Core/Visibility/TradeDealView.cs, src/Govern.Core/Visibility/TradePartnerView.cs, src/Govern.Core/Visibility/TradePreviewCalculator.cs]
updated: 2026-10-05
---

# Game logic: trading

*G17, decided 2026-10-03, reworked as G17b on 2026-10-05 (the owner's model; not a Civ copy).* How `Govern.Core` lets cities swap resources, step by step, in the style of [game logic](game-logic.md): every number is a ruleset key in brackets ([game data: trade](game-data-economy.md#trade-trade)). The player's version is [Trading in the rules reference](rules.md#trading). There is no money: gold is a resource like the others. G17b removed trade routes: a city has traders, each trader carries one deal, and the deal is what the two sides negotiate.

## Trading terms

| Term | Meaning |
|---|---|
| Tradable resource | A resource with [`resources.*.tradeValue`] ≥ 1 (food, wood, stone, gold); never [`territory.growthResource`] |
| Trade post | The tile of a city's territory holding [`trade.building`] (or an upgrade of it). Only a city with one trades: it has a market, trains traders and sends or hosts deals |
| Market order | Per city and tradable resource: sell or buy, up to a cap of 1..[`trade.maxCap`] a turn over all its deals |
| Demand level | Per city and tradable resource, in percent, starting at 100 and kept within [`trade.minDemandPercent`]..[`trade.maxDemandPercent`]; only levels other than 100 are stored |
| Value | A resource's [`tradeValue`] × the city's demand level. The fair rate between two resources in a city is the ratio of their values |
| Trader | A unit with `trade`. It has a **home city**: the city that trained it. A city has at most [`trade.tradersPerCity`] traders |
| Deal | One trader's terms between its home city and a host city (another city with a trade post, anyone's): what the home city gives each turn and what the host gives back, each a list of tradable resources with amounts 1..[`trade.maxCap`], each resource once. Its status: **proposed** (the host's owner answers), **countered** (the trader's owner answers), **travelling** (agreed, the trader is on its way to the host's post) or **active** (the trader reached it; the goods swap) |
| Agreed | Travelling or active. Only agreed deals use market caps; an offer on the table uses none |
| Committed | What a city's agreed deals use of an order's cap, as their home or their host |
| Free market | A city's orders with the committed amounts taken off each cap |
| Known post | An enemy city whose trade post the player has seen (it was on a visible tile once) or that sent them a deal: a city they may send deals to |

## The trade commands

Each command is checked in order; the first failing check is the answer (codes in brackets). A deal the actor is not a side of is always `trade.deal_not_found`.

1. **Set a market order** (city, resource, side or none, cap): the city is the actor's (`city.not_found`); the resource is tradable (`trade.not_tradable`); with a side, the cap is 1..[`trade.maxCap`] (`market.cap_out_of_range`); for each side, what the city's agreed deals commit is at most the new cap on that side, 0 for the other side (`market.below_deals`). The orders are kept in [`resources`] order.
2. **Propose a deal** (trader, host, gives, receives): the trader is the actor's (`unit.not_found`), has `trade` (`trade.cannot_trade`) and carries no deal (`trade.trader_has_deal`); its home city is the actor's (`trade.no_home`) with a trade post (`trade.no_trade_post`); the host is another city with a trade post that is the actor's own or a known post (`trade.no_partner`; a city they never learned of reads the same as none). The terms ([deal check](#the-deal-check)) must fit both free markets now. The deal is stored with a random id (amounts summed per resource, in [`resources`] order) as proposed (`trade_deal_changed`: proposed), and the host's owner learns of the home city's post (a known post for them). When the actor also owns the host, it is accepted at once (step 3's accept). The trader can be anywhere.
3. **Answer a deal** (deal, accept, decline or counter; terms with a counter; deals to cancel with an accept): the actor is a side of it (`trade.deal_not_found`) and the offer waits for them (`trade.no_proposal`): the host's owner answers a proposed deal, the trader's owner a countered one.
   - **Decline:** the deal goes (declined); the trader is free.
   - **Counter:** the new terms must fit both free markets ([deal check](#the-deal-check)); the deal keeps its id, takes the new terms and goes back to the other side: proposed becomes countered and countered becomes proposed (countered).
   - **Accept:** first each listed deal is cancelled (a deal listed twice counts once); each must be agreed, not this one, and have the answering city (the host for a proposal, the home city for a counter-offer) as its home or host (`trade.cannot_cancel`). Then the terms must fit both free markets. The deal becomes travelling (accepted): the trader drops any job or order, carries the deal and sets off for the host's post, [walking](#the-traders-walk) at once when its owner accepted, otherwise at the start of its owner's next turn.
   - Any other answer is `trade.unknown_answer`.
4. **Cancel a deal** (deal): either side, in any status (`trade.deal_not_found`). The deal goes (cancelled) and the trader, if alive, walks home to its own city's trade post, free for a new deal.

A trader carrying a deal (travelling or active) takes no other order: move, set destination, explore and upgrade answer `trade.on_deal`. A unit with `trade` can be produced only by a city that has [`trade.building`] (`trade.no_trade_post`) and fewer than [`trade.tradersPerCity`] traders (`trade.too_many_traders`); a finished trader waits in production while the city has them all, and is born with that city as its home.

### The deal check

1. Both sides give at least one resource (`trade.empty_deal`).
2. No item is missing (a null in the command's lists) and no amount is below 1 (`trade.bad_amount`).
3. Every resource is tradable (`trade.not_tradable`).
4. Each amount is 1..[`trade.maxCap`] and no resource appears twice on one side (`trade.bad_amount`).
5. The home city sells everything it gives and buys everything it receives, each within its free cap (`trade.home_market`).
6. The host buys everything the home city gives and sells everything it receives, within its free caps (`trade.host_market`).

## Sharing tiles

A unit type with `trade` shares tiles:
1. A trader's step ignores units and cities: it walks through any unit and any city, its owner's or not, and may stop on any of them, including a trade post with a unit on it. Terrain, rivers and its domain still apply ([moves](game-logic.md#move-a-unit)).
2. A trader never blocks: any unit may enter a tile where only traders stand, and a city can spawn a unit there. A tile's **holder** is its one unit that doesn't share tiles.
3. **Attack:** the defender on a tile is its holder; with none, another player's trader there, unless the tile is a city, which is captured as if empty (traders don't defend). A lone trader is attacked like any unit.
4. On the board units on one tile stand side by side, and clicks on the tile select them in turn (own units and the holder first), then the player's city there. A click on a lone enemy trader the selected unit can both reach and attack shows the forecast first; a second click walks onto it.

## The trader's walk

At the start of each of its owner's turns (step 10 of [End turn](game-logic.md#end-turn)) and when it is sent, a trader carrying a deal walks like a unit with a destination ([auto moves](game-logic.md#set-a-destination-auto-moves)), except:
1. Its destination is a trade post: the host's while the deal is travelling, then the post it is heading for.
2. It ignores units and cities ([sharing tiles](#sharing-tiles)) and doesn't stop when an enemy comes into sight.
3. With no known way, the deal is **paused** (an active deal swaps nothing) and the trader stays; the next walk that finds a way unpauses it.
4. On arriving at the host's post while travelling, the deal becomes active with that round (started) and the trader turns for its home post. From then on it turns round at each post, shuttling between the two by the cheapest known way for as long as the deal lasts.

## Exchange at the end of the host owner's turn

Step 5 of [End turn](game-logic.md#end-turn), after upkeep (a city never trades away what its units needed) and before territory and production (goods received can start an item that turn):
1. Take every active, unpaused deal whose host city belongs to the actor, oldest first (the round it became active, then the deal id).
2. For each: the home city is short when its stock lacks any of what it gives; the host is short when its stock lacks any of what it gives back. A `trade_exchanged` event says what was due and who was short.
3. When neither is short, the home city's stock loses what it gives and gains what it receives, and the host's the other way round. Otherwise nothing moves this turn (no half deals).
4. **Demand:** then each of the actor's cities with a trade post, by id, moves each tradable resource's level:
   - need = the free cap of its buy order (0 without one) + the upkeep of that resource it couldn't pay in step 4;
   - offer = the free cap of its sell order (0 without one);
   - new level = level + level × [`trade.demandStepPercent`] × (need − offer) ÷ (100 × (need + offer + [`trade.demandSmoothing`])), integer maths truncated toward zero, clamped to the bounds. Without need or offer it stays.

## Breaking, capture and the trader's death

After every successful command (with the elimination and victory checks):
1. A deal whose home city no longer belongs to its owner, or whose host changed hands, ends (ended); its trader is free.
2. A deal whose trader is gone is removed (broken).
3. A unit pointing at a deal that no longer has it as its trader is freed.
4. A trader whose home city is gone or no longer its owner's (or that has none, from a game saved before G17b) gets its owner's nearest city as its home, cities with a trade post first, ties by id; with no city it keeps none and can't propose (`trade.no_home`).

Games saved with G17 routes load with their routes dropped (the `tradeRoutes` and `routeId` fields are ignored); their traders get a home on the first command.

## What each side sees

- Both sides see each deal they are a side of, in every status: the two cities by name, the owners, the terms and the status. The trader's id and whether the deal is paused are the trader's owner's only.
- A trader's home city is its owner's only.
- For each known post that still has a trade post, the player sees a **trade partner**: the city's name and owner, its post, its demand levels, and its free market less the agreed deals the player is not a side of (the client takes off the player's own, so the preview equals the rules). Accepted leaks: a partner's free market tells the player roughly what its other deals use, and a rising demand level can show the city is short of upkeep in that resource; the partner stays known out of sight, so its market keeps updating.
- A city's own market and demand levels are its owner's only. An enemy city's trade post is shown on the board while its tile and the city are visible.
- `trade_deal_changed` and `trade_exchanged` events reach the two sides of the deal only. `trade_exchanged` says which side was short, so each side learns when the other couldn't pay its part (accepted: the swap visibly didn't happen). Traders are ordinary units on the board.

## Trade previews

From the view alone the client checks Propose a deal and Accept or Counter (with the deals to cancel), each giving exactly the engine's answer: the view holds every deal the player is a side of, their own markets and their traders' homes, and every trade partner with its market. It also lists the cities a trader can send a deal to and works out a city's free market. The parity is tested over every trader and city and many terms (`TradePreviewCalculatorTests`).

---
title: "Plan: G9 army units, health and counter-based combat"
tags: [plan, game, units, combat, health, counters, ranged, terrain-defence, healing, ruleset, preview, client]
code: [rulesets/default/v3.json, src/Govern.Core/Actions/AttackAction.cs, src/Govern.Core/Units, src/Govern.Client/Features/Games]
updated: 2026-09-30
---

# Plan: G9 army units, health and counter-based combat

**Status:** done 2026-09-30, PR #30. Approved by Andrii 2026-09-30 as proposed; this was the last item of content slice C1.

## Context
Combat today is one hit: any attack destroys the defender, so the only unit that fights (the soldier) always wins by moving first. G9 is the last item of content slice C1: units get health, a small roster with rock-paper-scissors counters, terrain defence (deferred here from G4) and a damage forecast before attacking that matches the engine exactly. Kept lean: only units and stats that a mechanic in this plan uses.

## Scope
- In:
  - **Roster (5 units):** settler and soldier keep their ids (a new ruleset version keeps every old id while games aren't pinned); add spearman, horseman and archer.

    | Unit | Class | Strength | Ranged strength / range | Movement | Sight | Cost |
    |---|---|---|---|---|---|---|
    | Settler | civilian | — | — | 2 | 2 | 6 |
    | Soldier | melee | 20 | — | 2 | 2 | 4 |
    | Spearman | anti_cavalry | 18 | — | 2 | 2 | 4 |
    | Horseman | cavalry | 20 | — | 3 | 3 | 6 |
    | Archer | ranged | 10 | 22 / 2 | 2 | 2 | 5 |

  - **Counters (ruleset `combat.counters`, each "class X gets +N strength against class Y", attacking or defending):** melee +5 vs anti_cavalry, anti_cavalry +10 vs cavalry, cavalry +5 vs ranged. Ranged beats melee by shooting from 2 tiles away without taking damage back.
  - **Health:** every unit has 100 health (`combat.maxHealth`). A unit fights at `strength × (100 + health) / 200`, so it loses up to half its strength as it gets hurt.
  - **Damage (integers only, so server and browser agree exactly):** `base + (attacker strength − defender strength) × perStrengthPoint`, clamped to `[min, max]`, then scaled by a random 80–120% from `DeterministicRandom.NextHashed`. Proposed numbers: base 30, 2 per strength point, min 5, max 100. Two soldiers on plains deal 24–36 to each other.
  - **Melee attack:** both sides take damage (defender's damage back uses its pre-combat strength). A unit at 0 health is destroyed; both can die. The attacker stays put (as today) and its movement drops to 0. An undefended enemy city is still captured by a melee attacker, as today.
  - **Ranged attack (ability `ranged_attack`):** any enemy unit within `range` that the archer can see; no damage back; it can't capture a city. Validator keeps `range ≤ sight`, so the target is always visible and the preview stays exact.
  - **Civilians** (units with no `strength`, the settler): destroyed by any attack and never fight back, as today.
  - **Terrain defence:** new terrain field `defenceBonus` (strength points for the defender): forest +3, hills +3.
  - **Healing:** at the end of your turn, each of your units that neither moved nor attacked heals 10, or 20 in one of your cities. No new state: "didn't act" is "movement left is full".
  - **State:** `Unit.Damage` (0 = unhurt) instead of a stored health, so saved games read unchanged with no schema bump and no ruleset lookup in the deserializer. Health is visible to everyone who can see the unit (like its type); the view carries `UnitView.Health`.
  - **Preview:** `GameEngine.CombatForecast(ruleset, view, unitId, target)` returns both strengths with their modifiers (health, counter, terrain) and the damage range each side takes, computed by the same function `AttackRule` uses. `AttackTargets` covers ranged targets.
  - **Ruleset v3:** `rulesets/default/v3.json` (v2 untouched); Api and client embed v3.
  - **Client:** a `HealthBar` on each damaged unit on the board and in the unit panel (own and enemy); unit panel rows for strength (and ranged strength and range); clicking an attack target now opens a **combat forecast** in the side panel with Attack and Cancel, instead of attacking at once (works on touch and follows "no action on a single accidental click"); sprite symbols for the three new units.
- Out (deferred until a mechanic needs it):
  - `attacksPerTurn` and moving after attacking: every unit would have 1 and "attacking ends the unit's turn" covers it. Add with the first unit that differs.
  - Siege, recon and later eras (catapult, scout, knight…): siege needs city health and city defence, recon has nothing to do yet.
  - City health, city defence and bombarding cities: cities stay "defended by the unit on them, captured when empty".
  - Resource costs (G5), river defence (G6), capturing civilians instead of destroying them, experience and promotions, zone of control.
  - The in-game "Civilopedia" screen (G2's deferred part): 5 units still fit the rules page.

## Affected areas
- Ruleset: `rulesets/default/v3.json` (new; v1 and v2 untouched).
- Core:
  - `Units/UnitDefinition.cs` (+ `Class`, `Strength?`, `RangedStrength?`, `Range?`), `Units/UnitAbility.cs` (+ `RangedAttack`), `Units/Unit.cs` (+ `Damage`).
  - `Map/TerrainDefinition.cs` (+ `DefenceBonus`).
  - `Rulesets/CombatRules.cs` (new: `maxHealth`, damage numbers, healing, counters), `Rulesets/Ruleset.cs`, `Rulesets/RulesetDocument.cs`, `Rulesets/RulesetValidator.cs`.
  - `Actions/AttackAction.cs` (melee and ranged, damage, deaths, capture as today).
  - `Engine/CombatCalculator.cs` (new, internal: strengths with modifiers and damage ranges; shared by the rule and the preview), `Engine/RuleViolations.cs` (`attack.out_of_range` if needed), `Engine/GameEngine.cs` (`CombatForecast`).
  - `Actions/EndTurnAction.cs` (healing).
  - `Events/UnitAttackedEvent.cs` (new: attacker, target, damage dealt and taken); `UnitDestroyedEvent` for each death.
  - `Visibility/UnitView.cs` (+ `Health`), `Visibility/CombatForecast.cs` (new, public record for the client), `Visibility/ViewPreviewCalculator.cs`, `Visibility/VisibilityCalculator.cs`.
  - Reuse: `DeterministicRandom.NextHashed`, `CommandGuardRule`, `AttackRule.Check`, `GameStateBuilder`, `TestRulesets.With`.
- Application / client: embed v3 in `Govern.Application.csproj` + `Games/Services/DefaultRulesetService.cs`, `Govern.Client.csproj` + `Features/Games/Services/RulesetService.cs`.
- Client:
  - `Components/Game/HealthBar.razor` (new), `Components/Game/GameSprite.razor` (unit symbols), `Features/Design/Pages/DesignPage.razor` (gallery entry).
  - `Features/Games/Components/Board/BoardPieces.razor` (health bar), `Features/Games/Components/UnitPanel.razor` (health, strength rows), `Features/Games/Components/CombatForecastPanel.razor` (new), `Features/Games/Helpers/BoardClickResolver.cs` and `Features/Games/Services/GameUiService.cs` (pending attack target), `Features/Games/Components/Screen/SidePanel.razor`, `ActionBar.razor` hint text.

## Steps
1. Core, ruleset data: new unit fields, `ranged_attack` ability, terrain `defenceBonus`, the `combat` section; validator checks with JSON paths: strength ≥ 1 when present; a unit with `attack` or `ranged_attack` has a strength; `ranged_attack` needs `rangedStrength` ≥ 1 and `1 ≤ range ≤ sight`; `defenceBonus` ≥ 0; `maxHealth` ≥ 1; damage `min ≤ max`, `min` ≥ 1; heal amounts ≥ 0; each counter names classes used by some unit and a bonus ≥ 1; texts for each class name.
2. Core, `rulesets/default/v3.json`: the table above, counters, combat numbers, terrain defence, texts.
3. Core, `CombatCalculator`: effective strengths (health, counter, terrain) with the list of modifiers, damage before the random factor, and the min/max after it; integer maths only.
4. Core, `AttackRule`: `Check` accepts targets within the unit's range (1 for melee), ranged only against units; `Apply` draws damage with `NextHashed`, applies it, destroys units at 0, captures undefended cities for melee as today.
5. Core, `EndTurnRule`: heal resting units before production.
6. Core, view and previews: `UnitView.Health`, `AttackTargets` for range 2, `GameEngine.CombatForecast`.
7. Application and client: embed v3.
8. Client: `HealthBar` component (+ gallery), board and unit panel health, strength rows, the forecast panel and the two-step attack, sprites for the new units.

## Tests
- Unit (Core):
  - `RulesetTests`: new fields parse; each validator check fails with its path.
  - `CombatCalculatorTests`: health scaling, each counter both ways, terrain defence only for the defender, clamping, the 80–120% range.
  - `AttackActionTests` (rewritten): damage within the forecast range; equal soldiers both survive; defender destroyed at 0; attacker destroyed by damage back; both destroyed; ranged at distance 2 takes no damage back; ranged can't target an empty city or a tile beyond range; civilian destroyed in one attack; city capture unchanged; same seed gives the same damage; damage draws are hashed.
  - `EndTurnActionTests`: resting unit heals 10, 20 in own city, capped at max; a unit that moved or attacked doesn't heal.
  - `GameEnginePreviewTests`: parity of `AttackTargets` for a range-2 archer (with hills, forest, mountains around); `CombatForecast` range equals the min and max of `Execute` over many seeds for melee, ranged, counters and terrain.
  - `VisibilityCalculatorTests`: enemy health is in the view on visible tiles; `GameSerializerTests`: a v2 snapshot without `damage` reads with full health.
  - `GameFactoryTests`: known-seed hash re-recorded only if the state JSON changes (it gains `damage: 0`).
- Integration: none new (no endpoint change); existing game tests run on v3, and an attack test that expected one-hit kills is updated.
- Client (bUnit): `HealthBar` (value, aria, hidden at full health on the board); `UnitPanel` health and strength rows for own and enemy units; `CombatForecastPanel` shows both ranges and modifiers, Attack sends the command, Cancel clears it; `BoardClickResolver` click on an attack target selects it instead of attacking; `GameUiService` pending target cleared on selection change.
- Architecture: none new; `RulesReferenceTests` reads v3 and gains the new unit and terrain columns, the counters table and the combat game numbers; `GameSpriteTests` covers the new units automatically.

## Docs & rules
- `docs/game/rules.md`: unit table (class, health, strength, ranged strength, range), terrain defence column, a rewritten "Attacking" section (melee, ranged, damage, counters table, civilians, healing), combat game numbers.
- `docs/game/game-data.md` (unit fields, `combat` section, terrain `defenceBonus`, v3; drop the G9 rows from "Future sections"), `docs/game/game-logic.md` (combat and healing algorithms), `docs/features/game-core.md`, `docs/features/game-ui.md` (`HealthBar` built, forecast panel, two-step attack), `docs/game/design-roadmap.md` (G9 what shipped and what's deferred), `docs/plans/backlog.md` (C1 done), `docs/README.md`.
- `.claude/rules/game-core.md`: combat draws use `NextHashed`; the forecast is a shared preview with a parity test; `Damage` rather than stored health.
- Close-out in this PR: plan to `docs/plans/done/`, backlog row to "Recently done".

## Verification
- `dotnet build Govern.slnx`, `dotnet format Govern.slnx --verify-no-changes`, `dotnet test Govern.slnx`: all green, summary shown.
- Run the Api and client against a Postgres container, play both seats: build a spearman, horseman and archer, check the forecast matches the damage dealt, the health bars, healing, and the counters; light and dark, phone width; screenshots in `/mnt/project-files/screenshots/g9-combat/`.

## Risks & open questions
- **Balance numbers** are a first guess; they are all in the ruleset, so tuning later is a new version file, not code.
- **Two-step attack** (click target, then confirm) is one more click than today. Proposal: keep it, since a random outcome deserves a look first; the alternative is a hover-only forecast that doesn't work on touch.
- **Stalemates:** with healing and no city defence, a defender can rest in a city and heal. Accepted for now; city defence comes with siege later.

## Decisions made during implementation
- **Capture stays one click.** Clicking an undefended enemy city with a melee unit captures it at once: there is no roll to forecast.
- **Only the forecast's Attack button attacks.** The first design attacked on a second click on the target; the review pointed out that a double-click or double-tap would then attack before the forecast could be read, so a second click now keeps the forecast open.
- **Damage is stored, not health.** `Unit.Damage` (left out of the JSON while 0) keeps saved games readable without a schema bump. Until games are pinned to a ruleset version (G3b), a new version must not lower `combat.maxHealth`.
- **Combat numbers have upper bounds** (strengths, health and damage up to 1000, bonuses up to 100), so the integer maths can't overflow.
- **Accepted leak:** a hurt unit heals only if it kept all its movement, so an opponent watching a hurt unit learns whether it moved that turn (one bit). Documented in `UnitView` and [game logic](../../game/game-logic.md#fog-of-war).
- **Client split:** `CombatSideView` renders one side of the forecast, and the unit panel's logic moved to `UnitPanel.razor.cs` to stay within the size limits; `ProgressBar` now passes its label as `AriaLabel`.

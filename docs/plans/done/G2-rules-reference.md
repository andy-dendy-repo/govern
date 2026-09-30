---
title: "Plan: G2 game rules reference"
tags: [plan, game, rules, reference, docs, ruleset, architecture-tests]
code: [docs/game/rules.md, tests/Govern.ArchitectureTests/RulesReferenceTests.cs]
updated: 2026-09-30
---

# Plan: G2 game rules reference

**Status:** in progress on `claude/project-thread-6ko932`. Approved by Andrii 2026-09-30, with open questions 5 and 6 answered as proposed below.

## Context
The game's rules are spread over `docs/game/game-logic.md` (step-by-step algorithms with ruleset keys, for developers), `docs/features/game-core.md` (implementation) and the plans. G2 is the first item of content slice C1 because G4 and G9 each change the rules and should update one page as they go. The outcome is `docs/game/rules.md`: the complete, current rules written for a player, with its numbers kept equal to `rulesets/default/v1.json` by a test, and a Definition of Done line so every rules change updates it.

## Scope
- In:
  - `docs/game/rules.md`, player-facing, covering today's game only: the map and terrain, turns, units (settler, soldier) and their stats, moving, attacking, founding cities, production, fog of war, resigning, victory.
  - A drift test: the numbers in the page's tables must equal the ruleset.
  - Rule and doc updates so each rules change updates the page in the same PR.
  - Answer design-roadmap open questions 5 and 6 (see Risks).
- Out (deferred until a mechanic needs it):
  - An in-game "Civilopedia" screen in the client. Revisit when G9 adds a unit roster big enough that players need it in the game.
  - Generating the page from the ruleset. Hand-written prose plus a drift test is enough for three tables.
  - Translations (open questions 2 and 3 stay open).

## Affected areas
- `docs/game/rules.md` (new).
- `tests/Govern.ArchitectureTests/RulesReferenceTests.cs` (new). Reuses `Support/SourceTree.RepositoryRoot` (as `GameUiTests` does) and `Govern.Application/Games/Services/DefaultRulesetService` to load the ruleset the Api plays with.
- `docs/game/game-logic.md`, `docs/game/design-roadmap.md` (G2 marked done, open questions 5 and 6 answered), `docs/README.md`, `docs/plans/backlog.md`.
- `CLAUDE.md` (Definition of Done), `.claude/rules/game-core.md`, `.claude/rules/docs.md`, `.claude/rules/architecture-tests.md` (the test table).
- No `src/` change.

## Steps
1. Docs: write `docs/game/rules.md` with front matter and self-contained sections: Goal and victory; The map (hexes, size, terrain table: name, movement cost, passable); Turns and rounds; Units (table: name, movement, sight, production cost, abilities); Moving; Attacking and capturing cities; Founding cities (spacing, names); Production (per turn, what happens when surrounded); Fog of war (what the opponent sees of your units and cities); Resigning. Names come from the ruleset `texts` ("Settler", "Plains"). The intro links to `game-logic.md` for developers; a closing "Maintaining this page" section explains the checked tables.
2. Tests: `RulesReferenceTests` parses the markdown tables under fixed headings ("Terrain types", "Unit types", "Game numbers") and checks, against the ruleset:
   - every ruleset terrain and unit has exactly one row, found by its display name, and no extra rows;
   - each number and ability in a row equals the ruleset value;
   - the game numbers (players, map size, starting units, and the city sight, minimum spacing, production per turn, first production and capture reset) equal `setup`, `map` and `cities`.
   A new ruleset unit or terrain then fails the test until the page documents it. The ruleset is loaded through `DefaultRulesetService`, so the test checks the file the Api actually plays with.
3. Rules: add to the Definition of Done in `CLAUDE.md` ("a game rules change updates `docs/game/rules.md`"), to `.claude/rules/game-core.md` (rules section) and to `.claude/rules/docs.md` (the `docs/game/*.md` row: rules.md is the player-facing page; game-logic.md stays the developer algorithm page).
4. Docs housekeeping: `docs/README.md` index line; design roadmap G2 marked done with a link; backlog C1 row updated (G2 done, G4 next) and G2 added to "Recently done" on merge; plan moved to `docs/plans/done/`.

## Tests
- Unit: none (no Core change).
- Integration: none (no endpoint).
- Client: none (no UI).
- Architecture: `RulesReferenceTests` (new). Proven to fail by changing a number in the page and by adding a unit to a test copy of the ruleset.

## Docs & rules
- New `docs/game/rules.md`; updated `game-logic.md`, `design-roadmap.md`, `README.md`, `backlog.md`.
- `CLAUDE.md` Definition of Done, `.claude/rules/game-core.md`, `.claude/rules/docs.md`.

## Verification
- `dotnet build Govern.slnx`
- `dotnet test --project tests/Govern.ArchitectureTests` while iterating, then `dotnet test Govern.slnx` (all green, summary shown).
- `dotnet format Govern.slnx --verify-no-changes`

## Risks & open questions
- Open question 6 (is `game-logic.md` the player-facing reference?). Proposed answer: no. `rules.md` is for players, `game-logic.md` stays the developer algorithms with ruleset keys, and a rules change updates both.
- Open question 5 (should `game-logic.md` name Core classes?). Proposed answer: no, keep it rules-only; classes stay in `game-core.md`.
- The drift test ties the page's table layout to a parser. Kept simple (fixed headings, first column is the display name) and explained in the page's "Keeping this page true" paragraph so later items know the format.

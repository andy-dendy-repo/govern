---
title: "Plan: F12 player-chosen city names"
tags: [plan, game, cities, city-name, found-city, ruleset, fog-of-war, leak, client, action-bar]
code: [src/Govern.Core/Actions/FoundCityAction.cs, src/Govern.Core/Rulesets/CityRules.cs, src/Govern.Core/Rulesets/RulesetValidator.cs, src/Govern.Client/Features/Games/Components/Screen/ActionBar.razor, rulesets/default]
updated: 2026-10-03
---

# Plan: F12 player-chosen city names

**Status:** planned 2026-10-03 (backlog F12, Next 1). Waiting for Andrii's go.

## Context
A new city is named by the server from the ruleset template `City {n}`, where `n` counts the founder's cities at founding, hidden ones included. Players can't pick a name, and the number tells the opponent how many cities its founder had (a known residual leak in [game-core](../../features/game-core.md)). F12 lets the player name each city when founding it and removes the count from every name the game makes up.

## Scope
- In: a name on `found_city`, checked by Core (trimmed, 1 to 24 characters, letters, digits, spaces and `' - .` only, not already used by one of the player's own cities); a ruleset list of suggested names; the client's "Found city" opens a small name prompt prefilled with a random unused suggestion; the leak note removed from the docs.
- Out (deferred): renaming a city after founding (a new command plus a visibility-filtered rename event); a profanity filter (games are private 1v1 between people who invited each other); per-player or per-civilisation name lists. A captured city keeps its name, as today.

## Affected areas
- Core: `Actions/FoundCityAction.cs` (`FoundCityCommand(UnitId, string Name)`, name check), `Engine/RuleViolations.cs` (`city.name_invalid`, `city.name_taken`), `Engine/GameEngine.cs` (`CheckFoundCity` takes the name), `Rulesets/CityRules.cs` (`NameTemplate` replaced by `SuggestedNames`), `Rulesets/Ruleset.cs` (`CityName(int)` replaced by `SuggestedCityNames`), `Rulesets/RulesetValidator.cs`, new `Cities/CityNameRule.cs` (the name check shared by the rule and the client preview).
- Ruleset: new `rulesets/default/v9.json` (v8 is never edited); `Application/Games/Services/DefaultRulesetService.cs` and the embedded resource point at v9.
- Client: `Features/Games/Components/Screen/ActionBar.razor` and a new `Features/Games/Components/FoundCityDialog.razor` (name field, "Suggest another", inline reason from `CheckFoundCity`, Found / Cancel).
- Reuse: `CommandGuardRule`, `RuleViolation` messages already shown under "Found city", the Radzen dialog pattern of the resign confirm.

## Steps
1. Core: add `CityNameRule.Check(string name, IEnumerable<string> ownNames)` returning a `RuleViolation?`; normalise by trimming and collapsing inner whitespace, compare names case-insensitively. Only the actor's own cities count for "taken", so the rejection never reveals a hidden enemy city's name.
2. Core: `FoundCityCommand` gets a required `Name`; `FoundCityRule` runs the placement check, then the name check, and stores the normalised name. `GameEngine.CheckFoundCity(ruleset, view, unitId, name)` gives the client the same answer from the view.
3. Core rulesets: `CityRules.SuggestedNames` (text keys `city.name.<id>`, about 30 invented names, none taken from Civ); the validator requires at least one, each text to exist and to pass `CityNameRule`. `NameTemplate`, `CityName(int)` and the `city.default_name` text go.
4. Ruleset v9: copy v8, swap `nameTemplate` for `suggestedNames`, add the name texts; embed v9 instead of v8.
5. Api / Application: nothing new. The command still arrives through `POST /api/games/{id}/commands`; a bad name is a 409 with the violation code like any other rule violation, and a missing `name` is a 400 from the JSON binding.
6. Client: "Found city" opens `FoundCityDialog`, prefilled with a random suggestion not used by the player's own visible cities; the field shows the violation message live; Found sends `FoundCityCommand(unit.Id, name)`. The random pick is client-side, so the suggestion order tells the opponent nothing.

## Tests
- Unit (Core): founding stores the trimmed name; empty, too long, bad characters and a name the actor already uses are rejected with the right code and change nothing; an enemy city with the same name doesn't block it; `CheckFoundCity` agrees with the rule; the validator rejects a ruleset with no suggestions, a missing text or an invalid suggestion; the event carries the chosen name; JSON round-trip of `found_city` with `name`.
- Integration: found with a name (200, the city shows the name in both players' views once seen); invalid name (409 `city.name_invalid`); missing name (400); 401 without a token and 404 for an outsider are already covered by the commands endpoint tests.
- Client (bUnit): the dialog prefills a suggestion, "Suggest another" changes it, an invalid name disables Found and shows the reason, Found sends the command with the typed name, Cancel sends nothing.
- Architecture: none, existing rules cover it.

## Docs & rules
- `docs/game/rules.md` "Founding a city": you name the city (rules and limits); `docs/game/game-data.md`: `suggestedNames` replaces `nameTemplate`; `docs/game/game-logic.md` (founding steps); `docs/features/game-core.md`: drop the city-count leak from known residual leaks and describe the name check; `docs/features/game-ui.md`: the found-city dialog.
- Close-out in the same PR: this plan to `docs/plans/done/`, backlog row to Recently done, links fixed.
- No `.claude` rule changes.

## Verification
`dotnet build Govern.slnx`, `dotnet test Govern.slnx` (all green, summary shown), `dotnet format Govern.slnx --verify-no-changes`; then run the Api and client, found a city through the dialog with a typed name and with a suggestion, and screenshot the dialog (light, dark, 390 px) for the PR.

## Risks & open questions
- Saved games in the database hold v8 state and old `found_city` events with template names; those names stay as they are, nothing to migrate.
- Open: should renaming a city later be in F12? Planned as out (deferred) to keep F12 small.

---
title: Ruleset and game logic docs: open questions
tags: [plan, game, core, ruleset, config, data, texts, localization, versioning, rules-reference]
code: [src/Govern.Core]
updated: 2026-09-30
---

# Ruleset and game logic docs: open questions

**Status: open, to discuss later.** The goal: the game's mechanics stay in `Govern.Core`, while every customisable number, list and text becomes a loadable ruleset that Core reads. The drafts are [game data](../game/game-data.md) (the ruleset) and [game logic](../game/game-logic.md) (Core's algorithms). These questions must be answered before either becomes a plan; then fold the answers into both documents and delete this file.

Related: [game design roadmap](game-design-roadmap.md) items G2 (rules reference) and G3 (data-driven catalogs).

## Loading and storing the ruleset (decided 2026-09-30)

1. **Who reads the database.** Decided: the Api loads the ruleset JSON from the database and passes the text to Core, which parses and validates it (`Ruleset.Parse(json)`). Core stays base class library only.
2. **Where the ruleset is authored.** Decided: JSON files in the repository, reviewed in PRs, then written by a migration into a `rulesets` table as immutable versions (the columns are in [game data](../game/game-data.md#the-database-record)). Editing in the database (an admin page) may come later.
3. **Who can publish a ruleset.** Decided: both. Developers publish through a PR plus a migration, and an admin role publishes at run time. There is no admin role yet, so run-time publishing needs one (a later plan). Either way, a published version is immutable and must pass Core's validation.
4. **Pinning.** Decided: each game records its ruleset `key`, `version` and `hash`, so a balance change never alters a running game, and a replay uses the same rules.

## The ruleset format (next to discuss)

Decided so far: the database record (in [game data](../game/game-data.md#the-database-record)). Still open: the shape of the document: its sections, ids and references, flags or abilities, schema versioning, the hash, icons, and what v1 contains.

## Using the ruleset

5. **The client needs it too:** for the view previews, names, texts and icons. Proposal: a public, cacheable `GET /api/rulesets/{key}/{version}` returning the same JSON.
6. **One ruleset or several:** one "default" ruleset, or several (for example "quick game" and "classic") chosen in the lobby?

## Texts and languages

7. **Where texts live:** in the ruleset (proposed, so names and rules change together), or in a separate language pack?
8. **Which languages first?**
9. **The Api's error messages:** translated by the Api (from the `Accept-Language` header), or the Api sends the code and English, and the client translates by code?

## The game logic document

10. **Level of detail:** rules only, as the draft is now (what a player could read), or also naming the Core classes that implement each step?
11. **Relation to the rules reference (G2):** is [game logic](../game/game-logic.md) itself the player-facing rules reference, or the source a separate player-facing version is written from?

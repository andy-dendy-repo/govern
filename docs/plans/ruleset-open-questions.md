---
title: Ruleset and game logic docs: open questions
tags: [plan, game, core, ruleset, config, data, texts, localization, versioning, rules-reference]
code: [src/Govern.Core]
updated: 2026-09-30
---

# Ruleset and game logic docs: open questions

**Status: open, to discuss later.** The goal: the game's mechanics stay in `Govern.Core`, while every customisable number, list and text becomes a loadable ruleset that Core reads. The drafts are [game data](../game/game-data.md) (the ruleset) and [game logic](../game/game-logic.md) (Core's algorithms). These questions must be answered before either becomes a plan; then fold the answers into both documents and delete this file.

Related: [game design roadmap](game-design-roadmap.md) items G2 (rules reference) and G3 (data-driven catalogs).

## Loading and storing the ruleset

1. **Who reads the database.** Core must stay pure (base class library only, no database). Proposal: the Api loads the ruleset JSON from the database and passes the text to Core, which parses and validates it (`Ruleset.Parse(json)`). Is that split right?
2. **Where the ruleset is authored.** Proposal: JSON files in the repository, reviewed in PRs, then written by a migration or seeder into a `rulesets` table as immutable versions (`id`, `version`, `hash`, `jsonb`). Alternative: edited only in the database, for example through an admin page.
3. **Who can publish a ruleset:** only developers (a PR plus a migration), or an admin role at run time?
4. **Pinning.** Proposal: each game records its ruleset `id`, `version` and `hash`, so a balance change never alters a running game, and a replay uses the same rules. Agreed?

## Using the ruleset

5. **The client needs it too:** for the view previews, names, texts and icons. Proposal: a public, cacheable `GET /api/rulesets/{id}/{version}` returning the same JSON.
6. **One ruleset or several:** one "default" ruleset, or several (for example "quick game" and "classic") chosen in the lobby?

## Texts and languages

7. **Where texts live:** in the ruleset (proposed, so names and rules change together), or in a separate language pack?
8. **Which languages first?**
9. **The Api's error messages:** translated by the Api (from the `Accept-Language` header), or the Api sends the code and English, and the client translates by code?

## The game logic document

10. **Level of detail:** rules only, as the draft is now (what a player could read), or also naming the Core classes that implement each step?
11. **Relation to the rules reference (G2):** is [game logic](../game/game-logic.md) itself the player-facing rules reference, or the source a separate player-facing version is written from?

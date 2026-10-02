---
title: "Plan: F15 step 3b, real unit models on the 3D board (KayKit)"
tags: [plan, client, board, 3d, webgl, threejs, units, models, kaykit, cc0]
code: [tools/unit-models, src/Govern.Client/wwwroot/models/units, src/Govern.Client/wwwroot/js/board3d/pieces.js]
updated: 2026-10-02
---

# Plan: F15 step 3b, real unit models on the 3D board

**Status:** done 2026-10-02 in PR #44 (backlog F15, after [step 3](F15-step3-pieces-selection-lenses.md) of the [F15 plan](F15-3d-board.md)). Visuals only. The owner asked for real unit models in place of the code-built figures on 2026-10-02 and picked the CC0 KayKit Adventurers pack.

## Context
Step 3 drew units as figures built in code from boxes and cones, which read poorly next to the terrain. KayKit Adventurers (CC0, no attribution needed) has stylised low-poly characters with weapons that suit the board. This step bakes them into small static models and draws them in place of the figures.

## Scope
- In: soldier, spearman, archer, builder and settler as KayKit characters, and the horseman's rider; seat colour on each unit's base and on its cape, shield or hat; enemies darker; the baking tool and its licence.
- Out: animation (the characters are posed once; step 4 animates movement), a modelled horse (the pack has none, so the horse stays built in code), cities (still built in code).

## Affected areas
- `tools/unit-models/` (new): `bake.mjs` (downloads the pack's `.glb` files, reads the texture into vertex colours, poses each unit, keeps its accessories, makes the spear, splits the seat parts, writes JSON), `package.json`, `README.md`.
- `src/Govern.Client/wwwroot/models/units/` (new): `soldier`, `spearman`, `archer`, `builder`, `settler`, `rider` (`.json`, about 45 KB each gzipped) and the pack's `LICENSE.txt`.
- `wwwroot/js/board3d/pieces.js`: `loadFigures` fetches the models once; units stand on a seat-coloured base; the horseman rides a code-built horse shown side on; the parts list takes an optional turn.
- `wwwroot/js/board3d/scene.js`: rebuilds the pieces when the models arrive.
- `wwwroot/css/tokens.css`: `--gv-board3d-figure` is now the light on the characters (white, dimmer in the dark theme).

## Steps
1. Tool: `tools/unit-models/bake.mjs` and its output.
2. JS: load the figures and draw them as instanced meshes with vertex colours.
3. Tests, rules and docs.

## Tests
- Unit, integration, client: none (the C# side does not change).
- Architecture: `GameUiTests.Board3DPieces_EveryFigureHasItsBakedModel` checks every character `pieces.js` uses is listed in `FIGURES` and has its baked file with both parts.
- Manual: every unit in light and dark, in the harness and in a real game.

## Docs & rules
- `docs/features/game-ui.md` 3D section; `.claude/rules/blazor-client.md` and `game-ui.md` (units come from baked files, a new kind of unit needs its entry in `tools/unit-models`); `CLAUDE.md` and `project-map.md` list `tools/`.

## Verification
`dotnet build Govern.slnx`, `dotnet format Govern.slnx --verify-no-changes`, `dotnet test Govern.slnx` green; `npm run bake` in `tools/unit-models` reproduces the committed files.

## Risks & open questions
- **Download size:** about 280 KB gzipped for the six models, fetched only when the 3D board opens.
- **Style:** the characters are chibi-proportioned; the horse is still built in code and plainer than its rider.

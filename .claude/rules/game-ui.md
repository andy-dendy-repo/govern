---
paths:
  - "src/Govern.Client/**"
---

# Game UI design system

The approaches for every game screen, panel, number and icon. Full description, catalog and "adding UI for a new mechanic": `docs/features/game-ui.md`. Enforced by `tests/Govern.ArchitectureTests/GameUiTests` (colours, terrain palette, river styles, a 3D model builder per `FeatureModel` and per `PieceModel`, a baked model per unit character, the 3D water level) and `tests/Govern.ClientTests` (`GameSpriteTests`: icons per ruleset unit, terrain, improvement, building and resource; `PieceModelsTests`: a 3D model per ruleset unit; `GameBoardTests`: layer order). Change the rule and the tests together.

## Where game UI goes

| Adding… | Put it in |
|---|---|
| A shared game component (numbers, costs, progress, confirm, icons) | `Components/Game/XxxX.razor`, with bUnit tests in `tests/Govern.ClientTests/Components/Game` and an entry on the design gallery (`Features/Design/Pages/DesignPage`) |
| Something in a screen zone | `Features/Games/Components/Screen/`: counters in `TopBar`, unit actions in `ActionBar`, a panel per selection kind in `SidePanel` |
| A panel for one selection kind | `Features/Games/Components/XxxPanel.razor`, chosen by `SidePanel` |
| UI state of the game screen (selection, bridge mode, lens, open view) | `Features/Games/Services/GameUiService`, cascaded by `GameTable`; never game state |
| New map content | a `Features/Games/Enums/BoardLayer` slot, rendered as `<g data-layer="…">` in that order |
| An icon | a `<symbol>` in `Components/Game/GameSprite.razor`; its id in `Helpers/Icons` (`unit-<id>`, `terrain-<id>`, `improvement-<id>`, `building-<id>`, `resource-<id>`, `ui-<name>`) |
| A colour | a token in `wwwroot/css/tokens.css` (light, and dark under `prefers-color-scheme: dark`) |
| A map lens (toggleable overlay) | a sealed `Features/Games/Lenses/XxxLens` implementing `IMapLens`, registered in `DependencyInjection`; `LensToolbar` and `BoardLens` pick it up. Edge information (river crossings) goes in `EdgeMarksFor` |

## Rules

- **Colours only as tokens.** No hex, `rgb()`/`hsl()`, named or other colour values outside `wwwroot/css/tokens.css`, none in Razor `fill`/`stroke`/`color`/`background`/`style` attributes, and none in `wwwroot/js/board3d` (it reads tokens through `palette.js`; `GameUiTests.Board3DScripts_TakeColoursOnlyFromTokens`). The one exception is the baked unit models (`wwwroot/models/units`, from `tools/unit-models`), whose own colours come from the KayKit artwork; the theme reaches them through `--gv-board3d-figure` and the seat colour. Use `var(--gv-…)` and classes: `seat-<n>` (from `Helpers/Seats`, the turn order) for players, `terrain terrain-<id>` for terrain.
- **Every ruleset id has its UI.** A new unit, terrain, improvement, building or resource needs its sprite symbol; a new unit also needs its 3D model in `Helpers/PieceModels` (`PieceModelsTests`), a new kind of model also a builder in `js/board3d/pieces.js`, and a new character its entry in `tools/unit-models/bake.mjs`, its baked file and its name in `FIGURES` (`Board3DPieces_EveryFigureHasItsBakedModel`); a new terrain also needs `--gv-terrain-<id>` and `--gv-terrain-<id>-explored` in `tokens.css`, its `.terrain-<id>` rule in `board.css` and its 3D relief in `Helpers/TerrainReliefs` (`TerrainReliefsTests`); a new river kind needs its `.river-<id>` rule (its width, so kinds differ by more than colour) in `board.css` and its 3D width in `Helpers/RiverReliefs` (`RiverReliefsTests`); a new improvement or building needs its 3D model in `Helpers/FeatureModels` (`FeatureModelsTests`), a new kind of model also a builder in `js/board3d/models.js`. A terrain symbol is its hex plus a `.terrain-mark` drawing, which the board shows on its tiles so terrain is never told by colour alone.
- **Icons only from the sprite.** Draw with `GameIcon` (panels) or `<use href="#id">` (board); no other inline SVG icons, image files or icon fonts for game things (Radzen's Material icons stay for generic buttons such as End turn). `GameIcon` is decorative unless `Label` is set.
- **Select, then act.** Nothing happens on a single accidental click: a destructive action uses `ConfirmButton`; a disabled action shows the rule's reason next to it.
- **Exact numbers on demand.** A derived number shows its origin through `NumberTooltip` (or a `StatRow` detail).
- **Keyboard and touch.** Every action is a button reachable with Tab; no behaviour depends on hover or long press alone.
- **Reuse before adding.** Check the catalog in `docs/features/game-ui.md` and the gallery first; a new shared component is added to both.
- **Layout.** The screen zones are the CSS grid in `wwwroot/css/game-ui.css`; they stack map first on narrow screens. No component sets its own page-level layout.

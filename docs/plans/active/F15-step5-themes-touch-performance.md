---
title: "Plan: F15 step 5, themes, touch and performance on the 3D board"
tags: [plan, client, board, 3d, webgl, threejs, touch, pinch-zoom, performance, themes, game-ui]
code: [src/Govern.Client/wwwroot/js/board3d, src/Govern.Client/Features/Games/Components/Board/GameBoard3D.razor.cs, src/Govern.Client/Features/Design]
updated: 2026-10-02
---

# Plan: F15 step 5, themes, touch and performance

**Status:** planned 2026-10-02 (backlog F15, step 5, the last step of the [F15 plan](F15-3d-board.md), after [step 4](../done/F15-step4-animations.md)). Visuals only: Govern.Core, the server, the rules, the view and `BoardScene` stay as they are.

## Context
The 3D board now draws everything the 2D board does, but it was built and checked on 20×14 maps with a mouse. On a phone, the only way to zoom is the buttons, and a second finger is ignored. Every new view rebuilds the whole scene: terrain, rivers, buildings, borders and pieces. Every frame also moves every HTML label, even ones off screen. Step 5 adds pinch to zoom and two-finger pan, and makes the board stay smooth on the largest map the ruleset allows (40×40). It also closes the small gaps left in theming. After this step, F15 is done. Retiring the SVG board stays the deferred item F17.

## Scope
- In:
  - **Touch:** pinch to zoom and two-finger pan, and a tap tolerance sized for fingers.
  - **Performance:**
    - A dev-only bench on the largest map, used to measure before and after.
    - Each layer is rebuilt only when its part of the scene changes.
    - Labels are placed only when the camera or a following piece moves.
    - Shadows and pixel ratio are lowered on small or weak screens, only if the bench shows they are needed.
  - **Themes:** a test that every token the 3D board reads exists in both themes. The theme switch keeps the camera and any running animation.
- Out:
  - Retiring the SVG board (F17).
  - Camera rotation and tilt gestures.
  - A theme toggle inside the app. Themes follow the system, as everywhere else.
  - A Playwright test project in CI.
  - Patching single tiles within a layer. A layer is the unit of rebuild unless the bench shows that isn't enough.
  - Offering 40×40 maps in the lobby. `CreateGameRequest` has no size today, so every game is 20×14. The bench is how 40×40 gets checked.

## What is already there
- **Themes:**
  - Every colour comes from a `--gv-*` token through `js/board3d/palette.js`, enforced by `GameUiTests.Board3DScripts_TakeColoursOnlyFromTokens`.
  - Every `--gv-board3d-*` token has light and dark values in `tokens.css`.
  - `scene.js` rebuilds on a `prefers-color-scheme` change.
  - So "read every colour from the tokens at mount and on theme change" was done in steps 1 to 3. This step only checks the remaining gaps.
- **Instancing:**
  - Terrain decorations, pieces (per shape) and features use `InstancedMesh` (`shapes.js`, `terrain.js`).
  - Tiles are one merged mesh, and the mist is one sheet.
  - The work left here is measuring, not instancing from scratch.
- **On-demand drawing:** frames are drawn only after a change or while something moves (step 4).
- **Touch:**
  - `touch-action: none` is set on `.board3d`.
  - One finger pans.
  - Non-primary pointers are ignored on purpose, so a second finger can't make the board jump.

## Affected areas
- `wwwroot/js/board3d/scene.js`:
  - Pointer handling tracks up to two touches: one finger pans, two fingers pinch and pan.
  - A rebuild compares each layer's input with the last one and rebuilds only the layers that changed.
  - A theme change still rebuilds everything.
  - On small or weak screens, a lower pixel ratio cap and a smaller shadow map, if needed.
- `wwwroot/js/board3d/camera.js`: `pinch(fromA, fromB, toA, toB)`. It zooms by the change in distance between the fingers and keeps the ground under their midpoint in place, reusing `zoomAt` and `pan`.
- `wwwroot/js/board3d/labels.js`: `place` skips work when the camera matrix hasn't changed and no label follows a moving piece. It writes `style.transform` and `hidden` only when they change.
- `wwwroot/js/board3d/board.js`: `stats(host)`, which returns the last rebuild's time per layer, draw calls and triangles (`renderer.info`), used only by the bench.
- `Features/Design/Pages/BoardBenchPage.razor` (new, Development only like `DesignPage`, at `/design/board`):
  - Generates a 40×40 game with `GameFactory.Create` (fixed seed, `GameSetup` width and height 40).
  - Builds a view with every tile in sight for the worst case, plus the real fogged `VisibilityCalculator.ViewFor` view.
  - Draws it with `GameBoard3D`, with buttons that step through a scripted turn (move a unit, attack, found a city) and the numbers from `stats`.
  - No Api call.
- `wwwroot/css/board.css`: none expected. `touch-action: none` is already there.
- `tests/Govern.ArchitectureTests/GameUiTests.cs`: one new check that every `--gv-*` token named in `wwwroot/js/board3d` is defined in `tokens.css` in both the light block and the dark block.

## Steps
1. **Bench first.**
   - Add `BoardBenchPage` and `stats`.
   - Record the baseline in this plan:
     - mount time;
     - time to apply a new view, split into C# `BoardSceneBuilder`, interop and JS rebuild per layer;
     - frame time while panning, with and without the yields lens (its labels sit on every visible tile);
     - draw calls and triangles.
   - Measure at 1400 px and at 390 px with 4× CPU throttling (Chromium DevTools protocol), on the all-visible view and the fogged view.
2. **Touch.**
   - Two active touch pointers start a pinch. Moving them zooms around the midpoint and pans with it.
   - Lifting one finger hands panning to the other without a jump.
   - A gesture that ever had two fingers never ends in a click.
   - A touch press counts as a tap within 10 px (a mouse stays at 4 px, as the 2D board).
   - The wheel and the zoom buttons are unchanged.
3. **Layer rebuilds.**
   - `setScene` keeps, for each layer, the part of the scene it draws:
     - terrain: tiles;
     - rivers: rivers and bridges;
     - features: features;
     - borders: borders;
     - pieces: pieces.
   - It rebuilds only the layers whose part changed, compared by a cheap serialised key.
   - `placeSun` and the camera bounds run only when the map size changes.
   - A theme change and the loading of the baked figures still rebuild everything (figures rebuild only the pieces).
4. **Labels.**
   - `place` runs its loop only when the camera moved, the labels changed, or a piece is followed (step 4's `follow`).
   - It writes to the DOM only on a change.
   - Labels outside the canvas stay hidden without per-frame style writes.
5. **Small screens, only if the bench needs it.**
   - Applies when step 1's 390 px throttled pan stays above a 33 ms frame after steps 3 and 4.
   - Cap the pixel ratio at 1.5 and use a 1024 shadow map when the screen is narrow or the pointer is coarse (`matchMedia('(pointer: coarse)')`).
   - Otherwise leave the look as it is.
6. **Themes.**
   - Add the token check.
   - Confirm a theme switch mid-walk keeps the walk and the camera: the motion re-applies after a rebuild, as in step 4.
7. **Measure again** with the bench and record the after numbers next to the baseline.
8. **Docs, rules, close-out.**
   - The F15 plan and this plan move to `done/`.
   - The F15 backlog row moves to Recently done, in the same PR.

## Tests
- Unit: none (client only; Core does not change).
- Integration: none (no server change).
- Client (bUnit):
  - `BoardBenchPage` renders only in Development and builds a 40×40 scene.
  - `GameBoard3D` behaviour is unchanged: the existing tests stay green, and the scene is still sent once per view version.
- Architecture: `GameUiTests.Board3DScripts_UseTokensDefinedInBothThemes` (new). `ClientSecurityTests` and the colour rule already cover the JS changes. The bench page sits under the existing `Features/Design/Pages` rules (a `XxxPage` component).
- Manual (Playwright with touch emulation, `hasTouch` and `isMobile` at 390 px):
  - A pinch zooms around the fingers.
  - Two-finger pan works.
  - A pinch never selects.
  - A tap selects and moves.
  - One finger still pans.
  - Desktop wheel, drag and click are unchanged.
  - Light and dark.
  - Bench numbers before and after in this plan.
  - Screenshots to `/mnt/project-files/screenshots/f15-3d-board/step5/`.

## Docs & rules
- `docs/features/game-ui.md`:
  - The Performance section describes the 3D board: layer rebuilds, on-demand frames, label placement, the bench at `/design/board` and the recorded numbers.
  - The 3D section mentions pinch and two-finger pan.
- `docs/plans/active/F15-3d-board.md`: step 5 marked done and the plan moved to `done/` (F15 complete). `docs/plans/backlog.md`: F15 to Recently done, and F17's trigger ("F15 steps 4 and 5 are done") is met. Moving F17 back out of Deferred is for the owner to decide.
- `docs/README.md`: index entries for this plan and the moved F15 plan.
- `.claude/rules/game-ui.md`: one line on the 3D board's performance budget and that `/design/board` is where it is checked.

## Verification
- `dotnet build Govern.slnx`, `dotnet format Govern.slnx --verify-no-changes` and `dotnet test Govern.slnx` are green.
- Run `dotnet run --project src/Govern.Api` and `src/Govern.Client`, then:
  - The bench at `/design/board` meets the budgets:
    - a new view applied in under 100 ms on a desktop;
    - panning under 16 ms a frame on a desktop and under 33 ms at 390 px with 4× throttling, on the all-visible 40×40 map with the yields lens on.
  - A real two-account game on the 20×14 map with touch emulation: the manual checks above.

## Risks & open questions
- **The cloud container has no GPU.** Chromium there renders WebGL in software (SwiftShader), so its frame times are worse than any real device and show only before and after. A check on a real mid-range phone needs Andrii, through his Cloudflare tunnel, with the bench page. The budgets are judged on the bench's CPU-side numbers (rebuild and label time), which software rendering doesn't distort.
- **A full-visible 40×40 map isn't a real game today.** Games are 20×14 and fogged. The bench is a stress test, and the numbers that matter for play are the fogged view's.
- **Layer keys cost time too.** Serialising each layer's input to compare it is far cheaper than rebuilding, but the bench checks it. If keys show up, `BoardSceneBuilder` can stamp a per-layer version instead, which changes `BoardScene` and would be noted here.

---
title: "Plan: F15 step 4, animations on the 3D board"
tags: [plan, client, board, 3d, webgl, threejs, animations, motion, reduced-motion, game-ui]
code: [src/Govern.Client/Features/Games/Services/BoardAnimationService.cs, src/Govern.Client/Features/Games/Components/Board/GameBoard3D.razor.cs, src/Govern.Client/wwwroot/js/board3d]
updated: 2026-10-02
---

# Plan: F15 step 4, animations on the 3D board

**Status:** done 2026-10-02 (backlog F15, step 4 of the [F15 plan](../active/F15-3d-board.md), after [step 3b](F15-step3b-unit-models.md)); decisions made while building it are at the end. Visuals only: Govern.Core, the server, the rules, the view and the game log stay as they are.

## Context
The 2D board animates what happens since F11b ([plan](F11b-animations.md)): units walk, attacks lunge with floating damage, destroyed units fade and new cities and builds pulse. The 3D board still jumps straight to each new view, so an opponent's turn is easy to miss there. This step plays the same animations on the 3D board from the same source: `BoardAnimationService` and its `BoardMotion`, built from the game log's events. No new event, timing rule or mechanic is added; the 3D board only draws the motion the 2D board already gets.

## Scope
- In: the F11b set on the 3D board: walking tile by tile (turned to face the way they walk, following the ground's height), the attack lunge, floating damage numbers, a destroyed unit fading, the tile pulse for a city founded or captured and an improvement or bridge built; a banner and health bar that move with their piece; skipping on a click or a newer view; reduced motion.
- Out: new kinds of animation or events (a building finished, healing, upkeep damage, territory growth stay unanimated, as on the 2D board); skeletal or walk-cycle animation of the KayKit characters (they are baked static, step 3b); the camera following events; sound; a speed setting; camera rotation.

## Affected areas
- `Features/Games/Services/BoardAnimationService.cs`: unchanged in what it plays and when. It keeps working in 2D board points (`BoardPoint` from `HexLayout`); the 3D board converts them.
- `Features/Games/Models/ScenePiece.cs`: gains the unit's id (`Unit`, null for a city), so the renderer can find the piece a motion moves.
- `Features/Games/Models/SceneMotion.cs` (new): what the renderer animates, in scene units: `SceneMotionPosition` (unit id, x, z, height), ghosts as `ScenePiece`s with a `Fading` flag, and `SceneEffect` (key, kind, x, z, height, text).
- `Features/Games/Helpers/SceneMotionBuilder.cs` (new): `BoardMotion` + the drawn `BoardScene` → `SceneMotion`. Converts a board point with the same scale `BoardSceneBuilder` uses (factor it out of `BoardSceneBuilder` into one shared `Scale`), takes the height from the tile under the point (between two tiles, the nearer one; the JS smooths it), and builds a ghost's model, seat and icon with `ScenePieceBuilder`'s rules.
- `Features/Games/Components/Board/GameBoard3D.razor(.cs)`: a new `Motion` parameter; sends the `SceneMotion` only when `Motion` changes by reference (as the 2D `GameBoard` compares it), after the scene of the same view.
- `Features/Games/Components/GameTable.razor`: passes `Motion="@_animation.Motion"` to `GameBoard3D` too.
- `Features/Games/Services/Board3DInteropService.cs`: `RenderMotionAsync(SceneMotion)`; no-op when not mounted, as the others.
- `wwwroot/js/board3d/motion.js` (new): tweens. Each animated piece has a drawn position and a target; a new target starts a tween from where it is drawn, lasting `step` (120 ms, the 2D `Step`) for a walk and half of `lunge` (80 ms) for each half of a lunge, eased in and out; the piece turns to face its direction of travel and back towards the camera when it stops; its height follows the ground between tiles with a small hop per tile. Ghosts fade (opacity to 0 and sink slightly) over the effect lifetime (900 ms). The durations come from the interop call, not constants copied into JS, so the 2D and 3D boards never drift apart.
- `wwwroot/js/board3d/pieces.js`: pieces named by a motion are pulled out of the instanced batch and built as their own `Group` (same parts, rooted at the piece so moving and turning the group moves it, materials cloned so a ghost can fade); the batch is rebuilt only when the set of animated pieces changes (an animation starts or ends), not on every step.
- `wwwroot/js/board3d/scene.js`: `renderMotion(motion)`; while a tween or effect runs, draws every frame with `requestAnimationFrame`, otherwise stays on demand. A scene rebuild keeps the latest motion and re-applies it, so a view that arrives mid-walk doesn't snap the walking unit to its end tile.
- `wwwroot/js/board3d/labels.js`: a piece's banner, health bar and (for the selected unit) the selection ring follow its drawn position; damage numbers as HTML labels that float up and fade (in `wwwroot/css/board.css`, reusing the 2D board's `gv-float` and `gv-fade` keyframes and its `--gv-health-low` on `--gv-label-outline` colours).
- `wwwroot/js/board3d/overlays.js`: the pulse, a ring on the tile top that grows and fades in `--gv-selected`, the 2D pulse's colour (no new token).

## Steps
1. Client C#: `ScenePiece.Unit`; the shared scale; `SceneMotion` and `SceneMotionBuilder`; `GameBoard3D.Motion` and `GameTable` passing it; `Board3DInteropService.RenderMotionAsync` with the step, lunge and effect durations.
2. JS: `motion.js` tweens; `pieces.js` actors apart from the batch; `scene.js` frame loop while something moves and re-applying the motion after a rebuild.
3. JS and CSS: ghosts fading, damage labels, pulses, banners and the ring following their piece.
4. Skipping and reduced motion: `BoardMotion.None` (sent when the service finishes, on a click or a newer view) snaps every actor to its scene place, drops ghosts and effects and returns the pieces to the batch in one frame. Reduced motion needs no JS of its own: the service already skips walks and lunges and keeps effects short, and the damage label's CSS keyframes drop their movement under `prefers-reduced-motion: reduce`, as on the 2D board.
5. Docs, rules and screenshots.

## Tests
- Unit: none (client only; Core does not change).
- Integration: none (no server change).
- Client (bUnit): `SceneMotionBuilder` (a position lands on the same scene point as the tile it stands for, height from the tile under it, a ghost keeps its model, seat and fading flag, effects carry their key, kind and text, `BoardMotion.None` builds an empty motion); `ScenePieceBuilder` sets `Unit` on units and not on cities; `GameBoard3D` sends a motion only when `Motion` changes by reference and sends it after the scene of a new view; `Board3DInteropService.RenderMotionAsync` is a no-op when not mounted; `GameTable` passes the service's motion to the 3D board.
- Architecture: none new. `GameUiTests.Board3DScripts_TakeColoursOnlyFromTokens` and `ClientSecurityTests` already cover `motion.js` and the label changes.
- Manual (Playwright, two seats, 3D on): your own walk, attack and kill; the opponent's visible walk and attack on the other seat; a walk out of sight as a ghost; a city founded and a bridge built pulse; a click mid-walk finishes it; emulated reduced motion shows no walking; light and dark at 1400 and 390 px. Screenshots and a short screen recording (frames) to `/mnt/project-files/screenshots/f15-3d-board/step4/`.

## Docs & rules
- `docs/features/game-ui.md`: the "Board animations" section says both boards play the motion; the 3D section gains `SceneMotion`, `SceneMotionBuilder` and `motion.js`, and drops "animations follow in a later F15 step".
- `docs/plans/active/F15-3d-board.md`: step 4 marked done with its PR; this plan moved to `done/` and the backlog F15 row updated in the same PR.
- `docs/README.md` index entry for this plan.
- `.claude/rules/game-ui.md`: one line: board animations come only from `BoardAnimationService` (both boards); the 3D board never decides what or when to animate, only how to draw it.

## Verification
- `dotnet build Govern.slnx`, `dotnet format Govern.slnx --verify-no-changes`, `dotnet test Govern.slnx` green.
- `dotnet run --project src/Govern.Api` and `src/Govern.Client`, two accounts, 3D on: the manual checks above; the browser's performance panel shows no frames drawn while nothing moves.

## Risks & open questions
- **Rebuilding the batch when an animation starts or ends** costs one pieces rebuild each time (not per step). Fine for the 2-player maps of today; step 5's performance pass checks a 40×40 map.
- **Static characters slide rather than walk.** The baked KayKit figures have no skeleton, so a walk is a turn, a glide and a small hop per tile. A real walk cycle would mean baking the pack's animations, which is out of scope; it can be a later item if the glide reads poorly.
- **Durations live in C#.** If the 2D board is retired (backlog F17), `BoardAnimationService` stays as the one place they are set.

## Decisions made during implementation
- **The selection ring stays on the tile** while the selected unit walks (the overlay is not rebuilt per step); the banner and health bar do follow the piece.
- **The pulse is drawn over the pieces** (no depth test), like the 2D effects layer, since a city's model otherwise hides most of it; it grows from 0.8 to 1.15 of the tile, as the 2D pulse does.
- **A walk's first place is drawn at once.** The view is applied before the motion, so the scene already has the unit on its last tile; a first place a step or more from it is where the walk starts and is drawn there directly, while a shorter one (a lunge) glides from the tile.
- **Actors live until everything settles**, so a lunge's way back glides too and the shared meshes are rebuilt only when a unit starts animating and once at the end.
- **`HexLayout.Nearest`** (new) finds the tile under a point for its surface height; a point between tiles takes the nearer tile's.
- **A fading ghost casts no shadow**, which would otherwise stay dark while the model fades.
- **Played-out effects stay played out.** The service keeps every effect and fading ghost in its motion until the batch ends, so `motion.js` remembers the ones that have run their time and never starts them again.

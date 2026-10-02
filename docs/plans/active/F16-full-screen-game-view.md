---
title: "Plan: F16, full-screen game view"
tags: [plan, client, game-ui, layout, hud, board, 3d, phone]
code: [src/Govern.Client/Features/Games/Components/GameTable.razor, src/Govern.Client/Features/Games/Components/Screen, src/Govern.Client/wwwroot/css/game-ui.css, src/Govern.Client/Layout]
updated: 2026-10-02
---

# Plan: F16, full-screen game view

**Status:** planned 2026-10-02 (backlog F16). Visual and layout only: Govern.Core, the server, the rules, the view, `GameUiService`'s selection rules and the commands stay as they are.

## Context
A started game is a page inside the app shell: the app header and sidebar, a page title, then a grid with the top bar, a board capped at `min(70vh, 40rem)`, a 20rem side panel and the action bar floating over the board's corner. The board, which is the game, gets about half the screen on a laptop and a short strip on a phone, and the 3D board (F15) shows more when it has more room. F16 lets the board fill the screen and puts the controls over it as floating cards: counters and End turn in a top strip, the selected unit or city with its actions in a corner, the attack forecast next to its target, and the log in a drawer. It works over both boards, since the SVG board stays until F17.

## Scope
- In:
  - A started game (in progress or finished) fills the window below the app header: no sidebar, no page padding, no page title row, and the board takes the whole area on every screen size.
  - **Top strip** over the top edge: the game's name and the connection badge (moved from the page heading), then today's `TopBar` (seat, turn, resources, units that can still move, End turn, Resign), plus a Log button and a Full screen button.
  - **Selection card** in the bottom-left corner: today's `UnitPanel` with the `ActionBar` actions under it, or the `CityPanel` with its tabs. It has a header with the selection's name and a collapse button; the body scrolls inside a capped height. With nothing selected, a one-line hint sits there instead.
  - **Attack forecast next to the target:** `CombatForecastPanel` floats beside the target tile on both boards, follows it when the board pans or zooms, and is pushed back inside the screen when the tile is near an edge.
  - **Log drawer:** `GameLog` moves from the side panel into a drawer on the right, opened by the Log button and closed by default. The button shows how many entries arrived since it was last open.
  - **Notices:** the stale-command notice, the Api error and the game result float top-centre under the strip.
  - **Map controls:** the lens toolbar and the zoom, Fit and 2D/3D buttons stay where they are on the board, moved down below the top strip.
  - **Browser full screen:** the Full screen button uses the browser's Fullscreen API on the game screen (hiding the app header and the browser's own bars), Esc or the button leaves it. Hidden where the browser doesn't support it (iPhone Safari).
  - **Phones (narrower than 40rem):** the top strip keeps the turn, End turn and a menu button for the rest (resources, units that can still move, Log, Resign, Full screen); the selection card is a bottom sheet across the full width, at most 45% of the screen high and collapsible to its header; the forecast opens in that sheet instead of beside the tile; the log drawer takes the full width.
- Out:
  - Retiring the SVG board (F17).
  - Any change to what the panels show or to the rules.
  - The "Full-screen view" zone in `docs/features/game-ui.md` for large content (tech trees G11, the city screen, governors G12). That is a view that covers the board; F16 is the layout of the board screen itself. The zone is renamed "Large view" in the docs so the two aren't confused.
  - Draggable or resizable cards, and remembering the layout between visits beyond the collapse state.
  - A Playwright test project in CI (checked by hand as before).

## Affected areas
- `src/Govern.Client/Layout/GameLayout.razor` (new): the app header from `MainLayout` without the sidebar, and a body that fills the rest of the window with no padding. The header moves into a `Layout/AppHeader.razor` component used by both layouts, so it isn't copied.
- `src/Govern.Client/Features/Games/Pages/GamePage.razor`: `@layout GameLayout`. The lobby, "not found" and loading states keep their padded page inside it; a started game renders only `GameTable`, which now receives the title and the watch for the connection badge.
- `src/Govern.Client/Features/Games/Components/GameTable.razor(.cs)`: the zones become a board layer plus overlay slots (`hud-top`, `hud-notices`, `hud-selection`, `hud-log`), and it owns the Full screen interop and the forecast anchor.
- `src/Govern.Client/Features/Games/Components/Screen/`:
  - `TopBar.razor`: takes the title, the connection badge, the Log and Full screen buttons; on phones the secondary items go into a `RadzenProfileMenu`-style menu (or a plain popup built like `NumberTooltip`, whichever needs no new pattern).
  - `SelectionCard.razor` (new, replaces `SidePanel`): the card frame with header, collapse and scrolling body; chooses `UnitPanel` + `ActionBar` or `CityPanel` as `SidePanel` does today. On phones it also holds the forecast.
  - `LogDrawer.razor` (new): the drawer around today's `GameLog`, with the unread count.
  - `NoticeStack.razor` (new): the stale notice, `ApiErrorAlert` and `GameOverPanel`.
  - `SidePanel.razor`: removed; its tests move to `SelectionCard` and `LogDrawer`.
  - `ActionBar.razor`: no longer floats on its own; it renders inside the selection card, under the unit panel.
- `src/Govern.Client/Features/Games/Services/GameUiService.cs`: UI-only state: `SelectionCollapsed`, `LogOpen`, `LogSeen` (the log count when the drawer was last open), `FullScreen`. `Escape` closes the log drawer first when it is open. Never game state.
- Boards, for the forecast anchor (`Components/Board/GameBoard.razor(.cs)`, `GameBoard3D.razor(.cs)`): a new `Anchored` render fragment and `AnchorAt` hex parameter. Each board draws the fragment in a `.board-anchor` element positioned over that hex:
  - 2D: the position comes from the board's own `BoardViewport` and `HexLayout.Center` in C#, recomputed when the viewport changes.
  - 3D: the element is handed to `wwwroot/js/board3d/labels.js` as one more anchored label (a new `anchor(element, x, y, z)` / `unanchor` pair in `board.js`), so it follows the camera like the lens labels already do, at no cost when nothing moves.
  - Both clamp the anchor so the card stays on screen, and place it on the side of the tile with more room.
- `src/Govern.Client/wwwroot/js/fullscreen.js` (new, small module): `supported()`, `enter(element)`, `exit()`, and a `fullscreenchange` callback so Esc in the browser updates the button.
- `src/Govern.Client/wwwroot/css/game-ui.css`: the grid becomes a full-size `position: relative` box with the board filling it and the overlay slots absolutely positioned (`pointer-events: none` on the slots, `auto` on the cards, so drags between cards reach the board). A `--gv-hud-top` custom property carries the strip's height for the lens toolbar and zoom buttons. Cards keep today's look (raised surface, border, radius) with the existing `--gv-shadow`; any new colour is a token in `tokens.css` with both themes.
- `src/Govern.Client/wwwroot/css/board.css`: `.board-frame` and `.board3d` take `height: 100%` inside the game screen (the `min(70vh, 40rem)` cap stays for the design gallery and the bench); `.lens-toolbar` and `.board-zoom` start at `var(--gv-hud-top, var(--gv-space-2))`.
- Reused as they are: `UnitPanel`, `CityPanel` (and its tabs), `CombatForecastPanel`, `GameLog`, `GameOverPanel`, `ConnectionBadge`, `ConfirmButton`, `LensToolbar`, `BoardModeToggle`, `GameUiService.IdleUnits` and `SelectNextIdleUnit`.

## Steps
1. Client, layout: extract `Layout/AppHeader.razor` from `MainLayout`; add `Layout/GameLayout.razor`; `GamePage` uses it, and keeps its padded content for the lobby and error states.
2. Client, state: add `SelectionCollapsed`, `LogOpen`, `LogSeen`, `FullScreen` and their toggles to `GameUiService`; `Escape` closes the log drawer before clearing the selection. Selecting something new expands a collapsed card.
3. Client, overlay layout: rework `GameTable` and `game-ui.css` into the board plus overlay slots; move the title and badge into `TopBar`; add `NoticeStack`; board height 100% inside the game screen; the lens toolbar and zoom buttons below the strip.
4. Client, selection card: `SelectionCard` replaces `SidePanel` with `ActionBar` inside it; collapse; capped height with scrolling; the empty-selection hint.
5. Client, log drawer: `LogDrawer` with the Log button and unread count; Esc closes it.
6. Client, forecast anchor: `Anchored`/`AnchorAt` on both boards, the 3D `anchor`/`unanchor` interop, clamping and side choice; `GameTable` passes the forecast as the fragment while a target is pending.
7. Client, browser full screen: `fullscreen.js` and the Full screen button, hidden when unsupported.
8. Client, phones: the 40rem breakpoint: compact strip with the menu, bottom sheet, forecast in the sheet, full-width drawer.
9. Docs and close-out (in the same feature PR): `docs/features/game-ui.md`, the `game-ui.md` rule, the plan to `done/`, the backlog row to Recently done.

## Tests
- Unit: none (no Utils, Core or Domain change).
- Client (bUnit):
  - `GameTableTests`: the screen has the board layer and the `hud-top`, `hud-notices`, `hud-selection` and `hud-log` slots; replaces today's zone test.
  - `TopBarTests` (or the existing GameTable tests): shows the game's name and the connection badge; the Log button shows the unread count and opens the drawer; the Full screen button is absent when `fullscreen.supported()` returns false and calls `enter` when clicked.
  - `SelectionCardTests`: a selected unit shows `UnitPanel` with the action bar's buttons inside the card; a city shows `CityPanel`; nothing selected shows the hint; collapse hides the body and keeps the header; selecting another thing expands it. The existing `GameTableBuildTests`, `GameTableCityTabsTests`, `GameTableUpkeepTests` and `GameTableAnimationTests` keep passing with their selectors moved from the side panel to the card.
  - `LogDrawerTests` (from `GameTableLogTests`): closed by default; the count of new entries; opening clears it; Esc closes the drawer before it clears the selection.
  - Forecast: with a pending target, the forecast renders inside `.board-anchor` on the 2D board at the target hex's position for the current viewport, and moves when the viewport changes; on the 3D board `anchor` is called with the target's position and `unanchor` when the attack is cancelled or sent.
  - `GameUiServiceTests`: the new toggles, `Escape` order, expand on new selection.
- Integration: none (no server change).
- Architecture: none new. `GameUiTests` already checks that colours are tokens, which covers the new CSS; the `FolderStructureTests` whitelist already has `Layout`.

## Docs & rules
- `docs/features/game-ui.md`: rewrite "Game screen layout (zones)" as the board with its overlay slots (table of slot, component, holds, grows with), the phone layout, browser full screen; the forecast anchor under "Interaction rules"; the "Full-screen view" zone renamed "Large view"; the new UI state in "UI state (GameUiService)"; `SelectionCard`, `LogDrawer`, `NoticeStack` and `GameLayout` in the catalog.
- `.claude/rules/game-ui.md`: "Something in a screen zone" becomes "something over the board": counters in `TopBar`, unit actions in `ActionBar` (inside the selection card), a panel per selection kind in `SelectionCard`; the Layout rule describes the board plus overlay slots and says cards never cover the board's middle on desktop.
- `.claude/rules/blazor-client.md`: `Layout/` lists `GameLayout` and `AppHeader`.
- `docs/README.md`: the plan entry while active, then its done path.
- `docs/plans/backlog.md`: F16 status `planned` with a link to this plan now; at merge, the row moves to Recently done.

## Verification
- `dotnet build Govern.slnx` and `dotnet test Govern.slnx` green, with the summary shown; `dotnet format Govern.slnx --verify-no-changes`.
- By hand with Playwright against the running Api and client, light and dark, at 1400×900 and 390×844, on both boards, screenshots in the PR:
  - The board fills the window below the app header; nothing scrolls the page.
  - Select a unit: the card with its actions sits bottom-left; the board's middle stays clear; drags between cards pan the board.
  - Click an enemy: the forecast opens beside it; pan and zoom, and it follows; near an edge it stays on screen; Attack and Cancel work.
  - Select a city: the card with tabs scrolls inside its cap; collapse and expand.
  - Log button: the count, the drawer, Esc closes it.
  - Full screen: enters and leaves; Esc in the browser updates the button.
  - Phone width: compact strip and menu, bottom sheet, forecast in the sheet.
- The bench at `/design/board`: no new per-frame work while nothing moves with a forecast anchored (the anchor is placed only when the camera moves).

## Risks & open questions
- **The board's corners are covered.** The selection card covers the bottom-left corner and the strip the top edge. Fit and the opening view should leave room for them: `HexLayout.Around` and the 3D fit take a padding for the overlay sizes, so the player's units don't open under a card.
- **Tall city panel.** `CityPanel` with its tabs can be taller than the card's cap; it scrolls inside the card, and the tabs stay at the top of the scroll. If that feels cramped, the city panel is the first user of the "Large view" later, not part of F16.
- **Fullscreen on iPhone.** Safari on iPhone doesn't allow it for page elements, so the button is hidden there; the layout already fills the window, which is most of the gain. Adding the app to the home screen remains the way to drop the browser bars there.

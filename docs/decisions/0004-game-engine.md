---
title: "ADR 0004: pure, deterministic game engine in Govern.Core with an authoritative server"
tags: [adr, game, core, engine, determinism, hex, turns, fog-of-war, sse]
code: [src/Govern.Core, tests/Govern.ArchitectureTests/LayerDependencyTests.cs, tests/Govern.ArchitectureTests/ProjectReferenceTests.cs]
updated: 2026-09-28
---

# ADR 0004: pure, deterministic game engine

## Status
Accepted (2026-09-27).

## Context
Govern is a 2-player, browser-based strategy game (a simplified Civilization). Players must not be able to cheat from the browser. Rules should be easy to extend, and the client should be able to preview moves without duplicating logic.

## Decision
- **Authoritative server:** the client sends commands (intents), and the server validates and applies them. Only the server changes the stored state.
- **Engine shape:** the rules live in `Govern.Core` as `GameEngine.Execute(state, actor, command)`. It returns a new immutable `GameState` plus events, or a `RuleViolation`. There is one rule class per command, and unit and terrain stats are data catalogs.
- **Purity:** Core references no project and no package; it uses the base class library only. It does no I/O, reads no clock and never uses `System.Random`. That keeps it safe to ship to the Blazor WASM client. Core also avoids `Govern.Utils`, which carries FluentValidation and the mediator. Instead it has its own small `RuleViolation`/`CommandResult`, which Application will map to `Error` in one place.
- **Determinism:** seeded SplitMix64 stored in the state; ids drawn from the same generator (a counter until [ADR 0005](0005-live-updates.md)). Replaying a command log rebuilds the game exactly.
- **Game model:** hex map with axial coordinates (pointy-top, odd-r rectangular maps), alternating turns, settler and soldier units, fog of war from the start.
- **Player ids:** the host chooses each `PlayerId`. Govern uses the per-game seat id (`GamePlayer.Id`), never the user id, so a `PlayerView` carries no account ids (decided in [Plan 2](../plans/done-game-lobby.md)).
- **Fog of war:** clients only ever receive `PlayerView`, which hides unexplored terrain and enemy objects outside sight. The transport is SSE pushing each player's snapshot when their view changes ([ADR 0005](0005-live-updates.md)).
- **Storage format:** one JSON format (`GameSerializer`) with stable discriminators and a schema version, used for Postgres jsonb, the Api and the client.

## Consequences
- The rules are fully unit-testable without a database or HTTP. The client previews legal moves with the same code through `PlayerView`-based overloads (`GameEngine.ReachableTiles`/`AttackTargets`/`CheckFoundCity` over a view, added in Plan 5); the client never holds a `GameState`.
- Hidden information stays on the server. Raw events must be filtered per player before they are sent, and rule rejections are written so they reveal nothing hidden (`*.not_found` for enemy ids, `move.unreachable` for hidden blockers).
- The architecture tests (`Core_DependsOnlyOnBaseClassLibrary`, `Core_HasNoPackageReferences`) fail the build if a framework dependency creeps into Core.
- Alternating turns avoid move conflicts; simultaneous turns would need conflict resolution later.
- v1 combat (the attacker always wins) is deliberately simple. Strength-based combat can use `DeterministicRandom` without changing the architecture.

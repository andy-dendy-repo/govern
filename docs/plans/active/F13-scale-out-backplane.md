---
title: "Plan: F13 scale out beyond one Api instance (Postgres LISTEN/NOTIFY backplane)"
tags: [plan, server, sse, live-updates, scale-out, backplane, postgres, listen-notify, neon, realtime]
code: [src/Govern.Infrastructure/Realtime, src/Govern.Application.Common/Abstractions/IGameChangeService.cs, docs/decisions/0005-live-updates.md]
updated: 2026-09-30
---

# Plan: Scale out beyond one Api instance (Postgres LISTEN/NOTIFY backplane)

**Status:** planned 2026-09-30 (backlog F13, Next). Only needed when the Api runs more than one instance. Written together with the Neon move ([F14](F14-neon-postgres.md)) so its Neon constraints aren't forgotten.

## Context
Live game updates reach players through an in-process hub: `InMemoryGameChangeService` (`src/Govern.Infrastructure/Realtime/Services/`) behind `IGameChangeService` (`src/Govern.Application.Common/Abstractions/`). A change saved on one instance only wakes streams on that same instance ([ADR 0005](../../decisions/0005-live-updates.md)), so the Api must run as exactly one instance. With two or more, an opponent connected to the other instance would never see the move. Postgres `LISTEN/NOTIFY` lets every instance hear "game X changed" through the database the app already has, with no Redis or Service Bus.

## Scope
- In: a Postgres-backed `IGameChangeService` that publishes with `NOTIFY` and fans out locally on `LISTEN`; resync after a listener reconnect; Neon/hosting constraints below.
- Out: moving the other per-instance state to shared storage (rate limits, sign-in throttling). That's a separate item, needed at the same time for correct limits across instances.

## Affected areas
- `Infrastructure/Realtime/Services/PostgresGameChangeService.cs` (new): `Publish(gameId)` runs `SELECT pg_notify('game_changed', @id)` after the save; a hosted listener holds one dedicated `NpgsqlConnection` with `LISTEN game_changed` and signals the existing local subscriptions.
- Reuse `InMemoryGameChangeService` as the local fan-out (the listener calls its `Publish`); the Application layer and `WatchGameContract` don't change.
- `Infrastructure/DependencyInjection.cs`: choose the implementation by a `Realtime:Backplane` option (`InMemory` default, `Postgres`).
- `Options/RealtimeOptions.cs` (Infrastructure), `docs/operations/configuration.md`.

## Steps
1. Infrastructure: the listener as a `BackgroundService` on a **direct, unpooled** connection (`Pooling=false`, its own connection string key if the main one points at a pooler), with `Keepalive` set and `WaitAsync` in a loop.
2. Infrastructure: on connection loss, reconnect with backoff; after every (re)connect, signal **all** local subscriptions once. Streams re-read and push a snapshot only if the player's view changed, so a missed notification costs one extra read per open stream, never a missed move.
3. Infrastructure: `Publish` sends `NOTIFY` (payload is only the game id, no game data, as today) and also signals locally, so the same instance never waits on the round trip.
4. Api: none. Health: add the listener's state to `/health/ready`.

## Tests
- Integration: two test hosts on the same Testcontainers database. A command on host A wakes a stream on host B. After killing the listener's connection (`pg_terminate_backend`), the stream still gets the next change, and a change made during the outage arrives as a resync snapshot.
- Architecture: none; existing layer rules cover it.

## Docs & rules
- ADR 0005 Consequences (backplane done), `docs/features/games.md` ("Single instance"), `docs/operations/deployment.md` (how to scale out), backlog close-out in the same PR.

## Verification
`dotnet build Govern.slnx`, `dotnet test Govern.slnx` green; the two-host integration test passes.

## Risks & open questions
- **Neon scale to zero (if F14 is done):** Neon counts only running queries as activity and closes idle connections after 5 minutes before suspending. An idle `LISTEN` connection gets dropped; the reconnect wakes the compute again. So with a backplane the database effectively never sleeps: about 180 CU-hours a month at 0.25 CU, more than the free 100. **F13 on Neon means a paid Neon plan**, or another always-on Postgres.
- **Neon pooled endpoint:** the `-pooler` host runs PgBouncer in transaction mode, which silently loses `LISTEN` registrations (no error). The listener must use the direct host. F14 already uses the direct host for everything.
- **Delivery is at-most-once:** notifications sent while a listener is disconnected are lost. Step 2's resync covers it.
- **Other per-instance state:** rate limits and sign-in throttling are in memory per instance. With N instances the effective limits become N× looser. Decide before scaling out whether that's acceptable or needs shared storage.
- **Do we need it at all?** One small instance serves a turn-based 1v1 game for a long time. Revisit when one instance is actually short on CPU or memory, or zero-downtime deploys with overlapping instances are wanted.

Sources for the Neon points: Neon docs on [scale to zero](https://neon.com/docs/guides/scale-to-zero-guide), [compute lifecycle](https://neon.com/docs/introduction/compute-lifecycle) and [connection pooling](https://neon.tech/docs/connect/connection-pooling/). The docs site was read through search excerpts, and the reconnect behaviour is inferred from them.

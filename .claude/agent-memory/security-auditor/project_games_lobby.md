---
name: project-games-lobby
description: Games lobby feature (Plan 2, feature/game-lobby) audit facts 2026-09-27 - BOLA model, invite-code secret, open abuse/rate-limit findings to re-check at Plan 3
metadata:
  type: project
---

Audit 2026-09-27 of uncommitted feature/game-lobby (5 endpoints under /api/games, all RequireAuthorization() with default policy, no rate limiting): no Critical/High.

Verified facts (re-check if handlers change):
- BOLA: GetGame filters by Players.Any(UserId); StartGame loads by id then FindPlayer -> game.not_found before any other check; 403 not_creator only for seated players; ListMyGames filtered by user. Outsider == unknown id (404).
- Responses carry no user ids (PlayerId = GamePlayer.Id, Guid v7); LoggingPipeline logs only request name/error code; Npgsql redacts error Detail (no Include Error Detail anywhere).
- InviteCode: 16 CSPRNG bytes, exact 22-char base64url validation, unique index, body-only. Seed: CSPRNG ulong, not stored; RngState (= seed + 4*gamma, SplitMix64) lives only in jsonb, never in PlayerView.
- Join/start races settled by games.revision concurrency token + unique (game_id, seat)/(game_id, user_id).

Open at that audit:
- Medium (API4): 5-open-lobby cap is bypassed by create->join(alt)->start loop; started games are unbounded ~20 KB jsonb rows, no per-user limiter, Postgres 32 GB autoGrow Disabled -> read-only DB = full outage. Fix: per-user rate-limit policy on create/join/start + cap on active games per user.
- Low: lobby cap race is bounded only by request parallelism (doc says "+1"); no cancel/leave -> unstarted lobbies count against the cap forever.
- Low (carried from Core, now reachable via GET view): shared sequential NextId gaps and Version reveal opponent activity; enemy Producing/MovementLeft visible. Harmless until Plan 3 commands exist.
- Note for the client plan: put the invite code in a URL fragment (#code), not path/query (SWA logs, Referer).

**Why:** Plan 3 (commands + SSE) builds on these handlers; the abuse finding grows with command volume.
**How to apply:** at the Plan 3 audit, check whether a per-user limiter exists, whether the Medium was fixed, and re-verify BOLA on the command endpoint uses FindPlayer(userId).PlayerId as actor.

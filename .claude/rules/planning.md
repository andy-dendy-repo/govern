# Plan structure

Every implementation plan, whether in plan mode, `/develop-feature` step 1 or `docs/plans/active/*.md`, uses this structure. Keep it scannable, write only the recommended approach (no option surveys), and name files with repo-relative paths.

```markdown
# Plan: <feature / change title>

## Context
Why this change is needed, what prompted it, the intended outcome (2–4 sentences).

## Scope
- In: …
- Out: … (explicitly deferred)

## Affected areas
Project → folders/files, using project-map terms (e.g. `Application/Users/Contracts/RegisterUserContract.cs`).
Existing code to reuse (with paths).

## Steps
Ordered by layer; each step small enough to build on its own:
1. Domain — …
2. Application.Common — …
3. Application — …
4. Infrastructure — … (migration name if schema changes)
5. Api — … (routes, auth policy, status codes)
6. Client — …

## Tests
- Unit (Utils, Core and Domain only): cases…
- Integration: per endpoint → happy path, validation 400, auth 401/403, domain errors…
- Architecture: new/changed rules (or "none — existing rules cover it")

## Docs & rules
- docs/… to add/update
- .claude/rules|skills changes (only if a convention changes)

## Verification
Exact commands (`dotnet build Govern.slnx`, `dotnet test Govern.slnx`, manual HTTP calls in `Govern.Api.http`, …) and expected results.

## Risks & open questions
Only real ones; ask the user before implementing if an open question changes the design.
```

Rules:
- If the requirements are ambiguous, ask first (AskUserQuestion) instead of planning around guesses.
- Mention any conflict with existing rules and propose the rule change explicitly. Don't silently deviate.
- Multi-feature work is split into separate plans, each delivering a working, tested increment.

# Backlog

`docs/plans/backlog.md` is the one list of open work and the only place priorities are kept. Design docs (`docs/game/design-roadmap.md`) and plan files describe items; they never rank them.

- **Buckets:** Now (at most 3, in order), Next (in order), Deferred, Ideas. Only the owner moves an item between buckets; propose a move, don't make it silently.
- **Deferred** items keep their detail and are never deleted. Each row states the date, the reason and a "revisit when" trigger.
- **Ids** are stable and never reused: `F` follow-ups, `G` game design roadmap items, `C` content slices. A split item gets a letter (`G3a`, `G3b`).
- **Status:** `needs decision`, `needs plan`, `planned`, `in progress` (branch or PR), `blocked by <id>`.
- **Open questions** sit next to the item they block (in its plan or design section), not in a separate file.

## Plan lifecycle

1. Picked up: write the plan in the format above as `docs/plans/active/<id>-<slug>.md`; the backlog status becomes `planned`.
2. Implementing (`/develop-feature`): status `in progress` with the branch or PR.
3. Merged: `git mv` the plan to `docs/plans/done/`, remove the row from its bucket, add it to "Recently done" (id, date, PR), and update every link.
4. Parked: `git mv` the plan to `docs/plans/deferred/` with a status line (date, why, revisit when), and move the row to Deferred.

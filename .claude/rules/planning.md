# Plan structure

Every implementation plan, whether in plan mode, `/develop-feature` step 1 or `docs/plans/*.md`, uses this structure. Keep it scannable, write only the recommended approach (no option surveys), and name files with repo-relative paths.

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

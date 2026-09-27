---
name: convention-sweep-gaps
description: When a Govern convention changes, the rule/doc sweep tends to miss .claude/agents/*.md, .claude/skills (develop-feature, verify) and code examples inside rule files
metadata:
  type: feedback
---

When a change edits a convention, grep beyond `.claude/rules` and `docs/`. It has been missed twice:
- 2026-09-27, unit-test scope widened to Utils + Domain: the sweep missed `.claude/agents/code-reviewer.md` ("Unit tests exist only for Utils"), the `integration-tests.md` scope line, and the stale `User.SignUp("")` example in `error-handling.md`.
- 2026-09-27, Blazor client foundation (new `Govern.ClientTests`, `blazor-client.md`, `client-tests.md`): `code-reviewer.md` inputs and the tests dimension, the `develop-feature` step 3 test list, the `/verify` phases and report table, and the `integration-tests.md` "everything except" scope were not updated.

- 2026-09-27, health split + HostAllowListMiddleware: `api-endpoints.md` "Pipeline order in Program.cs" and "Middleware ... registered transient; the current one is ExceptionHandlingMiddleware" were left stale (new middleware registered singleton).
- 2026-09-27, game core (new `gamecore` integration branch): CLAUDE.md Layout line and game-delivery.md updated, but `.github/workflows/ci.yml` triggers, `.claude/hooks/bash-guard.sh` protected branches, CLAUDE.md "Branching" and docs/operations/branching.md were not.

**Why:** CLAUDE.md DoD item 5 requires rules, skills, agents and docs to change together with the convention. Stale agent prompts and skills make later features (and reviews) enforce the old rule.
**How to apply:** on any rule change or new test project, grep `.claude/agents`, `.claude/skills` and code snippets inside rule files for the old wording, old signatures and the list of test projects. Also check value-object converters: `Create` in `HasConversion` validates on read, so legacy rows that fail the new rule throw on materialisation.

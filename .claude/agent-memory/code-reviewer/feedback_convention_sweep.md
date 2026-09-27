---
name: convention-sweep-gaps
description: When a Govern convention changes, the rule/doc sweep tends to miss .claude/agents/*.md and code examples inside rule files
metadata:
  type: feedback
---

When a change edits a convention (e.g. 2026-09-27 unit-test scope widened from Utils to Utils + Domain), grep beyond `.claude/rules` and `docs/`: the sweep missed `.claude/agents/code-reviewer.md` ("Unit tests exist only for Utils"), `integration-tests.md` scope line, and the stale `User.SignUp("")` example in `error-handling.md`.

**Why:** CLAUDE.md DoD item 5 requires rules, skills, agents and docs to change together with the convention; stale agent prompts make later reviews enforce the old rule.
**How to apply:** on any rule change, grep `.claude/agents`, `.claude/skills` and code snippets inside rule files for the old wording or old signatures. Also check value-object converters: `Create` in `HasConversion` validates on read, so legacy rows that fail the new rule throw on materialisation.

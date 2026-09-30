---
paths:
  - "docs/**"
---

# docs/ — knowledge base (RAG)

`docs/` is the project's knowledge base. Humans read it, and it is chunked and indexed for retrieval, so each document must make sense on its own when retrieved as a chunk.

## Layout

| Path | Content |
|---|---|
| `docs/README.md` | Index: one line per document, grouped by folder. Update it whenever a doc is added, renamed or removed |
| `docs/architecture.md` | Layers, folder templates, request flow (human-facing overview of `.claude/rules/architecture.md`) |
| `docs/features/<feature>.md` | One per Application feature: purpose, use cases (contracts), endpoints (method, route, auth, status codes), entities, events, business rules |
| `docs/decisions/NNNN-<slug>.md` | Architecture decision records: context, decision, consequences |
| `docs/operations/<topic>.md` | Infra, deployment, configuration, runbooks |
| `docs/plans/backlog.md` | The one list of open work and the only place priorities are kept (see "Backlog" in `planning.md`) |
| `docs/plans/{active,deferred,done}/<slug>.md` | Plans by state (see `planning.md`). Move a plan with `git mv` when its state changes, then update every link to it |
| `docs/game/*.md` | The game's design: `rules.md` (the rules reference for players, no code or ruleset keys outside its closing maintainer section; its tables are checked against the ruleset by `RulesReferenceTests`), `game-data.md` (the loadable ruleset: every configurable number, list and text, its format and examples) and `game-logic.md` (Core's algorithms for developers, step by step, with the ruleset keys they use, no class names). A game rule change updates `rules.md` and `game-logic.md`, and `game-data.md` when the ruleset format changes. `design-roadmap.md` describes future mechanics (G items); it holds no priorities |

## Writing rules

- Every document starts with front matter:
  ```yaml
  ---
  title: Users feature
  tags: [users, auth, jwt]
  code: [src/Govern.Application/Users, src/Govern.Api/Features/Users]
  updated: 2026-09-27
  ---
  ```
- One topic per file. Split a file when it grows past about 300 lines.
- Headings are descriptive and self-contained ("Registering a user", not "Flow"). A section should be understandable without the sections before it: repeat the key noun instead of writing "it" or "as above".
- Prefer tables and short lists to prose. Include real routes, type names and config keys, since those are what retrieval is matched against.
- Document behaviour and the reasons behind it; don't paste code. Link to the code paths instead.
- Keep docs in sync. A feature change that alters behaviour, an endpoint or configuration updates its doc in the same change (it's part of the Definition of Done).
- Never include secrets, connection strings or personal data.

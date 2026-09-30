---
name: develop-feature
description: >
  The development flow for any feature, use case, endpoint, entity or behaviour change in Govern:
  plan → implement layer by layer → unit/integration/architecture tests → XML docs → docs/ (RAG) →
  .claude rules correction → build + ALL tests green → code review → report.
  Use whenever the user asks to add, implement, build, change or fix functionality in src/.
---

# Develop a feature

Follow the steps in order. **Never report a feature as done without a green `dotnet test Govern.slnx` run in this session whose output you show.** A Stop hook (`.claude/hooks/require-tests.sh`) also enforces this.

## 1. Understand and plan
- Read `.claude/rules/project-map.md` and `architecture.md`, and look at existing features for patterns to reuse. Use `cwm-roslyn-navigator` (`find_symbol`, `get_project_graph`, `get_endpoint_map`) rather than reading whole files.
- Ask the user (AskUserQuestion) about anything ambiguous that changes the design.
- Write the plan in the `.claude/rules/planning.md` format. For non-trivial work, get approval before coding.
- Make sure you are on a feature branch from `stage` (`git switch stage && git switch -c feature/<name>`). Never work on `main` or `stage` directly.

## 2. Implement layer by layer
Path-scoped rules load only when a matching file is read. Before **creating** files in a new area, read the matching rule files yourself: `csharp-style`, `xml-docs` and `logging` for `src/`, the test rules for `tests/`, and `docs` for `docs/`.

Domain → Application.Common → Application → Infrastructure → Api → Client. After each layer, run `dotnet build Govern.slnx`; warnings are errors.
- Folders only as needed, per the templates. Names per the suffix table. No `Helper` classes.
- Use cases go in `[Feature]/Contracts/[UseCase]Contract.cs` (Request / Response / Validator / Handler).
- Schema change: `dotnet ef migrations add <Name> --project src/Govern.Infrastructure --startup-project src/Govern.Api --output-dir Persistence/Migrations`. Never hand-edit generated migration files.
- Follow `csharp-style.md` and `logging.md` (`[LoggerMessage]`, no sensitive data).
- Use `roslyn-refactor` tools for renames, moves, signature changes and extractions instead of manual multi-file edits.

## 3. Tests
- **Unit** (`tests/Govern.UnitTests`): only when Utils, Core or Domain changed (`unit-tests.md`).
- **Integration** (`tests/Govern.IntegrationTests`): every new or changed endpoint needs the happy path, validation 400, auth 401/403 and domain errors (`integration-tests.md`).
- **Client** (`tests/Govern.ClientTests`, bUnit): every new or changed client page, component, typed client or auth service (`client-tests.md`).
- **Architecture** (`tests/Govern.ArchitectureTests`): if you introduced a new convention, folder or project, add or adjust a rule and prove it can fail (`architecture-tests.md`).

## 4. XML docs
Every new public or protected member is documented (`xml-docs.md`); the build fails on CS1591. Endpoints need `<response>` codes.

## 5. docs/ (RAG)
Update or create `docs/features/<feature>.md` and `docs/README.md`, plus `docs/architecture.md`, `docs/operations/*` or an ADR when relevant (`docs.md`).

## 6. Correct the Claude rules
If this work established or changed a convention, update the matching `.claude/rules/*.md`, skill or `CLAUDE.md` and the architecture test. Don't let rules drift from the code.

## 7. Verify
Run `/verify` (build, format, arch, unit and integration tests, analyzers). Loop fix → re-run until everything is green. If a test is flaky or can't run (e.g. Docker not running), say so explicitly; don't claim success.

## 8. Review
Run the `code-reviewer` agent on the diff. If auth, endpoints, CORS, configuration, infra or CI changed, also run the `security-auditor` agent. Fix Critical and High findings, then re-run step 7 if code changed.

## 9. Report
Keep it short:
- what changed, as a list of files by layer
- the test summary (total / failed / succeeded) copied from the real run
- docs and rules updated
- follow-ups and open questions

Commit or push only when the user asks: conventional commit messages, on the feature branch, with the PR to `stage`.

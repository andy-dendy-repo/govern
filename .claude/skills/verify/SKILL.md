---
name: verify
description: >
  Quality gate for Govern: build (warnings as errors), dotnet format check, architecture, unit and
  integration tests, plus Roslyn analyzer/antipattern checks on changed files. Produces a pass/fail report.
  Use before reporting work as done, before commits/PRs, or when the user says "verify", "check everything", "run the tests".
---

# Verify

Run the phases in order. **Build and test failures stop the run**: fix them, then restart from phase 1. Show real command output; never summarise a run you didn't execute.

## Phases

1. **Build:** `dotnet build Govern.slnx`. It must end with `Build succeeded` and 0 errors (all warnings are errors). CS1591 means an XML doc is missing (see `xml-docs.md`).
2. **Format:** `dotnet format Govern.slnx --verify-no-changes --exclude src/Govern.Core`. On failure, run the same command without `--verify-no-changes` and review the diff. Core is ignored.
3. **Architecture tests:** `dotnet test --project tests/Govern.ArchitectureTests`. Failures name the offending folder, file or type. Fix the code, or update rule and test together if the convention is deliberately changing (ask the user first).
4. **Unit tests:** `dotnet test --project tests/Govern.UnitTests`.
5. **Integration tests:** `dotnet test --project tests/Govern.IntegrationTests`. Docker must be running for Testcontainers. If it isn't, report "integration tests NOT run — Docker unavailable" and treat the gate as failed.
6. **Full run:** `dotnet test Govern.slnx` (no filters). This is the run the Stop-hook test gate records, and it must show `Test run summary: Passed!`.
7. **Static analysis** on changed files (`git diff --name-only stage...HEAD` plus uncommitted changes), using `cwm-roslyn-navigator`:
   - `get_diagnostics` for changed files
   - `detect_antipatterns` (DateTime.Now, logging interpolation, missing CancellationToken, sync-over-async, AsNoTracking, broad catch…)
   - `get_di_registrations` when DI changed (captive dependencies, duplicates)
   - `get_endpoint_map` when endpoints changed (every endpoint has an explicit auth decision)

   Fix high-confidence findings, and list the medium ones in the report.

## Report

```
## Verify: PASS | FAIL
| Phase | Result |
|---|---|
| Build | ✅ |
| Format | ✅ |
| Architecture | ✅ 44/44 |
| Unit | ✅ n/n |
| Integration | ✅ n/n (or ❌ not run: reason) |
| Full run | ✅ total/failed/succeeded |
| Analysis | ✅ / ⚠️ findings… |
```
Then list any remaining issues with `file:line`.

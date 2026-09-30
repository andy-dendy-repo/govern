---
title: Branching and release strategy
tags: [git, branching, pull-request, release, ci, branch-protection]
code: [.github/workflows/ci.yml, .github/workflows/release.yml, .claude/hooks/bash-guard.sh]
updated: 2026-09-30
---

# Branching and release strategy

| Branch | Purpose | How it changes |
|---|---|---|
| `main` | Production. Every push is released | Only by a PR from `stage` |
| `stage` | Integration branch; the base of all work | Only by PRs from feature branches |
| `gamecore` | Retired (2026-09-30): it was the game's integration branch ([game delivery plan](../plans/done/game-delivery.md)) and is merged into `stage`. New game work branches from `stage` | No new PRs |
| `feature/<name>`, `fix/<name>`, `chore/<name>` | One change each | Branched from `stage` (game work too) |

## Flow

1. `git switch stage && git pull && git switch -c feature/<name>`.
2. Commit with conventional commits (`feat(users): …`, `fix(api): …`, `docs: …`).
3. Open a PR to `stage`. CI (`ci.yml`) must pass; the `code-reviewer` agent reviews locally.
4. **Release:** run the `security-auditor` agent on `main...stage`. Critical or High findings block the release. Then open a PR from `stage` to `main`. Merging it runs `release.yml`.

## Protection

- **GitHub branch protection** for `main` and `stage`: require a PR, require the "CI / build-and-test" check, and block force pushes and deletion.
- **Locally:** Claude Code's `bash-guard.sh` hook blocks commits or pushes on `main`/`stage`, force pushes, and `--no-verify`.

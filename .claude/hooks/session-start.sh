#!/usr/bin/env bash
# SessionStart: puts the current branch and branch-strategy reminder into context, and in Claude Code cloud
# sessions starts the Docker daemon that the integration tests (Testcontainers) need.
set -uo pipefail

cd "${CLAUDE_PROJECT_DIR:-.}" || exit 0
branch=$(git branch --show-current 2>/dev/null || echo "unknown")

echo "Current git branch: $branch"
if [[ "$branch" == "main" || "$branch" == "stage" ]]; then
  echo "WARNING: '$branch' is protected. Before committing, create a feature branch from stage (git switch stage && git switch -c feature/<name>) and open the PR to stage."
fi

# Cloud sessions only: the VM has Docker installed but not running. Locally Docker Desktop manages the engine.
if [[ "${CLAUDE_CODE_REMOTE:-}" == "true" ]] && command -v dockerd >/dev/null 2>&1 && ! docker info >/dev/null 2>&1; then
  (dockerd >/tmp/dockerd.log 2>&1 &)
  echo "Started the Docker daemon for Testcontainers (log: /tmp/dockerd.log)."
fi
exit 0

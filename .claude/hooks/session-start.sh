#!/usr/bin/env bash
# SessionStart: puts the current branch and branch-strategy reminder into context.
set -uo pipefail

cd "${CLAUDE_PROJECT_DIR:-.}" || exit 0
branch=$(git branch --show-current 2>/dev/null || echo "unknown")

echo "Current git branch: $branch"
if [[ "$branch" == "main" || "$branch" == "stage" ]]; then
  echo "WARNING: '$branch' is protected. Before committing, create a feature branch from stage (git switch stage && git switch -c feature/<name>) and open the PR to stage."
fi
exit 0

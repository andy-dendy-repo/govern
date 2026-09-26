#!/usr/bin/env bash
# PreToolUse(Bash): blocks destructive git/shell commands and direct work on protected branches.
# Exit 2 = block; stderr is shown to Claude.
set -euo pipefail

cmd=$(jq -r '.tool_input.command // ""')
[[ -z "$cmd" ]] && exit 0

block() { echo "Blocked by .claude/hooks/bash-guard.sh: $1" >&2; exit 2; }

protected='^(main|stage)$'
branch=$(git -C "${CLAUDE_PROJECT_DIR:-.}" branch --show-current 2>/dev/null || true)

# Destructive git operations
# --force, -f (also combined, e.g. -uf) or a '+refspec' (force-updates that ref); --force-with-lease is allowed off protected branches.
if grep -Eq 'git[[:space:]]+push\b.*([[:space:]]--force([[:space:]]|$)|[[:space:]]-[a-zA-Z]*f[a-zA-Z]*([[:space:]]|$)|[[:space:]]\+[^[:space:]])' <<<"$cmd"; then
  block "force push is not allowed (use --force-with-lease on your own feature branch if really needed, and ask first)."
fi
grep -Eq 'git[[:space:]]+reset\b.*--hard' <<<"$cmd" && block "git reset --hard discards work; ask the user first."
grep -Eq 'git[[:space:]]+clean\b.*-[a-zA-Z]*f' <<<"$cmd" && block "git clean -f deletes untracked files; ask the user first."
grep -Eq 'git[[:space:]]+(checkout|restore)[[:space:]]+(--[[:space:]]+)?\.([[:space:]]|$)' <<<"$cmd" && block "discarding all working-tree changes; ask the user first."
grep -Eq -- '--no-verify' <<<"$cmd" && block "--no-verify skips git hooks; fix the underlying problem instead."

# Branch strategy: branch from stage, PR to stage; stage -> main is a release. Never commit/push to main or stage directly.
# Matches main, +main, HEAD:main, HEAD:refs/heads/main (not feature/main-x).
if grep -Eq 'git[[:space:]]+push\b.*[[:space:]]\+?([^[:space:]]*:)?(refs/heads/)?(main|stage)([[:space:]]|$)' <<<"$cmd"; then
  block "pushing to main/stage directly is not allowed; push a feature branch and open a PR to stage."
fi
if [[ "$branch" =~ $protected ]]; then
  grep -Eq 'git[[:space:]]+commit\b' <<<"$cmd" && block "you are on '$branch'. Create a feature branch from stage first (git switch -c feature/<name>)."
  grep -Eq 'git[[:space:]]+push\b' <<<"$cmd" && block "you are on '$branch'. Push a feature branch and open a PR to stage."
fi

# Recursive + forced rm (-rf, -Rf, -r -f, --recursive --force, ...) only for build output or temp folders.
set -f
while IFS= read -r segment; do
  recursive=0; force=0; targets=()
  for token in $segment; do
    case "$token" in
      rm) ;;
      --recursive) recursive=1 ;;
      --force) force=1 ;;
      --*) ;;
      -*) [[ "$token" == *[rR]* ]] && recursive=1; [[ "$token" == *f* ]] && force=1 ;;
      *) targets+=("$token") ;;
    esac
  done
  if (( recursive && force )); then
    for t in "${targets[@]}"; do
      if ! grep -Eq '(^|/)(bin|obj|TestResults|node_modules)/?$|^(/private)?/tmp/' <<<"$t"; then
        block "recursive forced rm is only allowed for bin/, obj/, TestResults/, node_modules/ or temp folders (target: $t)."
      fi
    done
  fi
done < <(grep -oE '(^|[;&|[:space:]])rm[[:space:]][^;&|]*' <<<"$cmd" | sed -E 's/^[;&|[:space:]]*//' || true)
set +f

exit 0

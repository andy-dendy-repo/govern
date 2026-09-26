#!/usr/bin/env bash
# Stop: "MUST RUN TESTS BEFORE TELLING ME THAT FEAT IS DONE".
# Blocks finishing the turn when build/test-relevant files changed since the last green full `dotnet test` run.
# Reminds once per stop (stop_hook_active prevents loops).
set -uo pipefail

input=$(cat)
[[ "$(jq -r '.stop_hook_active // false' <<<"$input")" == "true" ]] && exit 0

cd "${CLAUDE_PROJECT_DIR:-.}" || exit 0
source "$(dirname "$0")/lib/test-gate.sh"

changed=$(gate_changed_files)
[[ -z "$changed" ]] && exit 0

stale=()
while IFS= read -r line; do
  f=${line:3}
  if [[ ! -e "$f" ]]; then
    # Deletions have no mtime: stale unless the file was already deleted at the last green run.
    if [[ ! -f "$GATE_MARKER" ]] || ! grep -Fxq -- "$f" "$GATE_MARKER"; then stale+=("$f (deleted)"); fi
  elif [[ ! -f "$GATE_MARKER" || "$f" -nt "$GATE_MARKER" ]]; then
    stale+=("$f")
  fi
done <<<"$changed"

[[ ${#stale[@]} -eq 0 ]] && exit 0

{
  echo "Code changed since the last green full test run. Run 'dotnet test Govern.slnx' (no filters, no --no-build) and show the result before reporting the work as done."
  echo "If you are only pausing to ask the user a question, say so explicitly."
  echo "Changed files:"
  printf '  %s\n' "${stale[@]:0:15}"
} >&2
exit 2

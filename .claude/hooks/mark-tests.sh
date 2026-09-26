#!/usr/bin/env bash
# PostToolUse(Bash): records a green full-solution test run for the Stop test gate (require-tests.sh).
# Only an unfiltered, compiling `dotnet test` whose Microsoft Testing Platform summary says "Passed!" counts.
# The marker's mtime is the green time; its content lists files that were already deleted at that time.
set -uo pipefail

input=$(cat)
cmd=$(jq -r '.tool_input.command // ""' <<<"$input")

grep -Eq '(^|[;&|[:space:]])dotnet[[:space:]]+test\b' <<<"$cmd" || exit 0
grep -Eq -- '--project|--filter|--treenode-filter|--no-build' <<<"$cmd" && exit 0

output=$(jq -r '[.tool_response.stdout?, .tool_response.stderr?, (.tool_response | strings)] | map(select(. != null)) | join("\n")' <<<"$input" 2>/dev/null)
# Green = "Passed!" summary, or (when output was trimmed, e.g. | tail) the MTP counts "failed: 0" + "succeeded: N>0".
passed=0
grep -q 'Test run summary: Passed!' <<<"$output" && passed=1
grep -Eq '^[[:space:]]*failed: 0$' <<<"$output" && grep -Eq '^[[:space:]]*succeeded: [1-9][0-9]*$' <<<"$output" && passed=1
grep -Eq 'Test run summary: Failed!|^[[:space:]]*failed: [1-9]|Build FAILED|error [A-Z]+[0-9]+:' <<<"$output" && passed=0
if (( passed )); then
  cd "${CLAUDE_PROJECT_DIR:-.}" || exit 0
  source "$(dirname "$0")/lib/test-gate.sh"
  mkdir -p "$(dirname "$GATE_MARKER")"
  gate_deleted_files > "$GATE_MARKER"
fi
exit 0

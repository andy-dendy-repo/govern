#!/usr/bin/env bash
# PostToolUse(Edit|Write): fast whitespace formatting of the edited .cs file per .editorconfig.
# Never blocks. Full style/analyzer formatting is checked by /verify (dotnet format --verify-no-changes).
set -uo pipefail

file=$(jq -r '.tool_input.file_path // ""')
[[ "$file" == *.cs && -f "$file" ]] || exit 0

cd "${CLAUDE_PROJECT_DIR:-.}" || exit 0
dotnet format whitespace --folder --include "$file" >/dev/null 2>&1 || true
exit 0

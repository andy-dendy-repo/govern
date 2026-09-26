#!/usr/bin/env bash
# PostToolUse(Edit|Write): restores packages after a project or central props file changes.
# Exit 2 surfaces restore errors to Claude (the edit itself is kept).
set -uo pipefail

file=$(jq -r '.tool_input.file_path // ""')
case "$file" in
  *.csproj|*/Directory.Packages.props|*/Directory.Build.props|*/global.json) ;;
  *) exit 0 ;;
esac

cd "${CLAUDE_PROJECT_DIR:-.}" || exit 0
if ! output=$(dotnet restore Govern.slnx --verbosity quiet 2>&1); then
  echo "dotnet restore failed after editing $file:" >&2
  echo "$output" | tail -20 >&2
  exit 2
fi
exit 0

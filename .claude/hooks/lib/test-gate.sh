#!/usr/bin/env bash
# Shared by mark-tests.sh and require-tests.sh: which changes require a fresh green `dotnet test Govern.slnx`.

GATE_MARKER=".claude/.state/last-green-tests"
GATE_PATHS=(src tests Govern.slnx .editorconfig global.json Directory.Build.props Directory.Packages.props)
GATE_FILES_REGEX='(\.(cs|razor|csproj|props|json|slnx)|\.editorconfig)$'

# Prints "<status> <path>" for changed files that affect build or test results (run from the repo root).
gate_changed_files() {
  git status --porcelain --untracked-files=all -- "${GATE_PATHS[@]}" 2>/dev/null \
    | sed -E 's/^(..) (.* -> )?/\1 /' \
    | grep -E "$GATE_FILES_REGEX" || true
}

# Prints the paths of changed files that are deleted in the working tree or index.
gate_deleted_files() {
  gate_changed_files | awk '$1 ~ /D/ { $1 = ""; sub(/^ /, ""); print }'
}

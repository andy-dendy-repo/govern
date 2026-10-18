<!-- Open the PR to `stage`. Title it as a conventional commit, e.g. `feat(games): …`, `fix(api): …`, `docs: …`. -->

## What and why

<!-- What does this change, and why? Link the issue it closes, e.g. "Closes #123". -->

## How

<!-- The approach, and anything a reviewer should look at closely. -->

## Definition of Done

<!-- See CONTRIBUTING.md. Tick what applies; strike through or explain what doesn't. -->

- [ ] Follows the conventions in [architecture](https://github.com/andy-dendy-repo/govern/blob/stage/docs/architecture.md) and [`.claude/rules`](https://github.com/andy-dendy-repo/govern/tree/stage/.claude/rules)
- [ ] Tested at the right level (unit, integration, client, architecture)
- [ ] XML docs on every new public member in `src/`
- [ ] `docs/` updated (feature doc and index; `docs/game/rules.md` for a game rules change)
- [ ] Green locally: `dotnet build`, `dotnet format --verify-no-changes`, `dotnet test`
